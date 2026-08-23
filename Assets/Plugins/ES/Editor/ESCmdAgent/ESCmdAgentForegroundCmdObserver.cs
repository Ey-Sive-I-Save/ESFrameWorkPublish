using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ES
{
    internal enum ESCmdAgentForegroundCmdObservationKind : byte
    {
        Unavailable = 0,
        NoForegroundWindow = 1,
        DirectCmd = 2,
        TerminalCmdCandidate = 3,
        TerminalCmdAmbiguous = 4,
        TerminalWithoutCmd = 5,
        OtherForegroundProcess = 6,
        ObservationFailed = 7
    }

    // This is intentionally observation-only. It never supplies a SessionId or any control handle.
    internal sealed class ESCmdAgentForegroundCmdObservation
    {
        public ESCmdAgentForegroundCmdObservationKind kind;
        public int hostProcessId;
        public int cmdProcessId;
        public int candidateCount;
        public string hostProcessName = string.Empty;
        public string windowTitle = string.Empty;
        public string summary = string.Empty;
        public string source = string.Empty;
        public bool isAmbiguous;
        public string observedAtUtc = string.Empty;

        public ESCmdAgentForegroundCmdObservation Clone(string nextSource)
        {
            return new ESCmdAgentForegroundCmdObservation
            {
                kind = kind,
                hostProcessId = hostProcessId,
                cmdProcessId = cmdProcessId,
                candidateCount = candidateCount,
                hostProcessName = hostProcessName,
                windowTitle = windowTitle,
                summary = summary,
                source = nextSource ?? string.Empty,
                isAmbiguous = isAmbiguous,
                observedAtUtc = observedAtUtc
            };
        }

        public string BuildTooltip()
        {
            var builder = new StringBuilder();
            builder.Append("来源：").Append(string.IsNullOrWhiteSpace(source) ? "未知" : source);
            builder.Append("\n时间：").Append(string.IsNullOrWhiteSpace(observedAtUtc) ? "未知" : observedAtUtc);
            builder.Append("\n宿主进程：")
                .Append(string.IsNullOrWhiteSpace(hostProcessName) ? "未知" : hostProcessName)
                .Append(" (PID ").Append(hostProcessId).Append(')');
            if (cmdProcessId > 0)
                builder.Append("\nCMD 候选 PID：").Append(cmdProcessId);
            if (candidateCount > 0)
                builder.Append("\nCMD 候选数：").Append(candidateCount);
            if (!string.IsNullOrWhiteSpace(windowTitle))
                builder.Append("\n窗口标题：").Append(windowTitle);
            builder.Append("\n仅只读观察：不接管、不投递、不注入输入，也不会建立 Session 映射。");
            return builder.ToString();
        }
    }

    // A candidate is an observation target only. Its process identity is verified again by the
    // one-time claim responder and must never be treated as a Codex SessionId.
    internal sealed class ESCmdAgentExternalCmdCandidate
    {
        public int cmdProcessId;
        public string cmdProcessStartedAtUtc = string.Empty;
        public int hostProcessId;
        public string hostProcessName = string.Empty;
        public string windowTitle = string.Empty;
        public string source = string.Empty;
        public string observedAtUtc = string.Empty;
        public int ordinal;
        public int candidateCount;

        public string DisplayName
        {
            get
            {
                if (candidateCount <= 1)
                    return "当前 CMD";
                return "CMD 候选 " + ordinal + " / " + candidateCount;
            }
        }

        public string BuildDescription()
        {
            string host = string.IsNullOrWhiteSpace(hostProcessName) ? "终端宿主" : hostProcessName;
            string title = string.IsNullOrWhiteSpace(windowTitle) ? "未提供窗口标题" : windowTitle;
            return host + " · " + title;
        }

        public string BuildTooltip()
        {
            var builder = new StringBuilder();
            builder.Append("来源：").Append(string.IsNullOrWhiteSpace(source) ? "未知" : source);
            builder.Append("\n观察时间：")
                .Append(string.IsNullOrWhiteSpace(observedAtUtc) ? "未知" : observedAtUtc);
            builder.Append("\n终端宿主：")
                .Append(string.IsNullOrWhiteSpace(hostProcessName) ? "未知" : hostProcessName)
                .Append(" (PID ").Append(hostProcessId).Append(')');
            builder.Append("\nCMD 进程：PID ").Append(cmdProcessId);
            builder.Append("\nCMD 启动时间：")
                .Append(string.IsNullOrWhiteSpace(cmdProcessStartedAtUtc) ? "未知" : cmdProcessStartedAtUtc);
            if (!string.IsNullOrWhiteSpace(windowTitle))
                builder.Append("\n窗口标题：").Append(windowTitle);
            builder.Append("\n选择后仍需在该 CMD 内执行一次性回签；PID 与启动时间不一致会被拒绝。");
            return builder.ToString();
        }
    }

    internal sealed class ESCmdAgentExternalCmdDiscovery
    {
        public ESCmdAgentForegroundCmdObservation observation;
        public List<ESCmdAgentExternalCmdCandidate> candidates = new List<ESCmdAgentExternalCmdCandidate>();
        public string summary = string.Empty;
        public string failure = string.Empty;

        public bool Succeeded => string.IsNullOrWhiteSpace(failure);
    }

    internal static class ESCmdAgentForegroundCmdObserver
    {
        private const uint EventSystemForeground = 0x0003;
        private const uint WinEventOutOfContext = 0x0000;
        private const uint WinEventSkipOwnProcess = 0x0002;
        private const uint Th32csSnapProcess = 0x00000002;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);
        private static readonly object ObservationGate = new object();
        private static readonly WinEventDelegate ForegroundChangedCallback = HandleForegroundChanged;
        private static IntPtr foregroundChangedHook;
        private static int observerReferenceCount;
        private static int unityProcessId;
        private static ESCmdAgentForegroundCmdObservation lastExternalForeground;

        internal static void Acquire()
        {
            if (!IsWindows())
                return;
            lock (ObservationGate)
            {
                observerReferenceCount++;
                if (foregroundChangedHook != IntPtr.Zero)
                    return;
                unityProcessId = GetCurrentProcessId();
                foregroundChangedHook = SetWinEventHook(EventSystemForeground, EventSystemForeground, IntPtr.Zero,
                    ForegroundChangedCallback, 0, 0, WinEventOutOfContext | WinEventSkipOwnProcess);
            }
        }

        internal static void Release()
        {
            if (!IsWindows())
                return;
            lock (ObservationGate)
            {
                observerReferenceCount = Math.Max(0, observerReferenceCount - 1);
                if (observerReferenceCount > 0 || foregroundChangedHook == IntPtr.Zero)
                    return;
                UnhookWinEvent(foregroundChangedHook);
                foregroundChangedHook = IntPtr.Zero;
                lastExternalForeground = null;
            }
        }

        internal static ESCmdAgentForegroundCmdObservation Observe()
        {
            if (!IsWindows())
                return CreateUnavailable("当前平台不支持 Windows 前台 CMD 观察。");
            try
            {
                ESCmdAgentForegroundCmdObservation foreground = CaptureForeground("当前前台窗口");
                if (foreground == null)
                    return CreateUnavailable("无法读取当前前台窗口。");
                if (foreground.hostProcessId != unityProcessId)
                    return foreground;

                lock (ObservationGate)
                {
                    if (lastExternalForeground == null)
                    {
                        return CreateUnavailable("当前由 Unity 占前台，尚未记录前台 CMD。请先切到目标 CMD，再返回此窗口检索。");
                    }
                    return lastExternalForeground.Clone("最近外部前台窗口（Unity 点击前）");
                }
            }
            catch (Exception exception)
            {
                return CreateFailure("前台 CMD 观察失败：" + exception.GetBaseException().Message);
            }
        }

        internal static Task<ESCmdAgentExternalCmdDiscovery> DiscoverCandidatesAsync()
        {
            return Task.Run((Func<ESCmdAgentExternalCmdDiscovery>)DiscoverCandidates);
        }

        private static ESCmdAgentExternalCmdDiscovery DiscoverCandidates()
        {
            var discovery = new ESCmdAgentExternalCmdDiscovery();
            try
            {
                ESCmdAgentForegroundCmdObservation observation = Observe();
                discovery.observation = observation;
                if (observation == null)
                {
                    discovery.failure = "无法读取最近激活的 CMD。请先切换到目标 CMD，再回到 Agent 控制台重试。";
                    return discovery;
                }

                var candidateIds = new List<int>();
                if (observation.cmdProcessId > 0)
                {
                    candidateIds.Add(observation.cmdProcessId);
                }
                else if (observation.hostProcessId > 0 && IsWindowsTerminal(observation.hostProcessName))
                {
                    Dictionary<int, ProcessSnapshotEntry> processes = CaptureProcesses();
                    candidateIds.AddRange(FindCmdDescendants(observation.hostProcessId, processes));
                }

                candidateIds = candidateIds.Distinct().OrderBy(value => value).ToList();
                if (candidateIds.Count > 24)
                {
                    discovery.failure = "最近激活终端的 CMD 候选超过 24 个，已拒绝截断选择。请关闭无关终端后重试。";
                    return discovery;
                }
                for (int index = 0; index < candidateIds.Count; index++)
                {
                    string startedAtUtc;
                    if (!TryReadCmdProcessStartedAtUtc(candidateIds[index], out startedAtUtc))
                        continue;
                    discovery.candidates.Add(new ESCmdAgentExternalCmdCandidate
                    {
                        cmdProcessId = candidateIds[index],
                        cmdProcessStartedAtUtc = startedAtUtc,
                        hostProcessId = observation.hostProcessId,
                        hostProcessName = observation.hostProcessName,
                        windowTitle = observation.windowTitle,
                        source = observation.source,
                        observedAtUtc = observation.observedAtUtc,
                        ordinal = discovery.candidates.Count + 1,
                        candidateCount = candidateIds.Count
                    });
                }

                for (int index = 0; index < discovery.candidates.Count; index++)
                {
                    discovery.candidates[index].ordinal = index + 1;
                    discovery.candidates[index].candidateCount = discovery.candidates.Count;
                }

                if (discovery.candidates.Count == 0)
                {
                    discovery.failure = "未发现可回签的 CMD。请先切到目标 CMD，再立即回到 Agent 控制台重新发现。";
                    return discovery;
                }

                discovery.summary = discovery.candidates.Count == 1
                    ? "已发现 1 个最近激活的 CMD。"
                    : "已发现 " + discovery.candidates.Count
                        + " 个 CMD 候选。Windows Terminal 不公开页签身份，必须选择目标后在同一 CMD 回签。";
                return discovery;
            }
            catch (Exception exception)
            {
                discovery.failure = "发现已有 CMD 失败：" + exception.GetBaseException().Message;
                return discovery;
            }
        }

        private static void HandleForegroundChanged(IntPtr hook, uint eventType, IntPtr windowHandle,
            int objectId, int childId, uint eventThread, uint eventTime)
        {
            if (eventType != EventSystemForeground || windowHandle == IntPtr.Zero)
                return;
            try
            {
                ESCmdAgentForegroundCmdObservation observation = CaptureWindow(windowHandle, "最近外部前台窗口");
                if (observation == null || observation.hostProcessId == unityProcessId)
                    return;
                lock (ObservationGate)
                    lastExternalForeground = observation;
            }
            catch
            {
                // Foreground notifications are best-effort diagnostics and must never disrupt Unity's UI thread.
            }
        }

        private static ESCmdAgentForegroundCmdObservation CaptureForeground(string source)
        {
            IntPtr windowHandle = GetForegroundWindow();
            return windowHandle == IntPtr.Zero
                ? new ESCmdAgentForegroundCmdObservation
                {
                    kind = ESCmdAgentForegroundCmdObservationKind.NoForegroundWindow,
                    source = source ?? string.Empty,
                    summary = "当前没有可观察的前台窗口。",
                    observedAtUtc = DateTime.UtcNow.ToString("O")
                }
                : CaptureWindow(windowHandle, source);
        }

        private static ESCmdAgentForegroundCmdObservation CaptureWindow(IntPtr windowHandle, string source)
        {
            uint nativeProcessId;
            GetWindowThreadProcessId(windowHandle, out nativeProcessId);
            int hostProcessId = unchecked((int)nativeProcessId);
            if (hostProcessId <= 0)
                return CreateFailure("无法读取前台窗口所属进程。");

            Dictionary<int, ProcessSnapshotEntry> processes = CaptureProcesses();
            ProcessSnapshotEntry host;
            processes.TryGetValue(hostProcessId, out host);
            string hostName = NormalizeProcessName(host != null ? host.executableName : string.Empty);
            if (string.IsNullOrWhiteSpace(hostName))
                hostName = ReadProcessName(hostProcessId);
            string title = ReadWindowTitle(windowHandle);
            string observedAtUtc = DateTime.UtcNow.ToString("O");

            int ancestorCmdProcessId = FindCmdAncestor(hostProcessId, processes);
            if (ancestorCmdProcessId > 0)
            {
                return CreateCmdObservation(ESCmdAgentForegroundCmdObservationKind.DirectCmd, hostProcessId,
                    ancestorCmdProcessId, 1, hostName, title, source, observedAtUtc, false,
                    "已观察前台 CMD；未接管、未投递、未建立 Session 映射。");
            }

            if (IsWindowsTerminal(hostName))
            {
                List<int> cmdCandidates = FindCmdDescendants(hostProcessId, processes);
                if (cmdCandidates.Count == 1)
                {
                    return CreateCmdObservation(ESCmdAgentForegroundCmdObservationKind.TerminalCmdCandidate,
                        hostProcessId, cmdCandidates[0], 1, hostName, title, source, observedAtUtc, false,
                        "Windows Terminal 检出 1 个 CMD 候选；无法证明它属于当前页签，未接管。" );
                }
                if (cmdCandidates.Count > 1)
                {
                    return CreateCmdObservation(ESCmdAgentForegroundCmdObservationKind.TerminalCmdAmbiguous,
                        hostProcessId, 0, cmdCandidates.Count, hostName, title, source, observedAtUtc, true,
                        "Windows Terminal 下存在多个 CMD 后代；拒绝猜测当前页签。" );
                }
                return new ESCmdAgentForegroundCmdObservation
                {
                    kind = ESCmdAgentForegroundCmdObservationKind.TerminalWithoutCmd,
                    hostProcessId = hostProcessId,
                    hostProcessName = hostName,
                    windowTitle = title,
                    source = source ?? string.Empty,
                    observedAtUtc = observedAtUtc,
                    summary = "当前 Windows Terminal 下未检出 CMD 后代。"
                };
            }

            return new ESCmdAgentForegroundCmdObservation
            {
                kind = ESCmdAgentForegroundCmdObservationKind.OtherForegroundProcess,
                hostProcessId = hostProcessId,
                hostProcessName = hostName,
                windowTitle = title,
                source = source ?? string.Empty,
                observedAtUtc = observedAtUtc,
                summary = "前台不是 CMD 或 Windows Terminal。"
            };
        }

        private static ESCmdAgentForegroundCmdObservation CreateCmdObservation(
            ESCmdAgentForegroundCmdObservationKind kind, int hostProcessId, int cmdProcessId, int candidateCount,
            string hostName, string title, string source, string observedAtUtc, bool ambiguous, string summary)
        {
            return new ESCmdAgentForegroundCmdObservation
            {
                kind = kind,
                hostProcessId = hostProcessId,
                cmdProcessId = cmdProcessId,
                candidateCount = candidateCount,
                hostProcessName = hostName,
                windowTitle = title,
                source = source ?? string.Empty,
                observedAtUtc = observedAtUtc,
                isAmbiguous = ambiguous,
                summary = summary
            };
        }

        private static ESCmdAgentForegroundCmdObservation CreateUnavailable(string summary)
        {
            return new ESCmdAgentForegroundCmdObservation
            {
                kind = ESCmdAgentForegroundCmdObservationKind.Unavailable,
                summary = summary ?? string.Empty,
                observedAtUtc = DateTime.UtcNow.ToString("O")
            };
        }

        private static ESCmdAgentForegroundCmdObservation CreateFailure(string summary)
        {
            return new ESCmdAgentForegroundCmdObservation
            {
                kind = ESCmdAgentForegroundCmdObservationKind.ObservationFailed,
                summary = summary ?? string.Empty,
                observedAtUtc = DateTime.UtcNow.ToString("O")
            };
        }

        private static Dictionary<int, ProcessSnapshotEntry> CaptureProcesses()
        {
            IntPtr snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
            if (snapshot == InvalidHandleValue)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "无法枚举 Windows 进程。");
            try
            {
                var result = new Dictionary<int, ProcessSnapshotEntry>();
                ProcessEntry32 entry = new ProcessEntry32 { dwSize = (uint)Marshal.SizeOf(typeof(ProcessEntry32)) };
                if (!Process32First(snapshot, ref entry))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "无法读取 Windows 进程快照。");
                do
                {
                    int processId = unchecked((int)entry.th32ProcessID);
                    if (processId > 0)
                    {
                        result[processId] = new ProcessSnapshotEntry
                        {
                            parentProcessId = unchecked((int)entry.th32ParentProcessID),
                            executableName = entry.szExeFile ?? string.Empty
                        };
                    }
                    entry.dwSize = (uint)Marshal.SizeOf(typeof(ProcessEntry32));
                } while (Process32Next(snapshot, ref entry));
                return result;
            }
            finally
            {
                CloseHandle(snapshot);
            }
        }

        private static int FindCmdAncestor(int processId, IReadOnlyDictionary<int, ProcessSnapshotEntry> processes)
        {
            var visited = new HashSet<int>();
            int currentProcessId = processId;
            while (currentProcessId > 0 && visited.Add(currentProcessId))
            {
                ProcessSnapshotEntry entry;
                if (!processes.TryGetValue(currentProcessId, out entry))
                    return 0;
                if (IsCmd(NormalizeProcessName(entry.executableName)))
                    return currentProcessId;
                currentProcessId = entry.parentProcessId;
            }
            return 0;
        }

        private static List<int> FindCmdDescendants(int hostProcessId,
            IReadOnlyDictionary<int, ProcessSnapshotEntry> processes)
        {
            var result = new List<int>();
            foreach (KeyValuePair<int, ProcessSnapshotEntry> pair in processes)
            {
                if (pair.Key != hostProcessId && IsCmd(NormalizeProcessName(pair.Value.executableName))
                    && IsDescendantOf(pair.Key, hostProcessId, processes))
                    result.Add(pair.Key);
            }
            result.Sort();
            return result;
        }

        private static bool IsDescendantOf(int processId, int ancestorProcessId,
            IReadOnlyDictionary<int, ProcessSnapshotEntry> processes)
        {
            var visited = new HashSet<int>();
            int currentProcessId = processId;
            while (currentProcessId > 0 && visited.Add(currentProcessId))
            {
                ProcessSnapshotEntry entry;
                if (!processes.TryGetValue(currentProcessId, out entry))
                    return false;
                currentProcessId = entry.parentProcessId;
                if (currentProcessId == ancestorProcessId)
                    return true;
            }
            return false;
        }

        private static bool IsCmd(string processName)
        {
            return string.Equals(processName, "cmd", StringComparison.OrdinalIgnoreCase)
                || string.Equals(processName, "cmd.exe", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWindowsTerminal(string processName)
        {
            return string.Equals(processName, "windowsterminal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(processName, "windowsterminal.exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(processName, "wt", StringComparison.OrdinalIgnoreCase)
                || string.Equals(processName, "wt.exe", StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadProcessName(int processId)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                    return process.ProcessName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool TryReadCmdProcessStartedAtUtc(int processId, out string startedAtUtc)
        {
            startedAtUtc = string.Empty;
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    if (process.HasExited || !IsCmd(process.ProcessName))
                        return false;
                    startedAtUtc = process.StartTime.ToUniversalTime().ToString("O");
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string ReadWindowTitle(IntPtr windowHandle)
        {
            var title = new StringBuilder(512);
            GetWindowText(windowHandle, title, title.Capacity);
            return title.ToString().Trim();
        }

        private static string NormalizeProcessName(string processName)
        {
            return (processName ?? string.Empty).Trim();
        }

        private static int GetCurrentProcessId()
        {
            using (Process currentProcess = Process.GetCurrentProcess())
                return currentProcess.Id;
        }

        private static bool IsWindows()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT;
        }

        private sealed class ProcessSnapshotEntry
        {
            public int parentProcessId;
            public string executableName = string.Empty;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void WinEventDelegate(IntPtr hook, uint eventType, IntPtr windowHandle,
            int objectId, int childId, uint eventThread, uint eventTime);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct ProcessEntry32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr windowHandle, StringBuilder text, int maxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr moduleHandle,
            WinEventDelegate callback, uint processId, uint threadId, uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWinEvent(IntPtr hook);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
