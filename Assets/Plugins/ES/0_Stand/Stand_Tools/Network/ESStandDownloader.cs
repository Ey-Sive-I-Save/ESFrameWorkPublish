using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine.Networking;

namespace ES
{
    public enum ESDownloadState
    {
        None,
        Preparing,
        Downloading,
        RetryWaiting,
        Verifying,
        Completed,
        Failed,
        Cancelled
    }

    public enum ESDownloadContentKind
    {
        Binary,
        AssetBundle,
        Json,
        Raw,
        Text,
        Web
    }

    [Serializable]
    public sealed class ESDownloadRequest
    {
        public ESDownloadRequest() { }

        public ESDownloadRequest(string url, string destinationPath)
        {
            Url = url;
            DestinationPath = destinationPath;
        }

        public string Url;
        public string DestinationPath;
        public string ExpectedSha256;
        public long ExpectedSize;
        public ESDownloadContentKind ContentKind = ESDownloadContentKind.Binary;
        public bool EnableResume = true;
        public int TimeoutSeconds = 30;
        public int MaxRetryCount = 3;
        public float RetryDelaySeconds = 1.5f;
        public Dictionary<string, string> Headers;

        [NonSerialized] private ESDownloadRuntimeStatus runtimeStatus;
        [NonSerialized] private string temporaryPath;

        public string TemporaryPath
        {
            get
            {
                if (string.IsNullOrEmpty(temporaryPath))
                    temporaryPath = DestinationPath + ".download-" + Guid.NewGuid().ToString("N");
                return temporaryPath;
            }
        }
        public ESDownloadRuntimeStatus RuntimeStatus => runtimeStatus ?? (runtimeStatus = new ESDownloadRuntimeStatus());

        public static ESDownloadRequest AssetBundle(string url, string destinationPath, string sha256 = null, long size = 0L)
        {
            return new ESDownloadRequest(url, destinationPath)
            {
                ContentKind = ESDownloadContentKind.AssetBundle,
                ExpectedSha256 = sha256,
                ExpectedSize = size
            };
        }

        public static ESDownloadRequest Json(string url, string destinationPath, string sha256 = null)
        {
            return new ESDownloadRequest(url, destinationPath) { ContentKind = ESDownloadContentKind.Json, ExpectedSha256 = sha256 };
        }

        public static ESDownloadRequest Raw(string url, string destinationPath, string sha256 = null, long size = 0L)
        {
            return new ESDownloadRequest(url, destinationPath)
            {
                ContentKind = ESDownloadContentKind.Raw,
                ExpectedSha256 = sha256,
                ExpectedSize = size
            };
        }
    }

    [Serializable]
    public sealed class ESWebRequestOptions
    {
        public int TimeoutSeconds = 30;
        public int MaxRetryCount = 3;
        public float RetryDelaySeconds = 1.5f;
        public Dictionary<string, string> Headers;
        [NonSerialized] public Encoding TextEncoding = Encoding.UTF8;
    }

    public sealed class ESWebResult<T>
    {
        public bool Success { get; internal set; }
        public T Data { get; internal set; }
        public long ResponseCode { get; internal set; }
        public int Attempts { get; internal set; }
        public string ContentType { get; internal set; }
        public string Error { get; internal set; }
        public ESDownloadAttemptReceipt[] AttemptReceipts { get; internal set; }
        public int AttemptReceiptCount { get; internal set; }
        public int RetryCount => Math.Max(0, Attempts - 1);
    }

    public readonly struct ESDownloadProgress
    {
        public readonly ESDownloadState State;
        public readonly long DownloadedBytes;
        public readonly long TotalBytes;
        public readonly double BytesPerSecond;
        public readonly double RemainingSeconds;
        public readonly int Attempt;
        public readonly string Message;

        public float Normalized => TotalBytes > 0 ? (float)Math.Min(1d, (double)DownloadedBytes / TotalBytes) : 0f;

        public ESDownloadProgress(ESDownloadState state, long downloadedBytes, long totalBytes,
            double bytesPerSecond, double remainingSeconds, int attempt, string message)
        {
            State = state;
            DownloadedBytes = downloadedBytes;
            TotalBytes = totalBytes;
            BytesPerSecond = bytesPerSecond;
            RemainingSeconds = remainingSeconds;
            Attempt = attempt;
            Message = message;
        }
    }

    public sealed class ESDownloadResult
    {
        public ESDownloadState State { get; internal set; }
        public string Url { get; internal set; }
        public string FilePath { get; internal set; }
        public long FileSize { get; internal set; }
        public string Sha256 { get; internal set; }
        public int Attempts { get; internal set; }
        public string Error { get; internal set; }
        public ESDownloadAttemptReceipt[] AttemptReceipts { get; internal set; }
        public int AttemptReceiptCount { get; internal set; }
        public int RetryCount => Math.Max(0, Attempts - 1);
        public bool Success => State == ESDownloadState.Completed;
    }

    public readonly struct ESDownloadStatusSnapshot
    {
        public readonly ESDownloadState State;
        public readonly long DownloadedBytes;
        public readonly long TotalBytes;
        public readonly long BytesPerSecond;
        public readonly double RemainingSeconds;
        public readonly int Attempt;
        public readonly string Message;
        public readonly string Error;
        public float Normalized => TotalBytes > 0 ? (float)Math.Min(1d, (double)DownloadedBytes / TotalBytes) : 0f;
        public bool IsDone => State == ESDownloadState.Completed || State == ESDownloadState.Failed || State == ESDownloadState.Cancelled;

        internal ESDownloadStatusSnapshot(ESDownloadState state, long downloadedBytes, long totalBytes,
            long bytesPerSecond, long remainingMilliseconds, int attempt, string message, string error)
        {
            State = state;
            DownloadedBytes = downloadedBytes;
            TotalBytes = totalBytes;
            BytesPerSecond = bytesPerSecond;
            RemainingSeconds = remainingMilliseconds > 0 ? remainingMilliseconds / 1000d : 0d;
            Attempt = attempt;
            Message = message;
            Error = error;
        }
    }

    public sealed class ESDownloadRuntimeStatus
    {
        private int state;
        private int attempt;
        private long downloadedBytes;
        private long totalBytes;
        private long bytesPerSecond;
        private long remainingMilliseconds;
        private string message;
        private string error;

        public ESDownloadStatusSnapshot Snapshot => new ESDownloadStatusSnapshot(
            (ESDownloadState)Volatile.Read(ref state),
            Interlocked.Read(ref downloadedBytes),
            Interlocked.Read(ref totalBytes),
            Interlocked.Read(ref bytesPerSecond),
            Interlocked.Read(ref remainingMilliseconds),
            Volatile.Read(ref attempt), message, error);

        internal void Update(ESDownloadProgress value, string errorMessage = null)
        {
            Interlocked.Exchange(ref downloadedBytes, value.DownloadedBytes);
            Interlocked.Exchange(ref totalBytes, value.TotalBytes);
            Interlocked.Exchange(ref bytesPerSecond, (long)Math.Max(0d, value.BytesPerSecond));
            Interlocked.Exchange(ref remainingMilliseconds, (long)Math.Max(0d, value.RemainingSeconds * 1000d));
            Volatile.Write(ref attempt, value.Attempt);
            message = value.Message;
            error = errorMessage;
            Volatile.Write(ref state, (int)value.State);
        }
    }

    public readonly struct ESDownloadAttemptReceipt
    {
        public readonly int Attempt;
        public readonly ESDownloadState State;
        public readonly long ResponseCode;
        public readonly long NetworkBytes;
        public readonly long ResumedFromBytes;
        public readonly double DurationSeconds;
        public readonly double RetryDelaySeconds;
        public readonly string Error;

        public ESDownloadAttemptReceipt(int attempt, ESDownloadState state, long responseCode, long networkBytes,
            long resumedFromBytes, double durationSeconds, double retryDelaySeconds, string error)
        {
            Attempt = attempt;
            State = state;
            ResponseCode = responseCode;
            NetworkBytes = networkBytes;
            ResumedFromBytes = resumedFromBytes;
            DurationSeconds = durationSeconds;
            RetryDelaySeconds = retryDelaySeconds;
            Error = error;
        }
    }

    public readonly struct ESDownloadTrafficSnapshot
    {
        public readonly long ReceivedBytes;
        public readonly long CommittedBytes;
        public readonly long DiscardedOrRetryBytes;
        public readonly long RequestCount;
        public readonly double SessionSeconds;
        public readonly double AverageBytesPerSecond;

        internal ESDownloadTrafficSnapshot(long received, long committed, long requests, double seconds)
        {
            ReceivedBytes = received;
            CommittedBytes = committed;
            DiscardedOrRetryBytes = Math.Max(0L, received - committed);
            RequestCount = requests;
            SessionSeconds = seconds;
            AverageBytesPerSecond = seconds > 0d ? received / seconds : 0d;
        }
    }

    public static class ESDownloadTrafficMonitor
    {
        private static long receivedBytes;
        private static long committedBytes;
        private static long requestCount;
        private static long sessionStartTimestamp = Stopwatch.GetTimestamp();

        public static ESDownloadTrafficSnapshot Snapshot
        {
            get
            {
                double seconds = (Stopwatch.GetTimestamp() - Interlocked.Read(ref sessionStartTimestamp)) / (double)Stopwatch.Frequency;
                return new ESDownloadTrafficSnapshot(
                    Interlocked.Read(ref receivedBytes),
                    Interlocked.Read(ref committedBytes),
                    Interlocked.Read(ref requestCount), seconds);
            }
        }

        public static void Reset()
        {
            Interlocked.Exchange(ref receivedBytes, 0L);
            Interlocked.Exchange(ref committedBytes, 0L);
            Interlocked.Exchange(ref requestCount, 0L);
            Interlocked.Exchange(ref sessionStartTimestamp, Stopwatch.GetTimestamp());
        }

        internal static void AddRequest(long networkBytes)
        {
            Interlocked.Increment(ref requestCount);
            if (networkBytes > 0L) Interlocked.Add(ref receivedBytes, networkBytes);
        }

        internal static void AddCommitted(long bytes)
        {
            if (bytes > 0L) Interlocked.Add(ref committedBytes, bytes);
        }
    }

    public readonly struct ESDownloadBatchProgress
    {
        public readonly int CompletedCount;
        public readonly int TotalCount;
        public readonly long DownloadedBytes;
        public readonly long TotalBytes;
        public readonly string CurrentUrl;

        public float Normalized => TotalBytes > 0
            ? (float)Math.Min(1d, (double)DownloadedBytes / TotalBytes)
            : TotalCount > 0 ? (float)CompletedCount / TotalCount : 1f;

        public ESDownloadBatchProgress(int completedCount, int totalCount, long downloadedBytes, long totalBytes, string currentUrl)
        {
            CompletedCount = completedCount;
            TotalCount = totalCount;
            DownloadedBytes = downloadedBytes;
            TotalBytes = totalBytes;
            CurrentUrl = currentUrl;
        }
    }

    /// <summary>
    /// Stand 层通用文件下载器。不依赖资源表、GameManager 或启动场景业务。
    /// 文件先写入 .download，完成大小/摘要校验后才替换正式文件。
    /// </summary>
    public static class ESStandDownloader
    {
        private const int BufferSize = 128 * 1024;

        /// <summary>低分配核心入口。进度结构体按值传递，不创建 Unity Progress 包装对象。</summary>
        public static UniTask<ESDownloadResult> DownloadAsync(
            ESDownloadRequest request,
            Action<ESDownloadProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            return DownloadCoreAsync(request, progress, cancellationToken);
        }

        /// <summary>兼容 Unity 标准 IProgress 的便捷入口；高频下载建议使用 Action 入口。</summary>
        public static UniTask<ESDownloadResult> DownloadWithProgressAsync(
            ESDownloadRequest request,
            IProgress<ESDownloadProgress> progress,
            CancellationToken cancellationToken = default)
        {
            return DownloadCoreAsync(request, progress == null ? null : progress.Report, cancellationToken);
        }

        /// <summary>使用 ESCallback 体系的稳定回调入口。不会自动回收到对象池，由调用方决定回收时机。</summary>
        public static async UniTask<ESDownloadResult> DownloadAsync(
            ESDownloadRequest request,
            ESDownloadCallback<ESDownloadResult> callback,
            CancellationToken cancellationToken = default)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));

            var result = await DownloadCoreAsync(request, progress =>
            {
                if (progress.TotalBytes > 0) callback.TotalSize = progress.TotalBytes;
                callback.UpdateDownloadProgress(progress.DownloadedBytes, progress.Message);
            }, cancellationToken);

            if (result.Success) callback.Success(result);
            else callback.Error(result.Error ?? (result.State == ESDownloadState.Cancelled ? "下载已取消" : "下载失败"));
            return result;
        }

        private static async UniTask<ESDownloadResult> DownloadCoreAsync(
            ESDownloadRequest request,
            Action<ESDownloadProgress> progress,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request);
            string allowedRoot;
            string destination = ValidateManagedDestination(request.DestinationPath, out allowedRoot);
            string temporary = ValidateManagedDestination(request.TemporaryPath, out _);

            int maxAttempts = Math.Max(1, request.MaxRetryCount);
            ESDownloadRuntimeStatus runtimeStatus = request.RuntimeStatus;
            var result = new ESDownloadResult
            {
                Url = request.Url,
                FilePath = destination,
                AttemptReceipts = new ESDownloadAttemptReceipt[maxAttempts]
            };
            long knownTotal = request.ExpectedSize;
            if (knownTotal <= 0)
            {
                try { knownTotal = await TryGetRemoteSizeAsync(request, cancellationToken); }
                catch (OperationCanceledException)
                {
                    result.State = ESDownloadState.Cancelled;
                    result.Error = "下载已取消";
                    PublishStatus(runtimeStatus, progress, new ESDownloadProgress(ESDownloadState.Cancelled, 0L, 0L, 0d, 0d, 0, result.Error), result.Error);
                    return result;
                }
            }
            bool forceRestart = !request.EnableResume;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                result.Attempts = attempt;
                var attemptWatch = Stopwatch.StartNew();
                long attemptNetworkBytes = 0L;
                long responseCode = 0L;
                long resumedFromBytes = 0L;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (forceRestart) DeleteManagedFileIfExists(temporary, allowedRoot);
                    if (request.EnableResume) forceRestart = false;

                    long existingBytes = request.EnableResume && File.Exists(temporary) ? new FileInfo(temporary).Length : 0L;
                    resumedFromBytes = existingBytes;
                    if (knownTotal > 0 && existingBytes > knownTotal)
                    {
                        DeleteManagedFileIfExists(temporary, allowedRoot);
                        existingBytes = 0L;
                    }

                    PublishStatus(runtimeStatus, progress, new ESDownloadProgress(ESDownloadState.Preparing, existingBytes, knownTotal, 0d, 0d, attempt, "准备下载"));
                    bool requestedRange = existingBytes > 0;
                    bool append = requestedRange;

                    using (var webRequest = UnityWebRequest.Get(request.Url))
                    {
                        ApplyHeaders(webRequest, request.Headers);
                        if (requestedRange) webRequest.SetRequestHeader("Range", "bytes=" + existingBytes + "-");
                        webRequest.timeout = Math.Max(1, request.TimeoutSeconds);
                        webRequest.downloadHandler = new DownloadHandlerFile(temporary, append) { removeFileOnAbort = false };

                        UnityWebRequestAsyncOperation operation = webRequest.SendWebRequest();
                        var stopwatch = Stopwatch.StartNew();
                        long lastBytes = existingBytes;
                        double lastTime = 0d;
                        double smoothedSpeed = 0d;

                        while (!operation.isDone)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                webRequest.Abort();
                                cancellationToken.ThrowIfCancellationRequested();
                            }

                            long currentBytes = existingBytes + (long)webRequest.downloadedBytes;
                            attemptNetworkBytes = (long)webRequest.downloadedBytes;
                            if (knownTotal <= 0) knownTotal = ResolveTotalSize(webRequest, existingBytes);
                            double now = stopwatch.Elapsed.TotalSeconds;
                            double deltaTime = now - lastTime;
                            if (deltaTime >= 0.2d)
                            {
                                double instantSpeed = Math.Max(0L, currentBytes - lastBytes) / deltaTime;
                                smoothedSpeed = smoothedSpeed <= 0d ? instantSpeed : smoothedSpeed * 0.75d + instantSpeed * 0.25d;
                                lastBytes = currentBytes;
                                lastTime = now;
                            }
                            double remaining = smoothedSpeed > 0d && knownTotal > currentBytes ? (knownTotal - currentBytes) / smoothedSpeed : 0d;
                            PublishStatus(runtimeStatus, progress, new ESDownloadProgress(ESDownloadState.Downloading, currentBytes, knownTotal, smoothedSpeed, remaining, attempt, "下载中"));
                            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                        }

                        attemptNetworkBytes = (long)webRequest.downloadedBytes;
                        responseCode = webRequest.responseCode;
                        if (webRequest.result != UnityWebRequest.Result.Success)
                            throw new IOException($"HTTP {webRequest.responseCode}: {webRequest.error}");

                        // 服务器忽略 Range 并返回完整文件时，放弃已经被追加的临时文件并从零重试。
                        if (requestedRange && webRequest.responseCode != 206)
                        {
                            ESDownloadTrafficMonitor.AddRequest(attemptNetworkBytes);
                            DeleteManagedFileIfExists(temporary, allowedRoot);
                            forceRestart = true;
                            attempt--;
                            continue;
                        }

                        if (knownTotal <= 0) knownTotal = ResolveTotalSize(webRequest, existingBytes);
                    }

                    PublishStatus(runtimeStatus, progress, new ESDownloadProgress(ESDownloadState.Verifying, FileLength(temporary), knownTotal, 0d, 0d, attempt, "校验文件"));
                    long actualSize = FileLength(temporary);
                    if (request.ExpectedSize > 0 && actualSize != request.ExpectedSize)
                        throw new InvalidDataException($"文件大小校验失败，期望 {request.ExpectedSize}，实际 {actualSize}");
                    if (knownTotal > 0 && actualSize != knownTotal)
                        throw new InvalidDataException($"下载未完成，期望 {knownTotal}，实际 {actualSize}");

                    string sha256 = await ComputeSha256Async(temporary, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(request.ExpectedSha256)
                        && !string.Equals(sha256, NormalizeHash(request.ExpectedSha256), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("SHA-256 校验失败");

                    CommitFile(temporary, destination, allowedRoot);
                    string committedSha256 = await ComputeSha256Async(destination, cancellationToken);
                    if (!string.Equals(sha256, committedSha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("下载提交后 SHA-256 校验失败");
                    result.State = ESDownloadState.Completed;
                    result.FileSize = FileLength(destination);
                    result.Sha256 = committedSha256;
                    ESDownloadTrafficMonitor.AddRequest(attemptNetworkBytes);
                    ESDownloadTrafficMonitor.AddCommitted(attemptNetworkBytes);
                    RecordAttempt(result, attempt, ESDownloadState.Completed, responseCode, attemptNetworkBytes, resumedFromBytes, attemptWatch.Elapsed.TotalSeconds, 0d, null);
                    PublishStatus(runtimeStatus, progress, new ESDownloadProgress(ESDownloadState.Completed, result.FileSize, result.FileSize, 0d, 0d, attempt, "下载完成"));
                    return result;
                }
                catch (OperationCanceledException)
                {
                    result.State = ESDownloadState.Cancelled;
                    result.Error = "下载已取消";
                    ESDownloadTrafficMonitor.AddRequest(attemptNetworkBytes);
                    RecordAttempt(result, attempt, ESDownloadState.Cancelled, responseCode, attemptNetworkBytes, resumedFromBytes, attemptWatch.Elapsed.TotalSeconds, 0d, result.Error);
                    PublishStatus(runtimeStatus, progress, new ESDownloadProgress(ESDownloadState.Cancelled, FileLength(temporary), knownTotal, 0d, 0d, attempt, result.Error), result.Error);
                    return result;
                }
                catch (Exception ex)
                {
                    result.Error = ex.Message;
                    ESDownloadTrafficMonitor.AddRequest(attemptNetworkBytes);
                    if (ex is InvalidDataException)
                    {
                        DeleteManagedFileIfExists(temporary, allowedRoot);
                        forceRestart = true;
                    }
                    double retryDelay = attempt < maxAttempts ? Math.Max(0d, request.RetryDelaySeconds) * attempt : 0d;
                    RecordAttempt(result, attempt, ESDownloadState.Failed, responseCode, attemptNetworkBytes, resumedFromBytes, attemptWatch.Elapsed.TotalSeconds, retryDelay, ex.Message);
                    if (attempt >= maxAttempts) break;
                    PublishStatus(runtimeStatus, progress, new ESDownloadProgress(
                        attempt < maxAttempts ? ESDownloadState.RetryWaiting : ESDownloadState.Failed,
                        FileLength(temporary), knownTotal, 0d, retryDelay, attempt,
                        attempt < maxAttempts ? "下载失败，准备重试：" + ex.Message : ex.Message), ex.Message);
                    try
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(retryDelay), cancellationToken: cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        result.State = ESDownloadState.Cancelled;
                        result.Error = "下载已取消";
                        PublishStatus(runtimeStatus, progress, new ESDownloadProgress(ESDownloadState.Cancelled, FileLength(temporary), knownTotal, 0d, 0d, attempt, result.Error), result.Error);
                        return result;
                    }
                }
            }

            result.State = ESDownloadState.Failed;
            PublishStatus(runtimeStatus, progress, new ESDownloadProgress(ESDownloadState.Failed, FileLength(temporary), knownTotal, 0d, 0d, result.Attempts, result.Error), result.Error);
            return result;
        }

        /// <summary>适用于较小的 Raw、配置、网页响应。AB 和大型资源应使用 DownloadAsync 直接写磁盘。</summary>
        public static async UniTask<ESWebResult<byte[]>> GetBytesAsync(
            string url,
            ESWebRequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("URL 不能为空", nameof(url));

            int maxAttempts = Math.Max(1, options?.MaxRetryCount ?? 3);
            var result = new ESWebResult<byte[]>
            {
                AttemptReceipts = new ESDownloadAttemptReceipt[maxAttempts]
            };
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                result.Attempts = attempt;
                long attemptBytes = 0L;
                var attemptWatch = Stopwatch.StartNew();
                try
                {
                    using (var request = UnityWebRequest.Get(url))
                    {
                        ApplyHeaders(request, options?.Headers);
                        request.timeout = Math.Max(1, options?.TimeoutSeconds ?? 30);
                        UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                        while (!operation.isDone)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                request.Abort();
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                        }

                        result.ResponseCode = request.responseCode;
                        result.ContentType = request.GetResponseHeader("Content-Type");
                        attemptBytes = (long)request.downloadedBytes;
                        if (request.result != UnityWebRequest.Result.Success)
                            throw new IOException($"HTTP {request.responseCode}: {request.error}");

                        result.Success = true;
                        result.Data = request.downloadHandler.data;
                        ESDownloadTrafficMonitor.AddRequest(attemptBytes);
                        ESDownloadTrafficMonitor.AddCommitted(attemptBytes);
                        RecordWebAttempt(result, attempt, ESDownloadState.Completed, result.ResponseCode, attemptBytes, attemptWatch.Elapsed.TotalSeconds, 0d, null);
                        return result;
                    }
                }
                catch (OperationCanceledException)
                {
                    ESDownloadTrafficMonitor.AddRequest(attemptBytes);
                    result.Error = "请求已取消";
                    RecordWebAttempt(result, attempt, ESDownloadState.Cancelled, result.ResponseCode, attemptBytes, attemptWatch.Elapsed.TotalSeconds, 0d, result.Error);
                    return result;
                }
                catch (Exception ex)
                {
                    ESDownloadTrafficMonitor.AddRequest(attemptBytes);
                    result.Error = ex.Message;
                    double retryDelay = attempt < maxAttempts ? Math.Max(0f, options?.RetryDelaySeconds ?? 1.5f) * attempt : 0d;
                    RecordWebAttempt(result, attempt, ESDownloadState.Failed, result.ResponseCode, attemptBytes, attemptWatch.Elapsed.TotalSeconds, retryDelay, ex.Message);
                    if (attempt >= maxAttempts) return result;
                    try
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(retryDelay), cancellationToken: cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        result.Error = "请求已取消";
                        return result;
                    }
                }
            }
            return result;
        }

        public static async UniTask<ESWebResult<string>> GetTextAsync(
            string url,
            ESWebRequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            ESWebResult<byte[]> bytes = await GetBytesAsync(url, options, cancellationToken);
            var result = CopyWebResult<byte[], string>(bytes);
            if (!bytes.Success) return result;
            try
            {
                result.Data = (options?.TextEncoding ?? Encoding.UTF8).GetString(bytes.Data ?? Array.Empty<byte>());
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }
            return result;
        }

        public static async UniTask<ESWebResult<T>> GetJsonAsync<T>(
            string url,
            ESWebRequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            ESWebResult<string> text = await GetTextAsync(url, options, cancellationToken);
            var result = CopyWebResult<string, T>(text);
            if (!text.Success) return result;
            try
            {
                result.Data = JsonConvert.DeserializeObject<T>(text.Data);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = "JSON 解析失败：" + ex.Message;
            }
            return result;
        }

        public static async UniTask<ESWebResult<byte[]>> GetBytesAsync(
            string url,
            ESCallback<byte[]> callback,
            ESWebRequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            ESWebResult<byte[]> result = await GetBytesAsync(url, options, cancellationToken);
            if (result.Success) callback.Success(result.Data);
            else callback.Error(result.Error ?? "网页字节请求失败");
            return result;
        }

        public static async UniTask<ESWebResult<string>> GetTextAsync(
            string url,
            ESCallback<string> callback,
            ESWebRequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            ESWebResult<string> result = await GetTextAsync(url, options, cancellationToken);
            if (result.Success) callback.Success(result.Data);
            else callback.Error(result.Error ?? "网页文本请求失败");
            return result;
        }

        public static async UniTask<ESWebResult<T>> GetJsonAsync<T>(
            string url,
            ESCallback<T> callback,
            ESWebRequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            ESWebResult<T> result = await GetJsonAsync<T>(url, options, cancellationToken);
            if (result.Success) callback.Success(result.Data);
            else callback.Error(result.Error ?? "JSON 请求失败");
            return result;
        }

        public static async UniTask<ESDownloadResult[]> DownloadAllAsync(
            IReadOnlyList<ESDownloadRequest> requests,
            int maxConcurrency = 4,
            Action<ESDownloadBatchProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            if (requests.Count == 0) return Array.Empty<ESDownloadResult>();

            int concurrency = Math.Max(1, maxConcurrency);
            var semaphore = new SemaphoreSlim(concurrency, concurrency);
            var results = new ESDownloadResult[requests.Count];
            var downloaded = new long[requests.Count];
            var totals = new long[requests.Count];
            int completed = 0;
            object progressLock = new object();
            var tasks = new UniTask[requests.Count];

            for (int i = 0; i < requests.Count; i++)
            {
                int index = i;
                tasks[index] = DownloadOne(index);
            }

            try
            {
                await UniTask.WhenAll(tasks);
                return results;
            }
            finally
            {
                semaphore.Dispose();
            }

            async UniTask DownloadOne(int index)
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    Action<ESDownloadProgress> itemProgress = value =>
                    {
                        lock (progressLock)
                        {
                            downloaded[index] = value.DownloadedBytes;
                            totals[index] = value.TotalBytes;
                            ReportBatch(requests[index].Url);
                        }
                    };
                    results[index] = await DownloadAsync(requests[index], itemProgress, cancellationToken);
                    lock (progressLock)
                    {
                        completed++;
                        ReportBatch(requests[index].Url);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }

            void ReportBatch(string currentUrl)
            {
                long downloadedSum = 0L;
                long totalSum = 0L;
                for (int i = 0; i < downloaded.Length; i++)
                {
                    downloadedSum += downloaded[i];
                    totalSum += totals[i];
                }
                if (progress == null) return;
                try { progress(new ESDownloadBatchProgress(completed, requests.Count, downloadedSum, totalSum, currentUrl)); }
                catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
            }
        }

        public static UniTask<ESDownloadResult[]> DownloadAllWithProgressAsync(
            IReadOnlyList<ESDownloadRequest> requests,
            int maxConcurrency,
            IProgress<ESDownloadBatchProgress> progress,
            CancellationToken cancellationToken = default)
        {
            return DownloadAllAsync(requests, maxConcurrency, progress == null ? null : progress.Report, cancellationToken);
        }

        public static void DeleteTemporaryFile(ESDownloadRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.DestinationPath)) return;
            string allowedRoot;
            string path = ValidateManagedDestination(request.TemporaryPath, out allowedRoot);
            DeleteManagedFileIfExists(path, allowedRoot);
        }

        private static async UniTask<long> TryGetRemoteSizeAsync(ESDownloadRequest request, CancellationToken cancellationToken)
        {
            try
            {
                using (var head = UnityWebRequest.Head(request.Url))
                {
                    ApplyHeaders(head, request.Headers);
                    head.timeout = Math.Max(1, request.TimeoutSeconds);
                    UnityWebRequestAsyncOperation operation = head.SendWebRequest();
                    while (!operation.isDone)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                    }
                    return head.result == UnityWebRequest.Result.Success && long.TryParse(head.GetResponseHeader("Content-Length"), out long size) ? size : 0L;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { return 0L; }
        }

        private static long ResolveTotalSize(UnityWebRequest request, long existingBytes)
        {
            string contentRange = request.GetResponseHeader("Content-Range");
            if (!string.IsNullOrEmpty(contentRange))
            {
                int slash = contentRange.LastIndexOf('/');
                if (slash >= 0 && long.TryParse(contentRange.Substring(slash + 1), out long rangedTotal)) return rangedTotal;
            }
            return long.TryParse(request.GetResponseHeader("Content-Length"), out long length) ? existingBytes + length : 0L;
        }

        private static async UniTask<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            await UniTask.SwitchToThreadPool();
#endif
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan))
                using (var sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(stream);
                    cancellationToken.ThrowIfCancellationRequested();
                    return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
                }
            }
            finally
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                await UniTask.SwitchToMainThread();
#endif
            }
        }

        private static void CommitFile(string temporary, string destination, string allowedRoot)
        {
            ESManagedFileIO.EnsurePath(temporary, true, allowedRoot);
            if (!File.Exists(temporary)) throw new FileNotFoundException("下载临时文件不存在", temporary);
            ESManagedFileIO.EnsurePath(destination, false, allowedRoot);
            if (!File.Exists(destination))
            {
                File.Move(temporary, destination);
            }
            else
            {
                // 优先使用原子替换；平台不支持或文件系统拒绝替换时，保留旧文件并走可恢复的移动提交。
                try
                {
                    File.Replace(temporary, destination, null);
                }
                catch (PlatformNotSupportedException)
                {
                    PromoteWithBackup(temporary, destination, allowedRoot);
                }
                catch (IOException)
                {
                    PromoteWithBackup(temporary, destination, allowedRoot);
                }
                catch (UnauthorizedAccessException)
                {
                    PromoteWithBackup(temporary, destination, allowedRoot);
                }
            }

            ESManagedFileIO.EnsurePath(destination, true, allowedRoot);
            if (!File.Exists(destination))
                throw new IOException("下载提交后目标文件不存在：" + destination);
        }

        private static void PromoteWithBackup(string temporary, string destination, string allowedRoot)
        {
            string backup = destination + ".backup-" + Guid.NewGuid().ToString("N");
            ESManagedFileIO.EnsurePath(backup, false, allowedRoot);
            File.Move(destination, backup);
            bool promoted = false;
            try
            {
                File.Move(temporary, destination);
                ESManagedFileIO.EnsurePath(destination, true, allowedRoot);
                promoted = true;
            }
            catch (Exception commitException)
            {
                try
                {
                    if (!File.Exists(destination) && File.Exists(backup))
                        File.Move(backup, destination);
                }
                catch (Exception restoreException)
                {
                    throw new AggregateException("下载提交失败且旧文件恢复失败。", commitException, restoreException);
                }
                throw;
            }
            finally
            {
                if (promoted && File.Exists(backup))
                {
                    try { File.Delete(backup); }
                    catch { /* 保留备份现场，不覆盖已成功提交结果。 */ }
                }
            }
        }

        private static void ApplyHeaders(UnityWebRequest request, Dictionary<string, string> headers)
        {
            if (headers == null) return;
            foreach (var pair in headers)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null) request.SetRequestHeader(pair.Key, pair.Value);
            }
        }

        private static ESWebResult<TOut> CopyWebResult<TIn, TOut>(ESWebResult<TIn> source)
        {
            return new ESWebResult<TOut>
            {
                Success = source.Success,
                ResponseCode = source.ResponseCode,
                Attempts = source.Attempts,
                ContentType = source.ContentType,
                Error = source.Error,
                AttemptReceipts = source.AttemptReceipts,
                AttemptReceiptCount = source.AttemptReceiptCount
            };
        }

        private static void PublishStatus(ESDownloadRuntimeStatus runtimeStatus, Action<ESDownloadProgress> progress,
            ESDownloadProgress value, string error = null)
        {
            runtimeStatus?.Update(value, error);
            if (progress == null) return;
            try { progress(value); }
            catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
        }

        private static void RecordAttempt(ESDownloadResult result, int attempt, ESDownloadState state,
            long responseCode, long networkBytes, long resumedFromBytes, double durationSeconds,
            double retryDelaySeconds, string error)
        {
            if (result?.AttemptReceipts == null || attempt <= 0 || attempt > result.AttemptReceipts.Length) return;
            result.AttemptReceipts[attempt - 1] = new ESDownloadAttemptReceipt(
                attempt, state, responseCode, networkBytes, resumedFromBytes,
                durationSeconds, retryDelaySeconds, error);
            result.AttemptReceiptCount = Math.Max(result.AttemptReceiptCount, attempt);
        }

        private static void RecordWebAttempt<T>(ESWebResult<T> result, int attempt, ESDownloadState state,
            long responseCode, long networkBytes, double durationSeconds, double retryDelaySeconds, string error)
        {
            if (result?.AttemptReceipts == null || attempt <= 0 || attempt > result.AttemptReceipts.Length) return;
            result.AttemptReceipts[attempt - 1] = new ESDownloadAttemptReceipt(
                attempt, state, responseCode, networkBytes, 0L, durationSeconds, retryDelaySeconds, error);
            result.AttemptReceiptCount = Math.Max(result.AttemptReceiptCount, attempt);
        }

        private static void ValidateRequest(ESDownloadRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.Url)) throw new ArgumentException("下载 URL 不能为空", nameof(request));
            if (string.IsNullOrWhiteSpace(request.DestinationPath)) throw new ArgumentException("目标文件路径不能为空", nameof(request));
        }

        private static string ValidateManagedDestination(string path, out string allowedRoot)
        {
            allowedRoot = null;
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("下载目标路径不能为空。", nameof(path));
            if (ContainsParentTraversal(path))
                throw new UnauthorizedAccessException("下载目标路径不得包含 .. 段：" + path);

            string candidate = ESManagedFileIO.NormalizeFullPath(path);
            string[] roots = { UnityEngine.Application.persistentDataPath, UnityEngine.Application.temporaryCachePath };
            foreach (string root in roots)
            {
                if (string.IsNullOrWhiteSpace(root))
                    continue;
                string normalizedRoot = ESManagedFileIO.NormalizeFullPath(root);
                if (!ESManagedFileIO.IsWithinRoot(candidate, normalizedRoot)
                    || string.Equals(candidate, normalizedRoot, StringComparison.OrdinalIgnoreCase))
                    continue;

                string directory = Path.GetDirectoryName(candidate);
                if (string.IsNullOrEmpty(directory))
                    throw new InvalidDataException("下载目标目录无效：" + path);
                Directory.CreateDirectory(directory);
                ESManagedFileIO.EnsurePath(candidate, false, normalizedRoot);
                allowedRoot = normalizedRoot;
                return candidate;
            }

            throw new UnauthorizedAccessException("下载目标必须位于 persistentDataPath 或 temporaryCachePath：" + path);
        }

        private static void DeleteManagedFileIfExists(string path, string allowedRoot)
        {
            if (File.Exists(path))
                ESManagedFileIO.DeleteFile(path, allowedRoot);
        }

        private static bool ContainsParentTraversal(string path)
        {
            foreach (string segment in path.Replace('\\', '/').Split('/'))
                if (segment == "..") return true;
            return false;
        }

        private static string NormalizeHash(string hash) => hash.Replace("-", string.Empty).Trim().ToLowerInvariant();
        private static long FileLength(string path) => !string.IsNullOrEmpty(path) && File.Exists(path) ? new FileInfo(path).Length : 0L;
    }

    /// <summary>
    /// ES 对业务层公开的统一下载门面。
    /// 大多数调用只需要记住 Download、Bytes、Text、Json、Batch 五个入口。
    /// </summary>
    public static class ESDownload
    {
        public static UniTask<ESDownloadResult> DownloadAsync(
            ESDownloadRequest request,
            CancellationToken cancellationToken = default)
        {
            return ESStandDownloader.DownloadAsync(request, (Action<ESDownloadProgress>)null, cancellationToken);
        }

        public static UniTask<ESDownloadResult> DownloadAsync(
            ESDownloadRequest request,
            ESDownloadCallback<ESDownloadResult> callback,
            CancellationToken cancellationToken = default)
        {
            return ESStandDownloader.DownloadAsync(request, callback, cancellationToken);
        }

        public static UniTask<ESWebResult<byte[]>> BytesAsync(
            string url,
            ESWebRequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return ESStandDownloader.GetBytesAsync(url, options, cancellationToken);
        }

        public static UniTask<ESWebResult<byte[]>> BytesAsync(
            string url,
            ESCallback<byte[]> callback,
            ESWebRequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return ESStandDownloader.GetBytesAsync(url, callback, options, cancellationToken);
        }

        public static UniTask<ESWebResult<string>> TextAsync(
            string url,
            ESWebRequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return ESStandDownloader.GetTextAsync(url, options, cancellationToken);
        }

        public static UniTask<ESWebResult<string>> TextAsync(
            string url,
            ESCallback<string> callback,
            ESWebRequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return ESStandDownloader.GetTextAsync(url, callback, options, cancellationToken);
        }

        public static UniTask<ESWebResult<T>> JsonAsync<T>(
            string url,
            ESWebRequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return ESStandDownloader.GetJsonAsync<T>(url, options, cancellationToken);
        }

        public static UniTask<ESWebResult<T>> JsonAsync<T>(
            string url,
            ESCallback<T> callback,
            ESWebRequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return ESStandDownloader.GetJsonAsync<T>(url, callback, options, cancellationToken);
        }

        public static UniTask<ESDownloadResult[]> BatchAsync(
            IReadOnlyList<ESDownloadRequest> requests,
            int maxConcurrency = 4,
            CancellationToken cancellationToken = default)
        {
            return ESStandDownloader.DownloadAllAsync(requests, maxConcurrency, null, cancellationToken);
        }

        public static ESDownloadStatusSnapshot GetStatus(ESDownloadRequest request)
        {
            return request == null ? default : request.RuntimeStatus.Snapshot;
        }

        public static ESDownloadTrafficSnapshot Traffic => ESDownloadTrafficMonitor.Snapshot;
        public static void ResetTraffic() => ESDownloadTrafficMonitor.Reset();
    }
}
