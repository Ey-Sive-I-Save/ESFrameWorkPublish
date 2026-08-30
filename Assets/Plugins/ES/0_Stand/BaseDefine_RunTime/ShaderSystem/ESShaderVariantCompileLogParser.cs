using System;

namespace ES
{
    /// <summary>
    /// 对外部 Shader 编译日志进行有界、只读解析。解析结果不等同于 Unity 构建或运行时验收。
    /// </summary>
    public sealed class ESShaderVariantCompileLogSummary
    {
        public int LineCount { get; internal set; }
        public int VariantRecordCount { get; internal set; }
        public int KeywordRecordCount { get; internal set; }
        public int ErrorCount { get; internal set; }
        public int WarningCount { get; internal set; }
        public int UnparsedLineCount { get; internal set; }
        public bool RuntimeAcceptance { get; internal set; }
    }

    public static class ESShaderVariantCompileLogParser
    {
        public const int MaxCharacters = 1_000_000;
        public const int MaxLines = 20_000;

        public static bool TryParse(string logText, out ESShaderVariantCompileLogSummary summary, out string reason)
        {
            summary = null;
            reason = string.Empty;
            if (string.IsNullOrEmpty(logText)) { reason = "empty-log"; return false; }
            if (logText.Length > MaxCharacters) { reason = "log-too-large"; return false; }

            var result = new ESShaderVariantCompileLogSummary();
            string[] lines = logText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (lines.Length > MaxLines) { reason = "line-limit-exceeded"; return false; }
            foreach (string raw in lines)
            {
                string line = (raw ?? string.Empty).Trim();
                if (line.Length == 0) continue;
                result.LineCount++;
                string lower = line.ToLowerInvariant();
                if (lower.Contains("error") || lower.Contains("failed") || lower.Contains("failure")) { result.ErrorCount++; continue; }
                if (lower.Contains("warning")) { result.WarningCount++; continue; }
                if (lower.Contains("variant") || lower.Contains("shader variant")) { result.VariantRecordCount++; continue; }
                if (lower.Contains("keyword") || lower.Contains("multi_compile") || lower.Contains("shader_feature")) { result.KeywordRecordCount++; continue; }
                result.UnparsedLineCount++;
            }
            result.RuntimeAcceptance = false;
            summary = result;
            reason = "parsed-static-log";
            return true;
        }
    }
}
