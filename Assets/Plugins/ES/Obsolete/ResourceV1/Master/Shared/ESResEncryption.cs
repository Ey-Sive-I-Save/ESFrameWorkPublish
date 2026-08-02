using System;
using System.IO;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 资源加密/解密接口
    /// 
    /// 用途：
    /// 1. AB包文件加密存储（防止资源被直接提取）
    /// 2. 配置文件加密保护
    /// 3. 热更新资源完整性校验
    /// 
    /// 使用方式：
    /// - 实现IESResEncryptor接口
    /// - 在ESResMaster.Settings中设置加密器实例
    /// - 系统自动在加载/保存时调用加密/解密方法
    /// </summary>
    public interface IESResEncryptor
    {
        /// <summary>
        /// 加密数据
        /// </summary>
        /// <param name="rawData">原始数据</param>
        /// <param name="key">加密密钥（可选）</param>
        /// <returns>加密后的数据</returns>
        byte[] Encrypt(byte[] rawData, string key = null);

        /// <summary>
        /// 解密数据
        /// </summary>
        /// <param name="encryptedData">加密的数据</param>
        /// <param name="key">解密密钥（可选）</param>
        /// <returns>解密后的数据</returns>
        byte[] Decrypt(byte[] encryptedData, string key = null);

        /// <summary>
        /// 验证数据完整性
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="expectedHash">预期的哈希值</param>
        /// <returns>是否完整</returns>
        bool VerifyIntegrity(byte[] data, string expectedHash);

        /// <summary>
        /// 计算数据哈希
        /// </summary>
        byte[] ComputeHash(byte[] data);
    }

    /// <summary>
    /// XOR加密器（示例实现，简单快速）
    /// </summary>
    public class ESXOREncryptor : IESResEncryptor
    {
        private readonly byte[] _xorKey;

        public ESXOREncryptor(string key = "ESFramework2026")
        {
            _xorKey = System.Text.Encoding.UTF8.GetBytes(key);
        }

        public byte[] Encrypt(byte[] rawData, string key = null)
        {
            if (rawData == null || rawData.Length == 0)
                return rawData;

            byte[] keyBytes = string.IsNullOrEmpty(key) ? _xorKey : System.Text.Encoding.UTF8.GetBytes(key);
            byte[] result = new byte[rawData.Length];

            for (int i = 0; i < rawData.Length; i++)
            {
                result[i] = (byte)(rawData[i] ^ keyBytes[i % keyBytes.Length]);
            }

            return result;
        }

        public byte[] Decrypt(byte[] encryptedData, string key = null)
        {
            // XOR加密是对称的，解密和加密相同
            return Encrypt(encryptedData, key);
        }

        public bool VerifyIntegrity(byte[] data, string expectedHash)
        {
            var hash = ComputeHash(data);
            var hashString = BitConverter.ToString(hash).Replace("-", "").ToLower();
            return hashString.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        public byte[] ComputeHash(byte[] data)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                return md5.ComputeHash(data);
            }
        }
    }

    /// <summary>
    /// AES加密器（高安全性）
    /// </summary>
    public class ESAESEncryptor : IESResEncryptor
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;

        public ESAESEncryptor(string key = "ESFramework2026!", string iv = "ESInit1234567890")
        {
            _key = System.Text.Encoding.UTF8.GetBytes(key.PadRight(16).Substring(0, 16));
            _iv = System.Text.Encoding.UTF8.GetBytes(iv.PadRight(16).Substring(0, 16));
        }

        public byte[] Encrypt(byte[] rawData, string key = null)
        {
            if (rawData == null || rawData.Length == 0)
                return rawData;

            using (var aes = System.Security.Cryptography.Aes.Create())
            {
                aes.Key = string.IsNullOrEmpty(key) ? _key : System.Text.Encoding.UTF8.GetBytes(key.PadRight(16).Substring(0, 16));
                aes.IV = _iv;
                aes.Mode = System.Security.Cryptography.CipherMode.CBC;
                aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    using (var cs = new System.Security.Cryptography.CryptoStream(ms, encryptor, System.Security.Cryptography.CryptoStreamMode.Write))
                    {
                        cs.Write(rawData, 0, rawData.Length);
                        cs.FlushFinalBlock();
                    }
                    return ms.ToArray();
                }
            }
        }

        public byte[] Decrypt(byte[] encryptedData, string key = null)
        {
            if (encryptedData == null || encryptedData.Length == 0)
                return encryptedData;

            using (var aes = System.Security.Cryptography.Aes.Create())
            {
                aes.Key = string.IsNullOrEmpty(key) ? _key : System.Text.Encoding.UTF8.GetBytes(key.PadRight(16).Substring(0, 16));
                aes.IV = _iv;
                aes.Mode = System.Security.Cryptography.CipherMode.CBC;
                aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(encryptedData))
                using (var cs = new System.Security.Cryptography.CryptoStream(ms, decryptor, System.Security.Cryptography.CryptoStreamMode.Read))
                using (var resultStream = new MemoryStream())
                {
                    cs.CopyTo(resultStream);
                    return resultStream.ToArray();
                }
            }
        }

        public bool VerifyIntegrity(byte[] data, string expectedHash)
        {
            var hash = ComputeHash(data);
            var hashString = BitConverter.ToString(hash).Replace("-", "").ToLower();
            return hashString.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        public byte[] ComputeHash(byte[] data)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                return sha256.ComputeHash(data);
            }
        }
    }

    /// <summary>
    /// 空加密器（不加密，用于测试）
    /// </summary>
    public class ESNoEncryptor : IESResEncryptor
    {
        public byte[] Encrypt(byte[] rawData, string key = null)
        {
            return rawData;
        }

        public byte[] Decrypt(byte[] encryptedData, string key = null)
        {
            return encryptedData;
        }

        public bool VerifyIntegrity(byte[] data, string expectedHash)
        {
            return true; // 不验证
        }

        public byte[] ComputeHash(byte[] data)
        {
            return new byte[0];
        }
    }

    /// <summary>
    /// 资源加密工具类
    /// </summary>
    public static class ESResEncryptionHelper
    {
        private static IESResEncryptor _currentEncryptor;

        /// <summary>
        /// 设置全局加密器
        /// </summary>
        public static void SetEncryptor(IESResEncryptor encryptor)
        {
            _currentEncryptor = encryptor;
            Debug.Log($"[ESResEncryptionHelper] 已设置加密器: {encryptor?.GetType().Name ?? "null"}");
        }

        /// <summary>
        /// 获取当前加密器
        /// </summary>
        public static IESResEncryptor GetEncryptor()
        {
            if (_currentEncryptor == null)
            {
                _currentEncryptor = new ESNoEncryptor(); // 默认不加密
            }
            return _currentEncryptor;
        }

        /// <summary>
        /// 加密文件
        /// </summary>
        public static void EncryptFile(string inputPath, string outputPath, string key = null)
        {
            if (!File.Exists(inputPath))
            {
                Debug.LogError($"[ESResEncryptionHelper] 源文件不存在: {inputPath}");
                return;
            }

            byte[] rawData = File.ReadAllBytes(inputPath);
            byte[] encryptedData = GetEncryptor().Encrypt(rawData, key);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllBytes(outputPath, encryptedData);

            Debug.Log($"[ESResEncryptionHelper] 文件加密完成: {inputPath} → {outputPath} ({rawData.Length} → {encryptedData.Length} bytes)");
        }

        /// <summary>
        /// 解密文件
        /// </summary>
        public static byte[] DecryptFile(string inputPath, string key = null)
        {
            if (!File.Exists(inputPath))
            {
                Debug.LogError($"[ESResEncryptionHelper] 加密文件不存在: {inputPath}");
                return null;
            }

            byte[] encryptedData = File.ReadAllBytes(inputPath);
            byte[] decryptedData = GetEncryptor().Decrypt(encryptedData, key);

            Debug.Log($"[ESResEncryptionHelper] 文件解密完成: {inputPath} ({encryptedData.Length} → {decryptedData.Length} bytes)");
            return decryptedData;
        }

        /// <summary>
        /// 验证文件完整性
        /// </summary>
        public static bool VerifyFileIntegrity(string filePath, string expectedHash)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[ESResEncryptionHelper] 文件不存在: {filePath}");
                return false;
            }

            byte[] data = File.ReadAllBytes(filePath);
            bool isValid = GetEncryptor().VerifyIntegrity(data, expectedHash);

            if (isValid)
            {
                Debug.Log($"[ESResEncryptionHelper] 文件完整性验证通过: {filePath}");
            }
            else
            {
                Debug.LogError($"[ESResEncryptionHelper] 文件完整性验证失败: {filePath}");
            }

            return isValid;
        }
    }
}
