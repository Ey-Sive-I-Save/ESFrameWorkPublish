using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ES.Tests
{
    /// <summary>
    /// P0 文件/供应链边界的确定性故障注入。
    /// 不模拟真实磁盘耗尽；该场景需要隔离测试卷，避免污染开发机。
    /// </summary>
    public sealed class ESArtifactTrustAndManagedFileIOTests
    {
        private string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "ES-P0-Test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        [TearDown]
        public void TearDown()
        {
            if (string.IsNullOrEmpty(root)) return;
            try { if (Directory.Exists(root)) Directory.Delete(root, true); }
            catch { /* 保留现场不会影响后续测试；失败现场由测试日志提供路径。 */ }
        }

        [Test]
        public void SignedPayload_RejectsTamperingAndMalformedSignature()
        {
            byte[] payload = Encoding.UTF8.GetBytes("es-unitypackage-manifest-v1");
            string signature;
            string publicKey;
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                publicKey = rsa.ToXmlString(false);
                signature = Convert.ToBase64String(rsa.SignData(payload, CryptoConfig.MapNameToOID("SHA256")));
            }

            Assert.That(ESArtifactTrustVerifier.TryVerifyRsaSha256(publicKey, payload, signature, out string validError), Is.True, validError);
            payload[0] ^= 0x01;
            Assert.That(ESArtifactTrustVerifier.TryVerifyRsaSha256(publicKey, payload, signature, out _), Is.False);
            Assert.That(ESArtifactTrustVerifier.TryVerifyRsaSha256(publicKey, payload, "not-base64", out _), Is.False);
        }

        [Test]
        public void AtomicWrite_RejectsPathEscapeAndPreservesCompleteContent()
        {
            string managedRoot = Path.Combine(root, "managed");
            Directory.CreateDirectory(managedRoot);
            string destination = Path.Combine(managedRoot, "report.json");
            string escaped = Path.Combine(managedRoot, "..", "outside.json");

            Assert.Throws<UnauthorizedAccessException>(() => ESManagedFileIO.WriteTextAtomic(escaped, "escape", Encoding.UTF8, managedRoot));
            ESManagedFileIO.WriteTextAtomic(destination, "{\"status\":\"complete\"}", Encoding.UTF8, managedRoot);
            Assert.That(File.ReadAllText(destination, Encoding.UTF8), Is.EqualTo("{\"status\":\"complete\"}"));
            Assert.That(Directory.GetFiles(managedRoot, "*.tmp-*", SearchOption.TopDirectoryOnly), Is.Empty);
        }

        [Test]
        public void CreateNew_ConcurrentWritersHaveSingleWinner()
        {
            string managedRoot = Path.Combine(root, "managed");
            Directory.CreateDirectory(managedRoot);
            string destination = Path.Combine(managedRoot, "single.json");
            var tasks = Enumerable.Range(0, 8)
                .Select(index => Task.Run(() =>
                {
                    try
                    {
                        ESManagedFileIO.WriteTextAtomicCreateNew(destination, "writer-" + index, Encoding.UTF8, managedRoot);
                        return true;
                    }
                    catch (IOException) { return false; }
                    catch (UnauthorizedAccessException) { return false; }
                }))
                .ToArray();

            Task.WaitAll(tasks);
            Assert.That(tasks.Count(task => task.Result), Is.EqualTo(1));
            Assert.That(File.ReadAllText(destination, Encoding.UTF8), Does.StartWith("writer-"));
            Assert.That(Directory.GetFiles(managedRoot, "*.tmp-*", SearchOption.TopDirectoryOnly), Is.Empty);
        }

        [Test]
        public void StableIdentity_CapturesSizeAndSha256AndRejectsMissingFile()
        {
            string path = Path.Combine(root, "artifact.bin");
            byte[] bytes = Encoding.UTF8.GetBytes("stable-artifact");
            File.WriteAllBytes(path, bytes);

            Assert.That(ESArtifactTrustVerifier.TryCaptureStableFileIdentity(path, out long size, out string sha256, out string error), Is.True, error);
            Assert.That(size, Is.EqualTo(bytes.LongLength));
            Assert.That(ESArtifactTrustVerifier.IsSha256(sha256), Is.True);

            File.Delete(path);
            Assert.That(ESArtifactTrustVerifier.TryCaptureStableFileIdentity(path, out _, out _, out _), Is.False);
        }
    }
}
