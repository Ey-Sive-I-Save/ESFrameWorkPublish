#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

namespace ES
{
    internal sealed class ESWorldMapTerrainPreviewHandle
    {
        public Scene previewScene;
        public GameObject terrainObject;
        public TerrainData terrainData;
        public bool persistent;
    }

    internal interface IESWorldMapTerrainEditorBackend
    {
        bool CanHandle(ESWorldMapTerrainMode mode);
        bool TryCreatePreview(ESWorldMapDefinition definition, Scene previewScene, out ESWorldMapTerrainPreviewHandle handle, out string error);
        bool TryPaintHeight(ESWorldMapDefinition definition, Vector2 worldPoint, Vector2 worldMin, Vector2 worldMax, float normalizedHeight, out string error);
        bool TryBakePersistent(ESWorldMapAsset asset, string terrainDataPath, string scenePath, out string error);
    }

    internal sealed class ESWorldMapUnityTerrainEditorBackend : IESWorldMapTerrainEditorBackend
    {
        public bool CanHandle(ESWorldMapTerrainMode mode)
            => mode == ESWorldMapTerrainMode.UnityTerrain || mode == ESWorldMapTerrainMode.Heightfield;

        public bool TryCreatePreview(ESWorldMapDefinition definition, Scene previewScene, out ESWorldMapTerrainPreviewHandle handle, out string error)
        {
            handle = null;
            error = string.Empty;
            if (definition == null || definition.heightfield == null)
            {
                error = "地图定义缺少 Heightfield，无法创建 Unity Terrain 预览。";
                return false;
            }
            if (!previewScene.IsValid())
            {
                error = "PreviewScene 无效，无法创建 Unity Terrain 预览。";
                return false;
            }

            ESWorldMapHeightfield field = definition.heightfield;
            field.EnsureSamples();
            int resolution = GetTerrainResolution(Mathf.Max(field.width, field.height));
            TerrainData data = new TerrainData
            {
                heightmapResolution = resolution,
                size = new Vector3(
                    Mathf.Max(1f, definition.worldMax.x - definition.worldMin.x),
                    Mathf.Max(1f, definition.terrainHeightScale),
                    Mathf.Max(1f, definition.worldMax.y - definition.worldMin.y))
            };
            float[,] heights = new float[resolution, resolution];
            for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                {
                    int sx = Mathf.RoundToInt(x / (float)(resolution - 1) * (field.width - 1));
                    int sy = Mathf.RoundToInt(y / (float)(resolution - 1) * (field.height - 1));
                    heights[y, x] = field.Get(sx, sy);
                }
            data.SetHeights(0, 0, heights);
            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            SceneManager.MoveGameObjectToScene(terrainObject, previewScene);
            terrainObject.name = "ES 临时 Unity Terrain 预览";
            handle = new ESWorldMapTerrainPreviewHandle
            {
                previewScene = previewScene,
                terrainObject = terrainObject,
                terrainData = data
            };
            return true;
        }

        public bool TryPaintHeight(ESWorldMapDefinition definition, Vector2 worldPoint, Vector2 worldMin, Vector2 worldMax, float normalizedHeight, out string error)
        {
            error = string.Empty;
            if (definition == null || definition.heightfield == null) { error = "地图定义缺少 Heightfield。"; return false; }
            if (worldMax.x <= worldMin.x || worldMax.y <= worldMin.y) { error = "地图范围无效。"; return false; }
            ESWorldMapHeightfield field = definition.heightfield;
            field.EnsureSamples();
            int x = Mathf.RoundToInt(Mathf.InverseLerp(worldMin.x, worldMax.x, worldPoint.x) * (field.width - 1));
            int y = Mathf.RoundToInt(Mathf.InverseLerp(worldMin.y, worldMax.y, worldPoint.y) * (field.height - 1));
            field.Set(x, y, normalizedHeight);
            return true;
        }

        public bool TryBakePersistent(ESWorldMapAsset asset, string terrainDataPath, string scenePath, out string error)
        {
            error = string.Empty;
            if (asset == null || asset.Definition == null) { error = "地图资产无效。"; return false; }
            ESWorldMapDefinition definition = asset.Definition;
            if (string.IsNullOrWhiteSpace(terrainDataPath) || !terrainDataPath.StartsWith("Assets/", System.StringComparison.Ordinal)) { error = "TerrainData 路径必须位于 Assets/ 下。"; return false; }
            ESWorldMapHeightfield field = definition.heightfield;
            if (field == null || !field.IsValid(out error)) return false;
            field.EnsureSamples();
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataPath);
            if (data == null)
            {
                data = new TerrainData { heightmapResolution = GetTerrainResolution(Mathf.Max(field.width, field.height)) };
                AssetDatabase.CreateAsset(data, terrainDataPath);
            }
            data.heightmapResolution = GetTerrainResolution(Mathf.Max(field.width, field.height));
            data.size = new Vector3(Mathf.Max(1f, definition.worldMax.x - definition.worldMin.x), Mathf.Max(1f, definition.terrainHeightScale), Mathf.Max(1f, definition.worldMax.y - definition.worldMin.y));
            int resolution = data.heightmapResolution;
            float[,] heights = new float[resolution, resolution];
            for (int y = 0; y < resolution; y++) for (int x = 0; x < resolution; x++)
            {
                int sx = Mathf.RoundToInt(x / (float)(resolution - 1) * (field.width - 1));
                int sy = Mathf.RoundToInt(y / (float)(resolution - 1) * (field.height - 1));
                heights[y, x] = field.Get(sx, sy);
            }
            data.SetHeights(0, 0, heights);
            EditorUtility.SetDirty(data);
            definition.terrainDataAssetPath = terrainDataPath;
            definition.terrainAssetKey = terrainDataPath;
            AssetDatabase.SaveAssetIfDirty(data);
            if (!string.IsNullOrWhiteSpace(scenePath))
            {
                if (!scenePath.StartsWith("Assets/", System.StringComparison.Ordinal)) { error = "场景路径必须位于 Assets/ 下。"; return false; }
                Scene scene = File.Exists(scenePath) ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single) : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                GameObject terrainObject = null;
                Terrain[] terrains = Object.FindObjectsOfType<Terrain>();
                for (int i = 0; i < terrains.Length; i++) if (terrains[i].terrainData == data) { terrainObject = terrains[i].gameObject; break; }
                if (terrainObject == null)
                {
                    terrainObject = Terrain.CreateTerrainGameObject(data);
                    terrainObject.name = asset.name + "_Terrain";
                    SceneManager.MoveGameObjectToScene(terrainObject, scene);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (File.Exists(scenePath)) EditorSceneManager.SaveScene(scene);
                else EditorSceneManager.SaveScene(scene, scenePath);
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return true;
        }

        private static int GetTerrainResolution(int requested)
        {
            requested = Mathf.Clamp(requested, 33, 2049);
            int exponent = Mathf.RoundToInt(Mathf.Log(requested - 1, 2f));
            exponent = Mathf.Clamp(exponent, 5, 11);
            return (1 << exponent) + 1;
        }
    }

    /// <summary>地图绘制和预览的唯一 Editor 入口，业务窗口不直接选择后端实现。</summary>
    internal static class ESWorldMapTerrainEditorFacade
    {
        private static readonly IESWorldMapTerrainEditorBackend UnityTerrainBackend = new ESWorldMapUnityTerrainEditorBackend();

        public static bool TryCreatePreview(ESWorldMapDefinition definition, Scene previewScene, out ESWorldMapTerrainPreviewHandle handle, out string error)
        {
            if (definition == null)
            {
                handle = null;
                error = "地图定义为空。";
                return false;
            }
            if (!UnityTerrainBackend.CanHandle(definition.terrainMode))
            {
                handle = null;
                error = "当前地形后端暂未提供 Editor 预览实现：" + definition.terrainMode;
                return false;
            }
            return UnityTerrainBackend.TryCreatePreview(definition, previewScene, out handle, out error);
        }

        public static void DestroyPreview(ESWorldMapTerrainPreviewHandle handle)
        {
            if (handle == null) return;
            if (handle.terrainObject != null) Object.DestroyImmediate(handle.terrainObject);
            if (handle.terrainData != null) Object.DestroyImmediate(handle.terrainData);
            handle.terrainObject = null;
            handle.terrainData = null;
        }

        public static bool TryPaintHeight(ESWorldMapDefinition definition, Vector2 worldPoint, Vector2 worldMin, Vector2 worldMax, float normalizedHeight, out string error)
        {
            if (definition == null || !UnityTerrainBackend.CanHandle(definition.terrainMode))
            {
                error = "当前地形后端暂未提供统一绘制入口。";
                return false;
            }
            return UnityTerrainBackend.TryPaintHeight(definition, worldPoint, worldMin, worldMax, normalizedHeight, out error);
        }

        public static bool TryBakePersistent(ESWorldMapAsset asset, string terrainDataPath, string scenePath, out string error)
        {
            error = "正式 TerrainData 输出已封锁：当前实现缺少场景未保存检查、覆盖备份、原子提交与失败回滚。";
            return false;
        }
    }
}
#endif
