using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    public enum ESWorldMapTerrainMode
    {
        None = 0,
        UnityTerrain = 1,
        Heightfield = 2,
        Voxel = 3
    }

    [Serializable]
    public sealed class ESWorldMapSpaceTemplate
    {
        public string templateId = "default-space";
        public int gridWidth = 16;
        public int gridHeight = 16;
        public float cellSize = 16f;
        public bool sceneFreeAuthoring = true;
        public List<ESWorldMapSpaceAnchor> anchors = new List<ESWorldMapSpaceAnchor>();

        public bool IsValid(out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(templateId)) { error = "空间模板 templateId 不能为空。"; return false; }
            if (gridWidth <= 0 || gridHeight <= 0) { error = "空间模板网格尺寸必须大于 0。"; return false; }
            if (cellSize <= 0f || float.IsNaN(cellSize) || float.IsInfinity(cellSize)) { error = "空间模板 cellSize 无效。"; return false; }
            return true;
        }
    }

    [Serializable]
    public sealed class ESWorldMapSpaceAnchor
    {
        public string anchorId;
        public string category;
        public Vector2 normalizedPosition;
        public string contentKey;
    }

    [Serializable]
    public sealed class ESWorldMapHeightfield
    {
        public int width = 33;
        public int height = 33;
        [Range(0f, 1f)] public float defaultHeight;
        public List<float> samples = new List<float>();

        public bool IsValid(out string error)
        {
            error = null;
            if (width < 2 || height < 2) { error = "高度场尺寸至少为 2x2。"; return false; }
            int expected = width * height;
            if (samples != null && samples.Count != 0 && samples.Count != expected) { error = "高度场采样数量与尺寸不匹配。"; return false; }
            return true;
        }

        public void EnsureSamples()
        {
            int expected = Mathf.Max(2, width) * Mathf.Max(2, height);
            if (samples == null) samples = new List<float>(expected);
            while (samples.Count < expected) samples.Add(defaultHeight);
            if (samples.Count > expected) samples.RemoveRange(expected, samples.Count - expected);
        }

        public float Get(int x, int y)
        {
            EnsureSamples();
            x = Mathf.Clamp(x, 0, width - 1);
            y = Mathf.Clamp(y, 0, height - 1);
            return samples[y * width + x];
        }

        public void Set(int x, int y, float value)
        {
            EnsureSamples();
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            samples[y * width + x] = Mathf.Clamp01(value);
        }

        public float SampleNormalized(float u, float v)
        {
            EnsureSamples();
            float fx = Mathf.Clamp01(u) * (width - 1);
            float fy = Mathf.Clamp01(v) * (height - 1);
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            int x1 = Mathf.Min(x0 + 1, width - 1);
            int y1 = Mathf.Min(y0 + 1, height - 1);
            float tx = fx - x0;
            float ty = fy - y0;
            return Mathf.Lerp(Mathf.Lerp(Get(x0, y0), Get(x1, y0), tx), Mathf.Lerp(Get(x0, y1), Get(x1, y1), tx), ty);
        }

        public Vector3 SampleNormal(float u, float v, float worldWidth, float worldDepth, float heightScale)
        {
            float du = 1f / Mathf.Max(1, width - 1);
            float dv = 1f / Mathf.Max(1, height - 1);
            float left = SampleNormalized(u - du, v) * heightScale;
            float right = SampleNormalized(u + du, v) * heightScale;
            float down = SampleNormalized(u, v - dv) * heightScale;
            float up = SampleNormalized(u, v + dv) * heightScale;
            return Vector3.Normalize(new Vector3((left - right) / Mathf.Max(0.001f, worldWidth * du * 2f), 1f, (down - up) / Mathf.Max(0.001f, worldDepth * dv * 2f)));
        }
    }

    [Serializable]
    public struct ESWorldMapTerrainSample
    {
        public bool valid;
        public Vector3 worldPosition;
        public Vector3 normal;
        public float normalizedHeight;
        public float slopeDegrees;
        public bool walkable;
        public string surfaceTag;
    }

    [Serializable]
    public sealed class ESWorldMapSurfaceDefinition
    {
        public string surfaceTag = "Ground";
        public string displayName = "地面";
        public float movementMultiplier = 1f;
        public float friction = 1f;
        public string footstepCueKey;

        public bool IsValid(out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(surfaceTag)) { error = "地表 surfaceTag 不能为空。"; return false; }
            if (movementMultiplier < 0f || float.IsNaN(movementMultiplier) || float.IsInfinity(movementMultiplier)) { error = "地表 movementMultiplier 无效。"; return false; }
            if (friction < 0f || float.IsNaN(friction) || float.IsInfinity(friction)) { error = "地表 friction 无效。"; return false; }
            return true;
        }
    }

    [Serializable]
    public sealed class ESWorldMapMaterialLayer
    {
        public string layerId = "ground";
        public string materialKey;
        public string surfaceTag = "Ground";
        [Range(0f, 1f)] public float minHeight;
        [Range(0f, 1f)] public float maxHeight = 1f;
        [Range(0f, 90f)] public float maxSlope = 90f;
    }

    [Serializable]
    public sealed class ESWorldMapVegetationLayer
    {
        public string layerId = "vegetation";
        public string prefabSetKey;
        public string biomeTag = "Default";
        public int density = 100;
        public float minScale = 0.85f;
        public float maxScale = 1.15f;
        public bool alignToTerrain = true;
    }

    [Serializable]
    public sealed class ESWorldMapPrefabScatterLayer
    {
        public string layerId = "scatter";
        public string prefabSetKey;
        public int seed;
        public int count = 32;
        public float minSpacing = 4f;
        public float maxSlope = 35f;
    }

    [Serializable]
    public sealed class ESWorldMapNavigationSettings
    {
        public bool enabled = true;
        public float agentRadius = 0.4f;
        public float agentHeight = 1.8f;
        public float maxSlope = 45f;
        public float voxelSize = 0.2f;
        public string bakeProfileKey = "default";
    }

    [Serializable]
    public sealed class ESWorldMapWaterWeatherSettings
    {
        public bool waterEnabled;
        public string waterProfileKey;
        public bool weatherEnabled = true;
        public string weatherProfileKey = "default";
        [Range(0f, 1f)] public float ambientWetness;
    }

    [Serializable]
    public sealed class ESWorldMapStreamingSettings
    {
        public bool enabled = true;
        public int chunkRadius = 2;
        public int maxLoadedChunks = 16;
        public bool loadCollisionFirst = true;
        public bool unloadFarChunks = true;
    }

    [Serializable]
    public sealed class ESWorldMapCollisionSettings
    {
        public bool terrainCollider = true;
        public bool physicsMaterialEnabled;
        public string physicsMaterialKey;
        public bool generateTriggerVolume;
    }

    [Serializable]
    public sealed class ESWorldMapBuildSettings
    {
        public string outputKey = "world.map.baked";
        public string resourceLibraryPath = string.Empty;
        public string runtimeOutputPath = "ES/ResourcePipeline/Baked/world";
        public int buildVersion = 1;
        public bool includeEditorPreview;
        public bool includeNavigation = true;
        public bool includeVegetation = true;
        public bool includeScatter = true;
        public string formalSceneAssetPath = string.Empty;
        public string navigationDataAssetPath = string.Empty;
    }

    [Serializable]
    public sealed class ESWorldMapUgcLimits
    {
        public int maxWorldSize = 4096;
        public int maxLayers = 64;
        public int maxPrefabInstances = 10000;
        public int maxTerrainSamples = 4194304;
        public int maxBuildSeconds = 120;
        public int maxAssetBytes = 268435456;
    }

    /// <summary>
    /// 地图内容来源。来源只决定地图如何被构建，不改变 WorldDomain 对运行时状态的所有权。
    /// </summary>
    public enum ESWorldMapSourceMode
    {
        Procedural = 0,
        Scene = 1,
        Prefab = 2
    }

    [Serializable]
    public sealed class ESWorldMapDefinition
    {
        public const int CurrentSchemaVersion = 1;
        public string mapId;
        public int schemaVersion = CurrentSchemaVersion;
        public int contentVersion = 1;
        public string contentHash;
        public ESWorldMapSourceMode sourceMode = ESWorldMapSourceMode.Procedural;
        public string generatorKey;
        public int generatorVersion = 1;
        public int seed;
        public string sceneAssetKey;
        public string layoutAssetKey;
        public string prefabSetKey;
        public ESWorldMapTerrainMode terrainMode = ESWorldMapTerrainMode.UnityTerrain;
        public string terrainAssetKey;
        public string terrainDataAssetPath;
        public string heightmapAssetKey;
        public string terrainMaterialSetKey;
        public float terrainHeightScale = 80f;
        public float maxWalkableSlope = 45f;
        public string defaultSurfaceTag = "Ground";
        public List<ESWorldMapSurfaceDefinition> surfaces = new List<ESWorldMapSurfaceDefinition>();
        public List<ESWorldMapMaterialLayer> materialLayers = new List<ESWorldMapMaterialLayer>();
        public List<ESWorldMapVegetationLayer> vegetationLayers = new List<ESWorldMapVegetationLayer>();
        public List<ESWorldMapPrefabScatterLayer> scatterLayers = new List<ESWorldMapPrefabScatterLayer>();
        public ESWorldMapNavigationSettings navigation = new ESWorldMapNavigationSettings();
        public ESWorldMapWaterWeatherSettings waterWeather = new ESWorldMapWaterWeatherSettings();
        public ESWorldMapStreamingSettings streaming = new ESWorldMapStreamingSettings();
        public ESWorldMapCollisionSettings collision = new ESWorldMapCollisionSettings();
        public ESWorldMapBuildSettings build = new ESWorldMapBuildSettings();
        public ESWorldMapUgcLimits ugcLimits = new ESWorldMapUgcLimits();
        public ESWorldMapHeightfield heightfield = new ESWorldMapHeightfield();
        public ESWorldMapSpaceTemplate spaceTemplate = new ESWorldMapSpaceTemplate();
        public Vector2 worldMin;
        public Vector2 worldMax;
        public float chunkSize = 64f;
        public List<ESWorldMapRegionDefinition> regions = new List<ESWorldMapRegionDefinition>();
        public List<ESWorldMapPoiDefinition> pois = new List<ESWorldMapPoiDefinition>();
        public List<ESWorldMapPrefabPlacement> prefabPlacements = new List<ESWorldMapPrefabPlacement>();
        public List<ESWorldDialoguePlacement> dialoguePlacements = new List<ESWorldDialoguePlacement>();

        /// <summary>
        /// 补齐旧资产、草稿反序列化和域重载后可能为空的作者容器。
        /// 该方法只建立缺失容器，不重写已有内容，供编辑器会话和运行时校验共享。
        /// </summary>
        public void EnsureAuthoringContainers()
        {
            surfaces ??= new List<ESWorldMapSurfaceDefinition>();
            materialLayers ??= new List<ESWorldMapMaterialLayer>();
            vegetationLayers ??= new List<ESWorldMapVegetationLayer>();
            scatterLayers ??= new List<ESWorldMapPrefabScatterLayer>();
            navigation ??= new ESWorldMapNavigationSettings();
            waterWeather ??= new ESWorldMapWaterWeatherSettings();
            streaming ??= new ESWorldMapStreamingSettings();
            collision ??= new ESWorldMapCollisionSettings();
            build ??= new ESWorldMapBuildSettings();
            ugcLimits ??= new ESWorldMapUgcLimits();
            heightfield ??= new ESWorldMapHeightfield();
            spaceTemplate ??= new ESWorldMapSpaceTemplate();
            regions ??= new List<ESWorldMapRegionDefinition>();
            pois ??= new List<ESWorldMapPoiDefinition>();
            prefabPlacements ??= new List<ESWorldMapPrefabPlacement>();
            dialoguePlacements ??= new List<ESWorldDialoguePlacement>();
        }

        public bool IsValid(out string error)
        {
            EnsureAuthoringContainers();
            error = null;
            if (string.IsNullOrWhiteSpace(mapId)) { error = "地图 mapId 不能为空。"; return false; }
            if (schemaVersion != CurrentSchemaVersion) { error = "地图 schemaVersion 不受支持：" + schemaVersion; return false; }
            if (contentVersion <= 0 || string.IsNullOrWhiteSpace(contentHash)) { error = "地图内容版本或 Hash 无效。"; return false; }
            if (!ValidateSource(out error)) return false;
            if (!ValidateTerrain(out error)) return false;
            if (heightfield == null || !heightfield.IsValid(out error)) return false;
            if (spaceTemplate == null || !spaceTemplate.IsValid(out error)) return false;
            if (worldMax.x <= worldMin.x || worldMax.y <= worldMin.y) { error = "地图世界范围无效。"; return false; }
            if (chunkSize <= 0f || float.IsNaN(chunkSize) || float.IsInfinity(chunkSize)) { error = "地图 chunkSize 无效。"; return false; }
            if (terrainHeightScale <= 0f || float.IsNaN(terrainHeightScale) || float.IsInfinity(terrainHeightScale)) { error = "地图 terrainHeightScale 无效。"; return false; }
            if (maxWalkableSlope < 0f || maxWalkableSlope > 90f) { error = "地图 maxWalkableSlope 必须在 0 到 90 度之间。"; return false; }
            if (surfaces != null)
            {
                var surfaceIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < surfaces.Count; i++)
                {
                    ESWorldMapSurfaceDefinition surface = surfaces[i];
                    if (surface == null || !surface.IsValid(out error) || !surfaceIds.Add(surface.surfaceTag))
                    { if (error == null) error = "地表 surfaceTag 重复。"; return false; }
                }
            }
            if (navigation == null || navigation.agentRadius <= 0f || navigation.agentHeight <= 0f || navigation.maxSlope < 0f || navigation.maxSlope > 90f) { error = "导航配置无效。"; return false; }
            if (streaming == null || streaming.chunkRadius < 0 || streaming.maxLoadedChunks <= 0) { error = "流式加载配置无效。"; return false; }
            if (build == null || build.buildVersion <= 0 || string.IsNullOrWhiteSpace(build.outputKey) || string.IsNullOrWhiteSpace(build.runtimeOutputPath)) { error = "地图构建配置无效。"; return false; }
            if (ugcLimits == null || ugcLimits.maxWorldSize <= 0 || ugcLimits.maxLayers <= 0 || ugcLimits.maxPrefabInstances < 0 || ugcLimits.maxTerrainSamples <= 0) { error = "UGC 配额配置无效。"; return false; }
            if (worldMax.x - worldMin.x > ugcLimits.maxWorldSize || worldMax.y - worldMin.y > ugcLimits.maxWorldSize) { error = "地图尺寸超过 UGC 配额。"; return false; }
            int layerCount = (materialLayers == null ? 0 : materialLayers.Count) + (vegetationLayers == null ? 0 : vegetationLayers.Count) + (scatterLayers == null ? 0 : scatterLayers.Count);
            if (layerCount > ugcLimits.maxLayers) { error = "地图层数量超过 UGC 配额。"; return false; }
            if (!ValidateLayerIds(materialLayers, out error) || !ValidateLayerIds(vegetationLayers, out error) || !ValidateLayerIds(scatterLayers, out error)) return false;
            if (materialLayers != null)
                for (int i = 0; i < materialLayers.Count; i++)
                {
                    ESWorldMapMaterialLayer layer = materialLayers[i];
                    if (layer == null || layer.minHeight < 0f || layer.maxHeight > 1f || layer.minHeight > layer.maxHeight || layer.maxSlope < 0f || layer.maxSlope > 90f)
                    { error = "地形材质层参数无效。"; return false; }
                }
            if (vegetationLayers != null)
                for (int i = 0; i < vegetationLayers.Count; i++)
                {
                    ESWorldMapVegetationLayer layer = vegetationLayers[i];
                    if (layer == null || layer.density < 0 || layer.minScale <= 0f || layer.maxScale < layer.minScale)
                    { error = "植被层参数无效。"; return false; }
                }
            if (ugcLimits.maxBuildSeconds <= 0 || ugcLimits.maxAssetBytes <= 0) { error = "UGC 构建时长或资源体积配额无效。"; return false; }
            if (scatterLayers != null)
            {
                int scatterCount = 0;
                for (int i = 0; i < scatterLayers.Count; i++) if (scatterLayers[i] != null) scatterCount += Mathf.Max(0, scatterLayers[i].count);
                if (scatterCount > ugcLimits.maxPrefabInstances) { error = "Prefab 散布数量超过 UGC 配额。"; return false; }
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (regions != null)
                for (int i = 0; i < regions.Count; i++)
                {
                    ESWorldMapRegionDefinition item = regions[i];
                    if (item == null || !item.IsValid(out error) || !ids.Add(item.regionId))
                    { if (error == null) error = "地图区域 ID 重复。"; return false; }
                    if (item.min.x < worldMin.x || item.min.y < worldMin.y || item.max.x > worldMax.x || item.max.y > worldMax.y)
                    { error = "地图区域超出世界范围：" + item.regionId; return false; }
                }
            ids.Clear();
            if (pois != null)
                for (int i = 0; i < pois.Count; i++)
                {
                    ESWorldMapPoiDefinition item = pois[i];
                    if (item == null || !item.IsValid(out error) || !ids.Add(item.poiId))
                    { if (error == null) error = "地图 POI ID 重复。"; return false; }
                    if (!Contains(item.position)) { error = "地图 POI 超出世界范围：" + item.poiId; return false; }
                    if (!string.IsNullOrWhiteSpace(item.regionId) && !TryGetRegion(item.regionId, out _))
                    { error = "地图 POI 引用了不存在的区域：" + item.poiId; return false; }
                }
            ids.Clear();
            if (prefabPlacements != null)
                for (int i = 0; i < prefabPlacements.Count; i++)
                {
                    ESWorldMapPrefabPlacement item = prefabPlacements[i];
                    if (item == null || !item.IsValid(out error) || !ids.Add(item.placementId))
                    { if (error == null) error = "Prefab 放置 ID 重复。"; return false; }
                    if (!Contains(new Vector2(item.position.x, item.position.z)))
                    { error = "Prefab 放置超出世界范围：" + item.placementId; return false; }
                    if (!string.IsNullOrWhiteSpace(item.regionId) && !TryGetRegion(item.regionId, out _))
                    { error = "Prefab 放置引用了不存在的区域：" + item.placementId; return false; }
                }
            if (dialoguePlacements != null)
            {
                var placementIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < dialoguePlacements.Count; i++)
                {
                    ESWorldDialoguePlacement placement = dialoguePlacements[i];
                    if (placement == null || string.IsNullOrWhiteSpace(placement.placementId) || !placementIds.Add(placement.placementId))
                    { error = "对话放置 placementId 为空或重复。"; return false; }
                    if (string.IsNullOrWhiteSpace(placement.dialogueGraphKey))
                    { error = "对话放置缺少 dialogueGraphKey：" + placement.placementId; return false; }
                    if (placement.space == ESWorldDialoguePlacementSpace.Map2D && !Contains(new Vector2(placement.position.x, placement.position.z)))
                    { error = "2D 对话放置超出地图范围：" + placement.placementId; return false; }
                }
            }
            return true;
        }

        private static bool ValidateLayerIds<T>(List<T> layers, out string error) where T : class
        {
            error = null;
            if (layers == null) return true;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < layers.Count; i++)
            {
                string id = null;
                if (layers[i] is ESWorldMapMaterialLayer material) id = material.layerId;
                else if (layers[i] is ESWorldMapVegetationLayer vegetation) id = vegetation.layerId;
                else if (layers[i] is ESWorldMapPrefabScatterLayer scatter) id = scatter.layerId;
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) { error = "地图层 ID 为空或重复。"; return false; }
            }
            return true;
        }

        private bool ValidateTerrain(out string error)
        {
            error = null;
            if (terrainMode == ESWorldMapTerrainMode.None) return true;
            if (terrainMode == ESWorldMapTerrainMode.UnityTerrain) return true;
            if (terrainMode == ESWorldMapTerrainMode.Heightfield && string.IsNullOrWhiteSpace(heightmapAssetKey))
            { error = "Heightfield 模式必须提供 heightmapAssetKey。"; return false; }
            if (terrainMode == ESWorldMapTerrainMode.Voxel && string.IsNullOrWhiteSpace(terrainAssetKey))
            { error = "Voxel 模式必须提供 terrainAssetKey。"; return false; }
            return true;
        }

        private bool ValidateSource(out string error)
        {
            error = null;
            switch (sourceMode)
            {
                case ESWorldMapSourceMode.Procedural:
                    if (string.IsNullOrWhiteSpace(generatorKey)) { error = "随机地图必须提供 generatorKey。"; return false; }
                    if (generatorVersion <= 0) { error = "随机地图 generatorVersion 必须大于 0。"; return false; }
                    return true;
                case ESWorldMapSourceMode.Scene:
                    if (string.IsNullOrWhiteSpace(sceneAssetKey)) { error = "子场景地图必须提供 sceneAssetKey。"; return false; }
                    return true;
                case ESWorldMapSourceMode.Prefab:
                    if (string.IsNullOrWhiteSpace(layoutAssetKey) && string.IsNullOrWhiteSpace(prefabSetKey)) { error = "预制件地图必须提供 layoutAssetKey 或 prefabSetKey。"; return false; }
                    return true;
                default:
                    error = "地图来源模式不受支持：" + sourceMode;
                    return false;
            }
        }

        public bool TryGetRegion(string regionId, out ESWorldMapRegionDefinition region)
        {
            region = null;
            if (regions == null || string.IsNullOrWhiteSpace(regionId)) return false;
            for (int i = 0; i < regions.Count; i++)
                if (regions[i] != null && string.Equals(regions[i].regionId, regionId, StringComparison.Ordinal)) { region = regions[i]; return true; }
            return false;
        }

        public bool TryGetPoi(string poiId, out ESWorldMapPoiDefinition poi)
        {
            poi = null;
            if (pois == null || string.IsNullOrWhiteSpace(poiId)) return false;
            for (int i = 0; i < pois.Count; i++)
                if (pois[i] != null && string.Equals(pois[i].poiId, poiId, StringComparison.Ordinal)) { poi = pois[i]; return true; }
            return false;
        }

        public bool Contains(Vector2 position) => position.x >= worldMin.x && position.x <= worldMax.x && position.y >= worldMin.y && position.y <= worldMax.y;
    }

    [CreateAssetMenu(fileName = "ESWorldMap", menuName = "【ES】/世界/地图定义", order = 120)]
    public sealed partial class ESWorldMapAsset : ScriptableObject
    {
        [SerializeField] private ESWorldMapDefinition definition = new ESWorldMapDefinition();

        public ESWorldMapDefinition Definition => definition;

        private void OnEnable()
        {
            EnsureAuthoringContainers();
        }

        public void EnsureAuthoringContainers()
        {
            definition ??= new ESWorldMapDefinition();
            definition.EnsureAuthoringContainers();
        }

        public bool Validate(out string error)
        {
            EnsureAuthoringContainers();
            error = null;
            if (definition == null) { error = "地图资产缺少定义。"; return false; }
            return definition.IsValid(out error);
        }
    }

    [Serializable]
    public sealed class ESWorldMapRegionDefinition
    {
        public string regionId;
        public string displayName;
        public string semanticTag;
        public Vector2 min;
        public Vector2 max;
        public int priority;
        public bool IsValid(out string error) { error = null; if (string.IsNullOrWhiteSpace(regionId)) { error = "地图区域 regionId 不能为空。"; return false; } if (max.x <= min.x || max.y <= min.y) { error = "地图区域范围无效：" + regionId; return false; } return true; }
        public bool Contains(Vector2 position) => position.x >= min.x && position.x <= max.x && position.y >= min.y && position.y <= max.y;
    }

    [Serializable]
    public sealed class ESWorldMapPoiDefinition
    {
        public string poiId;
        public string displayName;
        public string category;
        public string regionId;
        public Vector2 position;
        public bool discoverable = true;
        public bool IsValid(out string error) { error = null; if (string.IsNullOrWhiteSpace(poiId)) { error = "地图 POI poiId 不能为空。"; return false; } if (string.IsNullOrWhiteSpace(category)) { error = "地图 POI category 不能为空：" + poiId; return false; } return true; }
    }

    [Serializable]
    public sealed class ESWorldMapPrefabPlacement
    {
        public string placementId;
        public string prefabKey;
        public string editorPrefabGuid;
        /// <summary>
        /// Optional formal Scene binding. These strings are editor-owned evidence;
        /// an empty value means this author placement has no formal Scene object yet.
        /// PreviewScene objects must never populate these fields.
        /// </summary>
        public string formalScenePath = string.Empty;
        public string formalObjectGlobalId = string.Empty;
        public string regionId;
        public Vector3 position;
        public Vector3 rotationEuler;
        public Vector3 scale = Vector3.one;
        public bool enabled = true;

        public bool IsValid(out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(placementId)) { error = "Prefab 放置 placementId 不能为空。"; return false; }
            if (string.IsNullOrWhiteSpace(prefabKey)) { error = "Prefab 放置 prefabKey 不能为空：" + placementId; return false; }
            if (string.IsNullOrWhiteSpace(formalScenePath) != string.IsNullOrWhiteSpace(formalObjectGlobalId))
            { error = "Prefab 正式 Scene 映射必须同时提供 formalScenePath 与 formalObjectGlobalId：" + placementId; return false; }
            if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f) { error = "Prefab 放置缩放必须大于 0：" + placementId; return false; }
            return true;
        }
    }

    [Serializable]
    public sealed class ESWorldMapRuntimeState
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string mapId;
        public int contentVersion;
        public string contentHash;
        public List<string> discoveredRegionIds = new List<string>();
        public List<string> unlockedPoiIds = new List<string>();
        public ESWorldMapRuntimeState Clone() => new ESWorldMapRuntimeState { schemaVersion = schemaVersion, mapId = mapId, contentVersion = contentVersion, contentHash = contentHash, discoveredRegionIds = discoveredRegionIds != null ? new List<string>(discoveredRegionIds) : new List<string>(), unlockedPoiIds = unlockedPoiIds != null ? new List<string>(unlockedPoiIds) : new List<string>() };
        public bool IsValid(out string error) { error = null; if (schemaVersion != CurrentSchemaVersion) { error = "地图运行状态 schemaVersion 不受支持：" + schemaVersion; return false; } if (string.IsNullOrWhiteSpace(mapId)) { error = "地图运行状态 mapId 不能为空。"; return false; } if (contentVersion <= 0 || string.IsNullOrWhiteSpace(contentHash)) { error = "地图运行状态内容签名无效。"; return false; } return true; }
    }

}
