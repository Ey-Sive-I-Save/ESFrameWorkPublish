using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace ES
{
    /// <summary>
    /// Reads only user-visible Codex transcript events for one exact, already registered session.
    /// It intentionally ignores reasoning records and never attempts to infer activity from a PID.
    /// </summary>
    internal static class ESCmdAgentCodexVisibleEventTail
    {
        private const int InitialTailBytes = 96 * 1024;
        private const int MaximumBytesPerDrain = 128 * 1024;
        private const int MaximumEventsPerDrain = 32;
        private const int MaximumRememberedEventIds = 160;
        private static readonly TimeSpan MissingTranscriptRetryDelay = TimeSpan.FromSeconds(2);
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal readonly struct VisibleEvent
        {
            public string id { get; }
            public string stage { get; }
            public string detail { get; }
            public string timestampUtc { get; }
            public string assistantMessage { get; }

            public VisibleEvent(string id, string stage, string detail, string timestampUtc,
                string assistantMessage)
            {
                this.id = id ?? string.Empty;
                this.stage = stage ?? string.Empty;
                this.detail = detail ?? string.Empty;
                this.timestampUtc = timestampUtc ?? string.Empty;
                this.assistantMessage = assistantMessage ?? string.Empty;
            }

            public bool HasAssistantMessage => !string.IsNullOrWhiteSpace(assistantMessage);
        }

        internal static int Drain(ESCmdAgentSession session, List<VisibleEvent> destination,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (session == null || destination == null || !Guid.TryParse(session.sessionId, out _))
                return 0;

            if (!TryResolveExactTranscript(session, out string transcriptPath, out diagnostic))
                return 0;

            try
            {
                var file = new FileInfo(transcriptPath);
                long length = file.Length;
                long offset = session.visibleTranscriptOffset;
                bool skipPartialFirstLine = false;
                if (offset < 0 || offset > length)
                {
                    offset = Math.Max(0, length - InitialTailBytes);
                    skipPartialFirstLine = offset > 0;
                }
                else if (offset == 0 && length > InitialTailBytes)
                {
                    offset = length - InitialTailBytes;
                    skipPartialFirstLine = true;
                }

                long remaining = length - offset;
                if (remaining <= 0)
                {
                    session.visibleTranscriptPath = transcriptPath;
                    return 0;
                }

                int requested = (int)Math.Min(remaining, MaximumBytesPerDrain);
                byte[] buffer = new byte[requested];
                int read;
                using (var stream = new FileStream(transcriptPath, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite | FileShare.Delete))
                {
                    stream.Seek(offset, SeekOrigin.Begin);
                    read = stream.Read(buffer, 0, requested);
                }
                if (read <= 0)
                    return 0;

                int completeLength = LastCompleteLineLength(buffer, read);
                if (completeLength <= 0)
                    return 0;

                string text = StrictUtf8.GetString(buffer, 0, completeLength);
                session.visibleTranscriptPath = transcriptPath;
                session.visibleTranscriptOffset = offset + completeLength;
                int initialCount = destination.Count;
                var recent = new Queue<VisibleEvent>();
                string[] lines = text.Split('\n');
                for (int index = skipPartialFirstLine ? 1 : 0; index < lines.Length; index++)
                {
                    string line = lines[index].TrimEnd('\r');
                    if (!TryReadVisibleEvent(line, out VisibleEvent visibleEvent)
                        || !RememberEvent(session, visibleEvent.id))
                        continue;
                    if (recent.Count >= MaximumEventsPerDrain)
                        recent.Dequeue();
                    recent.Enqueue(visibleEvent);
                }
                while (recent.Count > 0)
                    destination.Add(recent.Dequeue());
                return destination.Count - initialCount;
            }
            catch (Exception exception)
            {
                diagnostic = "读取当前 Codex 可见事件失败：" + exception.GetBaseException().Message;
                return 0;
            }
        }

        private static bool TryResolveExactTranscript(ESCmdAgentSession session, out string path,
            out string diagnostic)
        {
            path = string.Empty;
            diagnostic = string.Empty;
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".codex", "sessions");
            if (!Directory.Exists(root))
            {
                diagnostic = "本机未发现 Codex transcript 目录。";
                return false;
            }
            if (!IsSafeChildPath(root, root))
            {
                diagnostic = "Codex transcript 根目录经过重解析点，已拒绝读取。";
                return false;
            }

            string expectedSuffix = "-" + session.sessionId.Trim() + ".jsonl";
            if (!string.IsNullOrWhiteSpace(session.visibleTranscriptPath)
                && IsSafeTranscriptPath(root, session.visibleTranscriptPath, expectedSuffix))
            {
                path = Path.GetFullPath(session.visibleTranscriptPath);
                return true;
            }

            if (DateTime.TryParse(session.visibleTranscriptLookupAfterUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out DateTime retryAfterUtc)
                && retryAfterUtc.ToUniversalTime() > DateTime.UtcNow)
            {
                diagnostic = "当前精确 SessionId 的 transcript 尚未出现；将在短暂退避后重试。";
                return false;
            }

            // The known Codex layout is sessions/yyyy/MM/rollout-...-<exact SessionId>.jsonl.
            // Walk that bounded shape once, cache the exact result, and never enumerate another session's content.
            foreach (string yearDirectory in SafeDirectories(root))
            {
                foreach (string monthDirectory in SafeDirectories(yearDirectory))
                {
                    foreach (string candidate in SafeFiles(monthDirectory, "*.jsonl"))
                    {
                        if (!Path.GetFileName(candidate).EndsWith(expectedSuffix,
                                StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!IsSafeTranscriptPath(root, candidate, expectedSuffix))
                            continue;
                        path = Path.GetFullPath(candidate);
                        session.visibleTranscriptLookupAfterUtc = string.Empty;
                        return true;
                    }
                }
            }

            session.visibleTranscriptLookupAfterUtc = DateTime.UtcNow.Add(MissingTranscriptRetryDelay).ToString("O");
            diagnostic = "尚未发现当前精确 SessionId 的 Codex 可见事件文件。";
            return false;
        }

        private static IEnumerable<string> SafeDirectories(string path)
        {
            var result = new List<string>();
            try
            {
                foreach (string candidate in Directory.EnumerateDirectories(path))
                {
                    if (!HasReparsePoint(candidate))
                        result.Add(candidate);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return result;
        }

        private static IEnumerable<string> SafeFiles(string path, string searchPattern)
        {
            var result = new List<string>();
            try
            {
                foreach (string candidate in Directory.EnumerateFiles(path, searchPattern,
                             SearchOption.TopDirectoryOnly))
                {
                    if (!HasReparsePoint(candidate))
                        result.Add(candidate);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return result;
        }

        private static bool IsSafeTranscriptPath(string root, string candidate, string expectedSuffix)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate)
                || !Path.GetFileName(candidate).EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase))
                return false;
            return IsSafeChildPath(root, candidate) && !HasReparsePoint(candidate);
        }

        private static bool IsSafeChildPath(string root, string candidate)
        {
            try
            {
                string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                string normalizedCandidate = Path.GetFullPath(candidate);
                if (!string.Equals(normalizedRoot, normalizedCandidate, StringComparison.OrdinalIgnoreCase)
                    && !normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                    return false;
                string current = normalizedRoot;
                if (HasReparsePoint(current))
                    return false;
                string relative = normalizedCandidate.Substring(normalizedRoot.Length).TrimStart(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                foreach (string segment in relative.Split(Path.DirectorySeparatorChar,
                             Path.AltDirectorySeparatorChar))
                {
                    if (string.IsNullOrEmpty(segment))
                        continue;
                    current = Path.Combine(current, segment);
                    if ((Directory.Exists(current) || File.Exists(current)) && HasReparsePoint(current))
                        return false;
                }
                return true;
            }
            catch { return false; }
        }

        private static bool HasReparsePoint(string path)
        {
            try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
            catch { return true; }
        }

        private static int LastCompleteLineLength(byte[] buffer, int length)
        {
            for (int index = Math.Min(length, buffer.Length) - 1; index >= 0; index--)
            {
                if (buffer[index] == (byte)'\n')
                    return index + 1;
            }
            return 0;
        }

        private static bool TryReadVisibleEvent(string line, out VisibleEvent visibleEvent)
        {
            visibleEvent = default;
            if (string.IsNullOrWhiteSpace(line))
                return false;
            try
            {
                JObject record = JObject.Parse(line);
                string recordType = record.Value<string>("type") ?? string.Empty;
                JObject payload = record["payload"] as JObject;
                if (payload == null)
                    return false;
                string payloadType = payload.Value<string>("type") ?? string.Empty;
                string timestamp = record.Value<string>("timestamp") ?? string.Empty;
                string stage = string.Empty;
                string detail = string.Empty;
                string assistantMessage = string.Empty;

                if (recordType == "event_msg")
                {
                    if (payloadType == "agent_message")
                    {
                        assistantMessage = NormalizeText(payload.Value<string>("message"), 12000);
                        stage = "AI 可见进度";
                        detail = FirstLine(assistantMessage, 280);
                    }
                    else if (payloadType == "task_started")
                    {
                        stage = "Codex 任务";
                        detail = "已收到 Codex task_started 事件。";
                    }
                    else if (payloadType == "task_complete")
                    {
                        stage = "Codex 任务";
                        detail = "已收到 Codex task_complete 事件。";
                    }
                }
                else if (recordType == "response_item" && payloadType == "custom_tool_call")
                {
                    string toolName = payload.Value<string>("name") ?? "未命名工具";
                    string state = payload.Value<string>("status") ?? string.Empty;
                    stage = "AI 工具调用";
                    detail = string.Equals(state, "completed", StringComparison.OrdinalIgnoreCase)
                        ? "工具已完成：" + toolName : "正在调用工具：" + toolName;
                }
                else if (recordType == "response_item" && payloadType == "custom_tool_call_output")
                {
                    stage = "AI 工具调用";
                    detail = "工具已返回可见结果。";
                }
                else if (recordType == "response_item" && payloadType == "message"
                    && string.Equals(payload.Value<string>("role"), "assistant", StringComparison.OrdinalIgnoreCase))
                {
                    assistantMessage = ExtractAssistantText(payload["content"] as JArray);
                    if (!string.IsNullOrWhiteSpace(assistantMessage))
                    {
                        stage = "AI 回复";
                        detail = "已生成可见回复。";
                    }
                }

                if (string.IsNullOrWhiteSpace(detail))
                    return false;
                string identity = payload.Value<string>("id")
                    ?? payload.Value<string>("call_id")
                    ?? payload.Value<string>("turn_id")
                    ?? detail;
                visibleEvent = new VisibleEvent(BuildStableId(timestamp, recordType, payloadType, identity),
                    stage, detail, timestamp, assistantMessage);
                return true;
            }
            catch { return false; }
        }

        private static string ExtractAssistantText(JArray content)
        {
            if (content == null)
                return string.Empty;
            var builder = new StringBuilder();
            foreach (JObject item in content.OfType<JObject>())
            {
                if (!string.Equals(item.Value<string>("type"), "output_text",
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                string text = item.Value<string>("text");
                if (!string.IsNullOrWhiteSpace(text))
                    builder.AppendLine(text.Trim());
            }
            return NormalizeText(builder.ToString(), 12000);
        }

        private static string NormalizeText(string value, int maximumLength)
        {
            string normalized = (value ?? string.Empty).Replace("\r\n", "\n").Trim();
            if (normalized.Length <= maximumLength)
                return normalized;
            return normalized.Substring(0, maximumLength) + "\n[工作台已截断；请打开真实 CMD 查看完整内容]";
        }

        private static string FirstLine(string value, int maximumLength)
        {
            string first = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return first.Length <= maximumLength ? first : first.Substring(0, maximumLength) + "…";
        }

        private static string BuildStableId(string timestamp, string recordType, string payloadType,
            string identity)
        {
            string raw = (timestamp ?? string.Empty) + "|" + (recordType ?? string.Empty) + "|"
                + (payloadType ?? string.Empty) + "|" + (identity ?? string.Empty);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                return BitConverter.ToString(hash, 0, 12).Replace("-", string.Empty);
            }
        }

        private static bool RememberEvent(ESCmdAgentSession session, string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;
            session.visibleTranscriptEventIds ??= new List<string>();
            if (session.visibleTranscriptEventIds.Contains(id))
                return false;
            session.visibleTranscriptEventIds.Add(id);
            if (session.visibleTranscriptEventIds.Count > MaximumRememberedEventIds)
                session.visibleTranscriptEventIds.RemoveRange(0,
                    session.visibleTranscriptEventIds.Count - MaximumRememberedEventIds);
            return true;
        }
    }
}
