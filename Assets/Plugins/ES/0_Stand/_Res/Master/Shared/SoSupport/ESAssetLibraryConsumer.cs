using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    public class ESAssetLibraryConsumer : LibConsumer<ESAssetLibrary>
    {
        [LabelText("Consumer 稳定 ID"), ReadOnly]
        public string ConsumerId = string.Empty;

        [LabelText("总 Consumer（启动入口）")]
        public bool IsTotalConsumer;

        [LabelText("发布渠道")]
        public string Channel = "default";

        [LabelText("维护负责人")]
        public string Maintainer = string.Empty;

        [LabelText("对外版本说明"), TextArea(3, 8)]
        public string ReleaseNotes = string.Empty;

        [LabelText("内部备注"), TextArea(3, 8)]
        public string InternalNotes = string.Empty;

        [LabelText("标签")]
        public List<string> Tags = new List<string>();

        [LabelText("可选 Library")]
        public List<ESAssetLibrary> OptionalLibFolders = new List<ESAssetLibrary>();

        [LabelText("依赖 Consumer")]
        public List<ESAssetLibraryConsumer> RequiredConsumers = new List<ESAssetLibraryConsumer>();

        [LabelText("启动游戏核心")]
        [Tooltip("由资产烘焙自动维护。仅收集本 Consumer 必需 Library 中实现 IGameCoreSO 的 ScriptableObject。")]
        [SerializeReference]
        public List<ESAssetReferBase> GameCoreAssets = new List<ESAssetReferBase>();

        [LabelText("手动补充 GameCore")]
        [Tooltip("用于不在必需 Library 中的启动核心。烘焙同步不会覆盖此列表。")]
        [SerializeReference]
        public List<ESAssetReferBase> ManualGameCoreAssets = new List<ESAssetReferBase>();

        [LabelText("GameCore 依赖检查"), ReadOnly, MultiLineProperty(5)]
        public List<string> GameCoreValidationErrors = new List<string>();

        [LabelText("启用代码热更")]
        public bool EnableCodeHotUpdate;

        [HideInInspector]
        public string HotUpdateAssemblyDefinitionGuid = string.Empty;

        [HideInInspector]
        public string HotUpdateAssemblyName = string.Empty;

        [HideInInspector]
        public string HotUpdateSourceFolder = string.Empty;

        [LabelText("附加文件")]
        public List<ESConsumerCodePackageConfig> CodePackages = new List<ESConsumerCodePackageConfig>();

        [LabelText("构建修订号"), ReadOnly]
        public int BuildRevision;

        [LabelText("最后构建 UTC"), ReadOnly]
        public string LastBuildUtc = string.Empty;

        public string RuntimeVersion => (string.IsNullOrWhiteSpace(Version) ? "1.0.0" : Version.Trim()) + "." + BuildRevision;

        public bool EnsureStableIdentity()
        {
            if (!string.IsNullOrEmpty(ConsumerId))
                return false;

            ConsumerId = Guid.NewGuid().ToString("N");
            return true;
        }

        public void IncrementBuildRevision()
        {
            EnsureStableIdentity();
            BuildRevision++;
            LastBuildUtc = DateTime.UtcNow.ToString("O");
        }
    }

    public enum ESConsumerCodePackageKind
    {
        HotUpdateAssembly = 0,
        AotMetadata = 1,
        Symbols = 2,
        ManagedData = 3,
        RawBinary = 4
    }

    [Serializable]
    public sealed class ESConsumerCodePackageConfig
    {
        [LabelText("启用")]
        public bool Enabled = true;

        [LabelText("包 Key")]
        public string PackageKey = string.Empty;

        [LabelText("类型")]
        public ESConsumerCodePackageKind Kind = ESConsumerCodePackageKind.HotUpdateAssembly;

        [LabelText("源文件路径")]
        public string SourcePath = string.Empty;

        [LabelText("启动必需")]
        public bool RequiredAtBoot = true;

        [HideInInspector]
        public bool ManagedByHybridCLR;

        [LabelText("加载顺序")]
        public int LoadOrder;

        [LabelText("备注"), TextArea(2, 5)]
        public string Notes = string.Empty;
    }

    [System.Obsolete("Use ESAssetLibraryConsumer.")]
    public class ResLibConsumer : ESAssetLibraryConsumer
    {
    }
}
