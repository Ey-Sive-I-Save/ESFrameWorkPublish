using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ES
{
    public sealed class ESCmdAgentWindow : EditorWindow
    {
        private const string DefaultAgentAssetPath = "Assets/ESNormalAssets/Data/GlobalData/CmdAgent/ESCmdAgent.asset";
        private const string AIWarningsRelativePath = "Assets/Plugins/ES/AIWarnings";
        private const string AICommandsRelativePath = "Assets/Plugins/ES/AICommands";
        private const string ClipboardImageRelativeFolder = "Assets/Plugins/ES/Editor/ESCmdAgent/ClipboardImages";
        private const int FallbackMaxLocalTabs = 2;
        private const int FallbackOutputCharLimit = 12000;
        private const int MaxSessionCaptureAttempts = 12;
        private const int MaxCodexSessionCandidateFiles = 256;
        private const int MaxScannedDirectories = 512;
        private const double SessionCaptureIntervalSeconds = 5.0;
        private const string SessionStateKey = "ES.CmdAgent.WindowState";
        private const string LocalSessionMetadataKey = "ES.CmdAgent.LocalSessionMetadata";
        private static readonly Vector2 DefaultWindowSize = new Vector2(980, 680);
        private static readonly Vector2 MinimumWindowSize = new Vector2(860, 600);

        [SerializeField] private List<AgentSessionTab> tabs = new List<AgentSessionTab>();
        [SerializeField] private int selectedTabIndex;
        [SerializeField] private int mainPageIndex;
        [SerializeField] private bool showAdvancedSettings;
        [SerializeField] private bool showCommandDetails;
        [SerializeField] private bool showAICommandComposer;
        [SerializeField] private bool showTerminalTools;
        [SerializeField] private bool showTerminalAttachments;
        [SerializeField] private bool showTerminalDetails;
        [SerializeField] private string selectedAICommandAssetPath = "";
        [SerializeField] private string selectedAICommandName = "";
        [SerializeField] private string aiCommandUserSupplement = "";
        [SerializeField] private string aiCommandRequiredValue = "";
        [SerializeField] private List<AgentAttachment> aiCommandAttachments = new List<AgentAttachment>();
        [SerializeField] private Vector2 aiCommandPreviewScroll;

        private ESCmdAgent agent;

        [SerializeField] private bool architectIncludeCodexSessions = true;
        [SerializeField] private bool architectIncludeAITalkSessions = true;
        [SerializeField] private bool architectIncludeAIWarnings = true;
        [SerializeField] private int architectMaxCodexSessions = 16;
        [SerializeField] private int architectMaxFilesPerFolder = 80;
        [SerializeField] private string architectSearchText = "";
        [SerializeField] private Vector2 architectScroll;
        [SerializeField] private List<ArchitectNode> architectNodes = new List<ArchitectNode>();
        [SerializeField] private List<ArchitectEdge> architectEdges = new List<ArchitectEdge>();
        [SerializeField] private int architectSelectedNodeIndex = -1;

        private readonly Dictionary<string, GUIStyle> architectStyleCache = new Dictionary<string, GUIStyle>();
        private readonly Dictionary<Process, AgentSessionTab> processTabs = new Dictionary<Process, AgentSessionTab>();
        private readonly object processTabsLock = new object();
        private bool architectAutoBuiltThisOpen;
        private bool architectDraggingNode;
        private int architectDraggingNodeIndex = -1;
        private Vector2 architectDragOffset;

        [Serializable]
        private sealed class ArchitectNode
        {
            public string id;
            public string title;
            public string type;
            public string sourcePath;
            public string summary;
            public Rect rect;
            public Color color;
        }

        [Serializable]
        private sealed class ArchitectEdge
        {
            public int from;
            public int to;
            public string label;
        }

        [Serializable]
        private sealed class AgentAttachment
        {
            public string path;
            public string displayName;
            public long sizeBytes;
            public bool projectAsset;
            public bool image;
        }

        [Serializable]
        private sealed class AgentSessionTab
        {
            public string title = "会话";
            public string sessionId = "";
            public string createdAt = "";
            public string lastStartTime = "";
            public string lastStopTime = "";
            public string lastCommand = "";
            public string summary = "等待恢复";
            public string statusNotice = "";
            public int statusNoticeKind;
            public bool capturedSessionKey;
            public string createdSessionFile = "";
            public List<AgentAttachment> attachments = new List<AgentAttachment>();

            [NonSerialized] public string outputText = "";
            [NonSerialized] public string inputText = "";
            [NonSerialized] public Vector2 scroll;
            [NonSerialized] public Process process;
            [NonSerialized] public ConPtySession conPty;
            [NonSerialized] public TerminalScreen terminal;
            [NonSerialized] public ConcurrentQueue<string> pendingOutput;
            [NonSerialized] public DateTime startedAtUtc;
            [NonSerialized] public DateTime lastCaptureAttemptUtc;
            [NonSerialized] public int captureAttemptCount;

            public bool IsRunning
            {
                get { return conPty != null && conPty.IsRunning || process != null && !process.HasExited; }
            }

            public void EnsureRuntime()
            {
                if (pendingOutput == null)
                    pendingOutput = new ConcurrentQueue<string>();
                if (terminal == null)
                    terminal = new TerminalScreen(120, 140);
            }
        }

        [Serializable]
        private sealed class LocalSessionMetadata
        {
            public string sessionId = "";
            public string alias = "";
            public string note = "";
            public bool pinned;
            public long lastUsedTicks;
        }

        [Serializable]
        private sealed class LocalSessionMetadataStore
        {
            public List<LocalSessionMetadata> items = new List<LocalSessionMetadata>();
        }

        private sealed class TerminalScreen
        {
            private readonly int width;
            private readonly int height;
            private readonly List<StringBuilder> lines = new List<StringBuilder>();
            private string pendingEscape = "";
            private int row;
            private int column;

            public TerminalScreen(int width, int height)
            {
                this.width = Mathf.Clamp(width, 40, 240);
                this.height = Mathf.Clamp(height, 40, 500);
                EnsureLine(0);
            }

            public void Clear()
            {
                lines.Clear();
                pendingEscape = "";
                row = 0;
                column = 0;
                EnsureLine(0);
            }

            public void Write(string text)
            {
                if (string.IsNullOrEmpty(text))
                    return;

                if (!string.IsNullOrEmpty(pendingEscape))
                {
                    text = pendingEscape + text;
                    pendingEscape = "";
                }

                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    if (c == '\u001b')
                    {
                        int escapeEnd = FindEscapeEnd(text, i);
                        if (escapeEnd < 0)
                        {
                            pendingEscape = text.Substring(i);
                            break;
                        }

                        i = HandleEscape(text, i);
                        continue;
                    }

                    if (c == '\u0007')
                        continue;

                    if (c == '\r')
                    {
                        column = 0;
                        continue;
                    }

                    if (c == '\n')
                    {
                        NewLine();
                        continue;
                    }

                    if (c == '\b')
                    {
                        column = Mathf.Max(0, column - 1);
                        continue;
                    }

                    if (char.IsControl(c) && c != '\t')
                        continue;

                    if (c == '\t')
                    {
                        int target = ((column / 4) + 1) * 4;
                        while (column < target)
                            PutChar(' ');
                        continue;
                    }

                    PutChar(c);
                }
            }

            public string GetText(int maxChars)
            {
                TrimTopBlankLines();
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < lines.Count; i++)
                {
                    string line = lines[i].ToString().TrimEnd();
                    builder.Append(line);
                    if (i < lines.Count - 1)
                        builder.Append('\n');
                }

                string result = FilterTerminalNoise(CollapseExcessBlankLines(builder.ToString())).TrimEnd();
                if (maxChars > 0 && result.Length > maxChars)
                    result = "[本地输出已自动截断，仅保留最近内容]\n" + result.Substring(result.Length - maxChars);
                return result;
            }

            private static string FilterTerminalNoise(string text)
            {
                if (string.IsNullOrEmpty(text))
                    return "";

                string[] rawLines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                StringBuilder builder = new StringBuilder(text.Length);
                bool wroteMcpSummary = false;
                for (int i = 0; i < rawLines.Length; i++)
                {
                    string line = rawLines[i].TrimEnd();
                    string trimmed = line.Trim();

                    if (IsCodexEmptyPromptPlaceholder(trimmed))
                        continue;

                    if (trimmed.StartsWith("Tip:", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (trimmed.StartsWith("⚠ MCP client for `node_repl` failed to start", StringComparison.OrdinalIgnoreCase)
                        || trimmed.StartsWith("⚠ MCP startup incomplete", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!wroteMcpSummary)
                        {
                            AppendFilteredLine(builder, "⚠ node_repl MCP 启动失败：本地配置路径不存在，不影响普通 Codex 对话。");
                            wroteMcpSummary = true;
                        }
                        continue;
                    }

                    AppendFilteredLine(builder, FormatSpeakerLine(line));
                }

                return CollapseExcessBlankLines(builder.ToString());
            }

            private static string FormatSpeakerLine(string line)
            {
                string trimmed = (line ?? "").Trim();
                if (string.IsNullOrEmpty(trimmed))
                    return line ?? "";

                if (trimmed.StartsWith("你：", StringComparison.Ordinal)
                    || trimmed.StartsWith("›", StringComparison.Ordinal))
                    return ColorLine("你：" + TrimSpeakerPrefix(trimmed, "你：").TrimStart('›').TrimStart(), "#7EC8FF");

                if (trimmed.StartsWith("AI：", StringComparison.Ordinal))
                    return ColorLine(trimmed, "#8FE388");

                if (trimmed.StartsWith("系统：", StringComparison.Ordinal)
                    || trimmed.StartsWith("[ES Cmd Agent]", StringComparison.Ordinal)
                    || trimmed.StartsWith("⚠", StringComparison.Ordinal))
                    return ColorLine(trimmed, "#B8BEC8");

                if (trimmed.StartsWith("[错误]", StringComparison.Ordinal))
                    return ColorLine(trimmed, "#FF8A80");

                if (trimmed.StartsWith("【完成】", StringComparison.Ordinal))
                    return ColorLine(trimmed, "#B7F58A");

                if (trimmed.StartsWith("╭", StringComparison.Ordinal)
                    || trimmed.StartsWith("│", StringComparison.Ordinal)
                    || trimmed.StartsWith("╰", StringComparison.Ordinal))
                    return ColorLine(line, "#8A9099");

                if (trimmed.StartsWith("model:", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("directory:", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("OpenAI Codex", StringComparison.OrdinalIgnoreCase))
                    return ColorLine(line, "#AEB5BF");

                if (trimmed.StartsWith("•", StringComparison.Ordinal)
                    || trimmed.StartsWith("◦", StringComparison.Ordinal)
                    || trimmed.StartsWith("✓", StringComparison.Ordinal)
                    || trimmed.StartsWith("Running ", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("Working ", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("Ran ", StringComparison.OrdinalIgnoreCase))
                    return ColorLine("AI：" + trimmed, "#8FE388");

                return EscapeRichText(line);
            }

            private static string TrimSpeakerPrefix(string value, string prefix)
            {
                return value.StartsWith(prefix, StringComparison.Ordinal) ? value.Substring(prefix.Length) : value;
            }

            private static string ColorLine(string line, string color)
            {
                return "<color=" + color + ">" + EscapeRichText(line) + "</color>";
            }

            private static string EscapeRichText(string value)
            {
                if (string.IsNullOrEmpty(value))
                    return "";

                return value
                    .Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;");
            }

            private static bool IsCodexEmptyPromptPlaceholder(string trimmed)
            {
                if (string.IsNullOrEmpty(trimmed))
                    return false;

                return trimmed == "› Find and fix a bug in @filename"
                    || trimmed == "Find and fix a bug in @filename"
                    || trimmed == "› Explain this codebase"
                    || trimmed == "Explain this codebase";
            }

            private static void AppendFilteredLine(StringBuilder builder, string line)
            {
                if (builder.Length > 0)
                    builder.Append('\n');
                builder.Append(line);
            }

            private int HandleEscape(string text, int escapeIndex)
            {
                int i = escapeIndex + 1;
                if (i >= text.Length)
                    return escapeIndex;

                char kind = text[i];
                if (kind == '[')
                    return HandleCsi(text, i + 1);

                if (kind == ']')
                    return SkipOsc(text, i + 1);

                if (kind == 'c')
                {
                    Clear();
                    return i;
                }

                return i;
            }

            private static int FindEscapeEnd(string text, int escapeIndex)
            {
                int i = escapeIndex + 1;
                if (i >= text.Length)
                    return -1;

                char kind = text[i];
                if (kind == '[')
                {
                    i++;
                    while (i < text.Length)
                    {
                        char c = text[i];
                        if (c >= '@' && c <= '~')
                            return i;
                        i++;
                    }

                    return -1;
                }

                if (kind == ']')
                {
                    i++;
                    while (i < text.Length)
                    {
                        if (text[i] == '\u0007')
                            return i;
                        if (text[i] == '\u001b' && i + 1 < text.Length && text[i + 1] == '\\')
                            return i + 1;
                        i++;
                    }

                    return -1;
                }

                return i;
            }

            private int HandleCsi(string text, int start)
            {
                int i = start;
                while (i < text.Length)
                {
                    char c = text[i];
                    if (c >= '@' && c <= '~')
                    {
                        ApplyCsi(text.Substring(start, i - start), c);
                        return i;
                    }
                    i++;
                }

                return text.Length - 1;
            }

            private void ApplyCsi(string parameterText, char command)
            {
                string clean = parameterText.Replace("?", "").Replace(">", "");
                int[] args = ParseCsiArgs(clean);
                int first = args.Length > 0 && args[0] > 0 ? args[0] : 1;

                switch (command)
                {
                    case 'A':
                        row = Mathf.Max(0, row - first);
                        break;
                    case 'B':
                        row = Mathf.Min(height - 1, row + first);
                        EnsureLine(row);
                        break;
                    case 'C':
                        column = Mathf.Min(width - 1, column + first);
                        break;
                    case 'D':
                        column = Mathf.Max(0, column - first);
                        break;
                    case 'G':
                        column = Mathf.Clamp(first - 1, 0, width - 1);
                        break;
                    case 'H':
                    case 'f':
                        row = Mathf.Clamp((args.Length > 0 && args[0] > 0 ? args[0] : 1) - 1, 0, height - 1);
                        column = Mathf.Clamp((args.Length > 1 && args[1] > 0 ? args[1] : 1) - 1, 0, width - 1);
                        EnsureLine(row);
                        break;
                    case 'J':
                        if (args.Length == 0 || args[0] == 0)
                            ClearFromCursorToEnd();
                        else if (args[0] == 1)
                            ClearFromStartToCursor();
                        else if (args[0] == 2 || args[0] == 3)
                            Clear();
                        break;
                    case 'K':
                        if (args.Length == 0 || args[0] == 0)
                            ClearLineFromCursor();
                        else if (args[0] == 1)
                            ClearLineToCursor();
                        else if (args[0] == 2)
                            ClearCurrentLine();
                        break;
                    case 'm':
                    case 'h':
                    case 'l':
                    case 's':
                    case 'u':
                        break;
                }
            }

            private static int[] ParseCsiArgs(string parameterText)
            {
                if (string.IsNullOrWhiteSpace(parameterText))
                    return Array.Empty<int>();

                string[] parts = parameterText.Split(';');
                int[] values = new int[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                    int.TryParse(parts[i], out values[i]);
                return values;
            }

            private static int SkipOsc(string text, int start)
            {
                int i = start;
                while (i < text.Length)
                {
                    if (text[i] == '\u0007')
                        return i;
                    if (text[i] == '\u001b' && i + 1 < text.Length && text[i + 1] == '\\')
                        return i + 1;
                    i++;
                }

                return text.Length - 1;
            }

            private void PutChar(char c)
            {
                EnsureLine(row);
                StringBuilder line = lines[row];
                while (line.Length < column)
                    line.Append(' ');

                if (column < line.Length)
                    line[column] = c;
                else
                    line.Append(c);

                column++;
                if (column >= width)
                    NewLine();
            }

            private void NewLine()
            {
                row++;
                column = 0;
                if (row >= height)
                {
                    lines.RemoveAt(0);
                    row = height - 1;
                }
                EnsureLine(row);
            }

            private void EnsureLine(int index)
            {
                while (lines.Count <= index)
                    lines.Add(new StringBuilder());
            }

            private void ClearFromCursorToEnd()
            {
                ClearLineFromCursor();
                for (int i = row + 1; i < lines.Count; i++)
                    lines[i].Length = 0;
            }

            private void ClearFromStartToCursor()
            {
                for (int i = 0; i < row && i < lines.Count; i++)
                    lines[i].Length = 0;
                ClearLineToCursor();
            }

            private void ClearLineFromCursor()
            {
                EnsureLine(row);
                if (column < lines[row].Length)
                    lines[row].Length = column;
            }

            private void ClearLineToCursor()
            {
                EnsureLine(row);
                StringBuilder line = lines[row];
                int end = Mathf.Min(column, line.Length);
                for (int i = 0; i < end; i++)
                    line[i] = ' ';
            }

            private void ClearCurrentLine()
            {
                EnsureLine(row);
                lines[row].Length = 0;
                column = 0;
            }

            private void TrimTopBlankLines()
            {
                while (lines.Count > 1 && lines[0].Length == 0)
                {
                    lines.RemoveAt(0);
                    row = Mathf.Max(0, row - 1);
                }
            }
        }

        [Serializable]
        private sealed class AgentWindowState
        {
            public List<AgentSessionTab> tabs = new List<AgentSessionTab>();
            public int selectedTabIndex;
            public int mainPageIndex;
            public bool showAdvancedSettings;
            public bool showCommandDetails;
            public bool showAICommandComposer;
            public bool showTerminalTools;
            public bool showTerminalAttachments;
            public bool showTerminalDetails;
            public string selectedAICommandAssetPath;
            public string selectedAICommandName;
            public string aiCommandUserSupplement;
            public string aiCommandRequiredValue;
            public List<AgentAttachment> aiCommandAttachments = new List<AgentAttachment>();
            public Vector2 aiCommandPreviewScroll;
            public bool architectIncludeCodexSessions;
            public bool architectIncludeAITalkSessions;
            public bool architectIncludeAIWarnings;
            public int architectMaxCodexSessions;
            public int architectMaxFilesPerFolder;
            public string architectSearchText;
            public Vector2 architectScroll;
            public List<ArchitectNode> architectNodes = new List<ArchitectNode>();
            public List<ArchitectEdge> architectEdges = new List<ArchitectEdge>();
            public int architectSelectedNodeIndex;
        }

        private sealed class AICommandInfo
        {
            public string commandType = "未声明";
            public string defaultWrite = "未声明";
            public string riskLevel = "未声明";
            public string requirementTitle = "本次需求";
            public bool hasUserPlaceholder;

            public bool AllowsWrite
            {
                get { return defaultWrite.IndexOf("是", StringComparison.OrdinalIgnoreCase) >= 0; }
            }

            public bool IsHighRisk
            {
                get
                {
                    return riskLevel.IndexOf("L3", StringComparison.OrdinalIgnoreCase) >= 0
                        || riskLevel.IndexOf("L4", StringComparison.OrdinalIgnoreCase) >= 0
                        || commandType.IndexOf("执行", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
        }

        private sealed class ConPtySession : IDisposable
        {
            private const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;
            private const int EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
            private const uint STARTF_USESTDHANDLES = 0x00000100;

            private readonly ConcurrentQueue<string> outputQueue;
            private readonly SafeFileHandle inputWriteHandle;
            private readonly FileStream inputWriter;
            private readonly StreamWriter writer;
            private readonly FileStream outputReader;
            private readonly IntPtr pseudoConsole;
            private readonly IntPtr attributeList;
            private PROCESS_INFORMATION processInfo;
            private bool disposed;

            public bool IsRunning
            {
                get
                {
                    if (disposed || processInfo.hProcess == IntPtr.Zero)
                        return false;

                    return WaitForSingleObject(processInfo.hProcess, 0) == 0x00000102;
                }
            }

            private ConPtySession(
                ConcurrentQueue<string> outputQueue,
                SafeFileHandle inputWriteHandle,
                FileStream inputWriter,
                StreamWriter writer,
                FileStream outputReader,
                IntPtr pseudoConsole,
                IntPtr attributeList,
                PROCESS_INFORMATION processInfo)
            {
                this.outputQueue = outputQueue;
                this.inputWriteHandle = inputWriteHandle;
                this.inputWriter = inputWriter;
                this.writer = writer;
                this.outputReader = outputReader;
                this.pseudoConsole = pseudoConsole;
                this.attributeList = attributeList;
                this.processInfo = processInfo;
            }

            public static ConPtySession Start(string commandLine, string workingDirectory, ConcurrentQueue<string> outputQueue)
            {
                if (outputQueue == null)
                    outputQueue = new ConcurrentQueue<string>();

                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    throw new PlatformNotSupportedException("ConPTY 只支持 Windows。");

                CreatePipe(out SafeFileHandle inputReadSide, out SafeFileHandle inputWriteSide, IntPtr.Zero, 0);
                CreatePipe(out SafeFileHandle outputReadSide, out SafeFileHandle outputWriteSide, IntPtr.Zero, 0);

                COORD size = new COORD { X = 120, Y = 40 };
                int hr = CreatePseudoConsole(size, inputReadSide, outputWriteSide, 0, out IntPtr hpc);
                inputReadSide.Dispose();
                outputWriteSide.Dispose();
                if (hr != 0)
                    Marshal.ThrowExceptionForHR(hr);

                IntPtr attrList = IntPtr.Zero;
                STARTUPINFOEX startupInfo = new STARTUPINFOEX();
                startupInfo.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();
                startupInfo.StartupInfo.dwFlags = STARTF_USESTDHANDLES;

                IntPtr lpSize = IntPtr.Zero;
                InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);
                attrList = Marshal.AllocHGlobal(lpSize);
                if (!InitializeProcThreadAttributeList(attrList, 1, 0, ref lpSize))
                    throw new InvalidOperationException("初始化伪终端属性失败。");

                if (!UpdateProcThreadAttribute(attrList, 0, (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, hpc, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                    throw new InvalidOperationException("绑定伪终端属性失败。");

                startupInfo.lpAttributeList = attrList;
                PROCESS_INFORMATION processInfo = new PROCESS_INFORMATION();
                string mutableCommandLine = commandLine;
                bool created = CreateProcess(
                    null,
                    mutableCommandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    EXTENDED_STARTUPINFO_PRESENT,
                    IntPtr.Zero,
                    workingDirectory,
                    ref startupInfo,
                    out processInfo);

                if (!created)
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

                FileStream inputWriter = new FileStream(inputWriteSide, FileAccess.Write, 4096, false);
                StreamWriter writer = new StreamWriter(inputWriter, new UTF8Encoding(false)) { AutoFlush = true };
                FileStream outputReader = new FileStream(outputReadSide, FileAccess.Read, 4096, false);

                ConPtySession session = new ConPtySession(outputQueue, inputWriteSide, inputWriter, writer, outputReader, hpc, attrList, processInfo);
                session.StartReadLoop();
                return session;
            }

            public void WriteLine(string text)
            {
                if (disposed)
                    return;

                writer.Write(text ?? "");
                writer.Write("\r\n");
            }

            public void WriteRaw(string text)
            {
                if (disposed)
                    return;

                writer.Write(text ?? "");
                writer.Flush();
            }

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;

                try { writer?.Dispose(); } catch { }
                try { inputWriter?.Dispose(); } catch { }
                try { inputWriteHandle?.Dispose(); } catch { }
                try { outputReader?.Dispose(); } catch { }

                if (processInfo.hProcess != IntPtr.Zero)
                {
                    try
                    {
                        if (WaitForSingleObject(processInfo.hProcess, 0) == 0x00000102)
                            TerminateProcess(processInfo.hProcess, 0);
                    }
                    catch { }

                    CloseHandle(processInfo.hProcess);
                    processInfo.hProcess = IntPtr.Zero;
                }

                if (processInfo.hThread != IntPtr.Zero)
                {
                    CloseHandle(processInfo.hThread);
                    processInfo.hThread = IntPtr.Zero;
                }

                if (attributeList != IntPtr.Zero)
                {
                    DeleteProcThreadAttributeList(attributeList);
                    Marshal.FreeHGlobal(attributeList);
                }

                if (pseudoConsole != IntPtr.Zero)
                    ClosePseudoConsole(pseudoConsole);
            }

            private async void StartReadLoop()
            {
                byte[] buffer = new byte[4096];
                try
                {
                    while (!disposed)
                    {
                        int count = await outputReader.ReadAsync(buffer, 0, buffer.Length);
                        if (count <= 0)
                            break;

                        outputQueue.Enqueue(Encoding.UTF8.GetString(buffer, 0, count));
                    }
                }
                catch (Exception ex)
                {
                    if (!disposed)
                        outputQueue.Enqueue("[ES Cmd Agent] 后台伪终端读取失败: " + ex.Message + "\n");
                }
                finally
                {
                    if (!disposed)
                        outputQueue.Enqueue("[ES Cmd Agent] 后台伪终端已断开。\n");
                }
            }

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, IntPtr lpPipeAttributes, int nSize);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern int CreatePseudoConsole(COORD size, SafeFileHandle hInput, SafeFileHandle hOutput, uint dwFlags, out IntPtr phPC);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern void ClosePseudoConsole(IntPtr hPC);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            private static extern bool CreateProcess(
                string lpApplicationName,
                string lpCommandLine,
                IntPtr lpProcessAttributes,
                IntPtr lpThreadAttributes,
                bool bInheritHandles,
                int dwCreationFlags,
                IntPtr lpEnvironment,
                string lpCurrentDirectory,
                ref STARTUPINFOEX lpStartupInfo,
                out PROCESS_INFORMATION lpProcessInformation);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool CloseHandle(IntPtr hObject);

            [StructLayout(LayoutKind.Sequential)]
            private struct COORD
            {
                public short X;
                public short Y;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            private struct STARTUPINFO
            {
                public int cb;
                public string lpReserved;
                public string lpDesktop;
                public string lpTitle;
                public int dwX;
                public int dwY;
                public int dwXSize;
                public int dwYSize;
                public int dwXCountChars;
                public int dwYCountChars;
                public int dwFillAttribute;
                public uint dwFlags;
                public short wShowWindow;
                public short cbReserved2;
                public IntPtr lpReserved2;
                public IntPtr hStdInput;
                public IntPtr hStdOutput;
                public IntPtr hStdError;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            private struct STARTUPINFOEX
            {
                public STARTUPINFO StartupInfo;
                public IntPtr lpAttributeList;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct PROCESS_INFORMATION
            {
                public IntPtr hProcess;
                public IntPtr hThread;
                public int dwProcessId;
                public int dwThreadId;
            }
        }

        public static void OpenAndResume()
        {
            var window = GetWindow<ESCmdAgentWindow>();
            window.titleContent = new GUIContent("ES Cmd Agent");
            window.minSize = MinimumWindowSize;
            window.EnsureReasonableWindowSize();
            window.Show();
            window.Focus();
            window.EnsureAgent();

            if (window.agent != null && window.agent.enableAgent && window.agent.autoResumeOnOpen && !window.HasRunningTab())
                window.CreateAndResumeTab(window.GetPreferredResumeSessionId());
            else
                window.EnsureTabExists();
        }

        [MenuItem(MenuItemPathDefine.DEVELOPMENT_MAINTENANCE_PATH + "自动化/Cmd Agent（CMD 中转与架构师）", false, 10)]
        [MenuItem(MenuItemPathDefine.QUICK_WINDOWS_PATH + "Cmd Agent", false, -960)]
        public static void OpenFromMenu()
        {
            ESWindowCommandRegistry.RecordOpened("cmd_agent");
            OpenAndResume();
        }

        private void OnEnable()
        {
            minSize = MinimumWindowSize;
            RestoreWindowStateIfNeeded();
            EnsureAgent();
            EnsureTabExists();
            EnsureTabRuntime();
            EditorApplication.update += FlushOutput;
        }

        private void OnDisable()
        {
            EditorApplication.update -= FlushOutput;
            MarkTabsInterruptedByReload();
            SaveWindowState();
            ReduceLocalResidue();
            StopAllProcesses();
        }

        private void OnGUI()
        {
            minSize = MinimumWindowSize;
            EnsureAgent();
            EnsureTabExists();
            EnsureTabRuntime();

            DrawCompactHeader();

            if (agent == null)
            {
                EditorGUILayout.HelpBox("未找到 ESCmdAgent 全局配置。", MessageType.Warning);
                if (GUILayout.Button("创建或定位 ESCmdAgent", GUILayout.Height(30)))
                    EnsureAgentInteractive();
                return;
            }

            if (mainPageIndex == 0)
            {
                DrawPolishedSessionPage();
            }
            else
            {
                DrawPolishedArchitectPage();
            }

            if (GUI.changed)
                EditorUtility.SetDirty(agent);
        }

        private void DrawCompactHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(4);
                EditorGUILayout.LabelField("【ES】Cmd Agent", EditorStyles.boldLabel, GUILayout.Width(118));
                mainPageIndex = GUILayout.Toolbar(mainPageIndex, new[] { "AI任务", "架构AI" }, EditorStyles.toolbarButton, GUILayout.Width(180));
                GUILayout.Space(8);
                EditorGUILayout.LabelField(
                    mainPageIndex == 0 ? "Unity 内发起 Codex 任务" : "项目全局架构师",
                    EditorStyles.miniBoldLabel,
                    GUILayout.Width(170));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("命令行仍可用，这里负责项目上下文与附件", EditorStyles.miniLabel, GUILayout.Width(230));
            }
        }

        private void DrawPolishedMainPageTabs()
        {
            GUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(4);
                mainPageIndex = GUILayout.Toolbar(mainPageIndex, new[] { "AI任务", "架构AI" }, EditorStyles.toolbarButton, GUILayout.Width(180));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(mainPageIndex == 0 ? "发起 Codex 任务" : "项目全局架构师", EditorStyles.miniBoldLabel, GUILayout.Width(180));
                GUILayout.Space(4);
            }
        }

        private void EnsureReasonableWindowSize()
        {
            Rect rect = position;
            if (rect.width >= MinimumWindowSize.x && rect.height >= MinimumWindowSize.y)
                return;

            rect.width = Mathf.Max(rect.width, DefaultWindowSize.x);
            rect.height = Mathf.Max(rect.height, DefaultWindowSize.y);
            position = rect;
        }

        private bool HasRunningTab()
        {
            return tabs != null && tabs.Any(tab => tab != null && tab.IsRunning);
        }

        private void SaveWindowState()
        {
            try
            {
                var state = new AgentWindowState
                {
                    tabs = CloneSerializableTabs(),
                    selectedTabIndex = selectedTabIndex,
                    mainPageIndex = mainPageIndex,
                    showAdvancedSettings = showAdvancedSettings,
                    showCommandDetails = showCommandDetails,
                    showAICommandComposer = showAICommandComposer,
                    showTerminalTools = showTerminalTools,
                    showTerminalAttachments = showTerminalAttachments,
                    showTerminalDetails = showTerminalDetails,
                    selectedAICommandAssetPath = selectedAICommandAssetPath,
                    selectedAICommandName = selectedAICommandName,
                    aiCommandUserSupplement = aiCommandUserSupplement,
                    aiCommandRequiredValue = aiCommandRequiredValue,
                    aiCommandAttachments = aiCommandAttachments ?? new List<AgentAttachment>(),
                    aiCommandPreviewScroll = aiCommandPreviewScroll,
                    architectIncludeCodexSessions = architectIncludeCodexSessions,
                    architectIncludeAITalkSessions = architectIncludeAITalkSessions,
                    architectIncludeAIWarnings = architectIncludeAIWarnings,
                    architectMaxCodexSessions = architectMaxCodexSessions,
                    architectMaxFilesPerFolder = architectMaxFilesPerFolder,
                    architectSearchText = architectSearchText,
                    architectScroll = architectScroll,
                    architectNodes = architectNodes ?? new List<ArchitectNode>(),
                    architectEdges = architectEdges ?? new List<ArchitectEdge>(),
                    architectSelectedNodeIndex = architectSelectedNodeIndex
                };

                SessionState.SetString(SessionStateKey, JsonUtility.ToJson(state));
            }
            catch
            {
                // Best effort only; EditorWindow serialization still keeps the current inspector state.
            }
        }

        private List<AgentSessionTab> CloneSerializableTabs()
        {
            List<AgentSessionTab> result = new List<AgentSessionTab>();
            if (tabs == null)
                return result;

            foreach (AgentSessionTab tab in tabs)
            {
                if (tab == null)
                    continue;

                result.Add(new AgentSessionTab
                {
                    title = tab.title,
                    sessionId = tab.sessionId,
                    createdAt = tab.createdAt,
                    lastStartTime = tab.lastStartTime,
                    lastStopTime = tab.lastStopTime,
                    lastCommand = tab.lastCommand,
                    summary = tab.summary,
                    capturedSessionKey = tab.capturedSessionKey,
                    createdSessionFile = tab.createdSessionFile,
                    attachments = tab.attachments ?? new List<AgentAttachment>()
                });
            }

            return result;
        }

        private void RestoreWindowStateIfNeeded()
        {
            if (tabs != null && tabs.Count > 0)
                return;

            string json = SessionState.GetString(SessionStateKey, "");
            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                AgentWindowState state = JsonUtility.FromJson<AgentWindowState>(json);
                if (state == null)
                    return;

                tabs = state.tabs ?? new List<AgentSessionTab>();
                selectedTabIndex = state.selectedTabIndex;
                mainPageIndex = state.mainPageIndex;
                showAdvancedSettings = state.showAdvancedSettings;
                showCommandDetails = state.showCommandDetails;
                showAICommandComposer = false;
                showTerminalTools = false;
                showTerminalAttachments = false;
                showTerminalDetails = false;
                selectedAICommandAssetPath = state.selectedAICommandAssetPath ?? "";
                selectedAICommandName = state.selectedAICommandName ?? "";
                aiCommandUserSupplement = state.aiCommandUserSupplement ?? "";
                aiCommandRequiredValue = state.aiCommandRequiredValue ?? "";
                aiCommandAttachments = state.aiCommandAttachments ?? new List<AgentAttachment>();
                aiCommandPreviewScroll = state.aiCommandPreviewScroll;
                architectIncludeCodexSessions = state.architectIncludeCodexSessions;
                architectIncludeAITalkSessions = state.architectIncludeAITalkSessions;
                architectIncludeAIWarnings = state.architectIncludeAIWarnings;
                architectMaxCodexSessions = state.architectMaxCodexSessions > 0 ? state.architectMaxCodexSessions : architectMaxCodexSessions;
                architectMaxFilesPerFolder = state.architectMaxFilesPerFolder > 0 ? state.architectMaxFilesPerFolder : architectMaxFilesPerFolder;
                architectSearchText = state.architectSearchText ?? "";
                architectScroll = state.architectScroll;
                architectNodes = state.architectNodes ?? new List<ArchitectNode>();
                architectEdges = state.architectEdges ?? new List<ArchitectEdge>();
                architectSelectedNodeIndex = state.architectSelectedNodeIndex;
            }
            catch
            {
                tabs = new List<AgentSessionTab>();
            }
        }

        private void MarkTabsInterruptedByReload()
        {
            if (tabs == null || (!EditorApplication.isCompiling && !EditorApplication.isUpdating))
                return;

            foreach (AgentSessionTab tab in tabs)
            {
                if (tab == null || !tab.IsRunning)
                    continue;

                tab.lastStopTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                tab.summary = "Unity 编译重载已断开，可点击“重连本页”继续";
            }
        }

        private void DrawPolishedSessionPage()
        {
            AgentSessionTab tab = GetCurrentTab();
            if (tab == null)
                return;

            DrawTerminalCommandBar(tab);
            DrawTerminalStatusNotice(tab);
            DrawTerminalInputBar(tab);
            DrawTerminalScreen(tab);
            DrawTerminalQuickFoldoutBar();
            DrawTerminalFoldouts(tab);
        }

        private void DrawTerminalCommandBar(AgentSessionTab tab)
        {
            if (tabs == null || tabs.Count == 0 || tab == null)
                return;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(4);
                EditorGUILayout.LabelField(tab.IsRunning ? "● 运行中" : "○ 未运行", EditorStyles.miniBoldLabel, GUILayout.Width(72));

                using (new EditorGUI.DisabledScope(!agent.enableAgent || tab.IsRunning))
                {
                    if (DrawToolbarButton("恢复", 58, ButtonTone.Primary, true))
                        ShowResumeSessionMenu(GUILayoutUtility.GetLastRect());
                }

                using (new EditorGUI.DisabledScope(!agent.enableAgent || tab.IsRunning))
                {
                    if (DrawToolbarButton("新会话", 56, ButtonTone.Normal))
                        CreateAndStartFreshTab();
                }

                using (new EditorGUI.DisabledScope(!agent.enableAgent || tab.IsRunning))
                {
                    if (DrawToolbarButton("启动本页", 64, ButtonTone.Primary))
                        StartResume(tab, tab.sessionId);
                }

                using (new EditorGUI.DisabledScope(!tab.IsRunning))
                {
                    if (DrawToolbarButton("停止", 46, ButtonTone.Danger))
                        StopProcess(tab);
                }

                if (GUILayout.Button("更多", EditorStyles.toolbarButton, GUILayout.Width(46)))
                    ShowTerminalMoreMenu(tab);

                GUILayout.Space(8);

                string[] tabNames = BuildVisibleTabNames(out int[] visibleIndices);
                if (tabNames.Length > 0)
                {
                    int visibleSelected = Array.IndexOf(visibleIndices, Mathf.Clamp(selectedTabIndex, 0, tabs.Count - 1));
                    if (visibleSelected < 0)
                        visibleSelected = 0;

                    int nextVisible = GUILayout.Toolbar(visibleSelected, tabNames, EditorStyles.toolbarButton, GUILayout.MinWidth(260), GUILayout.MaxWidth(760));
                    selectedTabIndex = visibleIndices[Mathf.Clamp(nextVisible, 0, visibleIndices.Length - 1)];
                }

                GUILayout.Space(6);
                tab.title = EditorGUILayout.TextField(tab.title, EditorStyles.toolbarTextField, GUILayout.Width(130));

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(GetCompactSessionState(tab), EditorStyles.miniLabel, GUILayout.Width(210));

                if (showTerminalTools || showTerminalAttachments || showTerminalDetails)
                {
                    if (GUILayout.Button("收起扩展", EditorStyles.toolbarButton, GUILayout.Width(72)))
                    {
                        showTerminalTools = false;
                        showTerminalAttachments = false;
                        showTerminalDetails = false;
                    }
                }

                if (DrawToolbarButton("清屏", 46, ButtonTone.Normal))
                    ClearTabOutput(tab);
            }
        }

        private enum ButtonTone
        {
            Normal,
            Primary,
            Danger
        }

        private static bool DrawToolbarButton(string label, float width, ButtonTone tone, bool dropdown = false)
        {
            Rect rect = GUILayoutUtility.GetRect(width, 22, GUILayout.Width(width), GUILayout.Height(22));
            Color accent = tone switch
            {
                ButtonTone.Primary => new Color(0.28f, 0.78f, 0.68f),
                ButtonTone.Danger => new Color(0.92f, 0.34f, 0.30f),
                _ => new Color(0.54f, 0.56f, 0.60f)
            };

            bool enabled = GUI.enabled;
            if (!enabled)
                accent = new Color(0.36f, 0.36f, 0.36f);

            bool hover = enabled && rect.Contains(Event.current.mousePosition);
            Color body = tone == ButtonTone.Normal
                ? new Color(0.32f, 0.33f, 0.35f)
                : Color.Lerp(new Color(0.26f, 0.28f, 0.30f), accent, hover ? 0.38f : 0.24f);
            Color border = Color.Lerp(new Color(0.12f, 0.12f, 0.12f), accent, hover ? 0.70f : 0.45f);

            EditorGUI.DrawRect(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), border);
            EditorGUI.DrawRect(rect, body);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), accent);
            EditorGUI.DrawRect(new Rect(rect.x + 3, rect.y, rect.width - 3, 1), Color.Lerp(body, Color.white, 0.20f));

            GUIStyle style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = enabled ? new Color(0.92f, 0.94f, 0.96f) : new Color(0.58f, 0.58f, 0.58f) }
            };
            GUI.Label(rect, dropdown ? label + " ▾" : label, style);

            if (!enabled)
                return false;

            Event current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0 && rect.Contains(current.mousePosition))
            {
                current.Use();
                return true;
            }

            return false;
        }

        private string[] BuildVisibleTabNames(out int[] visibleIndices)
        {
            if (tabs == null || tabs.Count == 0)
            {
                visibleIndices = Array.Empty<int>();
                return Array.Empty<string>();
            }

            int selected = Mathf.Clamp(selectedTabIndex, 0, tabs.Count - 1);
            List<int> indices = new List<int>();
            indices.Add(selected);

            for (int i = 0; i < tabs.Count && indices.Count < 8; i++)
            {
                if (i != selected)
                    indices.Add(i);
            }

            indices.Sort();
            visibleIndices = indices.ToArray();
            string[] names = new string[visibleIndices.Length];
            for (int i = 0; i < visibleIndices.Length; i++)
            {
                AgentSessionTab item = tabs[visibleIndices[i]];
                string state = item != null && item.IsRunning ? "● " : "";
                string title = item != null && !string.IsNullOrWhiteSpace(item.title) ? item.title : "会话";
                names[i] = state + ShortLabel(title, 10);
            }

            return names;
        }

        private static string ShortLabel(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value ?? "";

            return value.Substring(0, Mathf.Max(1, maxLength - 1)) + "...";
        }

        private static string GetCompactSessionState(AgentSessionTab tab)
        {
            if (tab == null)
                return "";

            string key = string.IsNullOrWhiteSpace(tab.sessionId) ? "最近会话" : "Key " + ShortId(tab.sessionId);
            string summary = string.IsNullOrWhiteSpace(tab.summary) ? "等待启动" : tab.summary;
            return key + " / " + summary;
        }

        private void ShowTerminalMoreMenu(AgentSessionTab tab)
        {
            GenericMenu menu = new GenericMenu();
            if (string.IsNullOrWhiteSpace(tab?.sessionId))
                menu.AddDisabledItem(new GUIContent("复制恢复 Key（当前未记录）"));
            else
                menu.AddItem(new GUIContent("复制恢复 Key"), false, () => CopyResumeKey(tab));
            if (string.IsNullOrWhiteSpace(tab?.sessionId))
                menu.AddDisabledItem(new GUIContent("保存当前标题为会话名（当前未记录 Key）"));
            else
                menu.AddItem(new GUIContent("保存当前标题为会话名"), false, () => SaveCurrentTabAlias(tab));
            if (string.IsNullOrWhiteSpace(tab?.sessionId))
                menu.AddDisabledItem(new GUIContent("置顶/取消置顶当前会话（当前未记录 Key）"));
            else
                menu.AddItem(new GUIContent(IsSessionPinned(tab.sessionId) ? "取消置顶当前会话" : "置顶当前会话"), false, () => ToggleCurrentSessionPin(tab));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("按名称排序页签"), false, SortTabsByTitle);
            menu.AddItem(new GUIContent("按创建时间排序页签"), false, SortTabsByCreatedAt);
            menu.AddItem(new GUIContent("运行中优先"), false, SortTabsByRunningState);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("查看云端任务列表"), false, StartCloudTaskList);
            menu.AddDisabledItem(new GUIContent("云端任务不能直接当成本地 resume 会话恢复"));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("显示 AICommand / 项目记忆"), showTerminalTools, () => showTerminalTools = !showTerminalTools);
            menu.AddItem(new GUIContent("显示附件"), showTerminalAttachments, () => showTerminalAttachments = !showTerminalAttachments);
            menu.AddItem(new GUIContent("显示页签 / 设置"), showTerminalDetails, () => showTerminalDetails = !showTerminalDetails);
            menu.ShowAsContext();
        }

        private static void ClearTabOutput(AgentSessionTab tab)
        {
            if (tab == null)
                return;

            tab.outputText = "";
            tab.terminal?.Clear();
            tab.scroll = Vector2.zero;
        }

        private void DrawTerminalTabStrip()
        {
            if (tabs == null || tabs.Count == 0)
                return;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("恢复最近", EditorStyles.toolbarButton, GUILayout.Width(68)))
                    CreateAndResumeTab("");

                if (GUILayout.Button("新会话", EditorStyles.toolbarButton, GUILayout.Width(56)))
                    CreateAndStartFreshTab();

                if (GUILayout.Button("排序", EditorStyles.toolbarButton, GUILayout.Width(48)))
                    ShowTabSortMenu();

                using (new EditorGUI.DisabledScope(tabs.Count <= 1 || selectedTabIndex <= 0))
                {
                    if (GUILayout.Button("<", EditorStyles.toolbarButton, GUILayout.Width(24)))
                        MoveTabLeft(selectedTabIndex);
                }

                using (new EditorGUI.DisabledScope(tabs.Count <= 1 || selectedTabIndex < 0 || selectedTabIndex >= tabs.Count - 1))
                {
                    if (GUILayout.Button(">", EditorStyles.toolbarButton, GUILayout.Width(24)))
                        MoveTabRight(selectedTabIndex);
                }

                string[] tabNames = new string[tabs.Count];
                for (int i = 0; i < tabs.Count; i++)
                {
                    AgentSessionTab item = tabs[i];
                    string state = item != null && item.IsRunning ? "● " : "○ ";
                    tabNames[i] = state + (item != null && !string.IsNullOrWhiteSpace(item.title) ? item.title : "会话");
                }

                selectedTabIndex = Mathf.Clamp(selectedTabIndex, 0, tabs.Count - 1);
                selectedTabIndex = GUILayout.Toolbar(selectedTabIndex, tabNames, EditorStyles.toolbarButton);
            }
        }

        private void DrawTerminalTopBar(AgentSessionTab tab)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(4);
                string state = tab.IsRunning ? "运行中" : "未运行";
                EditorGUILayout.LabelField("后台 CMD", EditorStyles.miniBoldLabel, GUILayout.Width(62));
                EditorGUILayout.LabelField(state, EditorStyles.miniLabel, GUILayout.Width(48));

                using (new EditorGUI.DisabledScope(!agent.enableAgent || tab.IsRunning))
                {
                    if (GUILayout.Button("启动", EditorStyles.toolbarButton, GUILayout.Width(52)))
                        StartResume(tab, tab.sessionId);
                }

                using (new EditorGUI.DisabledScope(!tab.IsRunning))
                {
                    if (GUILayout.Button("停止", EditorStyles.toolbarButton, GUILayout.Width(52)))
                        StopProcess(tab);
                }

                if (GUILayout.Button("历史", EditorStyles.toolbarButton, GUILayout.Width(48)))
                    ShowCodexSessionIndexMenu(GUILayoutUtility.GetLastRect());

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(tab.sessionId)))
                {
                    if (GUILayout.Button("复制Key", EditorStyles.toolbarButton, GUILayout.Width(62)))
                        CopyResumeKey(tab);
                }

                GUILayout.Space(8);
                tab.title = EditorGUILayout.TextField(tab.title, EditorStyles.toolbarTextField, GUILayout.Width(150));
                GUILayout.Space(4);
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(tab.summary) ? "等待启动" : tab.summary, EditorStyles.miniLabel, GUILayout.MinWidth(160));
                GUILayout.FlexibleSpace();

                if (showTerminalTools || showTerminalAttachments || showTerminalDetails)
                {
                    if (GUILayout.Button("收起扩展", EditorStyles.toolbarButton, GUILayout.Width(72)))
                    {
                        showTerminalTools = false;
                        showTerminalAttachments = false;
                        showTerminalDetails = false;
                    }
                }

                if (GUILayout.Button("清屏", EditorStyles.toolbarButton, GUILayout.Width(52)))
                    ClearTabOutput(tab);
            }
        }

        private void DrawTerminalInputBar(AgentSessionTab tab)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.toolbar))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUIStyle inputStyle = new GUIStyle(EditorStyles.textArea)
                    {
                        wordWrap = true,
                        fontSize = 13
                    };
                    tab.inputText = EditorGUILayout.TextArea(tab.inputText, inputStyle, GUILayout.MinHeight(28), GUILayout.MaxHeight(54));

                    using (new EditorGUILayout.HorizontalScope(GUILayout.Width(384)))
                    {
                        string sendLabel = tab.conPty != null && tab.conPty.IsRunning ? "发送" : "启动并发送";
                        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(tab.inputText)))
                        {
                            if (DrawToolbarButton(sendLabel, 86, ButtonTone.Primary))
                                SendUserInputSmart(tab);
                        }

                        using (new EditorGUI.DisabledScope(tab.conPty == null || !tab.conPty.IsRunning))
                        {
                            if (GUILayout.Button("回车", EditorStyles.toolbarButton, GUILayout.Width(50), GUILayout.Height(28)))
                                SendRawConPtyLine(tab, "");
                            if (GUILayout.Button("信任1", EditorStyles.toolbarButton, GUILayout.Width(54), GUILayout.Height(28)))
                                SendRawConPtyLine(tab, "1");
                            if (DrawToolbarButton("Ctrl+C", 56, ButtonTone.Danger))
                                SendRawConPtyText(tab, "\u0003", "Ctrl+C");
                            if (GUILayout.Button("Esc", EditorStyles.toolbarButton, GUILayout.Width(38), GUILayout.Height(28)))
                                SendRawConPtyText(tab, "\u001b", "Esc");
                            if (GUILayout.Button("Tab", EditorStyles.toolbarButton, GUILayout.Width(42), GUILayout.Height(28)))
                                SendRawConPtyText(tab, "\t", "Tab");
                            if (GUILayout.Button("↑", EditorStyles.toolbarButton, GUILayout.Width(28), GUILayout.Height(28)))
                                SendRawConPtyText(tab, "\u001b[A", "↑");
                            if (GUILayout.Button("↓", EditorStyles.toolbarButton, GUILayout.Width(28), GUILayout.Height(28)))
                                SendRawConPtyText(tab, "\u001b[B", "↓");
                            if (GUILayout.Button("键", EditorStyles.toolbarDropDown, GUILayout.Width(28), GUILayout.Height(28)))
                                ShowTerminalKeyMenu(tab);
                        }
                    }
                }
            }
        }

        private static void DrawTerminalStatusNotice(AgentSessionTab tab)
        {
            if (tab == null || string.IsNullOrWhiteSpace(tab.statusNotice))
                return;

            Color color = tab.statusNoticeKind switch
            {
                1 => new Color(0.26f, 0.78f, 0.42f),
                2 => new Color(0.92f, 0.38f, 0.32f),
                _ => new Color(0.25f, 0.64f, 0.86f)
            };

            Rect rect = EditorGUILayout.GetControlRect(false, 24);
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.19f, 0.20f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), new Color(color.r, color.g, color.b, 0.65f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.x + 4, rect.y, rect.width - 4, rect.height), new Color(color.r, color.g, color.b, 0.10f));
            GUIStyle style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft
            };
            string prefix = tab.statusNoticeKind == 1 ? "完成  " : tab.statusNoticeKind == 2 ? "注意  " : "状态  ";
            GUI.Label(new Rect(rect.x + 10, rect.y, rect.width - 20, rect.height), prefix + tab.statusNotice, style);
        }

        private static void SetStatusNotice(AgentSessionTab tab, string text, int kind)
        {
            if (tab == null)
                return;

            tab.statusNotice = text ?? "";
            tab.statusNoticeKind = kind;
        }

        private void ShowTerminalKeyMenu(AgentSessionTab tab)
        {
            GenericMenu menu = new GenericMenu();
            if (tab?.conPty == null || !tab.conPty.IsRunning)
            {
                menu.AddDisabledItem(new GUIContent("后台 CMD 未运行"));
                menu.ShowAsContext();
                return;
            }

            menu.AddItem(new GUIContent("Esc"), false, () => SendRawConPtyText(tab, "\u001b", "Esc"));
            menu.AddItem(new GUIContent("Ctrl+C"), false, () => SendRawConPtyText(tab, "\u0003", "Ctrl+C"));
            menu.AddItem(new GUIContent("Tab"), false, () => SendRawConPtyText(tab, "\t", "Tab"));
            menu.AddItem(new GUIContent("上方向键"), false, () => SendRawConPtyText(tab, "\u001b[A", "↑"));
            menu.AddItem(new GUIContent("下方向键"), false, () => SendRawConPtyText(tab, "\u001b[B", "↓"));
            menu.ShowAsContext();
        }

        private void DrawTerminalScreen(AgentSessionTab tab)
        {
            GUIStyle terminalStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = false,
                richText = true,
                fontSize = 12,
                font = EditorStyles.miniLabel.font,
                normal =
                {
                    textColor = new Color(0.86f, 0.90f, 0.88f),
                    background = Texture2D.grayTexture
                }
            };

            tab.scroll = EditorGUILayout.BeginScrollView(tab.scroll, GUILayout.ExpandHeight(true));
            string output = string.IsNullOrEmpty(tab.outputText)
                ? "暂无输出。点击“启动”创建后台 CMD/codex，或直接输入内容后点击“启动并发送”。"
                : tab.outputText;
            EditorGUILayout.TextArea(output, terminalStyle, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void DrawTerminalQuickFoldoutBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                DrawFoldoutToggle("AICommand", ref showTerminalTools, 92);
                DrawFoldoutToggle("附件", ref showTerminalAttachments, 54);
                DrawFoldoutToggle("页签设置", ref showTerminalDetails, 76);
                GUILayout.FlexibleSpace();
                if (showTerminalTools || showTerminalAttachments || showTerminalDetails)
                {
                    if (GUILayout.Button("全部收起", EditorStyles.toolbarButton, GUILayout.Width(68)))
                    {
                        showTerminalTools = false;
                        showTerminalAttachments = false;
                        showTerminalDetails = false;
                    }
                }
            }
        }

        private static void DrawFoldoutToggle(string label, ref bool value, float width)
        {
            string prefix = value ? "▼ " : "▶ ";
            if (GUILayout.Button(prefix + label, EditorStyles.toolbarButton, GUILayout.Width(width)))
                value = !value;
        }

        private void DrawTerminalFoldouts(AgentSessionTab tab)
        {
            if (!showTerminalTools && !showTerminalAttachments && !showTerminalDetails)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
            showTerminalTools = EditorGUILayout.Foldout(showTerminalTools, "AICommand / 项目记忆", true);
            if (showTerminalTools)
            {
                DrawAICollaborationActions();
                DrawAICommandComposer();
            }

            showTerminalAttachments = EditorGUILayout.Foldout(showTerminalAttachments, "附件", true);
            if (showTerminalAttachments)
            {
                DrawAttachmentDropZone(
                    "把文件、Unity 资产或外部素材拖到这里，会追加到本页输入",
                    tab.attachments,
                    text => tab.inputText = AppendAttachmentBlock(tab.inputText, text));
                DrawAttachmentList(tab.attachments);
            }

            showTerminalDetails = EditorGUILayout.Foldout(showTerminalDetails, "页签 / 设置", true);
            if (showTerminalDetails)
            {
                DrawSessionTabs();
                DrawAdvancedSettings();
                DrawCurrentSessionDetailsOnly(tab);
            }
            }
        }

        private void DrawBeginnerHint()
        {
            AgentSessionTab current = GetCurrentTab();
            if (current != null && (current.IsRunning || !string.IsNullOrWhiteSpace(current.outputText)))
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("快速上手", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("直接在底部输入需求并点击“发送”，工具会自动恢复最近会话；需要固定某次对话时，在高级设置里填写会话 ID。", EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawPolishedStatusStrip()
        {
            AgentSessionTab current = GetCurrentTab();
            int runningCount = tabs.Count(tab => tab != null && tab.IsRunning);
            string keyState = current != null && !string.IsNullOrWhiteSpace(current.sessionId) ? ShortId(current.sessionId) : "等待记录";

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPolishedMetric("Agent", agent != null && agent.enableAgent ? "已启用" : "未启用", agent != null && agent.enableAgent);
                DrawPolishedMetric("运行页签", runningCount.ToString(), runningCount > 0);
                DrawPolishedMetric("恢复 Key", keyState, current != null && !string.IsNullOrWhiteSpace(current.sessionId));
                DrawPolishedMetric("本地留存", $"最多 {Mathf.Clamp(agent.maxLocalTabsToKeep, 1, 12)} 页签", true);
            }
        }

        private static void DrawPolishedMetric(string label, string value, bool ok)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 46, GUILayout.MinWidth(140));
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.19f, 0.21f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), ok ? new Color(0.25f, 0.70f, 0.42f) : new Color(0.86f, 0.48f, 0.25f));

            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.72f, 0.75f, 0.80f) }
            };
            GUIStyle valueStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(rect.x + 12, rect.y + 6, rect.width - 18, 16), label, labelStyle);
            GUI.Label(new Rect(rect.x + 12, rect.y + 23, rect.width - 18, 18), value, valueStyle);
        }

        private void DrawPolishedPrimaryActions()
        {
            AgentSessionTab current = GetCurrentTab();
            GUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(4);
                using (new EditorGUI.DisabledScope(!agent.enableAgent))
                {
                    if (GUILayout.Button("后台CMD", EditorStyles.toolbarButton, GUILayout.Width(76)))
                        CreateAndResumeTab("");

                    if (GUILayout.Button("CMD指定", EditorStyles.toolbarButton, GUILayout.Width(76)))
                        CreateAndResumeTab(agent.resumeSessionId);

                    using (new EditorGUI.DisabledScope(current == null || current.IsRunning))
                    {
                        if (GUILayout.Button("重启CMD", EditorStyles.toolbarButton, GUILayout.Width(76)))
                            StartResume(current, current.sessionId);
                    }
                }

                GUILayout.Space(10);
                using (new EditorGUI.DisabledScope(current == null || string.IsNullOrWhiteSpace(current.sessionId)))
                {
                    if (GUILayout.Button("复制 Key", EditorStyles.toolbarButton, GUILayout.Width(76)))
                        CopyResumeKey(current);
                }

                if (GUILayout.Button("历史会话", EditorStyles.toolbarButton, GUILayout.Width(78)))
                    ShowCodexSessionIndexMenu(GUILayoutUtility.GetLastRect());

                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(current == null || !current.IsRunning))
                {
                    if (GUILayout.Button("停止", EditorStyles.toolbarButton, GUILayout.Width(58)))
                        StopProcess(current);
                }

                if (GUILayout.Button("关闭页签", EditorStyles.toolbarButton, GUILayout.Width(78)))
                    CloseCurrentTab();

                if (GUILayout.Button("清理停止页", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    RemoveStoppedTabs();
                GUILayout.Space(4);
            }
        }

        private void DrawAICollaborationActions()
        {
            AgentSessionTab current = GetCurrentTab();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(4);
                EditorGUILayout.LabelField("常用指令", EditorStyles.miniBoldLabel, GUILayout.Width(60));

                if (GUILayout.Button("读取项目规则", EditorStyles.toolbarButton, GUILayout.Width(104)))
                    SendPromptToCurrentTab(BuildReadWarningsPrompt());

                if (GUILayout.Button("更新项目记忆", EditorStyles.toolbarButton, GUILayout.Width(104)))
                    SendPromptToCurrentTab(BuildUpdateWarningsPrompt());

                if (GUILayout.Button("执行预设指令", EditorStyles.toolbarButton, GUILayout.Width(104)))
                    ShowAICommandMenu(GUILayoutUtility.GetLastRect());

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(current != null && current.IsRunning ? "当前页可直接发送" : "未启动也可直接发送", EditorStyles.miniLabel, GUILayout.Width(150));
                GUILayout.Space(4);
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(4);
                EditorGUILayout.LabelField("文件入口", EditorStyles.miniBoldLabel, GUILayout.Width(60));

                if (GUILayout.Button("打开记忆库", EditorStyles.toolbarButton, GUILayout.Width(88)))
                    RevealProjectRelativePath(AIWarningsRelativePath);

                if (GUILayout.Button("打开指令库", EditorStyles.toolbarButton, GUILayout.Width(88)))
                    RevealProjectRelativePath(AICommandsRelativePath);

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("规则、预设指令和项目长期结论都从这里进入。", EditorStyles.miniLabel, GUILayout.MinWidth(220));
            }
        }

        private void DrawAICommandComposer()
        {
            if (!showAICommandComposer)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("指令准备区", EditorStyles.boldLabel, GUILayout.Width(86));
                    EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(selectedAICommandName) ? "未选择指令" : selectedAICommandName, EditorStyles.miniBoldLabel);

                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(selectedAICommandAssetPath)))
                    {
                        if (GUILayout.Button("定位文件", GUILayout.Width(72)))
                            RevealProjectRelativePath(selectedAICommandAssetPath);

                        if (GUILayout.Button("复制路径", GUILayout.Width(72)))
                            EditorGUIUtility.systemCopyBuffer = selectedAICommandAssetPath;
                    }

                    if (GUILayout.Button("关闭", GUILayout.Width(58)))
                        showAICommandComposer = false;
                }

                EditorGUILayout.LabelField("补充目标、路径、报错、对象名或期望结果；留空也可以发送，但 AI 会按命令文件的默认约束执行。", EditorStyles.wordWrappedMiniLabel);
                AICommandInfo commandInfo = ParseAICommandInfo(selectedAICommandAssetPath);
                DrawAICommandInfoStrip(commandInfo);

                EditorGUILayout.LabelField(commandInfo.requirementTitle, EditorStyles.miniBoldLabel);
                aiCommandRequiredValue = EditorGUILayout.TextArea(aiCommandRequiredValue, GUILayout.MinHeight(42), GUILayout.MaxHeight(86));

                EditorGUILayout.LabelField("额外补充", EditorStyles.miniBoldLabel);
                aiCommandUserSupplement = EditorGUILayout.TextArea(aiCommandUserSupplement, GUILayout.MinHeight(56), GUILayout.MaxHeight(110));
                DrawAttachmentDropZone(
                    "把文件、Unity 资产或外部素材拖到这里，会追加到指令补充",
                    aiCommandAttachments,
                    text => aiCommandUserSupplement = AppendAttachmentBlock(aiCommandUserSupplement, text));
                DrawAttachmentList(aiCommandAttachments);

                string preview = BuildUseAICommandPrompt(selectedAICommandAssetPath, aiCommandRequiredValue, aiCommandUserSupplement);
                EditorGUILayout.LabelField("将发送给 AI 的内容", EditorStyles.miniBoldLabel);
                aiCommandPreviewScroll = EditorGUILayout.BeginScrollView(aiCommandPreviewScroll, GUILayout.Height(88));
                EditorGUILayout.TextArea(preview, EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(selectedAICommandAssetPath)))
                    {
                        if (GUILayout.Button("发送指令", GUILayout.Height(26), GUILayout.Width(96)))
                            SendPreparedAICommand(preview, commandInfo);

                        if (GUILayout.Button("只填入输入框", GUILayout.Height(26), GUILayout.Width(104)))
                        {
                            AgentSessionTab tab = GetCurrentTab();
                            if (tab != null)
                                tab.inputText = preview;
                        }
                    }

                    if (GUILayout.Button("清空补充", GUILayout.Height(26), GUILayout.Width(88)))
                    {
                        aiCommandRequiredValue = "";
                        aiCommandUserSupplement = "";
                    }

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("不会自动改文件，是否执行仍由本轮 AI 根据指令和你的补充判断。", EditorStyles.miniLabel, GUILayout.MinWidth(280));
                }
            }
        }

        private void DrawAttachmentDropZone(string label, List<AgentAttachment> attachments, Action<string> appendText)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                Rect dropRect = GUILayoutUtility.GetRect(0, 34, GUILayout.ExpandWidth(true));
                bool hover = dropRect.Contains(Event.current.mousePosition);
                EditorGUI.DrawRect(dropRect, hover ? new Color(0.20f, 0.26f, 0.34f) : new Color(0.16f, 0.17f, 0.19f));
                GUI.Label(new Rect(dropRect.x + 10, dropRect.y + 8, dropRect.width - 20, 18), label, EditorStyles.miniLabel);

                HandleAttachmentDrag(dropRect, attachments, appendText);

                if (GUILayout.Button("粘贴剪贴板图片", GUILayout.Width(126), GUILayout.Height(28)))
                {
                    if (TrySaveClipboardImageToProject(out string assetPath, out string error))
                    {
                        List<AgentAttachment> newAttachments = BuildAttachmentEntries(new[] { assetPath });
                        AddAttachments(attachments, newAttachments);
                        appendText?.Invoke(BuildAttachmentText(newAttachments));
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("未读取到剪贴板图片", error, "知道了");
                    }
                }
            }
        }

        private static void HandleAttachmentDrag(Rect dropRect, List<AgentAttachment> attachments, Action<string> appendText)
        {
            Event current = Event.current;
            if (!dropRect.Contains(current.mousePosition))
                return;

            if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform)
                return;

            List<string> paths = CollectDraggedPaths();
            DragAndDrop.visualMode = paths.Count > 0 ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

            if (current.type == EventType.DragPerform && paths.Count > 0)
            {
                DragAndDrop.AcceptDrag();
                List<AgentAttachment> newAttachments = BuildAttachmentEntries(paths);
                AddAttachments(attachments, newAttachments);
                appendText?.Invoke(BuildAttachmentText(newAttachments));
            }

            current.Use();
        }

        private static List<string> CollectDraggedPaths()
        {
            List<string> paths = new List<string>();

            if (DragAndDrop.paths != null)
            {
                foreach (string path in DragAndDrop.paths)
                    AddUniquePath(paths, NormalizeAttachmentPath(path));
            }

            if (DragAndDrop.objectReferences != null)
            {
                foreach (UnityEngine.Object obj in DragAndDrop.objectReferences)
                {
                    if (obj == null)
                        continue;

                    AddUniquePath(paths, NormalizeAttachmentPath(AssetDatabase.GetAssetPath(obj)));
                }
            }

            return paths;
        }

        private static void AddUniquePath(List<string> paths, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (!paths.Any(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)))
                paths.Add(path);
        }

        private static string NormalizeAttachmentPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "";

            path = path.Trim().Replace('\\', '/');
            string projectRoot = NormalizePath(ProjectRoot).Replace('\\', '/');
            string normalized = NormalizePath(path).Replace('\\', '/');
            if (normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                return normalized.Substring(projectRoot.Length).TrimStart('/');

            return path;
        }

        private void DrawAttachmentList(List<AgentAttachment> attachments)
        {
            if (attachments == null || attachments.Count == 0)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"附件 {attachments.Count}", EditorStyles.miniBoldLabel, GUILayout.Width(70));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("清空附件", GUILayout.Width(78)))
                        attachments.Clear();
                }

                for (int i = attachments.Count - 1; i >= 0; i--)
                {
                    AgentAttachment attachment = attachments[i];
                    if (attachment == null || string.IsNullOrWhiteSpace(attachment.path))
                    {
                        attachments.RemoveAt(i);
                        continue;
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(attachment.image ? "图片" : "文件", EditorStyles.miniLabel, GUILayout.Width(34));
                        EditorGUILayout.LabelField(attachment.displayName, EditorStyles.miniBoldLabel, GUILayout.Width(160));
                        EditorGUILayout.LabelField(FormatBytes(attachment.sizeBytes), EditorStyles.miniLabel, GUILayout.Width(70));
                        EditorGUILayout.SelectableLabel(attachment.path, EditorStyles.textField, GUILayout.Height(18));

                        if (GUILayout.Button("定位", GUILayout.Width(44)))
                            RevealAttachment(attachment.path);

                        if (GUILayout.Button("移除", GUILayout.Width(44)))
                            attachments.RemoveAt(i);
                    }
                }
            }
        }

        private static List<AgentAttachment> BuildAttachmentEntries(IEnumerable<string> paths)
        {
            List<AgentAttachment> entries = new List<AgentAttachment>();
            foreach (string path in paths)
            {
                string normalized = NormalizeAttachmentPath(path);
                if (string.IsNullOrWhiteSpace(normalized))
                    continue;

                entries.Add(new AgentAttachment
                {
                    path = normalized,
                    displayName = Path.GetFileName(normalized.TrimEnd('/', '\\')),
                    sizeBytes = GetAttachmentSize(normalized),
                    projectAsset = normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase),
                    image = IsImagePath(normalized)
                });
            }

            return entries;
        }

        private static void AddAttachments(List<AgentAttachment> attachments, IEnumerable<AgentAttachment> newAttachments)
        {
            if (attachments == null || newAttachments == null)
                return;

            foreach (AgentAttachment attachment in newAttachments)
            {
                if (attachment == null || string.IsNullOrWhiteSpace(attachment.path))
                    continue;

                if (!attachments.Any(existing => string.Equals(existing.path, attachment.path, StringComparison.OrdinalIgnoreCase)))
                    attachments.Add(attachment);
            }
        }

        private static string BuildAttachmentText(IEnumerable<AgentAttachment> attachments)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("附件路径：");
            foreach (AgentAttachment attachment in attachments)
            {
                if (attachment == null || string.IsNullOrWhiteSpace(attachment.path))
                    continue;

                builder.AppendLine("- " + attachment.path);
            }

            return builder.ToString().TrimEnd();
        }

        private static long GetAttachmentSize(string path)
        {
            try
            {
                string fullPath = GetAttachmentFullPath(path);
                if (File.Exists(fullPath))
                    return new FileInfo(fullPath).Length;
            }
            catch
            {
                return 0;
            }

            return 0;
        }

        private static string GetAttachmentFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "";

            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(ProjectRoot, path.Replace('/', Path.DirectorySeparatorChar));

            return path.Replace('/', Path.DirectorySeparatorChar);
        }

        private static bool IsImagePath(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0)
                return "-";
            if (bytes < 1024)
                return bytes + " B";
            if (bytes < 1024 * 1024)
                return (bytes / 1024f).ToString("0.0") + " KB";

            return (bytes / 1024f / 1024f).ToString("0.0") + " MB";
        }

        private static void RevealAttachment(string path)
        {
            string fullPath = GetAttachmentFullPath(path);
            if (File.Exists(fullPath) || Directory.Exists(fullPath))
                EditorUtility.RevealInFinder(fullPath);
        }

        private static string AppendAttachmentBlock(string currentText, string attachmentText)
        {
            if (string.IsNullOrWhiteSpace(attachmentText))
                return currentText ?? "";

            if (string.IsNullOrWhiteSpace(currentText))
                return attachmentText;

            return currentText.TrimEnd() + "\n\n" + attachmentText;
        }

        private static bool TrySaveClipboardImageToProject(out string assetPath, out string error)
        {
            assetPath = "";
            error = "";

            try
            {
                EnsureAssetFolder(ClipboardImageRelativeFolder);
                string fileName = "Clipboard_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png";
                assetPath = ClipboardImageRelativeFolder + "/" + fileName;
                string fullPath = Path.Combine(ProjectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));

                string script = "$ErrorActionPreference='Stop';"
                    + "Add-Type -AssemblyName System.Windows.Forms;"
                    + "Add-Type -AssemblyName System.Drawing;"
                    + "$img=[System.Windows.Forms.Clipboard]::GetImage();"
                    + "if($null -eq $img){exit 2};"
                    + "$img.Save(" + QuotePowerShellString(fullPath) + ", [System.Drawing.Imaging.ImageFormat]::Png);";

                var process = new Process
                {
                    StartInfo =
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -STA -ExecutionPolicy Bypass -Command " + Quote(script),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };

                process.Start();
                if (!process.WaitForExit(5000))
                {
                    process.Kill();
                    error = "读取剪贴板超时。请确认剪贴板里是图片，再重试。";
                    return false;
                }

                if (process.ExitCode != 0 || !File.Exists(fullPath))
                {
                    error = "剪贴板里没有可读取的图片，或系统阻止了读取。";
                    return false;
                }

                AssetDatabase.ImportAsset(assetPath);
                AssetDatabase.Refresh();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string QuotePowerShellString(string value)
        {
            return "'" + (value ?? "").Replace("'", "''") + "'";
        }

        private void DrawPolishedArchitectPage()
        {
            EnsureArchitectGraphVisible();
            DrawPolishedArchitectToolbar();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("主架构AI", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "它不是单纯画图：它会读取 AIWarnings、AITalk、Codex 会话和项目目录，输出架构主线、风险、长期原则和下一步工程动作。下方节点图只是资料板。",
                    EditorStyles.wordWrappedMiniLabel);
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                DrawArchitectSummaryBadge("节点", architectNodes.Count.ToString());
                DrawArchitectSummaryBadge("关系", architectEdges.Count.ToString());
                GUILayout.Space(8);
                EditorGUILayout.LabelField("搜索", GUILayout.Width(34));
                architectSearchText = EditorGUILayout.TextField(architectSearchText, GUILayout.MinWidth(180));
                if (GUILayout.Button("清除", GUILayout.Width(58)))
                    architectSearchText = "";
                GUILayout.FlexibleSpace();
                if (position.width >= 980)
                    EditorGUILayout.LabelField("节点图用于定位来源和证据，不替代 AI 的架构判断。", EditorStyles.miniLabel, GUILayout.Width(360));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawArchitectCanvas();
                DrawPolishedArchitectInspector();
            }
        }

        private void DrawPolishedArchitectToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(4);
                if (GUILayout.Button("刷新资料板", EditorStyles.toolbarButton, GUILayout.Width(92)))
                    RebuildArchitectGraph();

                if (GUILayout.Button("自动布局", EditorStyles.toolbarButton, GUILayout.Width(78)))
                    LayoutArchitectNodes();

                if (GUILayout.Button("导出说明", EditorStyles.toolbarButton, GUILayout.Width(86)))
                    ExportArchitectMarkdown();

                if (DrawToolbarButton("主架构AI", 82, ButtonTone.Primary))
                    SendPromptToCurrentTab(BuildProjectArchitectPrompt());

                if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(54)))
                {
                    architectNodes.Clear();
                    architectEdges.Clear();
                    architectSelectedNodeIndex = -1;
                }

                GUILayout.FlexibleSpace();
                architectIncludeAIWarnings = GUILayout.Toggle(architectIncludeAIWarnings, "AIWarnings", EditorStyles.toolbarButton, GUILayout.Width(88));
                architectIncludeAITalkSessions = GUILayout.Toggle(architectIncludeAITalkSessions, "AITalk", EditorStyles.toolbarButton, GUILayout.Width(66));
                architectIncludeCodexSessions = GUILayout.Toggle(architectIncludeCodexSessions, "Codex", EditorStyles.toolbarButton, GUILayout.Width(62));
                GUILayout.Space(4);
            }
        }

        private static void DrawArchitectSummaryBadge(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope(GUILayout.Width(92)))
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(32));
                EditorGUILayout.LabelField(value, EditorStyles.miniBoldLabel, GUILayout.Width(44));
            }
        }

        private void DrawPolishedArchitectInspector()
        {
            float inspectorWidth = Mathf.Clamp(position.width * 0.32f, 280f, 360f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(inspectorWidth), GUILayout.ExpandHeight(true)))
            {
                EditorGUILayout.LabelField("节点详情", EditorStyles.boldLabel);
                ArchitectNode node = GetSelectedArchitectNode();
                if (node == null)
                {
                    EditorGUILayout.HelpBox("选择一个节点后，这里会显示摘要、来源定位和复制操作。", MessageType.Info);
                    DrawArchitectScanSettings();
                    return;
                }

                EditorGUILayout.LabelField(node.title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(node.type, EditorStyles.miniBoldLabel);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("摘要", EditorStyles.miniBoldLabel);
                EditorGUILayout.TextArea(node.summary, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(96));

                EditorGUILayout.Space(4);
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField("来源", node.sourcePath);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(node.sourcePath)))
                    {
                        if (GUILayout.Button("定位来源"))
                            RevealArchitectSource(node.sourcePath);
                    }

                    if (GUILayout.Button("复制摘要"))
                        EditorGUIUtility.systemCopyBuffer = $"{node.title}\n{node.summary}\n{node.sourcePath}";
                }

                EditorGUILayout.Space(8);
                DrawArchitectScanSettings();
            }
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("ES Cmd Agent", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Codex resume 面板", EditorStyles.miniLabel, GUILayout.Width(120));
                }

                EditorGUILayout.LabelField(
                    "默认用 resume 恢复最近会话；面板只保留页签壳、恢复 Key 和有限输出，避免本地长期堆积历史正文。",
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawMainPageTabs()
        {
            mainPageIndex = GUILayout.Toolbar(mainPageIndex, new[] { "会话", "架构" });
        }

        private void DrawStatusOverview()
        {
            AgentSessionTab current = GetCurrentTab();
            int runningCount = tabs.Count(tab => tab != null && tab.IsRunning);
            string keyState = current != null && !string.IsNullOrWhiteSpace(current.sessionId)
                ? ShortId(current.sessionId)
                : "等待记录";

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSummaryCell("Agent", agent.enableAgent ? "已启用" : "未启用", agent.enableAgent);
                DrawSummaryCell("运行页签", runningCount.ToString(), runningCount > 0);
                DrawSummaryCell("恢复 Key", keyState, current != null && !string.IsNullOrWhiteSpace(current.sessionId));
                DrawSummaryCell("本地留存", $"最多 {Mathf.Clamp(agent.maxLocalTabsToKeep, 1, 12)} 页签", true);
            }
        }

        private static void DrawSummaryCell(string label, string value, bool ok)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinWidth(120)))
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
                GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
                style.normal.textColor = ok ? new Color(0.35f, 0.75f, 0.45f) : new Color(0.9f, 0.55f, 0.35f);
                EditorGUILayout.LabelField(value, style);
            }
        }

        private void DrawPrimaryActions()
        {
            AgentSessionTab current = GetCurrentTab();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(!agent.enableAgent))
                {
                    if (GUILayout.Button("新页签恢复最近", EditorStyles.toolbarButton, GUILayout.Width(120)))
                        CreateAndResumeTab("");

                    if (GUILayout.Button("新页签恢复指定", EditorStyles.toolbarButton, GUILayout.Width(120)))
                        CreateAndResumeTab(agent.resumeSessionId);

                    using (new EditorGUI.DisabledScope(current == null || current.IsRunning))
                    {
                        if (GUILayout.Button("当前页签重新恢复", EditorStyles.toolbarButton, GUILayout.Width(130)))
                            StartResume(current, current.sessionId);
                    }
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(current == null || string.IsNullOrWhiteSpace(current.sessionId)))
                {
                    if (GUILayout.Button("复制恢复 Key", EditorStyles.toolbarButton, GUILayout.Width(95)))
                        CopyResumeKey(current);
                }

                using (new EditorGUI.DisabledScope(current == null || !current.IsRunning))
                {
                    if (GUILayout.Button("停止当前", EditorStyles.toolbarButton, GUILayout.Width(80)))
                        StopProcess(current);
                }

                if (GUILayout.Button("关闭页签", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    CloseCurrentTab();

                if (GUILayout.Button("清理已停止", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    RemoveStoppedTabs();
            }
        }

        private void DrawAdvancedSettings()
        {
            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "高级设置", true);
            if (!showAdvancedSettings)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField("配置资产", agent, typeof(ESCmdAgent), false);

                agent.enableAgent = EditorGUILayout.ToggleLeft("启用 Agent", agent.enableAgent);
                agent.autoResumeOnOpen = EditorGUILayout.ToggleLeft("打开入口时自动恢复最近会话", agent.autoResumeOnOpen);
                SetPreferExternalTerminal(agent, EditorGUILayout.ToggleLeft("优先后台 CMD 中转交互", GetPreferExternalTerminal(agent)));
                agent.autoCaptureResumeKey = EditorGUILayout.ToggleLeft("自动记录恢复 Key", agent.autoCaptureResumeKey);
                agent.codexCommand = EditorGUILayout.TextField("Codex 命令", string.IsNullOrWhiteSpace(agent.codexCommand) ? "codex.cmd" : agent.codexCommand);
                agent.workspacePath = EditorGUILayout.TextField("工作目录", agent.GetWorkspacePath());
                agent.resumeSessionId = EditorGUILayout.TextField("指定会话 ID（留空=最近会话）", agent.resumeSessionId ?? "");

                using (new EditorGUILayout.HorizontalScope())
                {
                    agent.maxLocalTabsToKeep = Mathf.Clamp(EditorGUILayout.IntField("本地页签上限", agent.maxLocalTabsToKeep), 1, 12);
                    agent.maxOutputCharsPerTab = Mathf.Clamp(EditorGUILayout.IntField("单页签输出上限", agent.maxOutputCharsPerTab), 2000, 200000);
                }
            }
        }

        private void DrawSessionTabs()
        {
            if (tabs.Count <= 0)
                return;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(4);

                if (GUILayout.Button("新建", EditorStyles.toolbarButton, GUILayout.Width(48)))
                    CreateAndResumeTab("");

                if (GUILayout.Button("排序", EditorStyles.toolbarButton, GUILayout.Width(48)))
                    ShowTabSortMenu();

                using (new EditorGUI.DisabledScope(tabs.Count <= 1 || selectedTabIndex <= 0))
                {
                    if (GUILayout.Button("<", EditorStyles.toolbarButton, GUILayout.Width(26)))
                        MoveTabLeft(selectedTabIndex);
                }

                using (new EditorGUI.DisabledScope(tabs.Count <= 1 || selectedTabIndex < 0 || selectedTabIndex >= tabs.Count - 1))
                {
                    if (GUILayout.Button(">", EditorStyles.toolbarButton, GUILayout.Width(26)))
                        MoveTabRight(selectedTabIndex);
                }

                GUILayout.Space(6);
                string[] tabNames = new string[tabs.Count];
                for (int i = 0; i < tabs.Count; i++)
                {
                    AgentSessionTab tab = tabs[i];
                    string state = tab != null && tab.IsRunning ? "运行 " : "空闲 ";
                    tabNames[i] = state + (tab != null ? tab.title : "会话");
                }

                selectedTabIndex = Mathf.Clamp(selectedTabIndex, 0, tabs.Count - 1);
                selectedTabIndex = GUILayout.Toolbar(selectedTabIndex, tabNames, EditorStyles.toolbarButton);
            }
        }

        private void DrawCurrentSession()
        {
            AgentSessionTab tab = GetCurrentTab();
            if (tab == null)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("当前页签", EditorStyles.boldLabel, GUILayout.Width(70));
                    tab.title = EditorGUILayout.TextField(tab.title);
                    GUILayout.Space(8);
                    EditorGUILayout.LabelField(tab.IsRunning ? "运行中" : "未运行", EditorStyles.miniBoldLabel, GUILayout.Width(60));
                }

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("恢复目标", string.IsNullOrWhiteSpace(tab.sessionId) ? "最近会话（等待自动记录 Key）" : tab.sessionId);
                    EditorGUILayout.TextField("摘要", tab.summary);
                    EditorGUILayout.TextField("最近启动", tab.lastStartTime);
                    EditorGUILayout.TextField("最近停止", tab.lastStopTime);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(tab.createdSessionFile)))
                    {
                        if (GUILayout.Button("定位会话文件", GUILayout.Width(110)))
                            EditorUtility.RevealInFinder(tab.createdSessionFile);
                    }

                    showCommandDetails = EditorGUILayout.Foldout(showCommandDetails, "显示命令与本地文件", true);
                }

                if (showCommandDetails)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.TextField("会话文件", tab.createdSessionFile);
                        EditorGUILayout.TextField("启动命令", tab.lastCommand);
                    }
                }
            }

            EditorGUILayout.LabelField("对话输出（仅保留最近内容）", EditorStyles.miniBoldLabel);
            tab.scroll = EditorGUILayout.BeginScrollView(tab.scroll, GUILayout.ExpandHeight(true));
            string output = string.IsNullOrEmpty(tab.outputText) ? "暂无输出。可以直接在下方输入需求并发送，工具会自动恢复最近会话；关闭窗口时不会长期保存正文。" : tab.outputText;
            EditorGUILayout.TextArea(output, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            DrawAttachmentDropZone(
                "把文件、Unity 资产或外部素材拖到这里，会追加到本页输入",
                tab.attachments,
                text => tab.inputText = AppendAttachmentBlock(tab.inputText, text));
            DrawAttachmentList(tab.attachments);

            using (new EditorGUILayout.HorizontalScope())
            {
                tab.inputText = EditorGUILayout.TextField(tab.inputText);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(tab.inputText)))
                {
                    string sendLabel = agent != null && GetPreferExternalTerminal(agent)
                        ? (tab.conPty != null && tab.conPty.IsRunning ? "发送到CMD" : "启动后台CMD")
                        : (tab.IsRunning ? "发送" : "启动并发送");
                    if (GUILayout.Button(sendLabel, GUILayout.Width(90)))
                        SendUserInputSmart(tab);
                }

                using (new EditorGUI.DisabledScope(tab.conPty == null || !tab.conPty.IsRunning))
                {
                    if (GUILayout.Button("信任目录(1)", GUILayout.Width(92)))
                        SendRawConPtyLine(tab, "1");

                    if (GUILayout.Button("回车", GUILayout.Width(52)))
                        SendRawConPtyLine(tab, "");
                }

                if (GUILayout.Button("清空本页输出", GUILayout.Width(100)))
                {
                    tab.outputText = "";
                    tab.terminal?.Clear();
                }
            }
        }

        private void DrawCurrentSessionDetailsOnly(AgentSessionTab tab)
        {
            if (tab == null)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("当前页签", EditorStyles.boldLabel, GUILayout.Width(70));
                    tab.title = EditorGUILayout.TextField(tab.title);
                    GUILayout.Space(8);
                    EditorGUILayout.LabelField(tab.IsRunning ? "运行中" : "未运行", EditorStyles.miniBoldLabel, GUILayout.Width(60));
                }

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("恢复目标", string.IsNullOrWhiteSpace(tab.sessionId) ? "最近会话" : tab.sessionId);
                    EditorGUILayout.TextField("摘要", tab.summary);
                    EditorGUILayout.TextField("最近启动", tab.lastStartTime);
                    EditorGUILayout.TextField("最近停止", tab.lastStopTime);
                    EditorGUILayout.TextField("会话文件", tab.createdSessionFile);
                    EditorGUILayout.TextField("启动命令", tab.lastCommand);
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(tab.createdSessionFile)))
                {
                    if (GUILayout.Button("定位会话文件", GUILayout.Width(110)))
                        EditorUtility.RevealInFinder(tab.createdSessionFile);
                }
            }
        }

        private void CopyResumeKey(AgentSessionTab tab)
        {
            if (tab == null || string.IsNullOrWhiteSpace(tab.sessionId))
                return;

            EditorGUIUtility.systemCopyBuffer = tab.sessionId;
            tab.summary = "已复制恢复 Key";
            Repaint();
        }

        private void ShowCodexSessionIndexMenu(Rect anchorRect)
        {
            ShowResumeSessionMenu(anchorRect);
        }

        private void ShowResumeSessionMenu(Rect anchorRect)
        {
            var entries = new List<ESSearchDropdown.Entry>();
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "sessions");
            if (!Directory.Exists(root))
            {
                entries.Add(ESSearchDropdown.Entry.Disabled("未找到本机 Codex 会话目录"));
                ESSearchDropdown.Open(anchorRect, "选择 Codex 历史会话", entries);
                return;
            }

            List<FileInfo> files = EnumerateFilesBounded(root, "*.jsonl", MaxCodexSessionCandidateFiles, MaxScannedDirectories)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(48)
                .ToList();

            List<(FileInfo file, string sessionId, string summary, LocalSessionMetadata meta)> sessions = new List<(FileInfo file, string sessionId, string summary, LocalSessionMetadata meta)>();
            foreach (FileInfo file in files)
            {
                if (!TryReadCodexSessionMeta(file.FullName, out string sessionId, out string cwd, out string summary))
                    continue;

                if (!WorkspaceContainsProject(cwd))
                    continue;

                sessions.Add((file, sessionId, summary, GetSessionMetadata(sessionId)));
                if (sessions.Count >= 12)
                    break;
            }

            if (sessions.Count == 0)
            {
                entries.Add(ESSearchDropdown.Entry.Disabled("没有匹配当前项目的历史会话"));
                ESSearchDropdown.Open(anchorRect, "选择 Codex 历史会话", entries);
                return;
            }

            sessions = sessions
                .OrderByDescending(item => item.meta != null && item.meta.pinned)
                .ThenByDescending(item => Math.Max(item.file.LastWriteTimeUtc.Ticks, item.meta != null ? item.meta.lastUsedTicks : 0))
                .ToList();

            FileInfo latestFile = sessions[0].file;
            string latestSessionId = sessions[0].sessionId;
            LocalSessionMetadata latestMeta = sessions[0].meta;
            string latestAlias = latestMeta != null && !string.IsNullOrWhiteSpace(latestMeta.alias) ? latestMeta.alias.Trim() : "未命名会话";
            entries.Add(ESSearchDropdown.Entry.Item(
                latestAlias,
                () => CreateAndResumeTab(latestSessionId),
                "快速恢复",
                EditorGUIUtility.IconContent("d_Refresh").image as Texture2D,
                subtitle: latestFile.LastWriteTime.ToString("yyyy-MM-dd HH:mm") + " · " + ShortId(latestSessionId),
                tooltip: latestFile.FullName,
                badge: latestMeta != null && latestMeta.pinned ? "置顶·最新" : "最新"));
            entries.Add(ESSearchDropdown.Entry.Disabled(
                "云端任务列表不能作为本地 resume 会话恢复"));

            foreach ((FileInfo file, string sessionId, string summary, LocalSessionMetadata meta) in sessions)
            {
                string label = meta != null && !string.IsNullOrWhiteSpace(meta.alias) ? meta.alias.Trim() : "未命名会话";
                string subtitle = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm") + " · " + ShortId(sessionId);
                string badge = meta != null && meta.pinned ? "置顶" : null;
                entries.Add(ESSearchDropdown.Entry.Item(
                    label,
                    () => ApplySessionFromIndex(sessionId, file.FullName, summary),
                    "选择到当前页",
                    EditorGUIUtility.IconContent("UnityEditor.InspectorWindow").image as Texture2D,
                    subtitle: subtitle,
                    tooltip: file.FullName,
                    badge: badge));
                entries.Add(ESSearchDropdown.Entry.Item(
                    label,
                    () => CreateAndResumeTab(sessionId),
                    "直接恢复到新页",
                    EditorGUIUtility.IconContent("d_Toolbar Plus").image as Texture2D,
                    subtitle: subtitle,
                    tooltip: file.FullName,
                    badge: badge));
            }

            ESSearchDropdown.Open(anchorRect, "选择 Codex 历史会话", entries, minimumWindowSize: new Vector2(680f, 380f));
        }

        private void ApplySessionFromIndex(string sessionId, string filePath, string summary)
        {
            AgentSessionTab tab = GetCurrentTab();
            if (tab == null)
                return;

            tab.sessionId = sessionId ?? "";
            tab.createdSessionFile = filePath ?? "";
            tab.title = string.IsNullOrWhiteSpace(sessionId) ? tab.title : GetSessionDisplayName(sessionId, "历史会话 " + ShortId(sessionId));
            tab.summary = string.IsNullOrWhiteSpace(summary) ? "已选择历史会话，点击“启动本页”继续" : summary;
            TouchSessionMetadata(sessionId, tab.title);
            selectedTabIndex = Mathf.Clamp(selectedTabIndex, 0, tabs.Count - 1);
            Repaint();
        }

        private static string BuildSessionMenuLabel(FileInfo file, string sessionId, LocalSessionMetadata meta)
        {
            string alias = meta != null && !string.IsNullOrWhiteSpace(meta.alias) ? meta.alias.Trim() : "未命名会话";
            string pin = meta != null && meta.pinned ? "★ " : "";
            return pin + alias + " / " + file.LastWriteTime.ToString("MM-dd HH:mm") + " / " + ShortId(sessionId);
        }

        private static string GetSessionDisplayName(string sessionId, string fallback)
        {
            LocalSessionMetadata meta = GetSessionMetadata(sessionId);
            if (meta != null && !string.IsNullOrWhiteSpace(meta.alias))
                return meta.alias.Trim();

            return fallback;
        }

        private static bool IsSessionPinned(string sessionId)
        {
            return GetSessionMetadata(sessionId)?.pinned == true;
        }

        private static void SaveCurrentTabAlias(AgentSessionTab tab)
        {
            if (tab == null || string.IsNullOrWhiteSpace(tab.sessionId))
                return;

            string alias = string.IsNullOrWhiteSpace(tab.title) ? "会话 " + ShortId(tab.sessionId) : tab.title.Trim();
            LocalSessionMetadataStore store = LoadSessionMetadataStore();
            LocalSessionMetadata meta = GetOrCreateSessionMetadata(store, tab.sessionId);
            meta.alias = alias;
            meta.lastUsedTicks = DateTime.UtcNow.Ticks;
            SaveSessionMetadataStore(store);
            tab.summary = "已保存本地会话名：" + alias;
        }

        private static void ToggleCurrentSessionPin(AgentSessionTab tab)
        {
            if (tab == null || string.IsNullOrWhiteSpace(tab.sessionId))
                return;

            LocalSessionMetadataStore store = LoadSessionMetadataStore();
            LocalSessionMetadata meta = GetOrCreateSessionMetadata(store, tab.sessionId);
            if (string.IsNullOrWhiteSpace(meta.alias))
                meta.alias = string.IsNullOrWhiteSpace(tab.title) ? "会话 " + ShortId(tab.sessionId) : tab.title.Trim();
            meta.pinned = !meta.pinned;
            meta.lastUsedTicks = DateTime.UtcNow.Ticks;
            SaveSessionMetadataStore(store);
            tab.summary = meta.pinned ? "已置顶当前会话" : "已取消置顶当前会话";
        }

        private static void TouchSessionMetadata(string sessionId, string aliasHint = "")
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return;

            LocalSessionMetadataStore store = LoadSessionMetadataStore();
            LocalSessionMetadata meta = GetOrCreateSessionMetadata(store, sessionId);
            if (string.IsNullOrWhiteSpace(meta.alias) && !string.IsNullOrWhiteSpace(aliasHint))
                meta.alias = aliasHint.Trim();
            meta.lastUsedTicks = DateTime.UtcNow.Ticks;
            SaveSessionMetadataStore(store);
        }

        private static LocalSessionMetadata GetSessionMetadata(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return null;

            LocalSessionMetadataStore store = LoadSessionMetadataStore();
            return store.items.FirstOrDefault(item => item != null && string.Equals(item.sessionId, sessionId, StringComparison.OrdinalIgnoreCase));
        }

        private static LocalSessionMetadata GetOrCreateSessionMetadata(LocalSessionMetadataStore store, string sessionId)
        {
            if (store.items == null)
                store.items = new List<LocalSessionMetadata>();

            LocalSessionMetadata meta = store.items.FirstOrDefault(item => item != null && string.Equals(item.sessionId, sessionId, StringComparison.OrdinalIgnoreCase));
            if (meta != null)
                return meta;

            meta = new LocalSessionMetadata { sessionId = sessionId };
            store.items.Add(meta);
            return meta;
        }

        private static LocalSessionMetadataStore LoadSessionMetadataStore()
        {
            string json = EditorPrefs.GetString(LocalSessionMetadataKey, "");
            if (string.IsNullOrWhiteSpace(json))
                return new LocalSessionMetadataStore();

            try
            {
                LocalSessionMetadataStore store = JsonUtility.FromJson<LocalSessionMetadataStore>(json);
                return store ?? new LocalSessionMetadataStore();
            }
            catch
            {
                return new LocalSessionMetadataStore();
            }
        }

        private static void SaveSessionMetadataStore(LocalSessionMetadataStore store)
        {
            if (store == null)
                store = new LocalSessionMetadataStore();

            if (store.items == null)
                store.items = new List<LocalSessionMetadata>();

            store.items = store.items
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.sessionId))
                .GroupBy(item => item.sessionId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(item => item.lastUsedTicks).First())
                .OrderByDescending(item => item.pinned)
                .ThenByDescending(item => item.lastUsedTicks)
                .Take(128)
                .ToList();

            EditorPrefs.SetString(LocalSessionMetadataKey, JsonUtility.ToJson(store));
        }

        private void EnsureAgent()
        {
            if (agent != null)
                return;

            agent = ESCmdAgent.Instance;
            if (agent == null)
                agent = CreateDefaultAgentAsset();
        }

        private void EnsureAgentInteractive()
        {
            agent = CreateDefaultAgentAsset();
            if (agent != null)
            {
                Selection.activeObject = agent;
                EditorGUIUtility.PingObject(agent);
            }
        }

        private string GetPreferredResumeSessionId()
        {
            if (agent == null)
                return "";

            if (!string.IsNullOrWhiteSpace(agent.resumeSessionId))
                return agent.resumeSessionId.Trim();

            if (!string.IsNullOrWhiteSpace(agent.lastResumeSessionId))
                return agent.lastResumeSessionId.Trim();

            return "";
        }

        private static bool GetPreferExternalTerminal(ESCmdAgent config)
        {
            if (config == null)
                return true;

            var field = config.GetType().GetField("preferExternalTerminal");
            return field == null || (field.FieldType == typeof(bool) && (bool)field.GetValue(config));
        }

        private static void SetPreferExternalTerminal(ESCmdAgent config, bool value)
        {
            if (config == null)
                return;

            var field = config.GetType().GetField("preferExternalTerminal");
            if (field == null || field.FieldType != typeof(bool))
                return;

            field.SetValue(config, value);
            EditorUtility.SetDirty(config);
        }

        private static ESCmdAgent CreateDefaultAgentAsset()
        {
            ESCmdAgent existing = AssetDatabase.LoadAssetAtPath<ESCmdAgent>(DefaultAgentAssetPath);
            if (existing != null)
                return existing;

            EnsureAssetFolder("Assets/ESNormalAssets/Data/GlobalData/CmdAgent");

            var created = CreateInstance<ESCmdAgent>();
            created.name = "ESCmdAgent";
            created.enableAgent = true;
            created.codexCommand = "codex.cmd";
            created.workspacePath = Application.dataPath.EndsWith("/Assets")
                ? Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length)
                : Application.dataPath;
            created.autoResumeOnOpen = false;
            SetPreferExternalTerminal(created, true);
            created.autoCaptureResumeKey = true;
            created.maxLocalTabsToKeep = FallbackMaxLocalTabs;
            created.maxOutputCharsPerTab = FallbackOutputCharLimit;
            created.HasConfirm = true;

            AssetDatabase.CreateAsset(created, DefaultAgentAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(created);
            return created;
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private void EnsureTabExists()
        {
            if (tabs == null)
                tabs = new List<AgentSessionTab>();

            if (tabs.Count == 0)
                tabs.Add(CreateTab(""));

            selectedTabIndex = Mathf.Clamp(selectedTabIndex, 0, tabs.Count - 1);
        }

        private void EnsureTabRuntime()
        {
            if (tabs == null)
                return;

            foreach (AgentSessionTab tab in tabs)
                tab?.EnsureRuntime();
        }

        private AgentSessionTab CreateTab(string sessionId)
        {
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string cleanSessionId = string.IsNullOrWhiteSpace(sessionId) ? "" : sessionId.Trim();
            int index = tabs == null ? 1 : tabs.Count + 1;

            return new AgentSessionTab
            {
                title = string.IsNullOrEmpty(cleanSessionId) ? $"最近会话 {index}" : $"指定会话 {ShortId(cleanSessionId)}",
                sessionId = cleanSessionId,
                createdAt = now,
                summary = "等待恢复"
            };
        }

        private void CreateAndResumeTab(string sessionId)
        {
            EnsureAgent();
            EnsureTabExists();

            AgentSessionTab tab = GetCurrentTab();
            if (!IsReusableEmptyTab(tab))
            {
                tab = CreateTab(sessionId);
                tabs.Add(tab);
            }
            else
            {
                string cleanSessionId = string.IsNullOrWhiteSpace(sessionId) ? "" : sessionId.Trim();
                tab.sessionId = cleanSessionId;
                tab.title = string.IsNullOrEmpty(cleanSessionId) ? $"最近会话 {tabs.Count}" : $"指定会话 {ShortId(cleanSessionId)}";
                tab.createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }

            selectedTabIndex = tabs.IndexOf(tab);
            TrimStoppedTabsToLimit();
            StartResume(tab, sessionId);
        }

        private void CreateAndStartFreshTab()
        {
            EnsureAgent();
            EnsureTabExists();

            AgentSessionTab tab = GetCurrentTab();
            if (!IsReusableEmptyTab(tab))
            {
                tab = CreateTab("");
                tabs.Add(tab);
            }

            tab.sessionId = "";
            tab.title = $"新会话 {tabs.Count}";
            tab.summary = "准备启动新会话";
            tab.createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            selectedTabIndex = tabs.IndexOf(tab);
            TrimStoppedTabsToLimit();
            StartFreshCodex(tab);
        }

        private void MoveTabLeft(int index)
        {
            MoveTab(index, index - 1);
        }

        private void EnsureArchitectGraphVisible()
        {
            if (architectAutoBuiltThisOpen || architectNodes.Count > 0)
                return;

            architectAutoBuiltThisOpen = true;
            RebuildArchitectGraph();
        }

        private void MoveTabRight(int index)
        {
            MoveTab(index, index + 1);
        }

        private void MoveTab(int from, int to)
        {
            if (tabs == null || from < 0 || from >= tabs.Count || to < 0 || to >= tabs.Count || from == to)
                return;

            AgentSessionTab tab = tabs[from];
            tabs.RemoveAt(from);
            tabs.Insert(to, tab);
            selectedTabIndex = to;
            Repaint();
        }

        private void ShowTabSortMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("按名称排序"), false, SortTabsByTitle);
            menu.AddItem(new GUIContent("按创建时间排序"), false, SortTabsByCreatedAt);
            menu.AddItem(new GUIContent("运行中优先"), false, SortTabsByRunningState);
            menu.ShowAsContext();
        }

        private void SortTabsByTitle()
        {
            SortTabs((left, right) => string.Compare(left?.title, right?.title, StringComparison.OrdinalIgnoreCase));
        }

        private void SortTabsByCreatedAt()
        {
            SortTabs((left, right) => string.Compare(left?.createdAt, right?.createdAt, StringComparison.OrdinalIgnoreCase));
        }

        private void SortTabsByRunningState()
        {
            SortTabs((left, right) => (right?.IsRunning == true ? 1 : 0).CompareTo(left?.IsRunning == true ? 1 : 0));
        }

        private void SortTabs(Comparison<AgentSessionTab> comparison)
        {
            if (tabs == null || tabs.Count <= 1)
                return;

            AgentSessionTab selected = GetCurrentTab();
            tabs.Sort(comparison);
            selectedTabIndex = Mathf.Max(0, tabs.IndexOf(selected));
            Repaint();
        }

        private static bool IsReusableEmptyTab(AgentSessionTab tab)
        {
            return tab != null
                && !tab.IsRunning
                && string.IsNullOrEmpty(tab.outputText)
                && string.IsNullOrEmpty(tab.lastStartTime)
                && tab.summary == "等待恢复";
        }

        private AgentSessionTab GetCurrentTab()
        {
            EnsureTabExists();
            if (tabs == null || tabs.Count == 0)
                return null;

            selectedTabIndex = Mathf.Clamp(selectedTabIndex, 0, tabs.Count - 1);
            return tabs[selectedTabIndex];
        }

        private void StartResume(AgentSessionTab tab, string sessionId = "", string prompt = "")
        {
            EnsureAgent();
            if (agent == null || tab == null)
                return;

            tab.EnsureRuntime();

            if (!agent.enableAgent)
            {
                AppendOutput(tab, "[ES Cmd Agent] Agent 未启用。\n");
                tab.summary = "未启用";
                return;
            }

            if (tab.IsRunning)
            {
                AppendOutput(tab, "[ES Cmd Agent] 当前页签已有进程在运行。\n");
                return;
            }

            string cleanSessionId = string.IsNullOrWhiteSpace(sessionId) ? "" : sessionId.Trim();
            tab.sessionId = cleanSessionId;
            tab.title = string.IsNullOrEmpty(cleanSessionId) ? tab.title : $"指定会话 {ShortId(cleanSessionId)}";

            string workspace = agent.GetWorkspacePath();
            string codex = string.IsNullOrWhiteSpace(agent.codexCommand) ? "codex.cmd" : agent.codexCommand.Trim();
            string promptToSend = string.IsNullOrWhiteSpace(prompt)
                ? "请恢复当前项目上下文，简要说明你已准备好继续。"
                : prompt.Trim();
            string resumeTarget = string.IsNullOrEmpty(cleanSessionId) ? "--last" : Quote(cleanSessionId);
            string command = $"chcp 65001 >nul && {codex} exec -C {Quote(workspace)} resume {resumeTarget} -";

            if (string.IsNullOrWhiteSpace(workspace) || !Directory.Exists(workspace))
            {
                tab.summary = "工作目录无效";
                AppendOutput(tab, "[ES Cmd Agent] 启动失败：工作目录不存在。\n");
                AppendOutput(tab, "建议：打开高级设置，确认“工作目录”指向 Unity 项目根目录。\n");
                return;
            }

            if (GetPreferExternalTerminal(agent))
            {
                StartHiddenCodexTerminal(tab, codex, workspace, cleanSessionId, prompt);
                return;
            }

            try
            {
                Process processToStart = new Process
                {
                    StartInfo =
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c " + command,
                        WorkingDirectory = workspace,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    },
                    EnableRaisingEvents = true
                };

                RegisterProcessTab(processToStart, tab);
                processToStart.OutputDataReceived += OnProcessOutputDataReceived;
                processToStart.ErrorDataReceived += OnProcessErrorDataReceived;
                processToStart.Exited += OnProcessExited;

                tab.process = processToStart;
                processToStart.Start();
                processToStart.StandardInput.WriteLine(promptToSend);
                processToStart.StandardInput.Close();
                processToStart.BeginOutputReadLine();
                processToStart.BeginErrorReadLine();

                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                tab.lastStartTime = now;
                tab.startedAtUtc = DateTime.UtcNow;
                tab.lastCommand = command;
                tab.summary = string.IsNullOrEmpty(cleanSessionId) ? "正在发送到最近会话" : "正在发送到指定会话";
                SetStatusNotice(tab, tab.summary, 0);
                tab.capturedSessionKey = false;
                tab.createdSessionFile = "";
                tab.lastCaptureAttemptUtc = default;
                tab.captureAttemptCount = 0;

                if (!string.IsNullOrWhiteSpace(cleanSessionId))
                {
                    agent.lastResumeSessionId = cleanSessionId;
                    TouchSessionMetadata(cleanSessionId, tab.title);
                }
                agent.lastStartTime = now;
                EditorUtility.SetDirty(agent);

                AppendOutput(tab, "[ES Cmd Agent] 已启动非交互任务。\n");
                AppendOutput(tab, "[ES Cmd Agent] 命令: " + command + "\n");
                AppendOutput(tab, "你：" + promptToSend + "\n");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                tab.summary = "启动失败";
                SetStatusNotice(tab, "启动失败：" + ex.Message, 2);
                AppendOutput(tab, "[ES Cmd Agent] 启动失败: " + ex.Message + "\n");
                AppendOutput(tab, BuildStartFailureAdvice(codex, workspace));
            }
        }

        private void StartCloudTaskList()
        {
            AgentSessionTab tab = GetCurrentTab();
            EnsureAgent();
            if (tab == null || agent == null)
                return;

            if (tab.IsRunning)
            {
                AppendOutput(tab, "[ES Cmd Agent] 当前页签已有任务在运行，云端任务列表暂不启动。\n");
                SetStatusNotice(tab, "当前页签已有任务在运行。", 2);
                return;
            }

            string workspace = agent.GetWorkspacePath();
            string codex = string.IsNullOrWhiteSpace(agent.codexCommand) ? "codex.cmd" : agent.codexCommand.Trim();
            string command = $"chcp 65001 >nul && {codex} cloud list --limit 20 --json";

            try
            {
                tab.EnsureRuntime();
                Process processToStart = new Process
                {
                    StartInfo =
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c " + command,
                        WorkingDirectory = Directory.Exists(workspace) ? workspace : ProjectRoot,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    },
                    EnableRaisingEvents = true
                };

                RegisterProcessTab(processToStart, tab);
                processToStart.OutputDataReceived += OnProcessOutputDataReceived;
                processToStart.ErrorDataReceived += OnProcessErrorDataReceived;
                processToStart.Exited += OnProcessExited;
                tab.process = processToStart;
                tab.lastCommand = command;
                tab.summary = "正在读取云端任务";
                SetStatusNotice(tab, "正在读取 Codex Cloud 任务列表。", 0);
                processToStart.Start();
                processToStart.BeginOutputReadLine();
                processToStart.BeginErrorReadLine();
                AppendOutput(tab, "[ES Cmd Agent] 正在读取 Codex Cloud 任务列表。\n");
                AppendOutput(tab, "[ES Cmd Agent] 注意：Cloud task 不是本地 resume 会话，不能直接作为恢复 Key。\n");
            }
            catch (Exception ex)
            {
                tab.summary = "云端任务读取失败";
                SetStatusNotice(tab, "云端任务读取失败：" + ex.Message, 2);
                AppendOutput(tab, "[ES Cmd Agent] 云端任务读取失败: " + ex.Message + "\n");
            }
        }

        private void StartFreshCodex(AgentSessionTab tab)
        {
            EnsureAgent();
            if (agent == null || tab == null)
                return;

            tab.EnsureRuntime();

            if (!agent.enableAgent)
            {
                AppendOutput(tab, "[ES Cmd Agent] Agent 未启用。\n");
                tab.summary = "未启用";
                SetStatusNotice(tab, "未启用：请先启用 ESCmdAgent。", 2);
                return;
            }

            if (tab.IsRunning)
            {
                AppendOutput(tab, "[ES Cmd Agent] 当前页签已有进程在运行。\n");
                SetStatusNotice(tab, "当前页签已有任务在运行。", 2);
                return;
            }

            string workspace = agent.GetWorkspacePath();
            string codex = string.IsNullOrWhiteSpace(agent.codexCommand) ? "codex.cmd" : agent.codexCommand.Trim();
            if (string.IsNullOrWhiteSpace(workspace) || !Directory.Exists(workspace))
            {
                tab.summary = "工作目录无效";
                SetStatusNotice(tab, "启动失败：工作目录不存在。", 2);
                AppendOutput(tab, "[ES Cmd Agent] 启动失败：工作目录不存在。\n");
                AppendOutput(tab, "建议：打开高级设置，确认“工作目录”指向 Unity 项目根目录。\n");
                return;
            }

            if (GetPreferExternalTerminal(agent))
            {
                StartHiddenFreshCodexTerminal(tab, codex, workspace);
                return;
            }

            AppendOutput(tab, "[ES Cmd Agent] 新会话需要后台 CMD/ConPTY 模式。已切回恢复最近；可在高级设置开启“优先后台 CMD 中转”。\n");
            StartResume(tab, "", "");
        }

        private void StartHiddenFreshCodexTerminal(AgentSessionTab tab, string codex, string workspace)
        {
            string command = $"cmd.exe /k chcp 65001 >nul && cd /d {Quote(workspace)} && {codex} -C {Quote(workspace)} --no-alt-screen";

            try
            {
                tab.EnsureRuntime();
                tab.conPty = ConPtySession.Start(command, workspace, tab.pendingOutput);

                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                tab.lastStartTime = now;
                tab.lastCommand = command;
                tab.lastStopTime = "";
                tab.sessionId = "";
                tab.summary = "新 Codex 会话已启动";
                SetStatusNotice(tab, "新 Codex 会话已启动。", 0);
                tab.startedAtUtc = DateTime.UtcNow;
                tab.capturedSessionKey = false;
                tab.lastCaptureAttemptUtc = default;
                tab.captureAttemptCount = 0;
                agent.lastStartTime = now;
                EditorUtility.SetDirty(agent);

                AppendOutput(tab, "[ES Cmd Agent] 已启动新的后台 CMD 伪终端。\n");
                AppendOutput(tab, "[ES Cmd Agent] 命令: " + command + "\n");
            }
            catch (Exception ex)
            {
                tab.summary = "新会话启动失败";
                SetStatusNotice(tab, "新会话启动失败：" + ex.Message, 2);
                AppendOutput(tab, "[ES Cmd Agent] 后台 CMD/ConPTY 启动失败: " + ex.Message + "\n");
                AppendOutput(tab, BuildStartFailureAdvice(codex, workspace));
            }
        }

        private static string BuildStartFailureAdvice(string codex, string workspace)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("建议排查：");
            builder.AppendLine("1. 确认高级设置里的 Codex 命令可用，例如 codex.cmd。");
            builder.AppendLine("2. 确认工作目录存在并指向当前 Unity 项目根目录。");
            builder.AppendLine("3. 如果刚编译过，先点“重连本页”。");
            builder.AppendLine("4. 如果命令行能用但面板不能用，检查系统 PATH 或把 Codex 命令改成绝对路径。");
            builder.AppendLine("当前配置：");
            builder.AppendLine("- Codex 命令：" + codex);
            builder.AppendLine("- 工作目录：" + workspace);
            return builder.ToString();
        }

        private void StartHiddenCodexTerminal(AgentSessionTab tab, string codex, string workspace, string sessionId, string prompt)
        {
            string resumeArg = string.IsNullOrWhiteSpace(sessionId) ? "--last" : Quote(sessionId.Trim());
            string command = $"cmd.exe /k chcp 65001 >nul && cd /d {Quote(workspace)} && {codex} resume {resumeArg} -C {Quote(workspace)} --no-alt-screen";

            try
            {
                tab.EnsureRuntime();
                tab.conPty = ConPtySession.Start(command, workspace, tab.pendingOutput);

                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                tab.lastStartTime = now;
                tab.lastCommand = command;
                tab.lastStopTime = "";
                tab.summary = "后台 CMD/codex 已启动";
                SetStatusNotice(tab, "后台 CMD/codex 已启动。", 0);
                tab.startedAtUtc = DateTime.UtcNow;
                tab.capturedSessionKey = false;
                tab.lastCaptureAttemptUtc = default;
                tab.captureAttemptCount = 0;

                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    agent.lastResumeSessionId = sessionId.Trim();
                    TouchSessionMetadata(sessionId.Trim(), tab.title);
                }
                agent.lastStartTime = now;
                EditorUtility.SetDirty(agent);

                AppendOutput(tab, "[ES Cmd Agent] 已启动后台 CMD 伪终端。\n");
                AppendOutput(tab, "[ES Cmd Agent] 命令: " + command + "\n");
                if (!string.IsNullOrWhiteSpace(prompt))
                {
                    tab.conPty.WriteLine(prompt.Trim());
                    AppendOutput(tab, "你：" + prompt.Trim() + "\n");
                }
            }
            catch (Exception ex)
            {
                tab.summary = "后台 CMD 启动失败";
                SetStatusNotice(tab, "后台 CMD 启动失败：" + ex.Message, 2);
                AppendOutput(tab, "[ES Cmd Agent] 后台 CMD/ConPTY 启动失败: " + ex.Message + "\n");
                AppendOutput(tab, "建议：确认系统是 Windows 10 1809+；如果仍失败，可临时关闭“优先后台 CMD 中转交互”，使用非交互 exec 模式。\n");
                AppendOutput(tab, BuildStartFailureAdvice(codex, workspace));
            }
        }

        private void SendInput(AgentSessionTab tab)
        {
            if (tab == null || string.IsNullOrWhiteSpace(tab.inputText))
                return;

            if (tab.conPty != null && tab.conPty.IsRunning)
            {
                string input = tab.inputText;
                tab.inputText = "";
                tab.conPty.WriteLine(input);
                AppendOutput(tab, "你：" + input + "\n");
                return;
            }

            if (tab.IsRunning)
            {
                AppendOutput(tab, "[ES Cmd Agent] 当前任务仍在运行，请等待完成后再发送。\n");
                return;
            }

            string prompt = tab.inputText;
            tab.inputText = "";
            StartResume(tab, tab.sessionId, prompt);
        }

        private void SendRawConPtyLine(AgentSessionTab tab, string text)
        {
            if (tab == null || tab.conPty == null || !tab.conPty.IsRunning)
                return;

            tab.conPty.WriteLine(text ?? "");
            AppendOutput(tab, string.IsNullOrEmpty(text) ? "你：[回车]\n" : "你：" + text + "\n");
        }

        private void SendRawConPtyText(AgentSessionTab tab, string text, string label)
        {
            if (tab == null || tab.conPty == null || !tab.conPty.IsRunning)
                return;

            tab.conPty.WriteRaw(text ?? "");
            AppendOutput(tab, "你：[" + label + "]\n");
        }

        private void SendUserInputSmart(AgentSessionTab tab)
        {
            if (tab == null || string.IsNullOrWhiteSpace(tab.inputText))
                return;

            if (tab.conPty != null && tab.conPty.IsRunning)
            {
                SendInput(tab);
                return;
            }

            if (tab.IsRunning)
            {
                AppendOutput(tab, "[ES Cmd Agent] 当前任务仍在运行，请等待完成后再发送。\n");
                return;
            }

            SendInput(tab);
        }

        private void SendPromptToCurrentTab(string prompt)
        {
            AgentSessionTab tab = GetCurrentTab();
            if (tab == null || string.IsNullOrWhiteSpace(prompt))
                return;

            if (tab.conPty != null && tab.conPty.IsRunning)
            {
                tab.inputText = prompt.Trim();
                SendInput(tab);
                return;
            }

            if (tab.IsRunning)
            {
                tab.inputText = prompt.Trim();
                AppendOutput(tab, "[ES Cmd Agent] 当前任务仍在运行，指令已保留在输入框。\n");
                return;
            }

            StartResume(tab, tab.sessionId, prompt);
        }

        private void ShowAICommandMenu(Rect anchorRect)
        {
            string root = GetProjectRelativeFullPath(AICommandsRelativePath);
            var entries = new List<ESSearchDropdown.Entry>();

            if (!Directory.Exists(root))
            {
                entries.Add(ESSearchDropdown.Entry.Disabled("未找到 AICommands 目录"));
                ESSearchDropdown.Open(anchorRect, "选择 AI 预设指令", entries);
                return;
            }

            List<string> files = Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .Take(200)
                .OrderByDescending(path => Path.GetFileName(path).StartsWith("方案_", StringComparison.OrdinalIgnoreCase))
                .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (files.Count == 0)
            {
                entries.Add(ESSearchDropdown.Entry.Disabled("没有可调用的 AICommand"));
                ESSearchDropdown.Open(anchorRect, "选择 AI 预设指令", entries);
                return;
            }

            foreach (string file in files)
            {
                string menuName = BuildAICommandMenuName(root, file);
                string assetPath = ToProjectRelativeAssetPath(file);
                int separator = menuName.LastIndexOf('/');
                string groupPath = separator > 0 ? menuName.Substring(0, separator) : null;
                string label = separator >= 0 ? menuName.Substring(separator + 1) : menuName;
                FileInfo fileInfo = new FileInfo(file);
                entries.Add(ESSearchDropdown.Entry.Item(
                    label,
                    () => OpenAICommandComposer(assetPath, menuName),
                    groupPath,
                    EditorGUIUtility.IconContent("TextAsset Icon").image as Texture2D,
                    subtitle: assetPath,
                    tooltip: file,
                    badge: fileInfo.Name.StartsWith("方案_", StringComparison.OrdinalIgnoreCase) ? "方案" : "指令",
                    selected: string.Equals(selectedAICommandAssetPath, assetPath, StringComparison.OrdinalIgnoreCase)));
            }

            ESSearchDropdown.Open(anchorRect, "选择 AI 预设指令", entries, minimumWindowSize: new Vector2(680f, 420f));
        }

        private void OpenAICommandComposer(string assetPath, string menuName)
        {
            selectedAICommandAssetPath = assetPath ?? "";
            selectedAICommandName = string.IsNullOrWhiteSpace(menuName) ? Path.GetFileNameWithoutExtension(assetPath) : menuName;
            aiCommandRequiredValue = "";
            aiCommandUserSupplement = "";
            aiCommandPreviewScroll = Vector2.zero;
            showAICommandComposer = true;
            Repaint();
        }

        private static AICommandInfo ParseAICommandInfo(string assetPath)
        {
            AICommandInfo info = new AICommandInfo();
            string fullPath = GetProjectRelativeFullPath(assetPath ?? "");
            if (!File.Exists(fullPath))
                return info;

            try
            {
                foreach (string rawLine in File.ReadLines(fullPath, Encoding.UTF8).Take(120))
                {
                    string line = rawLine.Trim();
                    if (line.StartsWith("命令类型：", StringComparison.OrdinalIgnoreCase))
                        info.commandType = line.Substring("命令类型：".Length).Trim().TrimEnd('。');
                    else if (line.StartsWith("默认改文件：", StringComparison.OrdinalIgnoreCase))
                        info.defaultWrite = line.Substring("默认改文件：".Length).Trim().TrimEnd('。');
                    else if (line.StartsWith("风险等级：", StringComparison.OrdinalIgnoreCase))
                        info.riskLevel = line.Substring("风险等级：".Length).Trim().TrimEnd('。');
                    else if (line.IndexOf("<用户在这里补充", StringComparison.OrdinalIgnoreCase) >= 0)
                        info.hasUserPlaceholder = true;
                }

                if (info.hasUserPlaceholder)
                    info.requirementTitle = "本次需求（建议必填）";
            }
            catch
            {
                // Keep default metadata; preview still works with the command path.
            }

            return info;
        }

        private static void DrawAICommandInfoStrip(AICommandInfo info)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                DrawTinyInfoCell("类型", info.commandType, 150);
                DrawTinyInfoCell("默认改文件", info.defaultWrite, 180);
                DrawTinyInfoCell("风险", info.riskLevel, 80);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(info.hasUserPlaceholder ? "该命令包含用户补充占位" : "未发现补充占位", EditorStyles.miniLabel, GUILayout.Width(150));
            }
        }

        private static void DrawTinyInfoCell(string label, string value, float width)
        {
            using (new EditorGUILayout.HorizontalScope(GUILayout.Width(width)))
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(58));
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(value) ? "未声明" : value, EditorStyles.miniBoldLabel, GUILayout.Width(width - 64));
            }
        }

        private void SendPreparedAICommand(string preview, AICommandInfo info)
        {
            if (info == null)
                info = new AICommandInfo();

            if (string.IsNullOrWhiteSpace(aiCommandRequiredValue) && info.hasUserPlaceholder)
            {
                bool continueWithoutRequirement = EditorUtility.DisplayDialog(
                    "缺少本次需求",
                    "这个命令包含用户补充占位，但你还没有填写“本次需求”。继续发送会要求 AI 缺参数时先问你。",
                    "继续发送",
                    "返回填写");
                if (!continueWithoutRequirement)
                    return;
            }

            if (info.AllowsWrite || info.IsHighRisk)
            {
                string message = $"命令类型：{info.commandType}\n默认改文件：{info.defaultWrite}\n风险等级：{info.riskLevel}\n\n发送后 AI 仍会按命令约束判断，但该命令可能涉及文件修改或较高风险。";
                bool confirmed = EditorUtility.DisplayDialog("确认发送高风险/可写指令", message, "确认发送", "取消");
                if (!confirmed)
                    return;
            }

            SendPromptToCurrentTab(preview);
        }

        private static string BuildReadWarningsPrompt()
        {
            return "请先快速读取项目 AIWarnings，优先读取 Assets/Plugins/ES/AIWarnings/README.md、项目最高警告、CodexNotes，以及和当前任务相关的警告文件。读取后先用短列表说明你看到的关键约束，再继续处理我的请求。";
        }

        private static string BuildUpdateWarningsPrompt()
        {
            return "请根据本轮已经完成的工作，更新或新增合适的 AIWarnings。要求：写入 Assets/Plugins/ES/AIWarnings 下的准确位置；内容要给后续 AI 可执行的约束、风险、路径和禁止事项；不要写空泛总结；不要产生乱码；更新后说明改了哪些文件。";
        }

        private static string BuildProjectArchitectPrompt()
        {
            return "请以【ES 项目全局架构师】身份工作。\n\n"
                + "目标：读取并整合当前项目的 AIWarnings、Codex 本机会话摘要、AITalk 协作记录、关键 asmdef/目录结构，生成一份高密度架构判断。\n\n"
                + "必须先读：\n"
                + "1. Assets/Plugins/ES/AIWarnings/README.md\n"
                + "2. Assets/Plugins/ES/AIWarnings/项目最高警告\n"
                + "3. Assets/Plugins/ES/AIWarnings/CodexNotes\n"
                + "4. 与当前任务相关的 Editor/CmdAgent/表格工具警告或记录\n\n"
                + "输出格式：\n"
                + "1. 当前架构主线\n"
                + "2. 已确认的长期原则\n"
                + "3. 当前最高风险\n"
                + "4. 近期最值得推进的 3-5 个工程动作\n"
                + "5. 不建议做的事及原因\n\n"
                + "要求：只给可执行判断，不写空泛愿景；需要查文件就查，不要猜；如果要修改文件，先说明计划和风险。";
        }

        private static string BuildUseAICommandPrompt(string assetPath, string requiredValue, string userSupplement)
        {
            string requirement = string.IsNullOrWhiteSpace(requiredValue)
                ? "用户未填写本次需求。若命令需要具体目标、路径、报错或对象名，先问用户，不要猜。"
                : requiredValue.Trim();
            string supplement = string.IsNullOrWhiteSpace(userSupplement)
                ? "用户本次没有补充额外信息，请严格按命令文件中的默认目标、约束和风险边界执行；遇到缺失参数时先问用户，不要猜。"
                : userSupplement.Trim();

            return $"请读取并执行这个 AICommand：{assetPath}。\n\n本次需求：\n{requirement}\n\n额外补充：\n{supplement}\n\n执行要求：\n1. 先复述该命令的目标、约束、风险等级和涉及路径。\n2. 如果命令缺少必要参数，先问用户，不要擅自猜测。\n3. 如果命令只适合分析，不要擅自写文件。\n4. 如果命令允许修改，也必须只做命令和用户补充共同允许的范围。";
        }

        private static string BuildAICommandMenuName(string root, string file)
        {
            string relative = file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            relative = relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
            string name = Path.ChangeExtension(relative, null);

            if (name.StartsWith("方案_", StringComparison.OrdinalIgnoreCase))
                return "方案/" + name.Substring("方案_".Length);
            if (name.StartsWith("执行_", StringComparison.OrdinalIgnoreCase))
                return "执行/" + name.Substring("执行_".Length);
            if (name.StartsWith("检查_", StringComparison.OrdinalIgnoreCase))
                return "检查/" + name.Substring("检查_".Length);
            if (name.StartsWith("信息_", StringComparison.OrdinalIgnoreCase))
                return "信息/" + name.Substring("信息_".Length);

            return "其他/" + name;
        }

        private static void RevealProjectRelativePath(string relativePath)
        {
            string fullPath = GetProjectRelativeFullPath(relativePath);
            if (Directory.Exists(fullPath) || File.Exists(fullPath))
                EditorUtility.RevealInFinder(fullPath);
        }

        private static string GetProjectRelativeFullPath(string relativePath)
        {
            return Path.Combine(ProjectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ToProjectRelativeAssetPath(string fullPath)
        {
            string normalizedProjectRoot = NormalizePath(ProjectRoot);
            string normalizedPath = NormalizePath(fullPath);
            if (normalizedPath.StartsWith(normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
                return normalizedPath.Substring(normalizedProjectRoot.Length).TrimStart('/').Replace('\\', '/');

            return fullPath.Replace('\\', '/');
        }

        private void FlushOutput()
        {
            if (tabs == null)
                return;

            bool changed = false;
            int outputLimit = GetOutputLimit();

            foreach (AgentSessionTab tab in tabs)
            {
                if (tab == null)
                    continue;

                tab.EnsureRuntime();
                TryCaptureSessionKey(tab);

                if (tab.conPty != null && !tab.conPty.IsRunning && string.IsNullOrWhiteSpace(tab.lastStopTime))
                {
                    tab.lastStopTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    tab.summary = "后台 CMD 已结束";
                    SetStatusNotice(tab, "任务已结束，可以继续发送或恢复其他会话。", 1);
                    tab.terminal.Write("\n【完成】后台 CMD/codex 已结束，可以继续发送或恢复其他会话。\n");
                    tab.outputText = tab.terminal.GetText(outputLimit);
                    changed = true;
                }

                while (tab.pendingOutput.TryDequeue(out string line))
                {
                    tab.terminal.Write(line);
                    tab.outputText = tab.terminal.GetText(outputLimit);
                    changed = true;
                }
            }

            if (changed)
                Repaint();
        }

        private void AppendOutput(AgentSessionTab tab, string text)
        {
            if (tab == null)
                return;

            tab.EnsureRuntime();
            tab.terminal.Write(text);
            tab.outputText = tab.terminal.GetText(GetOutputLimit());
            Repaint();
        }

        private static string CleanTerminalOutput(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            StringBuilder builder = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\u001b')
                {
                    i = SkipAnsiEscape(text, i);
                    continue;
                }

                if (c == '\u0007')
                    continue;

                if (c == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                        continue;

                    builder.Append('\n');
                    continue;
                }

                if (c == '\b')
                {
                    if (builder.Length > 0)
                        builder.Length--;
                    continue;
                }

                if (char.IsControl(c) && c != '\n' && c != '\t')
                    continue;

                builder.Append(c);
            }

            return CollapseExcessBlankLines(builder.ToString());
        }

        private static int SkipAnsiEscape(string text, int escapeIndex)
        {
            int i = escapeIndex + 1;
            if (i >= text.Length)
                return escapeIndex;

            char kind = text[i];
            if (kind == '[')
            {
                i++;
                while (i < text.Length)
                {
                    char c = text[i];
                    if (c >= '@' && c <= '~')
                        return i;
                    i++;
                }
                return text.Length - 1;
            }

            if (kind == ']')
            {
                i++;
                while (i < text.Length)
                {
                    if (text[i] == '\u0007')
                        return i;
                    if (text[i] == '\u001b' && i + 1 < text.Length && text[i + 1] == '\\')
                        return i + 1;
                    i++;
                }
                return text.Length - 1;
            }

            return i;
        }

        private static string CollapseExcessBlankLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            StringBuilder builder = new StringBuilder(text.Length);
            int consecutiveNewLines = 0;
            foreach (char c in text)
            {
                if (c == '\n')
                {
                    consecutiveNewLines++;
                    if (consecutiveNewLines <= 2)
                        builder.Append(c);
                    continue;
                }

                consecutiveNewLines = 0;
                builder.Append(c);
            }

            return builder.ToString();
        }

        private void TryCaptureSessionKey(AgentSessionTab tab)
        {
            if (agent == null || tab == null || tab.capturedSessionKey || !agent.autoCaptureResumeKey)
                return;

            if (!tab.IsRunning)
                return;

            if ((DateTime.UtcNow - tab.startedAtUtc).TotalSeconds < 2.0)
                return;

            if (tab.captureAttemptCount >= MaxSessionCaptureAttempts)
                return;

            if (tab.lastCaptureAttemptUtc != default
                && (DateTime.UtcNow - tab.lastCaptureAttemptUtc).TotalSeconds < SessionCaptureIntervalSeconds)
                return;

            tab.lastCaptureAttemptUtc = DateTime.UtcNow;
            tab.captureAttemptCount++;

            string sessionId = TryReadLatestSessionId(tab);
            if (string.IsNullOrWhiteSpace(sessionId))
                return;

            tab.sessionId = sessionId;
            tab.title = GetSessionDisplayName(sessionId, $"会话 {ShortId(sessionId)}");
            tab.capturedSessionKey = true;
            tab.summary = "已记录恢复 Key";
            SetStatusNotice(tab, "已记录恢复 Key，可在“更多”里保存当前标题为会话名。", 1);
            agent.lastResumeSessionId = sessionId;
            TouchSessionMetadata(sessionId, tab.title);
            EditorUtility.SetDirty(agent);
            Repaint();
        }

        private string TryReadLatestSessionId(AgentSessionTab tab)
        {
            try
            {
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "sessions");
                if (!Directory.Exists(root))
                    return "";

                string workspace = agent != null ? agent.GetWorkspacePath() : string.Empty;
                DateTime lowerBoundUtc = tab.startedAtUtc == default ? DateTime.UtcNow.AddMinutes(-5) : tab.startedAtUtc.AddSeconds(-3);

                List<FileInfo> candidates = EnumerateFilesBounded(root, "*.jsonl", MaxCodexSessionCandidateFiles, MaxScannedDirectories)
                    .Where(file => file.LastWriteTimeUtc >= lowerBoundUtc)
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .ToList();

                foreach (FileInfo file in candidates)
                {
                    if (TryReadSessionIdFromFile(file.FullName, workspace, out string sessionId))
                    {
                        tab.createdSessionFile = file.FullName;
                        return sessionId;
                    }
                }
            }
            catch
            {
                return "";
            }

            return "";
        }

        private static bool TryReadSessionIdFromFile(string filePath, string workspace, out string sessionId)
        {
            sessionId = string.Empty;

            try
            {
                using (StreamReader reader = new StreamReader(filePath, Encoding.UTF8, true))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.IndexOf("\"type\":\"session_meta\"", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        string id = ExtractJsonString(line, "session_id");
                        if (string.IsNullOrWhiteSpace(id))
                            id = ExtractJsonString(line, "id");

                        if (string.IsNullOrWhiteSpace(id))
                            continue;

                        if (!string.IsNullOrWhiteSpace(workspace))
                        {
                            string cwd = ExtractJsonString(line, "cwd");
                            if (!string.IsNullOrWhiteSpace(cwd) && !WorkspaceMatches(cwd, workspace))
                                continue;
                        }

                        sessionId = id;
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static string ExtractJsonString(string text, string key)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            string needle = "\"" + key + "\":\"";
            int index = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return string.Empty;

            index += needle.Length;
            int endIndex = text.IndexOf('"', index);
            if (endIndex < 0 || endIndex <= index)
                return string.Empty;

            return UnescapeSimpleJsonString(text.Substring(index, endIndex - index));
        }

        private static string UnescapeSimpleJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\\\\", "\\")
                .Replace("\\/", "/")
                .Replace("\\\"", "\"");
        }

        private static bool WorkspaceMatches(string left, string right)
        {
            return string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Replace('/', '\\').TrimEnd('\\');
        }

        private static List<FileInfo> EnumerateFilesBounded(string root, string pattern, int maxFiles, int maxDirectories)
        {
            List<FileInfo> files = new List<FileInfo>();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root) || maxFiles <= 0 || maxDirectories <= 0)
                return files;

            Queue<DirectoryInfo> queue = new Queue<DirectoryInfo>();
            queue.Enqueue(new DirectoryInfo(root));

            int visitedDirectories = 0;
            while (queue.Count > 0 && files.Count < maxFiles && visitedDirectories < maxDirectories)
            {
                DirectoryInfo directory = queue.Dequeue();
                visitedDirectories++;

                try
                {
                    foreach (FileInfo file in directory.EnumerateFiles(pattern, SearchOption.TopDirectoryOnly)
                                 .OrderByDescending(file => file.LastWriteTimeUtc))
                    {
                        files.Add(file);
                        if (files.Count >= maxFiles)
                            break;
                    }

                    if (files.Count >= maxFiles)
                        break;

                    foreach (DirectoryInfo child in directory.EnumerateDirectories()
                                 .OrderByDescending(child => child.LastWriteTimeUtc))
                    {
                        if (visitedDirectories + queue.Count >= maxDirectories)
                            break;

                        queue.Enqueue(child);
                    }
                }
                catch
                {
                    // Some user folders may be temporarily locked; skip them instead of stalling the editor.
                }
            }

            return files;
        }

        private void TrimOutput(AgentSessionTab tab, int maxChars)
        {
            if (tab == null || string.IsNullOrEmpty(tab.outputText) || tab.outputText.Length <= maxChars)
                return;

            int removeCount = tab.outputText.Length - maxChars;
            tab.outputText = "[本地输出已自动截断，仅保留最近内容]\n" + tab.outputText.Substring(removeCount);
        }

        private int GetOutputLimit()
        {
            return Mathf.Clamp(agent != null ? agent.maxOutputCharsPerTab : FallbackOutputCharLimit, 2000, 200000);
        }

        private int GetTabLimit()
        {
            return Mathf.Clamp(agent != null ? agent.maxLocalTabsToKeep : FallbackMaxLocalTabs, 1, 12);
        }

        private void CloseCurrentTab()
        {
            if (tabs == null || tabs.Count == 0)
                return;

            AgentSessionTab tab = GetCurrentTab();
            if (tab != null && tab.IsRunning)
            {
                bool stop = EditorUtility.DisplayDialog(
                    "关闭页签",
                    "当前页签仍在运行。关闭会停止本地命令行进程，但之后仍可通过 resume 恢复会话。",
                    "停止并关闭",
                    "取消");
                if (!stop)
                    return;
            }

            StopProcess(tab);
            tabs.RemoveAt(selectedTabIndex);
            EnsureTabExists();
            selectedTabIndex = Mathf.Clamp(selectedTabIndex, 0, tabs.Count - 1);
        }

        private void RemoveStoppedTabs()
        {
            if (tabs == null || tabs.Count == 0)
                return;

            for (int i = tabs.Count - 1; i >= 0; i--)
            {
                if (tabs[i] != null && !tabs[i].IsRunning)
                    tabs.RemoveAt(i);
            }

            EnsureTabExists();
            selectedTabIndex = Mathf.Clamp(selectedTabIndex, 0, tabs.Count - 1);
        }

        private void TrimStoppedTabsToLimit()
        {
            if (tabs == null)
                return;

            int limit = GetTabLimit();
            for (int i = 0; i < tabs.Count && tabs.Count > limit;)
            {
                if (tabs[i] != null && !tabs[i].IsRunning && i != selectedTabIndex)
                {
                    tabs.RemoveAt(i);
                    if (selectedTabIndex > i)
                        selectedTabIndex--;
                    continue;
                }

                i++;
            }

            selectedTabIndex = Mathf.Clamp(selectedTabIndex, 0, tabs.Count - 1);
        }

        private void ReduceLocalResidue()
        {
            if (tabs == null || tabs.Count == 0)
                return;

            int limit = Mathf.Clamp(GetTabLimit(), 1, 2);
            for (int i = tabs.Count - 1; i >= 0; i--)
            {
                AgentSessionTab tab = tabs[i];
                if (tab != null)
                {
                    tab.inputText = "";
                    tab.outputText = "";
                    tab.terminal?.Clear();
                    tab.scroll = Vector2.zero;
                }

                if (tab != null && tabs.Count > limit && !tab.IsRunning && i != selectedTabIndex)
                    tabs.RemoveAt(i);
            }
        }

        private void RegisterProcessTab(Process process, AgentSessionTab tab)
        {
            if (process == null || tab == null)
                return;

            lock (processTabsLock)
                processTabs[process] = tab;
        }

        private bool TryGetProcessTab(Process process, out AgentSessionTab tab)
        {
            if (process == null)
            {
                tab = null;
                return false;
            }

            lock (processTabsLock)
                return processTabs.TryGetValue(process, out tab);
        }

        private void UnregisterProcessTab(Process process)
        {
            if (process == null)
                return;

            lock (processTabsLock)
                processTabs.Remove(process);
        }

        private void UnregisterProcessEvents(Process process)
        {
            if (process == null)
                return;

            process.OutputDataReceived -= OnProcessOutputDataReceived;
            process.ErrorDataReceived -= OnProcessErrorDataReceived;
            process.Exited -= OnProcessExited;
            UnregisterProcessTab(process);
        }

        private void OnProcessOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null || sender is not Process process || !TryGetProcessTab(process, out AgentSessionTab tab))
                return;

            tab.pendingOutput.Enqueue(e.Data + "\n");
        }

        private void OnProcessErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null || sender is not Process process || !TryGetProcessTab(process, out AgentSessionTab tab))
                return;

            tab.pendingOutput.Enqueue("[错误] " + e.Data + "\n");
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            if (sender is not Process process || !TryGetProcessTab(process, out AgentSessionTab tab))
                return;

            tab.lastStopTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            tab.summary = "任务已完成";
            SetStatusNotice(tab, "任务已完成，可以查看输出或继续发送。", 1);
            tab.pendingOutput.Enqueue("【完成】AI 任务已完成，可以查看输出或继续发送。\n");
            tab.pendingOutput.Enqueue("[ES Cmd Agent] 如果没有得到预期回复，请检查 Codex 命令、网络、登录状态和恢复 Key，然后重新发送。\n");
            UnregisterProcessEvents(process);
            if (tab.process == process)
                tab.process = null;
            process.Dispose();
        }

        private void StopProcess(AgentSessionTab tab)
        {
            if (tab == null)
                return;

            if (tab.conPty != null)
            {
                try
                {
                    tab.conPty.Dispose();
                }
                catch
                {
                    // Ignore terminal shutdown races.
                }

                tab.conPty = null;
                tab.lastStopTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                tab.summary = "后台 CMD 已停止";
                SetStatusNotice(tab, "已停止后台 CMD。", 2);
            }

            if (tab.process == null)
                return;

            Process process = tab.process;
            try
            {
                UnregisterProcessEvents(process);
                if (!process.HasExited)
                    process.Kill();
            }
            catch
            {
                // Ignore process shutdown races.
            }
            finally
            {
                process.Dispose();
                tab.process = null;
                tab.lastStopTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                tab.summary = "已停止";
            }
        }

        private void StopAllProcesses()
        {
            if (tabs == null)
                return;

            foreach (AgentSessionTab tab in tabs)
                StopProcess(tab);
        }

        private void DrawArchitectPage()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    if (GUILayout.Button("扫描并生成思路图", EditorStyles.toolbarButton, GUILayout.Width(130)))
                        RebuildArchitectGraph();

                    if (GUILayout.Button("自动布局", EditorStyles.toolbarButton, GUILayout.Width(75)))
                        LayoutArchitectNodes();

                    if (GUILayout.Button("导出 Markdown", EditorStyles.toolbarButton, GUILayout.Width(95)))
                        ExportArchitectMarkdown();

                    if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    {
                        architectNodes.Clear();
                        architectEdges.Clear();
                        architectSelectedNodeIndex = -1;
                    }

                    GUILayout.FlexibleSpace();
                    architectIncludeCodexSessions = GUILayout.Toggle(architectIncludeCodexSessions, "Codex", EditorStyles.toolbarButton, GUILayout.Width(58));
                    architectIncludeAITalkSessions = GUILayout.Toggle(architectIncludeAITalkSessions, "AITalk", EditorStyles.toolbarButton, GUILayout.Width(60));
                    architectIncludeAIWarnings = GUILayout.Toggle(architectIncludeAIWarnings, "AIWarnings", EditorStyles.toolbarButton, GUILayout.Width(88));
                }

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"节点 {architectNodes.Count}", EditorStyles.miniBoldLabel, GUILayout.Width(80));
                    EditorGUILayout.LabelField($"关系 {architectEdges.Count}", EditorStyles.miniBoldLabel, GUILayout.Width(80));
                    architectSearchText = EditorGUILayout.TextField(architectSearchText);
                    if (GUILayout.Button("重置搜索", GUILayout.Width(80)))
                        architectSearchText = "";
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawArchitectCanvas();
                    DrawArchitectInspector();
                }
            }
        }

        private void DrawArchitectCanvas()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                Rect viewRect = GUILayoutUtility.GetRect(10, 10000, 10, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                architectScroll = GUI.BeginScrollView(viewRect, architectScroll, new Rect(0, 0, 2800, 1800));

                DrawArchitectGrid(new Vector2(2800, 1800), 24, new Color(1f, 1f, 1f, 0.05f));
                DrawArchitectGrid(new Vector2(2800, 1800), 120, new Color(1f, 1f, 1f, 0.08f));
                DrawArchitectEdges();
                DrawArchitectNodes();
                HandleArchitectCanvasEvents();

                GUI.EndScrollView();
            }
        }

        private void DrawArchitectInspector()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(300), GUILayout.ExpandHeight(true)))
            {
                EditorGUILayout.LabelField("节点详情", EditorStyles.boldLabel);
                ArchitectNode node = GetSelectedArchitectNode();
                if (node == null)
                {
                    EditorGUILayout.HelpBox("先点一个节点。这里会显示来源、摘要和定位操作。", MessageType.Info);
                    DrawArchitectScanSettings();
                    return;
                }

                EditorGUILayout.LabelField(node.title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(node.type, EditorStyles.miniBoldLabel);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("摘要", EditorStyles.miniBoldLabel);
                EditorGUILayout.TextArea(node.summary, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(88));

                EditorGUILayout.Space(4);
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField("来源", node.sourcePath);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(node.sourcePath)))
                    {
                        if (GUILayout.Button("定位文件"))
                            RevealArchitectSource(node.sourcePath);
                    }

                    if (GUILayout.Button("复制摘要"))
                        EditorGUIUtility.systemCopyBuffer = $"{node.title}\n{node.summary}\n{node.sourcePath}";
                }

                EditorGUILayout.Space(8);
                DrawArchitectScanSettings();
            }
        }

        private void DrawArchitectScanSettings()
        {
            EditorGUILayout.LabelField("扫描范围", EditorStyles.boldLabel);
            architectIncludeCodexSessions = EditorGUILayout.ToggleLeft("读取 Codex 本机会话", architectIncludeCodexSessions);
            architectIncludeAITalkSessions = EditorGUILayout.ToggleLeft("读取 AITalk 协作会话", architectIncludeAITalkSessions);
            architectIncludeAIWarnings = EditorGUILayout.ToggleLeft("读取 AIWarnings 长期结论", architectIncludeAIWarnings);
            architectMaxCodexSessions = Mathf.Clamp(EditorGUILayout.IntField("Codex 会话上限", architectMaxCodexSessions), 1, 100);
            architectMaxFilesPerFolder = Mathf.Clamp(EditorGUILayout.IntField("每类文件上限", architectMaxFilesPerFolder), 10, 500);
            EditorGUILayout.HelpBox("这里只做本地聚合和可视化，不调用模型。", MessageType.None);
        }

        private void RebuildArchitectGraph()
        {
            architectNodes.Clear();
            architectEdges.Clear();
            architectSelectedNodeIndex = -1;

            AddArchitectNode("root", "ES 项目架构总图", "Project", Application.dataPath, "由本地 AI 会话、AITalk 协作记录、AIWarnings 长期结论聚合生成。", new Color(0.28f, 0.50f, 0.88f), new Vector2(80, 70));

            if (architectIncludeAIWarnings)
                ScanArchitectAIWarnings();

            if (architectIncludeAITalkSessions)
                ScanArchitectAITalkSessions();

            if (architectIncludeCodexSessions)
                ScanArchitectCodexSessions();

            BuildArchitectHeuristicEdges();
            LayoutArchitectNodes();
            Repaint();
        }

        private void ScanArchitectAIWarnings()
        {
            string root = Path.Combine(ProjectRoot, "Assets/Plugins/ES/AIWarnings".Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(root))
                return;

            AddArchitectCategory("AIWarnings", "长期架构结论", "长期沉淀的项目规则、禁止事项和跨系统纠偏。", root, new Color(0.74f, 0.48f, 0.22f));

            foreach (string file in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories).Take(architectMaxFilesPerFolder))
            {
                string title = ReadArchitectTitle(file);
                string summary = ReadArchitectSummary(file);
                int index = AddArchitectNode("warning:" + file, title, "AIWarning", file, summary, new Color(0.78f, 0.58f, 0.30f), Vector2.zero);
                architectEdges.Add(new ArchitectEdge { from = FindArchitectNodeIndex("AIWarnings"), to = index, label = "沉淀" });
            }
        }

        private void ScanArchitectAITalkSessions()
        {
            string root = Path.Combine(ProjectRoot, "Assets/Plugins/ES/AITalk/Sessions".Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(root))
                return;

            AddArchitectCategory("AITalk", "AI 协作会话", "跨 AI 讨论、多人规则推演、阶段性共识和最终结论。", root, new Color(0.38f, 0.62f, 0.45f));

            foreach (string sessionDir in Directory.EnumerateDirectories(root).OrderByDescending(Directory.GetLastWriteTime).Take(architectMaxFilesPerFolder))
            {
                string name = Path.GetFileName(sessionDir);
                string summary = ReadArchitectSessionSummary(sessionDir);
                int sessionIndex = AddArchitectNode("aitalk:" + sessionDir, name, "AITalk Session", sessionDir, summary, new Color(0.42f, 0.70f, 0.50f), Vector2.zero);
                architectEdges.Add(new ArchitectEdge { from = FindArchitectNodeIndex("AITalk"), to = sessionIndex, label = "会话" });

                string consensus = Path.Combine(sessionDir, "Consensus");
                if (Directory.Exists(consensus))
                {
                    foreach (string file in Directory.EnumerateFiles(consensus, "*.md").Take(8))
                    {
                        int child = AddArchitectNode("consensus:" + file, ReadArchitectTitle(file), "Consensus", file, ReadArchitectSummary(file), new Color(0.50f, 0.74f, 0.56f), Vector2.zero);
                        architectEdges.Add(new ArchitectEdge { from = sessionIndex, to = child, label = "结论" });
                    }
                }
            }
        }

        private void ScanArchitectCodexSessions()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "sessions");
            if (!Directory.Exists(root))
                return;

            AddArchitectCategory("CodexSessions", "Codex 本机会话", "本机 Codex CLI 记录。只读取匹配当前工程 cwd 的 session_meta 和有限摘要。", root, new Color(0.42f, 0.48f, 0.82f));

            List<FileInfo> files = EnumerateFilesBounded(root, "*.jsonl", MaxCodexSessionCandidateFiles, MaxScannedDirectories)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(architectMaxCodexSessions * 3)
                .ToList();

            int added = 0;
            foreach (FileInfo file in files)
            {
                if (added >= architectMaxCodexSessions)
                    break;

                if (!TryReadCodexSessionMeta(file.FullName, out string sessionId, out string cwd, out string summary))
                    continue;

                if (!WorkspaceContainsProject(cwd))
                    continue;

                int index = AddArchitectNode("codex:" + sessionId, "Codex " + ShortId(sessionId), "Codex Session", file.FullName, summary, new Color(0.46f, 0.52f, 0.86f), Vector2.zero);
                architectEdges.Add(new ArchitectEdge { from = FindArchitectNodeIndex("CodexSessions"), to = index, label = "记录" });
                added++;
            }
        }

        private void AddArchitectCategory(string id, string title, string summary, string sourcePath, Color color)
        {
            int rootIndex = FindArchitectNodeIndex("root");
            int index = AddArchitectNode(id, title, "Category", sourcePath, summary, color, Vector2.zero);
            if (rootIndex >= 0)
                architectEdges.Add(new ArchitectEdge { from = rootIndex, to = index, label = "分类" });
        }

        private int AddArchitectNode(string id, string title, string type, string sourcePath, string summary, Color color, Vector2 position)
        {
            int existing = FindArchitectNodeIndex(id);
            if (existing >= 0)
                return existing;

            architectNodes.Add(new ArchitectNode
            {
                id = id,
                title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(sourcePath) : title.Trim(),
                type = type,
                sourcePath = sourcePath,
                summary = LimitText(summary),
                color = color,
                rect = new Rect(position.x, position.y, 230, 112)
            });

            return architectNodes.Count - 1;
        }

        private void BuildArchitectHeuristicEdges()
        {
            string[] keyTerms =
            {
                "表格", "资源", "输入", "玩家", "状态", "技能", "对象池", "GameManager", "运动", "Item", "AI", "架构", "编译", "ReloadDomain"
            };

            for (int i = 0; i < architectNodes.Count; i++)
            {
                ArchitectNode node = architectNodes[i];
                if (node.type == "Category" || node.type == "Project")
                    continue;

                foreach (string term in keyTerms)
                {
                    if (!ContainsArchitectTerm(node, term))
                        continue;

                    int anchor = FindOrCreateArchitectTermNode(term);
                    if (!HasArchitectEdge(anchor, i))
                        architectEdges.Add(new ArchitectEdge { from = anchor, to = i, label = "关联" });
                }
            }
        }

        private int FindOrCreateArchitectTermNode(string term)
        {
            string id = "term:" + term;
            int existing = FindArchitectNodeIndex(id);
            if (existing >= 0)
                return existing;

            int rootIndex = FindArchitectNodeIndex("root");
            int index = AddArchitectNode(id, term, "Topic", "", "自动从 AI 记录标题和摘要中识别出的项目主题。", new Color(0.45f, 0.45f, 0.45f), Vector2.zero);
            if (rootIndex >= 0)
                architectEdges.Add(new ArchitectEdge { from = rootIndex, to = index, label = "主题" });

            return index;
        }

        private static bool ContainsArchitectTerm(ArchitectNode node, string term)
        {
            return (!string.IsNullOrEmpty(node.title) && node.title.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrEmpty(node.summary) && node.summary.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrEmpty(node.sourcePath) && node.sourcePath.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private bool HasArchitectEdge(int from, int to)
        {
            return architectEdges.Any(edge => edge.from == from && edge.to == to);
        }

        private void LayoutArchitectNodes()
        {
            if (architectNodes.Count == 0)
                return;

            Dictionary<string, int> typeRows = new Dictionary<string, int>
            {
                ["Project"] = 0,
                ["Category"] = 1,
                ["Topic"] = 2,
                ["AIWarning"] = 3,
                ["AITalk Session"] = 4,
                ["Consensus"] = 5,
                ["Codex Session"] = 6
            };

            Dictionary<string, int> typeColumns = new Dictionary<string, int>();
            for (int i = 0; i < architectNodes.Count; i++)
            {
                ArchitectNode node = architectNodes[i];
                int row = typeRows.TryGetValue(node.type, out int knownRow) ? knownRow : 7;
                int column = typeColumns.TryGetValue(node.type, out int knownColumn) ? knownColumn : 0;
                typeColumns[node.type] = column + 1;
                node.rect.position = new Vector2(70 + column * 270, 60 + row * 170);
            }
        }

        private void DrawArchitectGrid(Vector2 size, float spacing, Color color)
        {
            Handles.BeginGUI();
            Handles.color = color;
            for (float x = 0; x < size.x; x += spacing)
                Handles.DrawLine(new Vector3(x, 0), new Vector3(x, size.y));
            for (float y = 0; y < size.y; y += spacing)
                Handles.DrawLine(new Vector3(0, y), new Vector3(size.x, y));
            Handles.color = Color.white;
            Handles.EndGUI();
        }

        private void DrawArchitectEdges()
        {
            Handles.BeginGUI();
            foreach (ArchitectEdge edge in architectEdges)
            {
                if (edge.from < 0 || edge.from >= architectNodes.Count || edge.to < 0 || edge.to >= architectNodes.Count)
                    continue;

                Rect from = architectNodes[edge.from].rect;
                Rect to = architectNodes[edge.to].rect;
                Vector3 start = new Vector3(from.xMax, from.center.y);
                Vector3 end = new Vector3(to.xMin, to.center.y);
                Vector3 startTan = start + Vector3.right * 70f;
                Vector3 endTan = end + Vector3.left * 70f;
                Handles.DrawBezier(start, end, startTan, endTan, new Color(0.85f, 0.85f, 0.85f, 0.45f), null, 2f);
            }
            Handles.EndGUI();
        }

        private void DrawArchitectNodes()
        {
            for (int i = 0; i < architectNodes.Count; i++)
            {
                ArchitectNode node = architectNodes[i];
                if (!PassArchitectSearch(node))
                    continue;

                Color old = GUI.color;
                GUI.color = architectSelectedNodeIndex == i ? Color.white : new Color(1f, 1f, 1f, 0.95f);
                node.rect = GUI.Window(i + 1000, node.rect, id => DrawArchitectNodeWindow(id - 1000), GUIContent.none, GetArchitectNodeStyle(node));
                GUI.color = old;
            }
        }

        private void DrawArchitectNodeWindow(int index)
        {
            if (index < 0 || index >= architectNodes.Count)
                return;

            ArchitectNode node = architectNodes[index];
            Rect header = new Rect(0, 0, node.rect.width, 24);
            EditorGUI.DrawRect(header, node.color);
            GUI.Label(new Rect(8, 4, node.rect.width - 16, 18), node.title, EditorStyles.whiteMiniLabel);
            GUI.Label(new Rect(8, 30, node.rect.width - 16, 18), node.type, EditorStyles.miniBoldLabel);
            GUI.Label(new Rect(8, 50, node.rect.width - 16, node.rect.height - 56), node.summary, EditorStyles.wordWrappedMiniLabel);
            GUI.DragWindow(new Rect(0, 0, node.rect.width, node.rect.height));

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                architectSelectedNodeIndex = index;
                Repaint();
            }
        }

        private GUIStyle GetArchitectNodeStyle(ArchitectNode node)
        {
            string key = node.type;
            if (architectStyleCache.TryGetValue(key, out GUIStyle style))
                return style;

            style = new GUIStyle(GUI.skin.window)
            {
                padding = new RectOffset(6, 6, 6, 6)
            };
            architectStyleCache[key] = style;
            return style;
        }

        private void HandleArchitectCanvasEvents()
        {
            Event current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0)
            {
                for (int i = architectNodes.Count - 1; i >= 0; i--)
                {
                    if (!PassArchitectSearch(architectNodes[i]) || !architectNodes[i].rect.Contains(current.mousePosition))
                        continue;

                    architectSelectedNodeIndex = i;
                    architectDraggingNode = true;
                    architectDraggingNodeIndex = i;
                    architectDragOffset = current.mousePosition - architectNodes[i].rect.position;
                    current.Use();
                    break;
                }
            }
            else if (current.type == EventType.MouseDrag && architectDraggingNode && architectDraggingNodeIndex >= 0 && architectDraggingNodeIndex < architectNodes.Count)
            {
                architectNodes[architectDraggingNodeIndex].rect.position = current.mousePosition - architectDragOffset;
                current.Use();
                Repaint();
            }
            else if (current.type == EventType.MouseUp)
            {
                architectDraggingNode = false;
                architectDraggingNodeIndex = -1;
            }
        }

        private bool PassArchitectSearch(ArchitectNode node)
        {
            if (node == null)
                return false;

            if (string.IsNullOrWhiteSpace(architectSearchText))
                return true;

            return ContainsArchitectTerm(node, architectSearchText);
        }

        private ArchitectNode GetSelectedArchitectNode()
        {
            if (architectSelectedNodeIndex < 0 || architectSelectedNodeIndex >= architectNodes.Count)
                return null;

            return architectNodes[architectSelectedNodeIndex];
        }

        private int FindArchitectNodeIndex(string id)
        {
            return architectNodes.FindIndex(node => node.id == id);
        }

        private static string ReadArchitectTitle(string filePath)
        {
            try
            {
                foreach (string line in File.ReadLines(filePath, Encoding.UTF8).Take(20))
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("#"))
                        return trimmed.TrimStart('#').Trim();
                }
            }
            catch
            {
                return Path.GetFileNameWithoutExtension(filePath);
            }

            return Path.GetFileNameWithoutExtension(filePath);
        }

        private static string ReadArchitectSummary(string filePath)
        {
            try
            {
                StringBuilder builder = new StringBuilder();
                foreach (string line in File.ReadLines(filePath, Encoding.UTF8).Take(80))
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                        continue;

                    builder.Append(trimmed).Append(' ');
                    if (builder.Length >= 220)
                        break;
                }

                return LimitText(builder.ToString());
            }
            catch
            {
                return "";
            }
        }

        private static string ReadArchitectSessionSummary(string sessionDir)
        {
            string final = Path.Combine(sessionDir, "Consensus", "最终结论_返回用户.md");
            if (File.Exists(final))
                return ReadArchitectSummary(final);

            string current = Path.Combine(sessionDir, "Consensus", "当前共同意见.md");
            if (File.Exists(current))
                return ReadArchitectSummary(current);

            string desc = Path.Combine(sessionDir, "00_会话说明.md");
            return File.Exists(desc) ? ReadArchitectSummary(desc) : Path.GetFileName(sessionDir);
        }

        private static bool TryReadCodexSessionMeta(string filePath, out string sessionId, out string cwd, out string summary)
        {
            sessionId = "";
            cwd = "";
            summary = "";

            try
            {
                foreach (string line in File.ReadLines(filePath, Encoding.UTF8).Take(40))
                {
                    if (line.IndexOf("\"type\":\"session_meta\"", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    sessionId = ExtractJsonString(line, "session_id");
                    if (string.IsNullOrWhiteSpace(sessionId))
                        sessionId = ExtractJsonString(line, "id");

                    cwd = ExtractJsonString(line, "cwd");
                    summary = $"会话 {ShortId(sessionId)}，工作目录：{cwd}";
                    return !string.IsNullOrWhiteSpace(sessionId);
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool WorkspaceContainsProject(string cwd)
        {
            if (string.IsNullOrWhiteSpace(cwd))
                return false;

            string projectRoot = NormalizePath(ProjectRoot);
            string normalizedCwd = NormalizePath(cwd);
            return string.Equals(projectRoot, normalizedCwd, StringComparison.OrdinalIgnoreCase)
                || projectRoot.StartsWith(normalizedCwd, StringComparison.OrdinalIgnoreCase)
                || normalizedCwd.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static string ProjectRoot
        {
            get
            {
                string dataPath = Application.dataPath;
                return dataPath.EndsWith("/Assets", StringComparison.OrdinalIgnoreCase)
                    ? dataPath.Substring(0, dataPath.Length - "/Assets".Length)
                    : Directory.GetParent(dataPath)?.FullName ?? dataPath;
            }
        }

        private void ExportArchitectMarkdown()
        {
            string folder = Path.Combine(ProjectRoot, "Assets/Plugins/ES/AIWarnings/ArchitectReports".Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string path = Path.Combine(folder, "项目全局架构师_思路图.md");
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# 项目全局架构师：思路图");
            builder.AppendLine();
            builder.AppendLine($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine();

            foreach (ArchitectNode node in architectNodes)
            {
                builder.AppendLine($"## {node.title}");
                builder.AppendLine();
                builder.AppendLine($"- 类型：{node.type}");
                builder.AppendLine($"- 来源：{node.sourcePath}");
                builder.AppendLine($"- 摘要：{node.summary}");
                builder.AppendLine();
            }

            builder.AppendLine("## 关系");
            builder.AppendLine();
            foreach (ArchitectEdge edge in architectEdges)
            {
                if (edge.from < 0 || edge.from >= architectNodes.Count || edge.to < 0 || edge.to >= architectNodes.Count)
                    continue;

                builder.AppendLine($"- {architectNodes[edge.from].title} -> {architectNodes[edge.to].title}：{edge.label}");
            }

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            EditorUtility.RevealInFinder(path);
        }

        private static void RevealArchitectSource(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                return;

            EditorUtility.RevealInFinder(sourcePath);
        }

        private static string LimitText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            value = value.Trim();
            return value.Length <= 220 ? value : value.Substring(0, 220) + "...";
        }

        private static string ShortId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            string clean = value.Trim();
            return clean.Length <= 10 ? clean : clean.Substring(0, 10);
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }
    }
}
