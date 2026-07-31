using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 发布目标只描述传输协议，不参与资源构建、Hash 计算或发布清单生成。
    /// 所有目标均消费同一份 ESAssetReleaseUploadPlan，因此可以安全替换或并行扩展。
    /// </summary>
    public enum ESAssetReleaseUploadMode
    {
        [InspectorName("手动上传计划（已可用·不执行网络）")]
        ManualPlan = 0,
        [InspectorName("阿里云 OSS 原生（已实现·待实测）")]
        AliyunOss = 1,
        [InspectorName("S3 兼容对象存储（待实现）")]
        S3Compatible = 2,
        [InspectorName("预签名 HTTP PUT（待实现）")]
        HttpPut = 3,
        [InspectorName("外部 CLI / CI（待实现）")]
        ExternalCommand = 4
    }

    [Serializable]
    public sealed class ESAssetReleaseUploadTarget
    {
        public string id = "default";
        public string displayName = "手动上传";
        public ESAssetReleaseUploadMode mode = ESAssetReleaseUploadMode.ManualPlan;
        public string region = string.Empty;
        public string publicBaseUrl = string.Empty;
        public string endpoint = string.Empty;
        public string bucket = string.Empty;
        public string objectPrefix = string.Empty;
        public string validationPrefix = ".es-validation";
        public string credentialProfile = string.Empty;
        public int maxConcurrency = 3;
        public int maxAttemptsPerFile = 3;
        public bool verifyRemoteAfterUpload = true;
        public bool refreshCdnAfterUpload;
    }

    public sealed class ESAssetReleaseUploadRequest
    {
        public ESAssetReleaseUploadTarget Target { get; }
        public ESAssetReleaseUploadPlan Plan { get; }
        public ESAssetReleaseUploadRequest(ESAssetReleaseUploadTarget target, ESAssetReleaseUploadPlan plan)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        }
    }

    /// <summary>Provider 只接收框架已排序、已校验的单文件任务，绝不自行决定上传次序。</summary>
    public sealed class ESAssetReleaseUploadFileRequest
    {
        public ESAssetReleaseUploadTarget Target { get; }
        public ESAssetReleaseUploadPlan Plan { get; }
        public ESAssetReleaseUploadPlanFile File { get; }
        /// <summary>Provider 必须把该值写入远端对象的 Cache-Control 元数据。</summary>
        public string CacheControl => File.cacheControl;
        /// <summary>
        /// 唯一允许用于远端存储的对象键：发布计划的平台层由框架统一补入。
        /// OSS/S3/HTTP 等 Provider 不得直接使用 File.relativePath，否则会把不同平台互相覆盖。
        /// </summary>
        public string RemoteObjectKey => BuildRemoteObjectKey(Target.objectPrefix, Plan.platform, File.relativePath);
        public ESAssetReleaseUploadFileRequest(ESAssetReleaseUploadTarget target, ESAssetReleaseUploadPlan plan, ESAssetReleaseUploadPlanFile file)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            File = file ?? throw new ArgumentNullException(nameof(file));
        }

        private static string BuildRemoteObjectKey(string prefix, string platform, string relativePath)
        {
            string normalizedPrefix = (prefix ?? string.Empty).Trim().Trim('/');
            string normalizedPlatform = (platform ?? string.Empty).Trim().Trim('/');
            string normalizedRelativePath = (relativePath ?? string.Empty).Replace('\\', '/').Trim('/');
            if (string.IsNullOrEmpty(normalizedPlatform)) throw new InvalidOperationException("上传计划缺少平台目录。");
            if (string.IsNullOrEmpty(normalizedRelativePath)) throw new InvalidOperationException("上传文件缺少相对路径。");
            return string.IsNullOrEmpty(normalizedPrefix)
                ? normalizedPlatform + "/" + normalizedRelativePath
                : normalizedPrefix + "/" + normalizedPlatform + "/" + normalizedRelativePath;
        }
    }

    public interface IESAssetReleaseUploadOperation
    {
        bool IsCompleted { get; }
        bool IsSuccess { get; }
        string Message { get; }
        void Poll();
        void Cancel();
    }

    internal sealed class ESCompletedReleaseUploadOperation : IESAssetReleaseUploadOperation
    {
        public bool IsCompleted => true;
        public bool IsSuccess { get; }
        public string Message { get; }
        public ESCompletedReleaseUploadOperation(bool isSuccess, string message) { IsSuccess = isSuccess; Message = message ?? string.Empty; }
        public void Poll() { }
        public void Cancel() { }
    }

    public sealed class ESAssetReleaseUploadResult
    {
        public bool IsSuccess { get; internal set; }
        public string Message { get; internal set; } = string.Empty;
        public int UploadedFileCount { get; internal set; }
        public IReadOnlyList<string> Errors { get; internal set; } = Array.Empty<string>();
    }

    /// <summary>第五步远端发布开始前的只读结果；预检不会产生网络写入。</summary>
    public sealed class ESAssetReleaseUploadPreflightResult
    {
        public bool IsSuccess { get; internal set; }
        public string Message { get; internal set; } = string.Empty;
        public int FileCount { get; internal set; }
        public long TotalBytes { get; internal set; }
    }

    /// <summary>每种发布方式的唯一扩展点。凭据必须由独立 CredentialProvider 读取，禁止写入 Target 或发布计划。</summary>
    public interface IESAssetReleaseUploadProvider
    {
        ESAssetReleaseUploadMode Mode { get; }
        bool CanHandle(ESAssetReleaseUploadTarget target, out string reason);
        IESAssetReleaseUploadOperation BeginValidation(ESAssetReleaseUploadTarget target);
        IESAssetReleaseUploadOperation BeginUpload(ESAssetReleaseUploadFileRequest request);
    }

    /// <summary>
    /// 手动计划不是远端发布 Provider。它只保留为第四步产物的人工交接格式，
    /// 防止第五步在没有真实上传与 HEAD 校验时伪报“发布成功”。
    /// </summary>
    public sealed class ESManualReleaseUploadProvider : IESAssetReleaseUploadProvider
    {
        public ESAssetReleaseUploadMode Mode => ESAssetReleaseUploadMode.ManualPlan;
        public bool CanHandle(ESAssetReleaseUploadTarget target, out string reason)
        {
            reason = "当前目标为“手动上传计划”，不能执行第五步远端发布。请安装并选择 OSS、S3 或 HTTP PUT Provider。";
            return false;
        }
        public IESAssetReleaseUploadOperation BeginUpload(ESAssetReleaseUploadFileRequest request)
        {
            return new ESCompletedReleaseUploadOperation(false, "手动上传计划没有远端写入能力。");
        }

        public IESAssetReleaseUploadOperation BeginValidation(ESAssetReleaseUploadTarget target)
        {
            return new ESCompletedReleaseUploadOperation(false, "手动上传计划没有远端验证能力。");
        }
    }

    /// <summary>
    /// 固定的上传总控：所有 Provider 共用同一顺序、输入验证和最终根清单屏障。
    /// 后续并发、断点续传、远端 HEAD/Hash 校验和重试也只能加在这里，Provider 不得重排文件。
    /// </summary>
    public static class ESAssetReleaseUploadCoordinator
    {
        public static ESEditorLongTask EnqueueValidation(ESAssetReleaseUploadTarget target, Action<ESAssetReleaseUploadResult> onFinished = null)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            IESAssetReleaseUploadProvider provider = ESAssetReleaseUploadProviderFactory.Get(target.mode);
            if (!provider.CanHandle(target, out string reason))
                throw new InvalidOperationException("远端验证目标不可用：" + reason);
            IESAssetReleaseUploadOperation operation = provider.BeginValidation(target);
            if (operation == null) throw new InvalidOperationException("远端验证适配器未返回操作。");
            return ESEditorHandle.EnqueueLongTask(new ESAssetReleaseValidationLongTask(operation, onFinished));
        }

        public static ESAssetReleaseUploadPreflightResult Preflight(ESAssetReleaseUploadRequest request)
        {
            var result = new ESAssetReleaseUploadPreflightResult();
            try
            {
                if (request == null) throw new ArgumentNullException(nameof(request));
                if (!TryGetOrderedFiles(request.Plan, out List<ESAssetReleaseUploadPlanFile> ordered, out string error))
                    throw new InvalidOperationException(error);
                IESAssetReleaseUploadProvider provider = ESAssetReleaseUploadProviderFactory.Get(request.Target.mode);
                if (!provider.CanHandle(request.Target, out string reason))
                    throw new InvalidOperationException("发布目标不可用：" + reason);

                long totalBytes = 0;
                for (int i = 0; i < ordered.Count; i++)
                {
                    ESAssetReleaseUploadPlanFile file = ordered[i];
                    if (string.IsNullOrWhiteSpace(file.sourcePath) || !System.IO.File.Exists(file.sourcePath))
                        throw new InvalidOperationException("上传源文件不存在：" + file.relativePath);
                    if (new System.IO.FileInfo(file.sourcePath).Length != file.size)
                        throw new InvalidOperationException("上传源文件大小已变化：" + file.relativePath);
                    if (string.IsNullOrWhiteSpace(file.sha256) || !ESResManifestIntegrity.VerifyFileSha256(file.sourcePath, file.sha256))
                        throw new InvalidOperationException("上传源文件 SHA-256 不匹配：" + file.relativePath);
                    if (!file.uploadLast && !string.Equals(file.cacheControl, "public, max-age=31536000, immutable", StringComparison.Ordinal))
                        throw new InvalidOperationException("版本化发布文件必须使用 immutable 长缓存：" + file.relativePath);
                    totalBytes += file.size;
                }

                result.IsSuccess = true;
                result.FileCount = ordered.Count;
                result.TotalBytes = totalBytes;
                result.Message = "预检通过：" + request.Plan.platform + " / " + request.Plan.releaseVersion + "，共 " + ordered.Count + " 个文件。根发布清单将最后上传。";
            }
            catch (Exception exception)
            {
                result.Message = exception.Message;
            }
            return result;
        }

        public static ESEditorLongTask Enqueue(ESAssetReleaseUploadRequest request, Action<ESAssetReleaseUploadResult> onFinished = null)
        {
            ESAssetReleaseUploadPreflightResult preflight = Preflight(request);
            if (!preflight.IsSuccess) throw new InvalidOperationException("远端发布预检失败：" + preflight.Message);
            IESAssetReleaseUploadProvider provider = ESAssetReleaseUploadProviderFactory.Get(request.Target.mode);
            TryGetOrderedFiles(request.Plan, out List<ESAssetReleaseUploadPlanFile> ordered, out _);
            return ESEditorHandle.EnqueueLongTask(new ESAssetReleaseUploadLongTask(request, provider, ordered, onFinished));
        }

        internal static bool TryGetOrderedFiles(ESAssetReleaseUploadPlan plan, out List<ESAssetReleaseUploadPlanFile> ordered, out string error)
        {
            List<ESAssetReleaseUploadPlanFile> files = (plan?.files ?? new List<ESAssetReleaseUploadPlanFile>()).ToList();
            List<ESAssetReleaseUploadPlanFile> rootFiles = files.Where(item => item != null && item.uploadLast).ToList();
            if (rootFiles.Count != 1 || !string.Equals(rootFiles[0].relativePath, "ESAssetReleaseManifest.json", StringComparison.Ordinal))
            {
                ordered = null;
                error = "上传计划缺少唯一的最后根发布清单。";
                return false;
            }
            if (!string.Equals(rootFiles[0].cacheControl, "no-cache, max-age=0, must-revalidate", StringComparison.Ordinal))
            {
                ordered = null;
                error = "根发布清单必须使用 no-cache, max-age=0, must-revalidate；请重新生成发布上传计划。";
                return false;
            }
            ordered = files.Where(item => item != null && !item.uploadLast).OrderBy(item => item.uploadOrder).ToList();
            ordered.Add(rootFiles[0]);
            error = string.Empty;
            return true;
        }
    }

    internal sealed class ESAssetReleaseValidationLongTask : ESEditorLongTask
    {
        private readonly IESAssetReleaseUploadOperation operation;
        private readonly Action<ESAssetReleaseUploadResult> onFinished;

        public ESAssetReleaseValidationLongTask(IESAssetReleaseUploadOperation operation, Action<ESAssetReleaseUploadResult> onFinished)
            : base("ES 远端发布区域验证", "es-release-validation", 10)
        {
            this.operation = operation;
            this.onFinished = onFinished;
        }

        public override ESEditorLongTaskStepResult ProcessStep(ESEditorLongTaskContext context)
        {
            SetProgress(0, 1, "验证远端隔离区 .es-validation");
            operation.Poll();
            if (!operation.IsCompleted) return ESEditorLongTaskStepResult.Continue;
            if (!operation.IsSuccess)
            {
                SetFailure(new InvalidOperationException(operation.Message));
                return ESEditorLongTaskStepResult.Fail;
            }
            return ESEditorLongTaskStepResult.Complete;
        }

        protected override void OnFinish()
        {
            if (Status != ESEditorLongTaskStatus.Succeeded)
                operation.Cancel();
            onFinished?.Invoke(new ESAssetReleaseUploadResult
            {
                IsSuccess = Status == ESEditorLongTaskStatus.Succeeded,
                UploadedFileCount = 1,
                Message = Status == ESEditorLongTaskStatus.Succeeded
                    ? "远端隔离区验证通过；探针对象已按 Provider 约定清理。"
                    : "远端隔离区验证失败：" + (LastError?.Message ?? operation.Message)
            });
        }
    }

    internal sealed class ESAssetReleaseUploadLongTask : ESEditorLongTask
    {
        private readonly ESAssetReleaseUploadRequest request;
        private readonly IESAssetReleaseUploadProvider provider;
        private readonly List<ESAssetReleaseUploadPlanFile> orderedFiles;
        private readonly Action<ESAssetReleaseUploadResult> onFinished;
        private IESAssetReleaseUploadOperation operation;
        private int index;
        private int attemptsStarted;

        public ESAssetReleaseUploadLongTask(ESAssetReleaseUploadRequest request, IESAssetReleaseUploadProvider provider, List<ESAssetReleaseUploadPlanFile> orderedFiles, Action<ESAssetReleaseUploadResult> onFinished)
            : base("ES 资源发布上传", "es-release-upload", 10)
        {
            this.request = request;
            this.provider = provider;
            this.orderedFiles = orderedFiles;
            this.onFinished = onFinished;
        }

        public override ESEditorLongTaskStepResult ProcessStep(ESEditorLongTaskContext context)
        {
            if (index >= orderedFiles.Count) return ESEditorLongTaskStepResult.Complete;
            ESAssetReleaseUploadPlanFile file = orderedFiles[index];
            SetProgress(index, orderedFiles.Count, "上传 " + (index + 1) + "/" + orderedFiles.Count + "：" + file.relativePath);
            if (string.IsNullOrWhiteSpace(file.sourcePath) || string.IsNullOrWhiteSpace(file.relativePath) || !System.IO.File.Exists(file.sourcePath)
                || new System.IO.FileInfo(file.sourcePath).Length != file.size)
            {
                SetFailure(new InvalidOperationException("上传源文件无效或已变化：" + file.relativePath));
                return ESEditorLongTaskStepResult.Fail;
            }

            if (operation == null)
            {
                attemptsStarted++;
                operation = provider.BeginUpload(new ESAssetReleaseUploadFileRequest(request.Target, request.Plan, file));
                if (operation == null)
                {
                    SetFailure(new InvalidOperationException("上传适配器未返回文件操作：" + file.relativePath));
                    return ESEditorLongTaskStepResult.Fail;
                }
            }

            operation.Poll();
            if (!operation.IsCompleted) return ESEditorLongTaskStepResult.Continue;
            if (operation.IsSuccess)
            {
                operation = null;
                attemptsStarted = 0;
                index++;
                return index >= orderedFiles.Count ? ESEditorLongTaskStepResult.Complete : ESEditorLongTaskStepResult.Continue;
            }
            string message = operation.Message;
            operation.Cancel();
            operation = null;
            if (attemptsStarted < Math.Max(1, request.Target.maxAttemptsPerFile)) return ESEditorLongTaskStepResult.Continue;
            SetFailure(new InvalidOperationException("上传失败：" + file.relativePath + " / " + message));
            return ESEditorLongTaskStepResult.Fail;
        }

        protected override void OnFinish()
        {
            operation?.Cancel();
            onFinished?.Invoke(new ESAssetReleaseUploadResult
            {
                IsSuccess = Status == ESEditorLongTaskStatus.Succeeded,
                UploadedFileCount = index,
                Message = Status == ESEditorLongTaskStatus.Succeeded ? "上传流程完成；根发布清单已在最后处理。" : "上传流程未完成；根发布清单没有被提前处理。",
                Errors = LastError == null ? Array.Empty<string>() : new[] { LastError.Message }
            });
        }
    }

    public static class ESAssetReleaseUploadProviderFactory
    {
        private static readonly Dictionary<ESAssetReleaseUploadMode, IESAssetReleaseUploadProvider> Providers = new Dictionary<ESAssetReleaseUploadMode, IESAssetReleaseUploadProvider>
        {
            { ESAssetReleaseUploadMode.ManualPlan, new ESManualReleaseUploadProvider() },
            { ESAssetReleaseUploadMode.AliyunOss, new ESAliyunOssReleaseUploadProvider() }
        };

        public static void Register(IESAssetReleaseUploadProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            Providers[provider.Mode] = provider;
        }

        public static IESAssetReleaseUploadProvider Get(ESAssetReleaseUploadMode mode)
        {
            if (Providers.TryGetValue(mode, out IESAssetReleaseUploadProvider provider)) return provider;
            throw new NotSupportedException("尚未安装发布目标适配器：" + mode + "。当前不会进行任何网络上传。");
        }
    }
}
