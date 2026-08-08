using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ES.EditorInternal;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 资产收集、ConfigKey 查询和 ResourcePlan 构建前检查的统一入口。
    /// 目标是让“资产在哪里、对应哪个 Key、是否已烘焙”在一个窗口内完成确认。
    /// </summary>
    public sealed class ESResourceCollectionWorkflowWindow : EditorWindow
    {
        private struct PlanField
        {
            public string FieldName;
            public ESAssetReferKind Kind;
            public string DisplayName;

            public PlanField(string fieldName, ESAssetReferKind kind, string displayName)
            {
                FieldName = fieldName;
                Kind = kind;
                DisplayName = displayName;
            }
        }

        private sealed class Issue
        {
            public UnityEngine.Object Owner;
            public string Title;
            public string Message;
            public bool Error;
        }

        private enum PipelineStageState
        {
            Missing,
            Invalid,
            Partial,
            Passed
        }

        private sealed class PipelineStageReport
        {
            public readonly string Title;
            public readonly PipelineStageState State;
            public readonly int ValidCount;
            public readonly int TotalCount;
            public readonly List<string> Issues;

            public bool IsPassed => State == PipelineStageState.Passed;

            public string StateText
            {
                get
                {
                    switch (State)
                    {
                        case PipelineStageState.Passed: return "已通过";
                        case PipelineStageState.Partial: return "部分成功";
                        case PipelineStageState.Invalid: return "无效";
                        default: return "缺失";
                    }
                }
            }

            public string Detail
            {
                get
                {
                    string count = TotalCount > 0 ? ValidCount + "/" + TotalCount : "0";
                    string firstIssue = Issues != null && Issues.Count > 0 ? " · " + Issues[0] : string.Empty;
                    return StateText + " · " + count + firstIssue;
                }
            }

            public PipelineStageReport(string title, PipelineStageState state, int validCount, int totalCount, List<string> issues)
            {
                Title = title;
                State = state;
                ValidCount = validCount;
                TotalCount = totalCount;
                Issues = issues ?? new List<string>();
            }

            public static PipelineStageReport Missing(string title, string reason)
            {
                var issues = new List<string>();
                if (!string.IsNullOrWhiteSpace(reason))
                    issues.Add(reason);
                return new PipelineStageReport(title, PipelineStageState.Missing, 0, 0, issues);
            }
        }

        private sealed class PipelineStageSnapshot
        {
            public PipelineStageReport Catalog;
            public PipelineStageReport Plan;
            public PipelineStageReport BundleManifest;
            public PipelineStageReport LocalRelease;
        }

        private sealed class IntegrityCacheEntry
        {
            public long Length;
            public DateTime LastWriteUtc;
            public string ExpectedSha256;
            public bool Valid;
        }

        /// <summary>
        /// 首屏状态只允许反映结构、协议、依赖和文件完整性均通过的产物。
        /// 文件存在本身不能作为阶段完成证据。
        /// </summary>
        private static class PipelineStageValidator
        {
            private const int CatalogFormatVersion = 3;
            private const int BuildPlanFormatVersion = 2;
            private const int MaxReportedIssues = 8;
            private static readonly Dictionary<string, IntegrityCacheEntry> IntegrityCache = new Dictionary<string, IntegrityCacheEntry>(StringComparer.OrdinalIgnoreCase);

            public static PipelineStageSnapshot Evaluate(string platform)
            {
                return new PipelineStageSnapshot
                {
                    Catalog = ValidateCatalogs(),
                    Plan = ValidatePlan(platform),
                    BundleManifest = ValidateBundleManifests(platform),
                    LocalRelease = ValidateLocalRelease(platform)
                };
            }

            private static PipelineStageReport ValidateCatalogs()
            {
                const string title = "Catalog";
                var issues = new List<string>();
                List<string> files = EnumerateFiles(ESAssetPipelineIO.BakeRoot, ESAssetPipelineIO.CatalogFileName);
                if (files.Count == 0)
                    return PipelineStageReport.Missing(title, "未发现烘焙 Catalog，请执行“烘焙引用”。");

                int valid = 0;
                int fatal = 0;
                int warnings = 0;
                foreach (string path in files.OrderBy(item => item, StringComparer.Ordinal))
                {
                    bool structurallyValid = true;
                    try
                    {
                        ESAssetLibraryCatalog catalog = ESAssetPipelineIO.ReadJson<ESAssetLibraryCatalog>(path);
                        if (catalog == null)
                        {
                            structurallyValid = false;
                            fatal++;
                            AddIssue(issues, path + "：内容为空。");
                            continue;
                        }
                        if (catalog.formatVersion != CatalogFormatVersion)
                        {
                            structurallyValid = false;
                            fatal++;
                            AddIssue(issues, path + "：Catalog 协议版本为 " + catalog.formatVersion + "，当前要求 " + CatalogFormatVersion + "。");
                        }
                        if (string.IsNullOrWhiteSpace(catalog.libraryName)
                            || string.IsNullOrWhiteSpace(catalog.libraryFolder)
                            || string.IsNullOrWhiteSpace(catalog.libraryBundleCode)
                            || string.IsNullOrWhiteSpace(catalog.libraryAssetGuid))
                        {
                            structurallyValid = false;
                            fatal++;
                            AddIssue(issues, path + "：Library 身份字段不完整。");
                        }
                        if (catalog.assets == null)
                        {
                            structurallyValid = false;
                            fatal++;
                            AddIssue(issues, path + "：assets 字段为空。");
                        }
                        else
                        {
                            var identities = new HashSet<string>(StringComparer.Ordinal);
                            foreach (ESAssetCatalogEntry entry in catalog.assets)
                            {
                                if (entry == null || entry.identity == null || !entry.identity.IsValid
                                    || string.IsNullOrWhiteSpace(entry.assetPath)
                                    || string.IsNullOrWhiteSpace(entry.libraryFolder))
                                {
                                    structurallyValid = false;
                                    fatal++;
                                    AddIssue(issues, path + "：存在无效 Catalog 资产条目。");
                                    continue;
                                }
                                if (!identities.Add(entry.identity.Key))
                                {
                                    structurallyValid = false;
                                    fatal++;
                                    AddIssue(issues, path + "：存在重复资产身份 " + entry.identity.Key + "。");
                                }
                            }
                            if (catalog.assets.Count == 0)
                            {
                                warnings++;
                                AddIssue(issues, path + "：Catalog 没有资产条目。");
                            }
                        }
                        if (catalog.errors != null && catalog.errors.Count > 0)
                        {
                            structurallyValid = false;
                            fatal++;
                            AddIssue(issues, path + "：包含 " + catalog.errors.Count + " 条烘焙错误。");
                        }
                        if (catalog.warnings != null && catalog.warnings.Count > 0)
                            warnings++;
                    }
                    catch (Exception exception)
                    {
                        structurallyValid = false;
                        fatal++;
                        AddIssue(issues, path + "：无法解析（" + exception.Message + "）。");
                    }

                    if (structurallyValid)
                        valid++;
                }
                return CreateReport(title, valid, files.Count, fatal, warnings, issues);
            }

            private static PipelineStageReport ValidatePlan(string platform)
            {
                const string title = "BuildPlan";
                string planPath = Path.Combine(ESAssetPipelineIO.PlanRoot(platform), ESAssetPipelineIO.PlanFileName);
                string assetListPath = Path.Combine(ESAssetPipelineIO.PlanRoot(platform), ESAssetPipelineIO.AssetListFileName);
                if (!File.Exists(planPath) || !File.Exists(assetListPath))
                    return PipelineStageReport.Missing(title, "BuildPlan 或 AssetList 缺失，请先完成“规划并标记 AB”。");

                var issues = new List<string>();
                int fatal = 0;
                int warnings = 0;
                try
                {
                    ESAssetBundleBuildPlan plan = ESAssetPipelineIO.ReadJson<ESAssetBundleBuildPlan>(planPath);
                    ESAssetBundleAssetList assetList = ESAssetPipelineIO.ReadJson<ESAssetBundleAssetList>(assetListPath);
                    if (plan == null || assetList == null)
                    {
                        fatal++;
                        AddIssue(issues, "BuildPlan 或 AssetList 内容为空。");
                    }
                    else
                    {
                        if (plan.formatVersion != BuildPlanFormatVersion || assetList.formatVersion != BuildPlanFormatVersion)
                        {
                            fatal++;
                            AddIssue(issues, "BuildPlan/AssetList 协议版本不是当前 v2。");
                        }
                        if (!string.Equals(plan.platform, platform, StringComparison.Ordinal)
                            || !string.Equals(assetList.platform, platform, StringComparison.Ordinal))
                        {
                            fatal++;
                            AddIssue(issues, "BuildPlan/AssetList 平台与当前平台不一致。");
                        }
                        if (plan.errors != null && plan.errors.Count > 0)
                        {
                            fatal++;
                            AddIssue(issues, "BuildPlan 包含 " + plan.errors.Count + " 条错误。");
                        }
                        if (plan.warnings != null && plan.warnings.Count > 0)
                        {
                            warnings++;
                            AddIssue(issues, "BuildPlan 包含 " + plan.warnings.Count + " 条警告。");
                        }
                        if (plan.assignments == null || assetList.assets == null)
                        {
                            fatal++;
                            AddIssue(issues, "BuildPlan assignments 或 AssetList assets 为空。");
                        }
                        else
                        {
                            if (plan.assignments.Count == 0)
                            {
                                warnings++;
                                AddIssue(issues, "BuildPlan 没有待构建资产。");
                            }
                            var assignmentByPath = new Dictionary<string, ESAssetBundleAssignment>(StringComparer.Ordinal);
                            foreach (ESAssetBundleAssignment assignment in plan.assignments)
                            {
                                if (assignment == null || string.IsNullOrWhiteSpace(assignment.assetPath) || string.IsNullOrWhiteSpace(assignment.assetBundleKey))
                                {
                                    fatal++;
                                    AddIssue(issues, "BuildPlan 存在无效 assignment。");
                                    continue;
                                }
                                if (!assignmentByPath.TryAdd(assignment.assetPath, assignment))
                                {
                                    fatal++;
                                    AddIssue(issues, "BuildPlan 存在重复资产路径：" + assignment.assetPath);
                                }
                            }
                            var businessIdentities = new HashSet<string>(StringComparer.Ordinal);
                            foreach (ESAssetBundleAssetEntry asset in assetList.assets)
                            {
                                if (asset == null || asset.identity == null || !asset.identity.IsValid
                                    || string.IsNullOrWhiteSpace(asset.internalName)
                                    || string.IsNullOrWhiteSpace(asset.assetBundleKey))
                                {
                                    fatal++;
                                    AddIssue(issues, "AssetList 存在无效资源条目。");
                                    continue;
                                }
                                if (asset.isBusinessAsset && !businessIdentities.Add(asset.identity.Key))
                                {
                                    fatal++;
                                    AddIssue(issues, "AssetList 存在重复业务资产身份：" + asset.identity.Key);
                                }
                                if (!assignmentByPath.TryGetValue(asset.internalName, out ESAssetBundleAssignment assignment)
                                    || !string.Equals(assignment.assetBundleKey, asset.assetBundleKey, StringComparison.Ordinal))
                                {
                                    fatal++;
                                    AddIssue(issues, "AssetList 与 BuildPlan 不一致：" + asset.internalName);
                                }
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    fatal++;
                    AddIssue(issues, "无法解析 BuildPlan/AssetList（" + exception.Message + "）。");
                }
                return CreateReport(title, fatal == 0 ? 1 : 0, 1, fatal, warnings, issues);
            }

            private static PipelineStageReport ValidateBundleManifests(string platform)
            {
                const string title = "Bundle Manifest";
                List<string> files = EnumerateFiles(ESAssetPipelineIO.StagingLibrariesRoot(platform), ESAssetPipelineIO.BundleManifestFileName);
                if (files.Count == 0)
                    return PipelineStageReport.Missing(title, "未发现 Staging Bundle Manifest，请先执行“构建资源包”。");

                var issues = new List<string>();
                int valid = 0;
                int fatal = 0;
                int warnings = 0;
                foreach (string path in files.OrderBy(item => item, StringComparer.Ordinal))
                {
                    bool structurallyValid = true;
                    string stageFolder = Path.GetDirectoryName(path) ?? string.Empty;
                    try
                    {
                        ESAssetBundleManifest manifest = ESAssetPipelineIO.ReadJson<ESAssetBundleManifest>(path);
                        if (manifest == null || manifest.formatVersion != ESAssetPipelineIO.RuntimeProtocolFormatVersion)
                        {
                            structurallyValid = false;
                            fatal++;
                            AddIssue(issues, path + "：Bundle Manifest 不是当前 v5 协议。");
                        }
                        if (manifest == null || !string.Equals(manifest.platform, platform, StringComparison.Ordinal)
                            || string.IsNullOrWhiteSpace(manifest.libraryName))
                        {
                            structurallyValid = false;
                            fatal++;
                            AddIssue(issues, path + "：平台或 Library 身份不完整。");
                        }
                        if (manifest == null || manifest.assetBundles == null)
                        {
                            structurallyValid = false;
                            fatal++;
                            AddIssue(issues, path + "：assetBundles 字段为空。");
                        }
                        else
                        {
                            var bundleKeys = new HashSet<string>(StringComparer.Ordinal);
                            foreach (ESAssetBundleRecord bundle in manifest.assetBundles)
                            {
                                if (!ValidateStagingBundleRecord(stageFolder, bundle, bundleKeys, issues))
                                {
                                    structurallyValid = false;
                                    fatal++;
                                }
                            }
                            if (manifest.assetBundles.Count == 0)
                            {
                                warnings++;
                                AddIssue(issues, path + "：Manifest 没有 AssetBundle 条目。");
                            }
                        }

                        string identityPath = Path.Combine(stageFolder, ESAssetPipelineIO.LibraryIdentityFileName);
                        string catalogPath = Path.Combine(stageFolder, ESAssetPipelineIO.CatalogFileName);
                        ESAssetLibraryIdentity identity = ESAssetPipelineIO.ReadJson<ESAssetLibraryIdentity>(identityPath);
                        ESAssetLibraryCatalog catalog = ESAssetPipelineIO.ReadJson<ESAssetLibraryCatalog>(catalogPath);
                        if (identity == null || identity.formatVersion != ESAssetPipelineIO.RuntimeProtocolFormatVersion
                            || !string.Equals(identity.platform, platform, StringComparison.Ordinal)
                            || string.IsNullOrWhiteSpace(identity.libraryFolder))
                        {
                            structurallyValid = false;
                            fatal++;
                            AddIssue(issues, stageFolder + "：Library Identity 无效或不是 v5。");
                        }
                        if (catalog == null || catalog.formatVersion != CatalogFormatVersion || catalog.errors == null || catalog.errors.Count > 0)
                        {
                            structurallyValid = false;
                            fatal++;
                            AddIssue(issues, stageFolder + "：发布候选中的 Catalog 无效或包含错误。");
                        }
                        if (identity != null && !VerifyIntegrity(catalogPath, identity.catalogSha256))
                        {
                            structurallyValid = false;
                            fatal++;
                            AddIssue(issues, stageFolder + "：Catalog Hash 不匹配。");
                        }
                        if (identity != null && !VerifyIntegrity(path, identity.assetBundleManifestSha256))
                        {
                            structurallyValid = false;
                            fatal++;
                            AddIssue(issues, path + "：Bundle Manifest Hash 不匹配。");
                        }
                    }
                    catch (Exception exception)
                    {
                        structurallyValid = false;
                        fatal++;
                        AddIssue(issues, path + "：无法完成完整性检查（" + exception.Message + "）。");
                    }

                    if (structurallyValid)
                        valid++;
                }
                return CreateReport(title, valid, files.Count, fatal, warnings, issues);
            }

            private static PipelineStageReport ValidateLocalRelease(string platform)
            {
                const string title = "Local Release";
                string root = ESAssetPipelineIO.LocalTestRoot(platform);
                string rootManifestPath = Path.Combine(root, ESAssetPipelineIO.ReleaseManifestFileName);
                if (!File.Exists(rootManifestPath))
                    return PipelineStageReport.Missing(title, "未发现本地 Root Manifest，请先执行“发布资源包”。");

                var issues = new List<string>();
                int fatal = 0;
                int warnings = 0;
                try
                {
                    ESAssetReleaseManifest release = ESAssetPipelineIO.ReadJson<ESAssetReleaseManifest>(rootManifestPath);
                    if (release == null || release.formatVersion != ESAssetPipelineIO.RuntimeProtocolFormatVersion)
                    {
                        fatal++;
                        AddIssue(issues, "Root Manifest 不是当前 v5 协议。");
                    }
                    if (release == null || !string.Equals(release.platform, platform, StringComparison.Ordinal)
                        || string.IsNullOrWhiteSpace(release.releaseVersion))
                    {
                        fatal++;
                        AddIssue(issues, "Root Manifest 的平台或 releaseVersion 无效。");
                    }
                    if (release == null || release.libraries == null || release.libraries.Count == 0)
                    {
                        fatal++;
                        AddIssue(issues, "Root Manifest 没有 Library 发布记录。");
                    }
                    if (release == null || string.IsNullOrWhiteSpace(release.bundleIndexSha256)
                        || string.IsNullOrWhiteSpace(release.totalConsumerSha256))
                    {
                        fatal++;
                        AddIssue(issues, "Root Manifest 缺少 Bundle Index 或总 Consumer Hash。");
                    }

                    if (release != null && !string.IsNullOrWhiteSpace(release.releaseVersion))
                    {
                        string versionRoot = Path.Combine(root, release.releaseVersion);
                        string bundleIndexPath = Path.Combine(versionRoot, ESAssetPipelineIO.ReleaseBundleIndexFileName);
                        if (!File.Exists(bundleIndexPath))
                        {
                            fatal++;
                            AddIssue(issues, "Bundle Index 缺失：" + bundleIndexPath);
                        }
                        else
                        {
                            ESAssetReleaseBundleIndex bundleIndex = ESAssetPipelineIO.ReadJson<ESAssetReleaseBundleIndex>(bundleIndexPath);
                            if (bundleIndex == null || bundleIndex.formatVersion != ESAssetPipelineIO.RuntimeProtocolFormatVersion
                                || !string.Equals(bundleIndex.platform, platform, StringComparison.Ordinal)
                                || !string.Equals(bundleIndex.releaseVersion, release.releaseVersion, StringComparison.Ordinal))
                            {
                                fatal++;
                                AddIssue(issues, "Bundle Index 协议、平台或版本不一致。");
                            }
                            if (!VerifyIntegrity(bundleIndexPath, release.bundleIndexSha256))
                            {
                                fatal++;
                                AddIssue(issues, "Root Manifest 引用的 Bundle Index Hash 不匹配。");
                            }

                            var indexedBundles = new HashSet<string>(StringComparer.Ordinal);
                            if (bundleIndex?.assetBundles == null)
                            {
                                fatal++;
                                AddIssue(issues, "Bundle Index 没有 assetBundles。");
                            }
                            else
                            {
                                foreach (ESAssetReleaseBundleRecord bundle in bundleIndex.assetBundles)
                                {
                                    if (!ValidateReleaseBundleRecord(root, release.releaseVersion, bundle, indexedBundles, issues))
                                        fatal++;
                                }
                                if (bundleIndex.assetBundles.Count == 0)
                                {
                                    warnings++;
                                    AddIssue(issues, "Bundle Index 没有资源包条目。");
                                }
                            }

                            string librariesRoot = Path.Combine(versionRoot, ESAssetPipelineIO.LibrariesFolderName);
                            List<string> identityFiles = EnumerateFiles(librariesRoot, ESAssetPipelineIO.LibraryIdentityFileName);
                            var libraryNames = new HashSet<string>(StringComparer.Ordinal);
                            var manifestBundleKeys = new HashSet<string>(StringComparer.Ordinal);
                            foreach (string identityPath in identityFiles.OrderBy(item => item, StringComparer.Ordinal))
                            {
                                bool libraryValid = true;
                                string libraryFolder = Path.GetFileName(Path.GetDirectoryName(identityPath) ?? string.Empty);
                                try
                                {
                                    ESAssetLibraryIdentity identity = ESAssetPipelineIO.ReadJson<ESAssetLibraryIdentity>(identityPath);
                                    string libraryRoot = Path.GetDirectoryName(identityPath) ?? string.Empty;
                                    ESAssetBundleManifest manifest = ESAssetPipelineIO.ReadJson<ESAssetBundleManifest>(Path.Combine(libraryRoot, ESAssetPipelineIO.BundleManifestFileName));
                                    ESAssetLibraryCatalog catalog = ESAssetPipelineIO.ReadJson<ESAssetLibraryCatalog>(Path.Combine(libraryRoot, ESAssetPipelineIO.CatalogFileName));
                                    if (identity == null || identity.formatVersion != ESAssetPipelineIO.RuntimeProtocolFormatVersion
                                        || !string.Equals(identity.platform, platform, StringComparison.Ordinal)
                                        || !string.Equals(identity.libraryFolder, libraryFolder, StringComparison.Ordinal))
                                    {
                                        libraryValid = false;
                                        fatal++;
                                        AddIssue(issues, libraryRoot + "：Library Identity 无效。");
                                    }
                                    if (manifest == null || manifest.formatVersion != ESAssetPipelineIO.RuntimeProtocolFormatVersion
                                        || !string.Equals(manifest.platform, platform, StringComparison.Ordinal))
                                    {
                                        libraryValid = false;
                                        fatal++;
                                        AddIssue(issues, libraryRoot + "：Bundle Manifest 无效。");
                                    }
                                    if (catalog == null || catalog.formatVersion != CatalogFormatVersion
                                        || catalog.errors == null || catalog.errors.Count > 0)
                                    {
                                        libraryValid = false;
                                        fatal++;
                                        AddIssue(issues, libraryRoot + "：Catalog 无效或包含错误。");
                                    }
                                    if (identity != null)
                                    {
                                        if (!VerifyIntegrity(Path.Combine(libraryRoot, ESAssetPipelineIO.CatalogFileName), identity.catalogSha256))
                                        {
                                            libraryValid = false;
                                            fatal++;
                                            AddIssue(issues, libraryRoot + "：Catalog Hash 不匹配。");
                                        }
                                        if (!VerifyIntegrity(Path.Combine(libraryRoot, ESAssetPipelineIO.BundleManifestFileName), identity.assetBundleManifestSha256))
                                        {
                                            libraryValid = false;
                                            fatal++;
                                            AddIssue(issues, libraryRoot + "：Bundle Manifest Hash 不匹配。");
                                        }
                                    }
                                    if (manifest?.assetBundles != null)
                                    {
                                        var localKeys = new HashSet<string>(StringComparer.Ordinal);
                                        foreach (ESAssetBundleRecord bundle in manifest.assetBundles)
                                        {
                                            if (!ValidateStagingBundleRecord(libraryRoot, bundle, localKeys, issues))
                                            {
                                                libraryValid = false;
                                                fatal++;
                                            }
                                            if (bundle != null && !string.IsNullOrWhiteSpace(bundle.assetBundleKey))
                                                manifestBundleKeys.Add(libraryFolder + "|" + bundle.assetBundleKey);
                                        }
                                    }
                                    if (!string.IsNullOrWhiteSpace(identity?.libraryName))
                                        libraryNames.Add(identity.libraryName);
                                }
                                catch (Exception exception)
                                {
                                    libraryValid = false;
                                    fatal++;
                                    AddIssue(issues, identityPath + "：无法完成 Library 校验（" + exception.Message + "）。");
                                }
                                if (!libraryValid)
                                    continue;
                            }

                            if (release.libraries != null && release.libraries.Count != identityFiles.Count)
                            {
                                fatal++;
                                AddIssue(issues, "Root Manifest Library 数量与本地 Library 目录不一致。");
                            }
                            if (release.libraries != null && release.libraries.Any(item => item == null || string.IsNullOrWhiteSpace(item.libraryName) || !libraryNames.Contains(item.libraryName)))
                            {
                                fatal++;
                                AddIssue(issues, "Root Manifest 存在无法对应本地 Identity 的 Library。");
                            }
                            if (bundleIndex?.assetBundles != null)
                            {
                                var indexedKeys = new HashSet<string>(bundleIndex.assetBundles
                                    .Where(item => item != null && !string.IsNullOrWhiteSpace(item.libraryFolder) && !string.IsNullOrWhiteSpace(item.assetBundleKey))
                                    .Select(item => item.libraryFolder + "|" + item.assetBundleKey), StringComparer.Ordinal);
                                if (!manifestBundleKeys.SetEquals(indexedKeys))
                                {
                                    fatal++;
                                    AddIssue(issues, "Bundle Index 与 Library Manifest 的 Bundle 闭包不一致。");
                                }
                            }

                            string consumersRoot = Path.Combine(versionRoot, "Consumers");
                            bool totalConsumerFound = false;
                            foreach (string consumerPath in EnumerateFiles(consumersRoot, "*.json"))
                            {
                                try
                                {
                                    ESAssetConsumerManifest consumer = ESAssetPipelineIO.ReadJson<ESAssetConsumerManifest>(consumerPath);
                                    if (consumer != null && consumer.formatVersion == ESAssetPipelineIO.RuntimeProtocolFormatVersion
                                        && consumer.isTotalConsumer && VerifyIntegrity(consumerPath, release.totalConsumerSha256))
                                    {
                                        totalConsumerFound = true;
                                        break;
                                    }
                                }
                                catch (Exception exception)
                                {
                                    warnings++;
                                    AddIssue(issues, consumerPath + "：Consumer 无法解析（" + exception.Message + "）。");
                                }
                            }
                            if (!totalConsumerFound)
                            {
                                fatal++;
                                AddIssue(issues, "Root Manifest 引用的总 Consumer 缺失、协议无效或 Hash 不匹配。");
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    fatal++;
                    AddIssue(issues, "无法解析本地 Release（" + exception.Message + "）。");
                }
                return CreateReport(title, fatal == 0 ? 1 : 0, 1, fatal, warnings, issues);
            }

            private static bool ValidateStagingBundleRecord(string root, ESAssetBundleRecord bundle, HashSet<string> bundleKeys, List<string> issues)
            {
                if (bundle == null || string.IsNullOrWhiteSpace(bundle.assetBundleKey)
                    || string.IsNullOrWhiteSpace(bundle.fileName)
                    || string.IsNullOrWhiteSpace(bundle.localRelativePath)
                    || bundle.size <= 0 || !IsSha256(bundle.sha256))
                {
                    AddIssue(issues, root + "：存在字段不完整的 Bundle 记录。");
                    return false;
                }
                if (!bundleKeys.Add(bundle.assetBundleKey))
                {
                    AddIssue(issues, root + "：重复 AssetBundleKey：" + bundle.assetBundleKey);
                    return false;
                }
                if (bundle.dependencies != null)
                {
                    var dependencies = new HashSet<string>(StringComparer.Ordinal);
                    foreach (string dependency in bundle.dependencies)
                    {
                        if (string.Equals(dependency, bundle.assetBundleKey, StringComparison.Ordinal) || !dependencies.Add(dependency))
                        {
                            AddIssue(issues, root + "：Bundle 依赖存在自依赖或重复：" + bundle.assetBundleKey);
                            return false;
                        }
                    }
                }
                try
                {
                    string filePath = ESAssetPipelineIO.ResolveGeneratedRelativePath(root, bundle.localRelativePath);
                    if (!File.Exists(filePath))
                    {
                        AddIssue(issues, "Bundle 文件缺失：" + filePath);
                        return false;
                    }
                    if (new FileInfo(filePath).Length != bundle.size || !VerifyIntegrity(filePath, bundle.sha256))
                    {
                        AddIssue(issues, "Bundle 文件 Size/Hash 不匹配：" + filePath);
                        return false;
                    }
                }
                catch (Exception exception)
                {
                    AddIssue(issues, root + "：Bundle 路径无效（" + exception.Message + "）。");
                    return false;
                }
                return true;
            }

            private static bool ValidateReleaseBundleRecord(string root, string releaseVersion, ESAssetReleaseBundleRecord bundle, HashSet<string> bundleKeys, List<string> issues)
            {
                if (bundle == null || string.IsNullOrWhiteSpace(bundle.libraryFolder)
                    || string.IsNullOrWhiteSpace(bundle.assetBundleKey)
                    || string.IsNullOrWhiteSpace(bundle.localRelativePath)
                    || bundle.size <= 0 || !IsSha256(bundle.sha256))
                {
                    AddIssue(issues, "Release Bundle Index 存在字段不完整的 Bundle 记录。");
                    return false;
                }
                string key = bundle.libraryFolder + "|" + bundle.assetBundleKey;
                if (!bundleKeys.Add(key))
                {
                    AddIssue(issues, "Release Bundle Index 存在重复 Bundle：" + key);
                    return false;
                }
                try
                {
                    string libraryRoot = ESAssetPipelineIO.ReleaseLibraryFolder(root, string.Empty, releaseVersion, bundle.libraryFolder);
                    string filePath = ESAssetPipelineIO.ResolveGeneratedRelativePath(libraryRoot, bundle.localRelativePath);
                    if (!File.Exists(filePath))
                    {
                        AddIssue(issues, "发布 Bundle 文件缺失：" + filePath);
                        return false;
                    }
                    if (new FileInfo(filePath).Length != bundle.size || !VerifyIntegrity(filePath, bundle.sha256))
                    {
                        AddIssue(issues, "发布 Bundle 文件 Size/Hash 不匹配：" + filePath);
                        return false;
                    }
                }
                catch (Exception exception)
                {
                    AddIssue(issues, "发布 Bundle 路径无效（" + exception.Message + "）。");
                    return false;
                }
                return true;
            }

            private static List<string> EnumerateFiles(string root, string pattern)
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                    return new List<string>();
                try
                {
                    return ESManagedFileIO.EnumerateFilesSafely(root, pattern).ToList();
                }
                catch (IOException)
                {
                    return new List<string>();
                }
                catch (UnauthorizedAccessException)
                {
                    return new List<string>();
                }
            }

            private static PipelineStageReport CreateReport(string title, int valid, int total, int fatal, int warnings, List<string> issues)
            {
                PipelineStageState state;
                if (total <= 0)
                    state = PipelineStageState.Missing;
                else if (valid <= 0)
                    state = PipelineStageState.Invalid;
                else if (valid < total || fatal > 0 || warnings > 0)
                    state = PipelineStageState.Partial;
                else
                    state = PipelineStageState.Passed;
                return new PipelineStageReport(title, state, valid, total, issues);
            }

            private static void AddIssue(List<string> issues, string message)
            {
                if (issues == null || issues.Count >= MaxReportedIssues || string.IsNullOrWhiteSpace(message))
                    return;
                issues.Add(message);
            }

            private static bool IsSha256(string value)
            {
                if (string.IsNullOrWhiteSpace(value) || value.Trim().Length != 64)
                    return false;
                foreach (char character in value.Trim())
                {
                    bool hex = (character >= '0' && character <= '9')
                        || (character >= 'a' && character <= 'f')
                        || (character >= 'A' && character <= 'F');
                    if (!hex)
                        return false;
                }
                return true;
            }

            private static bool VerifyIntegrity(string path, string expectedSha256)
            {
                if (!File.Exists(path) || !IsSha256(expectedSha256))
                    return false;
                FileInfo info = new FileInfo(path);
                string expected = expectedSha256.Trim().ToLowerInvariant();
                if (IntegrityCache.TryGetValue(path, out IntegrityCacheEntry cached)
                    && cached.Length == info.Length
                    && cached.LastWriteUtc == info.LastWriteTimeUtc
                    && string.Equals(cached.ExpectedSha256, expected, StringComparison.Ordinal))
                    return cached.Valid;

                bool valid = ESResManifestIntegrity.VerifyFileSha256(path, expected);
                IntegrityCache[path] = new IntegrityCacheEntry
                {
                    Length = info.Length,
                    LastWriteUtc = info.LastWriteTimeUtc,
                    ExpectedSha256 = expected,
                    Valid = valid
                };
                return valid;
            }
        }

        private static readonly PlanField[] ResourcePlanFields =
        {
            new PlanField("prefabs", ESAssetReferKind.Prefab, "Prefab"),
            new PlanField("sprites", ESAssetReferKind.Sprite, "Sprite"),
            new PlanField("audioClips", ESAssetReferKind.AudioClip, "AudioClip"),
            new PlanField("animationClips", ESAssetReferKind.AnimationClip, "AnimationClip"),
            new PlanField("animatorControllers", ESAssetReferKind.AnimatorController, "AnimatorController"),
            new PlanField("materials", ESAssetReferKind.Material, "Material"),
            new PlanField("meshes", ESAssetReferKind.Mesh, "Mesh"),
            new PlanField("textures", ESAssetReferKind.Texture, "Texture"),
            new PlanField("rawAssets", ESAssetReferKind.Raw, "Raw"),
            new PlanField("texture2Ds", ESAssetReferKind.Texture2D, "Texture2D"),
            new PlanField("spriteAtlases", ESAssetReferKind.SpriteAtlas, "SpriteAtlas"),
            new PlanField("avatars", ESAssetReferKind.Avatar, "Avatar"),
            new PlanField("playableAssets", ESAssetReferKind.PlayableAsset, "PlayableAsset"),
            new PlanField("scriptableObjects", ESAssetReferKind.ScriptableObject, "ScriptableObject"),
            new PlanField("timelineAssets", ESAssetReferKind.TimelineAsset, "TimelineAsset"),
            new PlanField("videoClips", ESAssetReferKind.VideoClip, "VideoClip"),
            new PlanField("terrainDatas", ESAssetReferKind.TerrainData, "TerrainData")
        };

        [SerializeField] private UnityEngine.Object selectedAsset;
        [SerializeField] private ESResourcePlanInfo targetPlan;
        [SerializeField] private Vector2 scrollPosition;
        private readonly List<Issue> issues = new List<Issue>();
        private string scanSummary = "尚未扫描 ResourcePlan。";
        private string catalogSummary = "尚未检查 Catalog。";
        private string workflowStatus = string.Empty;
        private string issueSearch = string.Empty;
        private bool showWarnings = true;
        private double stageStatusExpiresAt;
        private PipelineStageReport cachedCatalogStage = PipelineStageReport.Missing("Catalog", "尚未检查 Catalog。");
        private PipelineStageReport cachedPlanStage = PipelineStageReport.Missing("BuildPlan", "尚未检查 BuildPlan。");
        private PipelineStageReport cachedBundleManifestStage = PipelineStageReport.Missing("Bundle Manifest", "尚未检查 Bundle Manifest。");
        private PipelineStageReport cachedLocalReleaseStage = PipelineStageReport.Missing("Local Release", "尚未检查本地 Release。");

        [MenuItem("【ES】/资源与发布/资源收集/资源收集工作流", false, 2201)]
        public static void Open()
        {
            ESResourceCollectionWorkflowWindow window = GetWindow<ESResourceCollectionWorkflowWindow>();
            window.titleContent = new GUIContent("ES收集与Key");
            window.minSize = new Vector2(640f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            SyncFromSelection();
            InvalidateStageStatus();
            Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            SyncFromSelection();
            Repaint();
        }

        private void SyncFromSelection()
        {
            if (Selection.activeObject is ESResourcePlanInfo selectedPlan)
            {
                targetPlan = selectedPlan;
                return;
            }
            if (Selection.activeObject != null)
                selectedAsset = Selection.activeObject;
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawProductSummary();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawWorkflowGuide();
            DrawSelectedAssetSection();
            DrawPipelineSection();
            DrawIssueSection();
            EditorGUILayout.EndScrollView();
        }

        private void DrawProductSummary()
        {
            RefreshStageStatusIfNeeded();
            ResolveWorkflowPhase(out string nextAction, out MessageType messageType);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("ES 资源生产工作流", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "从资产身份、Catalog、ResourcePlan 到本地发布检查，集中确认当前资源阶段和下一步动作。",
                    EditorStyles.miniLabel);
                EditorGUILayout.HelpBox(
                    "当前状态：" + workflowPhase + "\n下一步：" + nextAction,
                    messageType);
            }
        }

        private string workflowPhase = "等待开始";

        private void ResolveWorkflowPhase(out string nextAction, out MessageType messageType)
        {
            messageType = MessageType.Info;
            if (!string.IsNullOrWhiteSpace(workflowStatus) && workflowStatus.Contains("失败"))
            {
                workflowPhase = "存在需要处理的失败";
                nextAction = workflowStatus + "；向下滚动查看问题详情并使用“定位”修复。";
                messageType = MessageType.Error;
                return;
            }

            Issue blockingIssue = issues.FirstOrDefault(item => item != null && item.Error);
            if (blockingIssue != null)
            {
                int blockingCount = issues.Count(item => item != null && item.Error);
                workflowPhase = "发现 " + blockingCount + " 个阻断问题";
                nextAction = "向下滚动查看详情；先修复“" + blockingIssue.Title + "”，再重新检查。";
                messageType = MessageType.Error;
                return;
            }

            if (ResolveCollectionTarget() == null)
            {
                workflowPhase = "等待设置当前收集 Library";
                nextAction = "打开资源窗口，设置当前收集 Library。";
                messageType = MessageType.Warning;
                return;
            }

            if (selectedAsset == null)
            {
                workflowPhase = "等待选择资产";
                nextAction = "从 Project 选中或拖入一个可配置资源。";
                messageType = MessageType.Warning;
                return;
            }

            if (targetPlan == null)
            {
                workflowPhase = "等待选择 ResourcePlan";
                nextAction = "选择目标 ResourcePlan，再加入需要的资源。";
                messageType = MessageType.Warning;
                return;
            }

            if (!cachedCatalogStage.IsPassed)
            {
                workflowPhase = "Catalog：" + cachedCatalogStage.StateText;
                nextAction = cachedCatalogStage.Detail + "；先确认资产已收集，再执行“烘焙引用”。";
                messageType = cachedCatalogStage.State == PipelineStageState.Invalid ? MessageType.Error : MessageType.Warning;
                return;
            }

            if (!cachedPlanStage.IsPassed)
            {
                workflowPhase = "BuildPlan：" + cachedPlanStage.StateText;
                nextAction = cachedPlanStage.Detail + "；检查烘焙结果后执行“规划并标记 AB”。";
                messageType = cachedPlanStage.State == PipelineStageState.Invalid ? MessageType.Error : MessageType.Warning;
                return;
            }

            if (!cachedBundleManifestStage.IsPassed)
            {
                workflowPhase = "Bundle Manifest：" + cachedBundleManifestStage.StateText;
                nextAction = cachedBundleManifestStage.Detail + "；确认 AB 标签与计划一致后构建资源包。";
                messageType = cachedBundleManifestStage.State == PipelineStageState.Invalid ? MessageType.Error : MessageType.Warning;
                return;
            }

            if (!cachedLocalReleaseStage.IsPassed)
            {
                workflowPhase = "Local Release：" + cachedLocalReleaseStage.StateText;
                nextAction = cachedLocalReleaseStage.Detail + "；检查 Staging、索引和依赖闭包后发布本地 Release。";
                messageType = cachedLocalReleaseStage.State == PipelineStageState.Invalid ? MessageType.Error : MessageType.Warning;
                return;
            }

            workflowPhase = "已有本地发布产物";
            nextAction = "进入运行时验证；远端发布仍需独立执行预检和验证。";
        }

        private void DrawWorkflowGuide()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("推荐操作顺序", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("1. 从 Project 选中或拖入资产；2. 确认它属于当前 Library；3. 选择目标 ResourcePlan；4. 扫描通过后再烘焙和构建。", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("不需要先记住 EnumKey。优先从资产开始，系统会自动找到或生成对应 Key。", EditorStyles.miniLabel);
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool compact = EditorGUIUtility.currentViewWidth < 680f;
                GUILayout.Label(compact ? "资源工作流" : "收集 → Catalog → ConfigKey → ResourcePlan → 构建",
                    EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(true));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("刷新注册表", EditorStyles.toolbarButton, GUILayout.Width(78f)))
                {
                    ESAssetCatalogKeyPicker.Invalidate();
                    InvalidateStageStatus();
                    AssetDatabase.Refresh();
                    Repaint();
                }
                if (GUILayout.Button("打开资源窗口", EditorStyles.toolbarButton, GUILayout.Width(88f)))
                    ESResWindow.TryOpenWindow();
            }
        }

        private void DrawSelectedAssetSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("① 从资产配置 Key", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawCollectionTarget();
                DrawAssetDropArea();

                UnityEngine.Object next = EditorGUILayout.ObjectField("资产", selectedAsset, typeof(UnityEngine.Object), false);
                if (next != selectedAsset)
                    selectedAsset = next;

                if (selectedAsset == null)
                {
                    EditorGUILayout.HelpBox("从 Project 拖入资产，或直接在 Project 里选中资产。推荐从资产开始，窗口会自动显示它所属的 Library、类型和 Key。", MessageType.Info);
                    return;
                }

                ESAssetReferKind kind = ESAssetPage.DetermineKind(selectedAsset);
                EditorGUILayout.LabelField("资产类型", ESAssetConfigKeyDrawerBase.ResolveKindDisplayName(kind));
                string path = AssetDatabase.GetAssetPath(selectedAsset);
                EditorGUILayout.LabelField("路径", path);

                if (kind == ESAssetReferKind.None || kind == ESAssetReferKind.Other)
                {
                    EditorGUILayout.HelpBox("该对象不是可配置的 ES 业务资源类型。", MessageType.Warning);
                    return;
                }

                if (!ESAssetCatalogKeyPicker.TryFindByAsset(kind, selectedAsset, out ESAssetCatalogKeyPicker.Candidate candidate))
                {
                    EditorGUILayout.HelpBox("当前资产尚未在 ESAssetRegistry 中找到。先把资产拖入 Library 的 Book，或打开资源窗口进行收集。", MessageType.Warning);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(ResolveCollectionTarget() == null))
                            if (GUILayout.Button("加入当前收集 Library"))
                                CollectAssets(new[] { selectedAsset });
                        if (GUILayout.Button("定位资源窗口"))
                            ESResWindow.TryOpenWindow();
                    }
                    return;
                }

                string key = !string.IsNullOrWhiteSpace(candidate.stringKey) ? candidate.stringKey : candidate.enumKey.ToString();
                EditorGUILayout.LabelField("收集状态", candidate.isBaked ? "已收集并已烘焙 Catalog" : "已收集，等待烘焙 Catalog");
                EditorGUILayout.LabelField("Library / Book", candidate.libraryName + " / " + candidate.pageName);
                EditorGUILayout.LabelField("最终 Key", key);
                EditorGUILayout.LabelField("枚举 Key（内部）", candidate.enumKey.ToString());
                EditorGUILayout.LabelField("字符串 Key（内部）", string.IsNullOrEmpty(candidate.stringKey) ? "—" : candidate.stringKey);
                int keyMatchCount = ESAssetCatalogKeyPicker.CountKeyMatches(kind, candidate.enumKey, candidate.stringKey);
                if (keyMatchCount > 1)
                    EditorGUILayout.HelpBox("当前 ConfigKey 同时映射到 " + keyMatchCount + " 个资产。请先在 Library 中消除重复 Key，否则运行时解析结果不唯一。", MessageType.Error);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("复制 Key"))
                    {
                        EditorGUIUtility.systemCopyBuffer = key;
                        ShowNotification(new GUIContent("Key 已复制"));
                    }
                    if (GUILayout.Button("复制 ConfigKey 摘要"))
                    {
                        EditorGUIUtility.systemCopyBuffer = "Kind=" + kind + ", EnumKey=" + candidate.enumKey + ", StringKey=" + (candidate.stringKey ?? string.Empty);
                        ShowNotification(new GUIContent("ConfigKey 摘要已复制"));
                    }
                    if (GUILayout.Button("定位 Library 页面"))
                        LocateRegistryPage(kind, candidate);
                    if (GUILayout.Button("Ping 资产"))
                    {
                        Selection.activeObject = selectedAsset;
                        EditorGUIUtility.PingObject(selectedAsset);
                    }
                }
            }
        }

        private void DrawCollectionTarget()
        {
            ESAssetLibrary target = ResolveCollectionTarget();
            bool compact = EditorGUIUtility.currentViewWidth < 760f;
            if (compact)
            {
                EditorGUILayout.LabelField("当前收集 Library", target != null ? target.Name : "未设置");
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawCollectionTargetActions(target);
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("当前收集 Library", target != null ? target.Name : "未设置", GUILayout.MinWidth(180f));
                    DrawCollectionTargetActions(target);
                }
            }
            if (target == null)
                EditorGUILayout.HelpBox("请先在资源窗口选择一个“当前收集 Library”。未设置时不会自动决定资源归属。", MessageType.Warning);
        }

        private void DrawCollectionTargetActions(ESAssetLibrary target)
        {
            if (target != null && GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(48f)))
            {
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
            }
            int selectedCount = Selection.objects.Count(IsCollectableAsset);
            using (new EditorGUI.DisabledScope(target == null || selectedCount == 0))
                if (GUILayout.Button("收集当前选择(" + selectedCount + ")", EditorStyles.miniButton, GUILayout.Width(108f)))
                    CollectAssets(Selection.objects);
            if (GUILayout.Button("资源窗口设置", EditorStyles.miniButton, GUILayout.Width(90f)))
                ESResWindow.TryOpenWindow();
        }

        private void DrawAssetDropArea()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 42f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "拖入一个或多个资产：自动加入当前收集 Library", EditorStyles.helpBox);
            Event current = Event.current;
            if (!rect.Contains(current.mousePosition)
                || (current.type != EventType.DragUpdated && current.type != EventType.DragPerform))
                return;

            bool canCollect = ResolveCollectionTarget() != null && DragAndDrop.objectReferences.Any(IsCollectableAsset);
            DragAndDrop.visualMode = canCollect ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            if (current.type == EventType.DragPerform && canCollect)
            {
                DragAndDrop.AcceptDrag();
                CollectAssets(DragAndDrop.objectReferences);
            }
            current.Use();
        }

        private static ESAssetLibrary ResolveCollectionTarget()
        {
            return ESGlobalResToolsSupportConfig.ActiveCollectLibrary;
        }

        private static bool IsCollectableAsset(UnityEngine.Object asset)
        {
            if (asset == null || string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(asset)))
                return false;
            ESAssetReferKind kind = ESAssetPage.DetermineKind(asset);
            return kind != ESAssetReferKind.None && kind != ESAssetReferKind.Other;
        }

        private bool CollectAssets(IEnumerable<UnityEngine.Object> source)
        {
            ESAssetLibrary library = ResolveCollectionTarget();
            if (library == null)
            {
                workflowStatus = "收集失败：尚未设置当前收集 Library。";
                return false;
            }

            UnityEngine.Object[] assets = (source ?? Array.Empty<UnityEngine.Object>())
                .Where(IsCollectableAsset)
                .Distinct()
                .ToArray();
            if (assets.Length == 0)
            {
                workflowStatus = "没有可收集的资源对象。";
                return false;
            }
            int alreadyRegistered = assets.Count(IsRegisteredAsset);
            assets = assets.Where(asset => !IsRegisteredAsset(asset)).ToArray();
            if (assets.Length == 0)
            {
                workflowStatus = "所选资产均已存在于 Library 注册表，本次未重复收集。";
                return true;
            }
            if (assets.Length > 1 && !EditorUtility.DisplayDialog(
                    "批量收集资产",
                    "将 " + assets.Length + " 个资产加入 Library【" + library.Name + "】的默认 Book？",
                    "收集",
                    "取消"))
                return false;

            Undo.RecordObject(library, "Collect Assets To Active Library");
            library.EditorOnly_DragAssetsToBooks(assets);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            ESAssetCatalogKeyPicker.Invalidate();
            InvalidateStageStatus();
            selectedAsset = assets[0];
            workflowStatus = "已收集 " + assets.Length + " 个资产到 Library【" + library.Name + "】"
                + (alreadyRegistered > 0 ? "，已登记资产跳过 " + alreadyRegistered + " 个" : string.Empty)
                + "；现在可直接配置 Key，构建前再统一烘焙。";
            Repaint();
            return true;
        }

        private static bool IsRegisteredAsset(UnityEngine.Object asset)
        {
            if (!IsCollectableAsset(asset))
                return false;
            ESAssetReferKind kind = ESAssetPage.DetermineKind(asset);
            ESPipelineAssetIdentity identity = ESAssetPipelineIO.GetIdentity(asset);
            return identity.IsValid && ESAssetRegistry.TryGetByAssetIdentity(kind, identity.guid, identity.localFileId, out _);
        }

        private static void LocateRegistryPage(ESAssetReferKind kind, ESAssetCatalogKeyPicker.Candidate candidate)
        {
            if (candidate == null || !ESAssetRegistry.TryGetByAssetIdentity(kind, candidate.guid, candidate.localFileId, out ESAssetPage page))
            {
                Debug.LogWarning("[ESRes][Workflow] 当前 Key 没有对应的 Library 页面。");
                ESResWindow.TryOpenWindow();
                return;
            }

            if (ESAssetReferEditorBridge.OpenRegistryPage != null)
                ESAssetReferEditorBridge.OpenRegistryPage(page);
            else
                ESResWindow.TryOpenWindow();
        }

        private void DrawPipelineSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("② 加入计划并检查构建", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox("先选择目标 ResourcePlan，再把当前选中的资产加入计划。扫描没有错误后，才执行烘焙、规划和发布。", MessageType.Info);
                DrawPipelineStageStatus();
                DrawPipelineActionButtons();
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!cachedCatalogStage.IsPassed))
                        if (GUILayout.Button(new GUIContent("规划并标记 AB", "仅在当前 Catalog 协议、身份和错误检查通过后生成资源包规划并写入 AB 标签"), GUILayout.Height(28f)))
                            StartPlan();
                    showWarnings = EditorGUILayout.ToggleLeft("显示警告", showWarnings, GUILayout.Width(90f));
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    targetPlan = (ESResourcePlanInfo)EditorGUILayout.ObjectField("目标 ResourcePlan", targetPlan, typeof(ESResourcePlanInfo), false);
                    using (new EditorGUI.DisabledScope(targetPlan == null))
                        if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(44f)))
                        {
                            Selection.activeObject = targetPlan;
                            EditorGUIUtility.PingObject(targetPlan);
                        }
                }
                int selectedCount = Selection.objects.Count(IsCollectableAsset);
                using (new EditorGUI.DisabledScope(targetPlan == null || selectedCount == 0))
                    if (GUILayout.Button("收集并加入 ResourcePlan（" + selectedCount + "）", GUILayout.Height(28f)))
                        AddSelectionToResourcePlan();
                if (targetPlan != null)
                    EditorGUILayout.LabelField("目标计划", BuildPlanEntrySummary(targetPlan), EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Plan 扫描", scanSummary, EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Catalog 检查", catalogSummary, EditorStyles.miniLabel);
                if (!string.IsNullOrWhiteSpace(workflowStatus))
                    EditorGUILayout.HelpBox(workflowStatus, workflowStatus.Contains("失败") ? MessageType.Error : MessageType.Info);
            }
        }

        private void DrawPipelineStageStatus()
        {
            RefreshStageStatusIfNeeded();

            bool compact = EditorGUIUtility.currentViewWidth < 760f;
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawStageBadge("① Catalog", cachedCatalogStage);
                DrawStageBadge("② 规划", cachedPlanStage);
                if (!compact)
                {
                    DrawStageBadge("③ AB", cachedBundleManifestStage);
                    DrawStageBadge("④ 发布", cachedLocalReleaseStage);
                }
            }
            if (compact)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawStageBadge("③ AB", cachedBundleManifestStage);
                    DrawStageBadge("④ 发布", cachedLocalReleaseStage);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("重新检查阶段", "重新解析协议、校验依赖和验证生成文件完整性"), EditorStyles.miniButton))
                    InvalidateStageStatus();
                if (GUILayout.Button(new GUIContent("复制阶段诊断", "复制四阶段完整状态和首批问题，便于提交 Console/问题单"), EditorStyles.miniButton))
                {
                    EditorGUIUtility.systemCopyBuffer = BuildStageDiagnosticText();
                    ShowNotification(new GUIContent("阶段诊断已复制"));
                }
            }
        }

        private void DrawPipelineActionButtons()
        {
            bool compact = EditorGUIUtility.currentViewWidth < 760f;
            DrawPipelineActionRow(drawPlanChecks: true, drawBakeChecks: !compact);
            if (compact)
                DrawPipelineActionRow(drawPlanChecks: false, drawBakeChecks: true);
        }

        private void DrawPipelineActionRow(bool drawPlanChecks, bool drawBakeChecks)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (drawPlanChecks)
                {
                    if (GUILayout.Button(new GUIContent("检查资源计划", "扫描所有 ResourcePlan 中的空 Key、重复 Key 和失效引用"), GUILayout.Height(28f)))
                        ScanResourcePlans();
                    if (GUILayout.Button(new GUIContent("同步过期 Key", "先预览并确认，再把 ES 受管目录中已绑定源资产的最新 Key 同步回快照；手填 Key 不会被改动"), GUILayout.Height(28f)))
                    {
                        int synchronized = ESResourcePlanConfigKeySynchronizer.SynchronizeAll();
                        workflowStatus = "已同步过期 ConfigKey：" + synchronized + " 项。";
                        ScanResourcePlans();
                    }
                }
                if (drawBakeChecks)
                {
                    if (GUILayout.Button(new GUIContent("检查烘焙结果", "检查 Catalog 数量、条目和错误"), GUILayout.Height(28f)))
                        ScanCatalogs();
                    if (GUILayout.Button(new GUIContent("烘焙引用", "把 Library 注册信息写入可供运行时读取的 Catalog"), GUILayout.Height(28f)))
                        StartBake();
                }
            }
        }

        private string BuildStageDiagnosticText()
        {
            string platform;
            try { platform = ESAssetPipelineIO.PlatformName; }
            catch (Exception exception) { platform = "未解析（" + exception.Message + "）"; }
            var lines = new List<string>
            {
                "ES 资源工作流阶段诊断",
                "Platform=" + platform,
                "Catalog=" + cachedCatalogStage.Detail,
                "BuildPlan=" + cachedPlanStage.Detail,
                "BundleManifest=" + cachedBundleManifestStage.Detail,
                "LocalRelease=" + cachedLocalReleaseStage.Detail
            };
            AppendStageIssues(lines, cachedCatalogStage);
            AppendStageIssues(lines, cachedPlanStage);
            AppendStageIssues(lines, cachedBundleManifestStage);
            AppendStageIssues(lines, cachedLocalReleaseStage);
            return string.Join("\n", lines);
        }

        private static void AppendStageIssues(List<string> lines, PipelineStageReport report)
        {
            if (report?.Issues == null || report.Issues.Count == 0)
                return;
            foreach (string issue in report.Issues)
                lines.Add("- " + report.Title + ": " + issue);
        }

        private void RefreshStageStatusIfNeeded()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < stageStatusExpiresAt)
                return;

            try
            {
                string platform = ESAssetPipelineIO.PlatformName;
                PipelineStageSnapshot snapshot = PipelineStageValidator.Evaluate(platform);
                cachedCatalogStage = snapshot.Catalog;
                cachedPlanStage = snapshot.Plan;
                cachedBundleManifestStage = snapshot.BundleManifest;
                cachedLocalReleaseStage = snapshot.LocalRelease;
            }
            catch (Exception exception)
            {
                string message = "阶段检查无法启动：" + exception.Message;
                cachedCatalogStage = CreateStageCheckFailure("Catalog", message);
                cachedPlanStage = CreateStageCheckFailure("BuildPlan", message);
                cachedBundleManifestStage = CreateStageCheckFailure("Bundle Manifest", message);
                cachedLocalReleaseStage = CreateStageCheckFailure("Local Release", message);
            }
            stageStatusExpiresAt = now + 2d;
        }

        private static PipelineStageReport CreateStageCheckFailure(string title, string message)
        {
            return new PipelineStageReport(title, PipelineStageState.Invalid, 0, 1, new List<string> { message });
        }

        private void InvalidateStageStatus()
        {
            stageStatusExpiresAt = 0d;
        }

        private static void DrawStageBadge(string title, PipelineStageReport report)
        {
            Color previous = GUI.backgroundColor;
            switch (report?.State ?? PipelineStageState.Missing)
            {
                case PipelineStageState.Passed:
                    GUI.backgroundColor = new Color(0.55f, 0.95f, 0.62f);
                    break;
                case PipelineStageState.Invalid:
                    GUI.backgroundColor = new Color(1f, 0.48f, 0.48f);
                    break;
                default:
                    GUI.backgroundColor = new Color(1f, 0.78f, 0.48f);
                    break;
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinWidth(122f)))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(report?.Detail ?? "缺失", EditorStyles.wordWrappedMiniLabel, GUILayout.MinHeight(32f));
            }
            GUI.backgroundColor = previous;
        }

        private void AddSelectionToResourcePlan()
        {
            if (targetPlan == null)
                return;

            UnityEngine.Object[] assets = Selection.objects.Where(IsCollectableAsset).Distinct().ToArray();
            UnityEngine.Object[] unregisteredAssets = assets.Where(asset => !IsRegisteredAsset(asset)).ToArray();
            if (unregisteredAssets.Length > 0 && !CollectAssets(unregisteredAssets))
                return;

            int added = 0;
            int duplicate = 0;
            var failures = new List<string>();
            Undo.RecordObject(targetPlan, "Add Assets To ResourcePlan");

            foreach (UnityEngine.Object asset in assets)
            {
                ESAssetReferKind kind = ESAssetPage.DetermineKind(asset);
                PlanField? mapping = FindPlanField(kind);
                if (!mapping.HasValue)
                {
                    failures.Add(asset.name + "：ResourcePlan 暂不支持类型 " + kind);
                    continue;
                }
                if (!ESAssetCatalogKeyPicker.TryFindByAsset(kind, asset, out ESAssetCatalogKeyPicker.Candidate candidate))
                {
                    failures.Add(asset.name + "：尚未收集到 Library");
                    continue;
                }

                FieldInfo listField = typeof(ESResourcePlanInfo).GetField(mapping.Value.FieldName, BindingFlags.Instance | BindingFlags.Public);
                IList list = listField?.GetValue(targetPlan) as IList;
                if (list == null)
                {
                    failures.Add(asset.name + "：无法访问 Plan 列表 " + mapping.Value.FieldName);
                    continue;
                }
                if (ContainsPlanKey(list, candidate))
                {
                    duplicate++;
                    continue;
                }

                Type entryType = list.GetType().GetGenericArguments()[0];
                object entry = Activator.CreateInstance(entryType);
                FieldInfo keyField = entryType.GetField("key", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object key = keyField?.GetValue(entry);
                if (key == null)
                {
                    failures.Add(asset.name + "：无法创建 ConfigKey");
                    continue;
                }
                ApplyCandidateToKey(key, candidate);
                list.Add(entry);
                added++;
            }

            EditorUtility.SetDirty(targetPlan);
            AssetDatabase.SaveAssets();
            workflowStatus = "ResourcePlan【" + targetPlan.name + "】新增 " + added + "，重复跳过 " + duplicate
                + (failures.Count > 0 ? "，失败 " + failures.Count + "：" + string.Join("；", failures) : "。" );
            Selection.activeObject = targetPlan;
            EditorGUIUtility.PingObject(targetPlan);
        }

        private static string BuildPlanEntrySummary(ESResourcePlanInfo plan)
        {
            if (plan == null)
                return "未选择";
            int total = 0;
            foreach (PlanField field in ResourcePlanFields)
            {
                FieldInfo listField = typeof(ESResourcePlanInfo).GetField(field.FieldName, BindingFlags.Instance | BindingFlags.Public);
                if (listField?.GetValue(plan) is IList list)
                    total += list.Count;
            }
            total += plan.prefabPrewarms?.Count ?? 0;
            return plan.name + " · 资源条目 " + total;
        }

        private static PlanField? FindPlanField(ESAssetReferKind kind)
        {
            for (int i = 0; i < ResourcePlanFields.Length; i++)
                if (ResourcePlanFields[i].Kind == kind)
                    return ResourcePlanFields[i];
            return null;
        }

        private static bool ContainsPlanKey(IList list, ESAssetCatalogKeyPicker.Candidate candidate)
        {
            foreach (object entry in list)
            {
                if (entry == null) continue;
                FieldInfo keyField = entry.GetType().GetField("key", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object key = keyField?.GetValue(entry);
                if (key == null) continue;
                int enumKey = ReadEnumKey(key);
                string stringKey = ReadField<string>(key, "stringKey") ?? string.Empty;
                if (enumKey == candidate.enumKey && string.Equals(stringKey, candidate.stringKey ?? string.Empty, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void ApplyCandidateToKey(object key, ESAssetCatalogKeyPicker.Candidate candidate)
        {
            FieldInfo enumField = FindField(key.GetType(), "enumKey");
            if (enumField != null)
                enumField.SetValue(key, Enum.ToObject(enumField.FieldType, candidate.enumKey));
            SetField(key, "stringKey", candidate.stringKey ?? string.Empty);
            SetField(key, "guid", candidate.guid ?? string.Empty);
            SetField(key, "localFileId", candidate.localFileId);
            SetField(key, "assetTypeName", candidate.assetTypeName ?? string.Empty);
            SetField(key, "editorPath", candidate.assetPath ?? string.Empty);
            SetField(key, "editorOnly", ESAssetPipelineIO.IsEditorOnly(candidate.assetPath, ESAssetCatalogKeyPicker.ResolveAsset(candidate)));
        }

        private static int ReadEnumKey(object key)
        {
            FieldInfo field = FindField(key.GetType(), "enumKey");
            object value = field?.GetValue(key);
            return value != null ? Convert.ToInt32(value) : 0;
        }

        private static T ReadField<T>(object target, string fieldName)
        {
            FieldInfo field = FindField(target.GetType(), fieldName);
            object value = field?.GetValue(target);
            return value is T typed ? typed : default;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FindField(target.GetType(), fieldName)?.SetValue(target, value);
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
                type = type.BaseType;
            }
            return null;
        }

        private void StartBake()
        {
            try
            {
                if (!ScanResourcePlans())
                {
                    scanSummary = "ResourcePlan ConfigKey validation failed. Resolve or synchronize stale bound Keys before baking.";
                    return;
                }
                ESAssetReferenceBaker.Bake();
                InvalidateStageStatus();
                scanSummary = "已启动烘焙长任务；完成后重新扫描 Plan。";
            }
            catch (Exception exception)
            {
                scanSummary = "烘焙启动失败：" + exception.Message;
                Debug.LogException(exception);
            }
        }

        private void StartPlan()
        {
            try
            {
                stageStatusExpiresAt = 0d;
                RefreshStageStatusIfNeeded();
                if (!cachedCatalogStage.IsPassed)
                {
                    workflowStatus = "规划阻断：" + cachedCatalogStage.Detail + "。请重新烘焙并修复 Catalog 后再试。";
                    return;
                }
                if (ESDesignUtility.SafeEditor.Wrap_DisplayDialog("规划并标记 AB", "会读取当前烘焙结果并修改 ES 管理的 AB 标签。继续吗？", "执行", "取消"))
                {
                    ESAssetBundleBuildPlanner.PlanAndMark();
                    InvalidateStageStatus();
                }
            }
            catch (Exception exception)
            {
                scanSummary = "规划失败：" + exception.Message;
                Debug.LogException(exception);
            }
        }

        private void ScanCatalogs()
        {
            stageStatusExpiresAt = 0d;
            RefreshStageStatusIfNeeded();
            catalogSummary = cachedCatalogStage.Detail;
            if (!cachedCatalogStage.IsPassed)
            {
                issues.Add(new Issue
                {
                    Title = "Catalog",
                    Message = cachedCatalogStage.Detail,
                    Error = cachedCatalogStage.State == PipelineStageState.Invalid
                });
            }
            Repaint();
        }

        private bool ScanResourcePlans()
        {
            ESAssetCatalogKeyPicker.RefreshForValidation();
            issues.Clear();
            int planCount = 0;
            int entryCount = 0;
            int errors = 0;
            int warnings = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ESResourcePlanInfo"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ESResourcePlanInfo plan = AssetDatabase.LoadAssetAtPath<ESResourcePlanInfo>(path);
                if (plan == null) continue;
                planCount++;
                SerializedObject serialized = new SerializedObject(plan);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (PlanField field in ResourcePlanFields)
                {
                    SerializedProperty list = serialized.FindProperty(field.FieldName);
                    if (list == null || !list.isArray) continue;
                    for (int i = 0; i < list.arraySize; i++)
                    {
                        SerializedProperty element = list.GetArrayElementAtIndex(i);
                        if (field.FieldName == "prefabPrewarms") continue;
                        SerializedProperty key = element.FindPropertyRelative("key");
                        if (key == null) continue;
                        entryCount++;
                        int enumKey = key.FindPropertyRelative("enumKey")?.intValue ?? 0;
                        string stringKey = key.FindPropertyRelative("stringKey")?.stringValue ?? string.Empty;
                        bool required = element.FindPropertyRelative("required")?.boolValue ?? true;
                        string identity = field.Kind + "|" + enumKey + "|" + stringKey;
                        if (enumKey == 0 && string.IsNullOrWhiteSpace(stringKey))
                        {
                            if (required) errors++; else warnings++;
                            issues.Add(new Issue { Owner = plan, Title = plan.name + " / " + field.DisplayName + "[" + i + "]", Message = "ConfigKey 为空。", Error = required });
                            continue;
                        }
                        if (!seen.Add(identity))
                        {
                            warnings++;
                            issues.Add(new Issue { Owner = plan, Title = plan.name + " / " + field.DisplayName + "[" + i + "]", Message = "同一计划内重复的 ConfigKey。", Error = false });
                        }
                        int keyMatchCount = ESAssetCatalogKeyPicker.CountKeyMatches(field.Kind, enumKey, stringKey);
                        ESAssetCatalogKeyPicker.Candidate authority = ESAssetCatalogKeyPicker.FindCurrent(field.Kind, key);
                        if (ESAssetCatalogKeyPicker.IsBoundSourceMissing(key, authority))
                        {
                            errors++;
                            issues.Add(new Issue
                            {
                                Owner = plan,
                                Title = plan.name + " / " + field.DisplayName + "[" + i + "]",
                                Message = "已绑定的源资产不在当前 Library/Catalog。请重新选择或收集该资产。",
                                Error = true
                            });
                            continue;
                        }
                        if (authority != null && ESAssetCatalogKeyPicker.IsStale(key, authority))
                        {
                            errors++;
                            issues.Add(new Issue
                            {
                                Owner = plan,
                                Title = plan.name + " / " + field.DisplayName + "[" + i + "]",
                                Message = "Bound source Key changed: " + ESConfigKeyMatch.Describe(enumKey, stringKey)
                                    + " -> " + ESConfigKeyMatch.Describe(authority.enumKey, authority.stringKey) + ". Sync this reference before baking.",
                                Error = true
                            });
                            continue;
                        }
                        if (keyMatchCount == 0)
                        {
                            if (required) errors++; else warnings++;
                            issues.Add(new Issue { Owner = plan, Title = plan.name + " / " + field.DisplayName + "[" + i + "]", Message = (required ? "必需" : "可选") + " ConfigKey 在当前 Library 注册表/Catalog 中无法解析。", Error = required });
                        }
                        else if (keyMatchCount > 1)
                        {
                            errors++;
                            issues.Add(new Issue { Owner = plan, Title = plan.name + " / " + field.DisplayName + "[" + i + "]", Message = "ConfigKey 同时映射到 " + keyMatchCount + " 个资产，运行时解析存在歧义。", Error = true });
                        }
                    }
                }

                SerializedProperty prewarms = serialized.FindProperty("prefabPrewarms");
                if (prewarms != null && prewarms.isArray)
                {
                    for (int i = 0; i < prewarms.arraySize; i++)
                    {
                        SerializedProperty element = prewarms.GetArrayElementAtIndex(i);
                        SerializedProperty data = element.FindPropertyRelative("data");
                        bool required = element.FindPropertyRelative("required")?.boolValue ?? true;
                        if (data != null && data.objectReferenceValue == null)
                        {
                            if (required) errors++; else warnings++;
                            issues.Add(new Issue
                            {
                                Owner = plan,
                                Title = plan.name + " / PrefabPrewarm[" + i + "]",
                                Message = "预热配置为空。",
                                Error = required
                            });
                        }
                    }
                }
            }
            scanSummary = "Plan=" + planCount + "，Key 条目=" + entryCount + "，错误=" + errors + "，警告=" + warnings;
            Repaint();
            return errors == 0;
        }

        private void DrawIssueSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("③ 修复检查问题", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                issueSearch = EditorGUILayout.TextField("筛选", issueSearch);
                IEnumerable<Issue> visible = issues.Where(item => item != null
                    && (showWarnings || item.Error)
                    && (string.IsNullOrWhiteSpace(issueSearch)
                        || (item.Title?.IndexOf(issueSearch, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                        || (item.Message?.IndexOf(issueSearch, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0));
                int count = 0;
                foreach (Issue issue in visible)
                {
                    count++;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUIContent icon = EditorGUIUtility.IconContent(issue.Error ? "console.erroricon" : "console.warnicon");
                        GUILayout.Label(icon, GUILayout.Width(18f), GUILayout.Height(18f));
                        EditorGUILayout.LabelField(issue.Title + "：" + issue.Message, EditorStyles.miniLabel);
                        if (issue.Owner != null && GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(42f)))
                        {
                            Selection.activeObject = issue.Owner;
                            EditorGUIUtility.PingObject(issue.Owner);
                        }
                    }
                }
                if (count == 0)
                    EditorGUILayout.LabelField("没有扫描结果。先点击“扫描 ResourcePlan”或“检查 Catalog”。", EditorStyles.miniLabel);
            }
        }
    }
}
