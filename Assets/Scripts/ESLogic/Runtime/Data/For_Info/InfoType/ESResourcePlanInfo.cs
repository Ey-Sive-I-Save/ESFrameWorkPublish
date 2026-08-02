using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    public abstract class ESResourcePlanEntryBase
    {
        [LabelText("必须准备成功")]
        [InfoBox("开启：准备失败会中止本计划。关闭：记录错误，但允许其他资源继续准备。")]
        public bool required = true;
    }

    [Serializable]
    public sealed class ESResourcePlanPrefabEntry : ESResourcePlanEntryBase
    {
        [LabelText("预制体"), InlineProperty] public ESAssetReferPrefabConfigKey key = new ESAssetReferPrefabConfigKey();
    }

    [Serializable]
    public sealed class ESResourcePlanPrefabPrewarmEntry : ESResourcePlanEntryBase
    {
        [LabelText("对象池预热"), Required]
        [InfoBox("预热内容、数量和对象池参数在预热配置中维护；资源计划只负责准备和释放。")]
        public PrefabPrewarmDataInfo data;
    }

    /// <summary>Editor-baked complete ConfigKey snapshot expanded from a Plan GameCore source.</summary>
    [Serializable]
    public sealed class ESResourcePlanBakedAssetEntry : ESResourcePlanEntryBase
    {
        [ReadOnly] public ESAssetReferKind kind;
        [ReadOnly] public int enumKey;
        [ReadOnly] public string stringKey;
        [ReadOnly] public string guid;
        [ReadOnly] public long localFileId;
        [ReadOnly, TextArea(1, 2)] public string source;

        public bool HasConfiguredKey => ESConfigKeyMatch.IsConfigured(enumKey, stringKey);
    }

    /// <summary>
    /// Optional-module authoring input. A source is interpreted exclusively by its registered
    /// editor extension and is never consulted by Player runtime after baking.
    /// </summary>
    [Serializable]
    public sealed class ESResourcePlanExtensionSourceEntry : ESResourcePlanEntryBase
    {
        [LabelText("扩展配置来源"), Required, AssetsOnly]
        public ScriptableObject source;
    }

    /// <summary>Immutable external-system recipe emitted during Bake.</summary>
    [Serializable]
    public sealed class ESResourcePlanBakedExtensionEntry : ESResourcePlanEntryBase
    {
        [ReadOnly] public string providerId;
        [ReadOnly] public int schemaVersion;
        [ReadOnly, TextArea(1, 2)] public string source;
        [ReadOnly, TextArea(1, 4)] public string payload;
        [ReadOnly] public List<ESResourcePlanBakedAssetEntry> assets = new List<ESResourcePlanBakedAssetEntry>(4);
    }

    [Serializable] public sealed class ESResourcePlanSpriteEntry : ESResourcePlanEntryBase { [InlineProperty] public ESAssetReferSpriteConfigKey key = new ESAssetReferSpriteConfigKey(); }
    [Serializable]
    public sealed class ESResourcePlanAudioCueEntry : ESResourcePlanEntryBase
    {
        [LabelText("Cue"), Required, AssetsOnly]
        public ESAudioCueInfo cue;
    }
    [Serializable] public sealed class ESResourcePlanAudioEntry : ESResourcePlanEntryBase { [InlineProperty] public ESAssetReferAudioClipConfigKey key = new ESAssetReferAudioClipConfigKey(); }
    [Serializable] public sealed class ESResourcePlanAnimationEntry : ESResourcePlanEntryBase { [InlineProperty] public ESAssetReferAnimationClipConfigKey key = new ESAssetReferAnimationClipConfigKey(); }
    [Serializable] public sealed class ESResourcePlanAnimatorEntry : ESResourcePlanEntryBase { [InlineProperty] public ESAssetReferAnimatorControllerConfigKey key = new ESAssetReferAnimatorControllerConfigKey(); }
    [Serializable] public sealed class ESResourcePlanMaterialEntry : ESResourcePlanEntryBase { [InlineProperty] public ESAssetReferMaterialConfigKey key = new ESAssetReferMaterialConfigKey(); }
    [Serializable] public sealed class ESResourcePlanMeshEntry : ESResourcePlanEntryBase { [InlineProperty] public ESAssetReferMeshConfigKey key = new ESAssetReferMeshConfigKey(); }
    [Serializable] public sealed class ESResourcePlanTextureEntry : ESResourcePlanEntryBase { [InlineProperty] public ESAssetReferTextureConfigKey key = new ESAssetReferTextureConfigKey(); }
    [Serializable] public sealed class ESResourcePlanTexture2DEntry : ESResourcePlanEntryBase { [InlineProperty] public ESAssetReferTexture2DConfigKey key = new ESAssetReferTexture2DConfigKey(); }
    [Serializable] public sealed class ESResourcePlanSpriteAtlasEntry : ESResourcePlanEntryBase { [InlineProperty] public ESAssetReferSpriteAtlasConfigKey key = new ESAssetReferSpriteAtlasConfigKey(); }
    [Serializable] public sealed class ESResourcePlanAvatarEntry : ESResourcePlanEntryBase { [InlineProperty] public ESAssetReferAvatarConfigKey key = new ESAssetReferAvatarConfigKey(); }
    [Serializable] public sealed class ESResourcePlanPlayableEntry : ESResourcePlanEntryBase { [InlineProperty] public ESAssetReferPlayableAssetConfigKey key = new ESAssetReferPlayableAssetConfigKey(); }
    [Serializable] public sealed class ESResourcePlanScriptableObjectEntry : ESResourcePlanEntryBase { [InlineProperty] public ESAssetReferScriptableObjectConfigKey key = new ESAssetReferScriptableObjectConfigKey(); }
    [Serializable] public sealed class ESResourcePlanTimelineEntry : ESResourcePlanEntryBase { [InlineProperty] public ESAssetReferTimelineAssetConfigKey key = new ESAssetReferTimelineAssetConfigKey(); }
    [Serializable] public sealed class ESResourcePlanVideoEntry : ESResourcePlanEntryBase { [InlineProperty] public ESAssetReferVideoClipConfigKey key = new ESAssetReferVideoClipConfigKey(); }
    [Serializable] public sealed class ESResourcePlanTerrainEntry : ESResourcePlanEntryBase { [InlineProperty] public ESAssetReferTerrainDataConfigKey key = new ESAssetReferTerrainDataConfigKey(); }
    [Serializable] public sealed class ESResourcePlanRawEntry : ESResourcePlanEntryBase { [InlineProperty] public ESAssetReferRawConfigKey key = new ESAssetReferRawConfigKey(); }

    [ESCreatePath("数据信息", "资源计划")]
    /// <summary>
    /// 描述一段玩法期间需要准备和持有的资源。普通业务只需直接引用本计划，
    /// 或使用 ESResourcePlanBinder 绑定 GameObject 生命周期。
    /// </summary>
    public sealed class ESResourcePlanInfo : SoDataInfo, IReceiveActiveLink
    {
        [PropertyOrder(-100)]
        [TitleGroup("使用与释放")]
        [LabelText("使用结束后自动释放")]
        [InfoBox("推荐开启。最后一个使用者离开后进入释放流程；短时间内再次使用会复用已准备的资源。")]
        public bool releaseOnExit = true;

        [PropertyOrder(-99)]
        [TitleGroup("使用与释放")]
        [LabelText("释放缓冲时间（秒）"), MinValue(0f)]
        [InfoBox("用于避免关卡或界面快速往返时反复加载。填 0 表示不等待。")]
        public float releaseDelaySeconds = 10f;

        [PropertyOrder(0)]
        [TitleGroup("常用资源")]
        [LabelText("预制体")]
        public List<ESResourcePlanPrefabEntry> prefabs = new List<ESResourcePlanPrefabEntry>(8);

        [PropertyOrder(1)]
        [TitleGroup("常用资源")]
        [LabelText("对象池预热")]
        public List<ESResourcePlanPrefabPrewarmEntry> prefabPrewarms = new List<ESResourcePlanPrefabPrewarmEntry>(4);

        [PropertyOrder(2)]
        [TitleGroup("常用资源")]
        [LabelText("图片（Sprite）")]
        public List<ESResourcePlanSpriteEntry> sprites = new List<ESResourcePlanSpriteEntry>(8);
        [PropertyOrder(3), TitleGroup("常用资源"), LabelText("材质")]
        public List<ESResourcePlanMaterialEntry> materials = new List<ESResourcePlanMaterialEntry>(4);
        [PropertyOrder(4), TitleGroup("常用资源"), LabelText("音频 Cue")]
        [InfoBox("只需选择需要预热的 Cue。资源烘焙会展开其 Unity Clip 依赖；运行时不会读取 Cue 的 Clip、Bank 或加载后端细节。")]
        public List<ESResourcePlanAudioCueEntry> audioCues = new List<ESResourcePlanAudioCueEntry>(8);

        [PropertyOrder(105), FoldoutGroup("更多资源"), LabelText("直接 AudioClip（兼容）")]
        [InfoBox("仅保留给已有计划兼容。新计划请使用“音频 Cue”。")]
        public List<ESResourcePlanAudioEntry> audioClips = new List<ESResourcePlanAudioEntry>(8);

        [PropertyOrder(100), FoldoutGroup("更多资源", Expanded = false), LabelText("网格")]
        public List<ESResourcePlanMeshEntry> meshes = new List<ESResourcePlanMeshEntry>(4);
        [PropertyOrder(101), FoldoutGroup("更多资源"), LabelText("纹理")]
        public List<ESResourcePlanTextureEntry> textures = new List<ESResourcePlanTextureEntry>(4);
        [PropertyOrder(102), FoldoutGroup("更多资源"), LabelText("二维纹理")]
        public List<ESResourcePlanTexture2DEntry> texture2Ds = new List<ESResourcePlanTexture2DEntry>(4);
        [PropertyOrder(103), FoldoutGroup("更多资源"), LabelText("精灵图集")]
        public List<ESResourcePlanSpriteAtlasEntry> spriteAtlases = new List<ESResourcePlanSpriteAtlasEntry>(4);
        [PropertyOrder(104), FoldoutGroup("更多资源"), LabelText("角色头像")]
        public List<ESResourcePlanAvatarEntry> avatars = new List<ESResourcePlanAvatarEntry>(4);
        [PropertyOrder(105), FoldoutGroup("更多资源"), LabelText("地形数据")]
        public List<ESResourcePlanTerrainEntry> terrainDatas = new List<ESResourcePlanTerrainEntry>(2);
        [PropertyOrder(106), FoldoutGroup("更多资源"), LabelText("动画片段")]
        public List<ESResourcePlanAnimationEntry> animationClips = new List<ESResourcePlanAnimationEntry>(4);
        [PropertyOrder(107), FoldoutGroup("更多资源"), LabelText("动画控制器")]
        public List<ESResourcePlanAnimatorEntry> animatorControllers = new List<ESResourcePlanAnimatorEntry>(4);
        [PropertyOrder(108), FoldoutGroup("更多资源"), LabelText("可播放资源")]
        public List<ESResourcePlanPlayableEntry> playableAssets = new List<ESResourcePlanPlayableEntry>(4);
        [PropertyOrder(109), FoldoutGroup("更多资源"), LabelText("时间轴")]
        public List<ESResourcePlanTimelineEntry> timelineAssets = new List<ESResourcePlanTimelineEntry>(4);
        [PropertyOrder(110), FoldoutGroup("更多资源"), LabelText("视频")]
        public List<ESResourcePlanVideoEntry> videoClips = new List<ESResourcePlanVideoEntry>(2);
        [PropertyOrder(111), FoldoutGroup("更多资源"), LabelText("数据资产")]
        public List<ESResourcePlanScriptableObjectEntry> scriptableObjects = new List<ESResourcePlanScriptableObjectEntry>(4);
        [PropertyOrder(112), FoldoutGroup("更多资源"), LabelText("Raw 二进制")]
        public List<ESResourcePlanRawEntry> rawAssets = new List<ESResourcePlanRawEntry>(4);

        [PropertyOrder(200)]
        [FoldoutGroup("GameCore 资源依赖", Expanded = false)]
        [LabelText("依赖来源")]
        [InfoBox("生成时会收集这些 GameCore 配置引用的资源。这里只引用配置，不会重复加载 GameCore 资产。")]
        [AssetsOnly]
        public List<ScriptableObject> gameCoreSources = new List<ScriptableObject>(2);

        [PropertyOrder(201)]
        [FoldoutGroup("GameCore 资源依赖")]
        [ShowInInspector, ReadOnly, LabelText("已生成的资源清单")]
        [ListDrawerSettings(ShowFoldout = true, IsReadOnly = true)]
        [SerializeField] private List<ESResourcePlanBakedAssetEntry> bakedAssets = new List<ESResourcePlanBakedAssetEntry>(16);
        [SerializeField, FoldoutGroup("扩展资源依赖"), ShowInInspector, ReadOnly, LabelText("已烘焙扩展快照")]
        private List<ESResourcePlanBakedExtensionEntry> bakedExtensions = new List<ESResourcePlanBakedExtensionEntry>(2);
        [SerializeField, HideInInspector] private string bakedExpansionHash = string.Empty;

        public IReadOnlyList<ESResourcePlanBakedAssetEntry> BakedAssets => bakedAssets;
        public IReadOnlyList<ESResourcePlanBakedExtensionEntry> BakedExtensions => bakedExtensions;
        public string BakedExpansionHash => bakedExpansionHash;

        public void ReplaceBakedAssets(List<ESResourcePlanBakedAssetEntry> entries, string inputHash)
        {
            bakedAssets = entries ?? new List<ESResourcePlanBakedAssetEntry>();
            bakedExpansionHash = inputHash ?? string.Empty;
        }

        public void ReplaceBakedExtensions(List<ESResourcePlanBakedExtensionEntry> entries)
        {
            bakedExtensions = entries ?? new List<ESResourcePlanBakedExtensionEntry>();
        }

        /// <summary>
        /// ActiveLinkList 的标准入口。实际 Scope、retain 与异步加载均由 GameManager 的
        /// ResourcePlans 服务统一管理，计划 SO 本身不持有任何运行时资源状态。
        /// </summary>
        public void OnLinkEnable()
        {
            ESGameManager.ResourcePlans?.RetainFromActiveLink(this);
        }

        /// <inheritdoc />
        public void OnLinkDisable()
        {
            ESGameManager.ResourcePlans?.ReleaseFromActiveLink(this);
        }
    }
}
