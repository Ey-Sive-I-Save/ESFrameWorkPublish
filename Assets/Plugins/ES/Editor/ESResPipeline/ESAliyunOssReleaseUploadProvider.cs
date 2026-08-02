using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine.Networking;

namespace ES
{
    internal sealed class ESAliyunOssCredentials
    {
        public string AccessKeyId;
        public string AccessKeySecret;
        public string SecurityToken;
    }

    internal static class ESAliyunOssCredentialSource
    {
        public static bool TryGet(string profile, out ESAliyunOssCredentials credentials, out string reason)
        {
            credentials = null;
            string suffix = string.IsNullOrWhiteSpace(profile)
                ? string.Empty
                : "_" + new string(profile.Trim().ToUpperInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
            string id = Environment.GetEnvironmentVariable("ES_OSS" + suffix + "_ACCESS_KEY_ID");
            string secret = Environment.GetEnvironmentVariable("ES_OSS" + suffix + "_ACCESS_KEY_SECRET");
            string token = Environment.GetEnvironmentVariable("ES_OSS" + suffix + "_SECURITY_TOKEN");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret))
            {
                reason = "未找到 OSS 凭据。请通过环境变量 ES_OSS" + suffix + "_ACCESS_KEY_ID / _ACCESS_KEY_SECRET 提供；Secret 不得写入资产。";
                return false;
            }
            credentials = new ESAliyunOssCredentials { AccessKeyId = id.Trim(), AccessKeySecret = secret.Trim(), SecurityToken = token?.Trim() };
            reason = string.Empty;
            return true;
        }
    }

    /// <summary>阿里云 OSS 原生 Provider：PUT、HEAD、SHA-256 元数据校验与隔离探针清理。</summary>
    public sealed class ESAliyunOssReleaseUploadProvider : IESAssetReleaseUploadProvider
    {
        public ESAssetReleaseUploadMode Mode => ESAssetReleaseUploadMode.AliyunOss;

        public bool CanHandle(ESAssetReleaseUploadTarget target, out string reason)
        {
            if (target == null || target.mode != Mode)
            {
                reason = "目标不是阿里云 OSS。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(target.endpoint) || string.IsNullOrWhiteSpace(target.bucket))
            {
                reason = "OSS Endpoint 或 Bucket 未配置。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(target.region))
            {
                reason = "OSS 地域未配置；请填写例如 cn-hangzhou。";
                return false;
            }
            if (!Uri.TryCreate(target.endpoint, UriKind.Absolute, out Uri endpointUri)
                || !string.Equals(endpointUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                reason = "OSS Endpoint 必须是 HTTPS 地址，禁止通过明文传输凭据。";
                return false;
            }
            if (!string.Equals(endpointUri.AbsolutePath, "/", StringComparison.Ordinal)
                || !string.IsNullOrEmpty(endpointUri.Query)
                || !string.IsNullOrEmpty(endpointUri.Fragment))
            {
                reason = "OSS Endpoint 必须是服务地域根地址（例如 https://oss-cn-hangzhou.aliyuncs.com），不得包含 Bucket、路径、查询参数或片段。";
                return false;
            }
            if (target.bucket.IndexOf('/') >= 0 || target.bucket.IndexOf('\\') >= 0 || target.bucket.IndexOf("..", StringComparison.Ordinal) >= 0
                || !IsSafePrefix(target.objectPrefix) || !IsSafePrefix(target.validationPrefix))
            {
                reason = "Bucket 不得包含路径；对象前缀不得包含 .. 或反斜杠。";
                return false;
            }
            if (!target.verifyRemoteAfterUpload)
            {
                reason = "阿里云 OSS 正式发布必须开启上传后的 HEAD 长度与 SHA-256 元数据校验。";
                return false;
            }
            if (!ESAliyunOssCredentialSource.TryGet(target.credentialProfile, out _, out reason))
                return false;
            return true;
        }

        public IESAssetReleaseUploadOperation BeginUpload(ESAssetReleaseUploadFileRequest request)
        {
            if (!ESAliyunOssCredentialSource.TryGet(request.Target.credentialProfile, out ESAliyunOssCredentials credentials, out string reason))
                return new ESCompletedReleaseUploadOperation(false, reason);

            var steps = new List<Func<UnityWebRequest>>
            {
                () => CreatePut(request.Target, request.RemoteObjectKey, request.File.sourcePath, request.CacheControl, request.File.sha256, credentials)
            };
            var validators = new List<Func<UnityWebRequest, string>>
            {
                ValidateSuccess
            };
            if (request.Target.verifyRemoteAfterUpload)
            {
                steps.Add(() => CreateHead(request.Target, request.RemoteObjectKey, credentials));
                validators.Add(response => ValidateHead(response, request.File.size, request.File.sha256, request.CacheControl));
            }
            return new ESAliyunOssSequenceOperation(steps, validators);
        }

        public IESAssetReleaseUploadOperation BeginValidation(ESAssetReleaseUploadTarget target)
        {
            if (!ESAliyunOssCredentialSource.TryGet(target.credentialProfile, out ESAliyunOssCredentials credentials, out string reason))
                return new ESCompletedReleaseUploadOperation(false, reason);
            string prefix = NormalizeKey(target.validationPrefix);
            if (string.IsNullOrEmpty(prefix)) prefix = ".es-validation";
            string key = prefix + "/probe-" + Guid.NewGuid().ToString("N") + ".txt";
            byte[] probe = Encoding.UTF8.GetBytes("ES OSS validation " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            string sha256 = ComputeSha256(probe);
            var steps = new List<Func<UnityWebRequest>>
            {
                () => CreatePut(target, key, probe, "no-cache, max-age=0", sha256, credentials),
                () => CreateHead(target, key, credentials),
                () => CreateDelete(target, key, credentials)
            };
            var validators = new List<Func<UnityWebRequest, string>>
            {
                ValidateSuccess,
                response => ValidateHead(response, probe.LongLength, sha256, "no-cache, max-age=0"),
                ValidateSuccess
            };
            return new ESAliyunOssSequenceOperation(steps, validators);
        }

        private static UnityWebRequest CreatePut(ESAssetReleaseUploadTarget target, string key, string sourcePath, string cacheControl, string sha256, ESAliyunOssCredentials credentials)
        {
            var request = new UnityWebRequest(BuildUrl(target, key), UnityWebRequest.kHttpVerbPUT)
            {
                uploadHandler = new UploadHandlerFile(sourcePath),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 120
            };
            ConfigureHeaders(request, "PUT", target, key, cacheControl, sha256, credentials);
            return request;
        }

        private static UnityWebRequest CreatePut(ESAssetReleaseUploadTarget target, string key, byte[] content, string cacheControl, string sha256, ESAliyunOssCredentials credentials)
        {
            var request = new UnityWebRequest(BuildUrl(target, key), UnityWebRequest.kHttpVerbPUT)
            {
                uploadHandler = new UploadHandlerRaw(content),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 30
            };
            ConfigureHeaders(request, "PUT", target, key, cacheControl, sha256, credentials);
            return request;
        }

        private static UnityWebRequest CreateHead(ESAssetReleaseUploadTarget target, string key, ESAliyunOssCredentials credentials)
        {
            var request = UnityWebRequest.Head(BuildUrl(target, key));
            request.timeout = 30;
            ConfigureHeaders(request, "HEAD", target, key, string.Empty, string.Empty, credentials);
            return request;
        }

        private static UnityWebRequest CreateDelete(ESAssetReleaseUploadTarget target, string key, ESAliyunOssCredentials credentials)
        {
            var request = UnityWebRequest.Delete(BuildUrl(target, key));
            request.timeout = 30;
            ConfigureHeaders(request, "DELETE", target, key, string.Empty, string.Empty, credentials);
            return request;
        }

        private static void ConfigureHeaders(UnityWebRequest request, string method, ESAssetReleaseUploadTarget target, string key, string cacheControl, string sha256, ESAliyunOssCredentials credentials)
        {
            string date = DateTime.UtcNow.ToString("R", CultureInfo.InvariantCulture);
            request.SetRequestHeader("Date", date);
            if (!string.IsNullOrEmpty(cacheControl)) request.SetRequestHeader("Cache-Control", cacheControl);
            if (!string.IsNullOrEmpty(sha256)) request.SetRequestHeader("x-oss-meta-es-sha256", sha256);
            if (!string.IsNullOrEmpty(credentials.SecurityToken)) request.SetRequestHeader("x-oss-security-token", credentials.SecurityToken);
            string canonicalHeaders = BuildCanonicalHeaders(cacheControl, sha256, credentials.SecurityToken);
            string contentType = method == "PUT" ? "application/octet-stream" : string.Empty;
            if (method == "PUT") request.SetRequestHeader("Content-Type", contentType);
            string canonical = method + "\n\n" + contentType + "\n" + date + "\n" + canonicalHeaders + "/" + target.bucket.Trim('/') + "/" + NormalizeKey(key);
            string signature = ComputeSignature(credentials.AccessKeySecret, canonical);
            request.SetRequestHeader("Authorization", "OSS " + credentials.AccessKeyId + ":" + signature);
        }

        private static string BuildCanonicalHeaders(string cacheControl, string sha256, string securityToken)
        {
            var headers = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(sha256)) headers["x-oss-meta-es-sha256"] = sha256;
            if (!string.IsNullOrEmpty(securityToken)) headers["x-oss-security-token"] = securityToken;
            var builder = new StringBuilder();
            foreach (KeyValuePair<string, string> pair in headers)
                builder.Append(pair.Key).Append(':').Append(pair.Value).Append('\n');
            return builder.ToString();
        }

        private static string ValidateSuccess(UnityWebRequest response)
        {
            return response.result == UnityWebRequest.Result.Success
                ? string.Empty
                : "HTTP " + response.responseCode + "：" + (response.error ?? "OSS 请求失败");
        }

        private static string ValidateHead(UnityWebRequest response, long expectedSize, string expectedSha256, string expectedCacheControl)
        {
            string success = ValidateSuccess(response);
            if (!string.IsNullOrEmpty(success)) return success;
            if (!long.TryParse(response.GetResponseHeader("Content-Length"), out long size) || size != expectedSize)
                return "OSS HEAD Content-Length 不匹配。";
            string remoteSha256 = response.GetResponseHeader("x-oss-meta-es-sha256");
            if (!string.Equals(remoteSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                return "OSS HEAD x-oss-meta-es-sha256 不匹配。";
            string remoteCacheControl = response.GetResponseHeader("Cache-Control");
            return string.Equals(remoteCacheControl, expectedCacheControl, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : "OSS HEAD Cache-Control 不匹配。";
        }

        private static string BuildUrl(ESAssetReleaseUploadTarget target, string key)
        {
            // OSS 原生 Endpoint 是服务根地址；请求必须采用 bucket.endpoint 的虚拟主机形式。
            // 不能使用 endpoint/bucket/key：该路径形式在部分 OSS Endpoint/CDN 配置下不被识别，
            // 还会让“上传成功”与实际客户端可访问路径脱节。
            var endpoint = new Uri(target.endpoint.Trim());
            var builder = new UriBuilder(endpoint.Scheme, target.bucket.Trim() + "." + endpoint.Host, endpoint.IsDefaultPort ? -1 : endpoint.Port)
            {
                Path = EncodeKey(key)
            };
            return builder.Uri.AbsoluteUri;
        }

        private static string EncodeKey(string key)
        {
            return string.Join("/", NormalizeKey(key).Split('/').Select(Uri.EscapeDataString));
        }

        private static string NormalizeKey(string key)
        {
            return (key ?? string.Empty).Replace('\\', '/').Trim('/');
        }

        private static bool IsSafePrefix(string prefix)
        {
            return string.IsNullOrEmpty(prefix)
                || (prefix.IndexOf("..", StringComparison.Ordinal) < 0 && prefix.IndexOf('\\') < 0);
        }

        private static string ComputeSignature(string secret, string canonical)
        {
            using (var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(secret)))
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
        }

        private static string ComputeSha256(byte[] content)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(content)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    internal sealed class ESAliyunOssSequenceOperation : IESAssetReleaseUploadOperation
    {
        private readonly IList<Func<UnityWebRequest>> factories;
        private readonly IList<Func<UnityWebRequest, string>> validators;
        private UnityWebRequest request;
        private int index;
        private bool completed;
        private bool success;
        private string message = string.Empty;

        public ESAliyunOssSequenceOperation(IList<Func<UnityWebRequest>> factories, IList<Func<UnityWebRequest, string>> validators)
        {
            this.factories = factories;
            this.validators = validators;
        }

        public bool IsCompleted => completed;
        public bool IsSuccess => success;
        public string Message => message;

        public void Poll()
        {
            if (completed) return;
            try
            {
                if (request == null)
                {
                    if (index >= factories.Count) { Complete(true, string.Empty); return; }
                    request = factories[index]();
                    request.SendWebRequest();
                    return;
                }
                if (!request.isDone) return;
                string error = validators[index](request);
                request.Dispose();
                request = null;
                if (!string.IsNullOrEmpty(error)) { Complete(false, error); return; }
                index++;
            }
            catch (Exception exception)
            {
                request?.Dispose();
                request = null;
                Complete(false, exception.Message);
            }
        }

        public void Cancel()
        {
            request?.Abort();
            request?.Dispose();
            request = null;
            if (!completed) Complete(false, "OSS 请求已取消。");
        }

        private void Complete(bool ok, string text)
        {
            completed = true;
            success = ok;
            message = text ?? string.Empty;
        }
    }
}
