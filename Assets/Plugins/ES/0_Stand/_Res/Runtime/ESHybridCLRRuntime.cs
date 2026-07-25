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
                string assemblyName = AssemblyName.GetAssemblyName(package.LocalPath).Name;
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(item => string.Equals(item.GetName().Name, assemblyName, StringComparison.Ordinal));
                if (assembly == null)
                {
#if UNITY_EDITOR
                    throw new InvalidOperationException("代码模块尚未完成编译：" + assemblyName);
#else
                    byte[] bytes = await UniTask.RunOnThreadPool(() => File.ReadAllBytes(package.LocalPath), cancellationToken: cancellationToken);
                    await UniTask.SwitchToMainThread(cancellationToken);
                    assembly = Assembly.Load(bytes);
#endif
                }
                RememberLoaded(package);
                loadedModuleNames.Add(assembly.GetName().Name);
            }

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
    }

    public static class ESRuntimeReleaseBootstrap
    {
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
            return await downloader.DownloadBootAndInitializeCodeAsync(cancellationToken);
        }
    }
}
