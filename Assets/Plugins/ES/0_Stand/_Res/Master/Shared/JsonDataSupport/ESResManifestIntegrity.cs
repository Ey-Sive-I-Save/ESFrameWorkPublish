using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ES
{
    /// <summary>发布清单和 AB 文件的内容完整性工具。</summary>
    public static class ESResManifestIntegrity
    {
        public static string ComputeFileSha256(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return string.Empty;
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
                return ToHex(sha256.ComputeHash(stream));
        }

        public static string ComputeFileSha256FromText(string text)
        {
            using (var sha256 = SHA256.Create())
                return ToHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty)));
        }

        public static bool VerifyFileSha256(string filePath, string expectedSha256)
        {
            return File.Exists(filePath) && (string.IsNullOrEmpty(expectedSha256) ||
                string.Equals(ComputeFileSha256(filePath), expectedSha256, StringComparison.OrdinalIgnoreCase));
        }

        private static string ToHex(byte[] hash)
        {
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
