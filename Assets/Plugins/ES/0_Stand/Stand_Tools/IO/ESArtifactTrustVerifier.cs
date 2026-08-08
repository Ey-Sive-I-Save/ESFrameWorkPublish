using System;
using System.IO;
using System.Security.Cryptography;

namespace ES
{
    /// <summary>
    /// Detached artifact trust primitives. The caller owns the trust-root registry and
    /// must never load a public key from beside the artifact being verified.
    /// </summary>
    public static class ESArtifactTrustVerifier
    {
        public static bool TryVerifyRsaSha256(
            string publicKeyXml,
            byte[] canonicalPayload,
            string base64Signature,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(publicKeyXml)) { error = "可信公钥为空。"; return false; }
            if (canonicalPayload == null || canonicalPayload.Length == 0) { error = "签名载荷为空。"; return false; }
            if (string.IsNullOrWhiteSpace(base64Signature)) { error = "签名为空。"; return false; }

            byte[] signature;
            try { signature = Convert.FromBase64String(base64Signature); }
            catch (FormatException) { error = "签名不是有效的 Base64。"; return false; }

            try
            {
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(publicKeyXml);
                    if (!rsa.VerifyData(canonicalPayload, CryptoConfig.MapNameToOID("SHA256"), signature))
                    {
                        error = "签名校验失败。";
                        return false;
                    }
                }
                return true;
            }
            catch (CryptographicException exception) { error = "可信公钥或签名格式无效：" + exception.Message; return false; }
            catch (ArgumentException exception) { error = "可信公钥格式无效：" + exception.Message; return false; }
        }

        public static bool TryCaptureStableFileIdentity(
            string path,
            out long size,
            out string sha256,
            out string error)
        {
            size = 0;
            sha256 = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { error = "待校验文件不存在。"; return false; }

            try
            {
                FileInfo before = new FileInfo(path);
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (SHA256 hash = SHA256.Create())
                {
                    byte[] digest = hash.ComputeHash(stream);
                    size = stream.Length;
                    sha256 = BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
                }
                FileInfo after = new FileInfo(path);
                if (!after.Exists || before.Length != after.Length || before.LastWriteTimeUtc != after.LastWriteTimeUtc || size != after.Length)
                {
                    size = 0;
                    sha256 = string.Empty;
                    error = "文件在读取期间发生变化。";
                    return false;
                }
                return true;
            }
            catch (IOException exception) { error = "读取文件身份失败：" + exception.Message; return false; }
            catch (UnauthorizedAccessException exception) { error = "读取文件权限不足：" + exception.Message; return false; }
        }

        public static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
            }
            return true;
        }
    }
}
