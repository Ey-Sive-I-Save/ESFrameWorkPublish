using System;
using System.IO;

namespace ES
{
    /// <summary>
    /// 渲染 Evidence Receipt 的输出路径策略。只校验路径，不创建目录或写入文件。
    /// </summary>
    public static class ESRenderEvidencePathPolicy
    {
        public static bool TryValidate(
            string projectRoot,
            string candidatePath,
            out string normalizedPath,
            out string reason)
        {
            normalizedPath = string.Empty;
            if (string.IsNullOrWhiteSpace(projectRoot) || string.IsNullOrWhiteSpace(candidatePath))
            {
                reason = "project-root-and-candidate-path-required";
                return false;
            }

            try
            {
                string root = EnsureTrailingSeparator(Path.GetFullPath(projectRoot));
                string evidenceRoot = EnsureTrailingSeparator(
                    Path.Combine(root, "ES", "Output", "RenderingEvidence"));
                string fullCandidate = Path.GetFullPath(candidatePath);
                if (!fullCandidate.StartsWith(evidenceRoot, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "evidence-path-outside-project-allowlist";
                    return false;
                }
                if (!string.Equals(Path.GetExtension(fullCandidate), ".json", StringComparison.OrdinalIgnoreCase))
                {
                    reason = "evidence-path-must-be-json";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(fullCandidate)))
                {
                    reason = "evidence-file-name-required";
                    return false;
                }

                normalizedPath = fullCandidate;
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                reason = "evidence-path-invalid-" + exception.GetType().Name;
                return false;
            }
        }

        private static string EnsureTrailingSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }
    }
}
