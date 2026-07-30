using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using HybridCLR;

namespace ES
{
    internal sealed class ESCodeModuleLoadResult
    {
        public IReadOnlyList<string> LoadedEnvironmentFiles { get; internal set; }
        public IReadOnlyList<string> LoadedCodeModules { get; internal set; }
        public bool IsEditorCheck { get; internal set; }
    }

    internal static class ESCodeModuleRuntime
    {
        private static readonly Dictionary<string, string> LoadedPackageHashes = new Dictionary<string, string>(StringComparer.Ordinal);

        public static async UniTask<ESCodeModuleLoadResult> LoadAsync(IEnumerable<ESRuntimeDownloadedCodePackage> packages, CancellationToken cancellationToken = default)
        {
            ESRuntimeDownloadedCodePackage[] orderedPackages = (packages ?? Enumerable.Empty<ESRuntimeDownloadedCodePackage>())
                .Where(item => item != null)
                .OrderBy(item => item.LoadOrder)
                .ThenBy(item => item.PackageKey, StringComparer.Ordinal)
                .ToArray();
            var loadedMetadata = new List<string>();
            var loadedModuleNames = new List<string>();

            foreach (ESRuntimeDownloadedCodePackage package in orderedPackages.Where(item => IsKind(item, ESConsumerCodePackageKind.AotMetadata)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsAlreadyLoaded(package)) continue;
                byte[] bytes = await UniTask.RunOnThreadPool(() => File.ReadAllBytes(package.LocalPath), cancellationToken: cancellationToken);
                await UniTask.SwitchToMainThread(cancellationToken);
                LoadImageErrorCode result = RuntimeApi.LoadMetadataForAOTAssembly(bytes, HomologousImageMode.SuperSet);
                if (result != LoadImageErrorCode.OK && result != LoadImageErrorCode.HOMOLOGOUS_ASSEMBLY_HAS_LOADED)
                    throw new InvalidOperationException("代码运行环境初始化失败。文件：" + package.PackageKey + "，错误码：" + result);
                RememberLoaded(package);
                loadedMetadata.Add(package.PackageKey);
            }

            foreach (ESRuntimeDownloadedCodePackage package in orderedPackages.Where(item => IsKind(item, ESConsumerCodePackageKind.HotUpdateAssembly)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsAlreadyLoaded(package)) continue;
#if UNITY_EDITOR
                // Editor/Mono 可以安全读取 DLL 元数据；程序集通常已由 Unity 编译域加载，
                // 此处只验证发布包对应的程序集确实存在。
                string assemblyName = AssemblyName.GetAssemblyName(package.LocalPath).Name;
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(item => string.Equals(item.GetName().Name, assemblyName, StringComparison.Ordinal));
                if (assembly == null)
                    throw new InvalidOperationException("代码模块尚未完成编译：" + assemblyName);
#else
                // IL2CPP 不支持 AssemblyName.GetAssemblyName(path) 对应的 InternalGetAssemblyName icall。
                // HybridCLR Player 必须直接从已校验的 DLL 字节加载，再从返回的 Assembly 获取名称。
                byte[] bytes = await UniTask.RunOnThreadPool(() => File.ReadAllBytes(package.LocalPath), cancellationToken: cancellationToken);
                await UniTask.SwitchToMainThread(cancellationToken);
                Assembly assembly = Assembly.Load(bytes);
                if (assembly == null)
                    throw new InvalidOperationException("代码模块加载失败：" + package.PackageKey);
#endif
                RememberLoaded(package);
                loadedModuleNames.Add(assembly.GetName().Name);
            }

            EnsureFrameworkRuntimeBridgeRegistered();

            return new ESCodeModuleLoadResult
            {
                LoadedEnvironmentFiles = loadedMetadata,
                LoadedCodeModules = loadedModuleNames,
#if UNITY_EDITOR
                IsEditorCheck = true
#else
                IsEditorCheck = false
#endif
            };
        }

        private static bool IsKind(ESRuntimeDownloadedCodePackage package, ESConsumerCodePackageKind kind)
        {
            return string.Equals(package.Kind, kind.ToString(), StringComparison.Ordinal);
        }

        private static bool IsAlreadyLoaded(ESRuntimeDownloadedCodePackage package)
        {
            if (!LoadedPackageHashes.TryGetValue(package.PackageKey, out string loadedHash)) return false;
            if (!string.Equals(loadedHash, package.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("代码模块版本已变化，请重新启动应用：" + package.PackageKey);
            return true;
        }

        private static void RememberLoaded(ESRuntimeDownloadedCodePackage package)
        {
            LoadedPackageHashes[package.PackageKey] = package.Sha256;
        }

        private static void EnsureFrameworkRuntimeBridgeRegistered()
        {
            if (ESResBootstrapRuntimeBridge.IsRegistered) return;

            const string bridgeTypeName = "ES.ESRuntimeDataBootstrapBridge";
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type bridgeType = assembly.GetType(bridgeTypeName, false);
                if (bridgeType == null) continue;
                MethodInfo method = bridgeType.GetMethod("EnsureRegistered", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null)
                    throw new MissingMethodException(bridgeTypeName, "EnsureRegistered");
                try
                {
                    method.Invoke(null, null);
                }
                catch (TargetInvocationException exception)
                {
                    throw new InvalidOperationException("ESLogic 运行时资源桥注册失败。", exception.InnerException ?? exception);
                }
                break;
            }

            if (!ESResBootstrapRuntimeBridge.IsRegistered)
                throw new InvalidOperationException("ESLogic 已下载，但未找到运行时资源初始化入口：" + bridgeTypeName);
        }
    }

    public static class ESRuntimeReleaseBootstrap
    {
        /// <summary>Internal framework bridge for an on-demand Consumer. Business code should
        /// enter through ESRuntimeDataModule.EnsureConsumerAvailableAsync instead.</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static UniTask InitializeAdditionalCodePackagesAsync(IEnumerable<ESRuntimeDownloadedCodePackage> packages, CancellationToken cancellationToken = default)
        {
            ESRuntimeCodePackageBootstrap.Register(async (items, token) => await ESCodeModuleRuntime.LoadAsync(items, token));
            return ESRuntimeCodePackageBootstrap.LoadAsync(packages, cancellationToken);
        }

        public static async UniTask<ESRuntimeReleaseDownloadResult> InitializeAsync(ESGlobalResSetting settings, CancellationToken cancellationToken = default)
        {
            ESAssetRunMode runMode = ESAssetRunModeSession.Lock(settings);
            if (runMode != ESAssetRunMode.LocalBuild && runMode != ESAssetRunMode.HotUpdate)
                throw new InvalidOperationException($"代码/资源发布启动只支持 LocalBuild 或 HotUpdate，当前模式为 {runMode}。");

            ESRuntimeCodePackageBootstrap.Register(async (packages, token) =>
            {
                await ESCodeModuleRuntime.LoadAsync(packages, token);
            });
            var downloader = new ESRuntimeReleaseDownloader(settings, runMode);
            return await InitializeAsync(settings, downloader, cancellationToken);
        }

        /// <summary>供 Bootstrap UI 注入同一个下载器，以获得完整进度与重试状态反馈。</summary>
        public static async UniTask<ESRuntimeReleaseDownloadResult> InitializeAsync(
            ESGlobalResSetting settings,
            ESRuntimeReleaseDownloader downloader,
            CancellationToken cancellationToken = default)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (downloader == null) throw new ArgumentNullException(nameof(downloader));
            ESAssetRunMode runMode = ESAssetRunModeSession.Lock(settings);
            if (runMode != ESAssetRunMode.LocalBuild && runMode != ESAssetRunMode.HotUpdate)
                throw new InvalidOperationException($"代码/资源发布启动只支持 LocalBuild 或 HotUpdate，当前模式为 {runMode}。");

            ESRuntimeCodePackageBootstrap.Register(async (packages, token) =>
            {
                await ESCodeModuleRuntime.LoadAsync(packages, token);
            });
            return await downloader.DownloadBootAndInitializeCodeAsync(cancellationToken);
        }
    }
}
