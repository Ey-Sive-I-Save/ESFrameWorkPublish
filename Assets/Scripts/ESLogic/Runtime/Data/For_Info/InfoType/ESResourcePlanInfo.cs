using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    public enum ESResourcePlanTargetKind : byte
    {
        [InspectorName("通用")] Global,
        [InspectorName("关卡")] Level,
        [InspectorName("地图")] Map,
        [InspectorName("游戏模式")] GameMode,
        [InspectorName("区域")] Region,
        [InspectorName("遭遇 / Boss")] Encounter
    }

    [Serializable]
    public abstract class ESResourcePlanEntryBase
    {
        [LabelText("必须就绪")]
        [InfoBox("必须资源失败会使整个计划失败；关闭后只记录该资源错误，不阻断其他资源。")]
        public bool required = true;
    }

    [Serializable]
    public sealed class ESResourcePlanPrefabEntry : ESResourcePlanEntryBase
    {
        [LabelText("Prefab Key"), InlineProperty] public ESAssetReferPrefabConfigKey key = new ESAssetReferPrefabConfigKey();
        [LabelText("对象池 Key")] public string poolKey;
        [LabelText("预热数量"), MinValue(0)] public int prewarmCount;
        [LabelText("独立池配置")] public bool useCustomPoolConfig;
        [ShowIf(nameof(useCustomPoolConfig)), HideLabel] public ESGameObjectPoolConfig poolConfig = new ESGameObjectPoolConfig();
    }

    [Serializable] public sealed class ESResourcePlanSpriteEntry : ESResourcePlanEntryBase { [InlineProperty] public ESAssetReferSpriteConfigKey key = new ESAssetReferSpriteConfigKey(); }
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

    [ESCreatePath("数据信息", "资源计划")]
    public sealed class ESResourcePlanInfo : SoDataInfo
    {
        [Title("计划归属"), LabelText("服务对象")]
        public ESResourcePlanTargetKind targetKind;

        [ShowIf(nameof(NeedsTargetInfoKey)), LabelText("所属配置键")]
        public string targetInfoKey;

        [Title("Prefab 与对象池"), LabelText("Prefab")]
        public List<ESResourcePlanPrefabEntry> prefabs = new List<ESResourcePlanPrefabEntry>(8);

        [Title("图形资源"), LabelText("Sprite")]
        public List<ESResourcePlanSpriteEntry> sprites = new List<ESResourcePlanSpriteEntry>(8);
        [LabelText("Material")] public List<ESResourcePlanMaterialEntry> materials = new List<ESResourcePlanMaterialEntry>(4);
        [LabelText("Mesh")] public List<ESResourcePlanMeshEntry> meshes = new List<ESResourcePlanMeshEntry>(4);
        [LabelText("Texture")] public List<ESResourcePlanTextureEntry> textures = new List<ESResourcePlanTextureEntry>(4);
        [LabelText("Texture2D")] public List<ESResourcePlanTexture2DEntry> texture2Ds = new List<ESResourcePlanTexture2DEntry>(4);
        [LabelText("SpriteAtlas")] public List<ESResourcePlanSpriteAtlasEntry> spriteAtlases = new List<ESResourcePlanSpriteAtlasEntry>(4);
        [LabelText("Avatar")] public List<ESResourcePlanAvatarEntry> avatars = new List<ESResourcePlanAvatarEntry>(4);
        [LabelText("TerrainData")] public List<ESResourcePlanTerrainEntry> terrainDatas = new List<ESResourcePlanTerrainEntry>(2);

        [Title("声音与播放"), LabelText("AudioClip")]
        public List<ESResourcePlanAudioEntry> audioClips = new List<ESResourcePlanAudioEntry>(8);
        [LabelText("AnimationClip")] public List<ESResourcePlanAnimationEntry> animationClips = new List<ESResourcePlanAnimationEntry>(4);
        [LabelText("AnimatorController")] public List<ESResourcePlanAnimatorEntry> animatorControllers = new List<ESResourcePlanAnimatorEntry>(4);
        [LabelText("PlayableAsset")] public List<ESResourcePlanPlayableEntry> playableAssets = new List<ESResourcePlanPlayableEntry>(4);
        [LabelText("TimelineAsset")] public List<ESResourcePlanTimelineEntry> timelineAssets = new List<ESResourcePlanTimelineEntry>(4);
        [LabelText("VideoClip")] public List<ESResourcePlanVideoEntry> videoClips = new List<ESResourcePlanVideoEntry>(2);

        [Title("数据资产"), LabelText("ScriptableObject")]
        public List<ESResourcePlanScriptableObjectEntry> scriptableObjects = new List<ESResourcePlanScriptableObjectEntry>(4);

        [Title("退出处理"), LabelText("退出时自动释放")]
        public bool releaseOnExit = true;
        [ShowIf(nameof(releaseOnExit)), LabelText("延迟释放秒数"), MinValue(0f)]
        public float releaseDelaySeconds = 10f;

        private bool NeedsTargetInfoKey() => targetKind != ESResourcePlanTargetKind.Global;
    }
}
