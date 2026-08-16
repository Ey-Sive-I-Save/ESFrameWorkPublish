#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using System;
using System.Collections.Generic;
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
                Terrain[] terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
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

        private sealed class FileBackup
        {
            public string assetPath;
            public string fullPath;
            public string backupPath;
            public string metaPath;
            public string metaBackupPath;
            public bool existed;
            public bool metaExisted;
        }

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
            if (handle.terrainObject != null) UnityEngine.Object.DestroyImmediate(handle.terrainObject);
            if (handle.terrainData != null) UnityEngine.Object.DestroyImmediate(handle.terrainData);
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
            error = string.Empty;
            if (!TryValidateOutputPaths(asset, terrainDataPath, scenePath, out string navigationPath, out error))
                return false;
            string sourceAssetPath = AssetDatabase.GetAssetPath(asset);
            if (!IsSafeAssetPath(sourceAssetPath, ".asset"))
            {
                error = "正式 World 输出要求地图资产已经保存到 Assets/ 下。";
                return false;
            }
            if (string.Equals(sourceAssetPath, terrainDataPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(sourceAssetPath, navigationPath, StringComparison.OrdinalIgnoreCase))
            {
                error = "地图定义资产不能与 TerrainData 或 NavMeshData 共用输出路径。";
                return false;
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loaded = SceneManager.GetSceneAt(i);
                if (loaded.IsValid() && loaded.isDirty)
                {
                    error = "存在未保存 Scene，正式 World 输出已取消：" + loaded.name;
                    return false;
                }
                if (loaded.IsValid() && loaded.isLoaded
                    && string.Equals(loaded.path, scenePath, StringComparison.OrdinalIgnoreCase))
                {
                    error = "目标正式 Scene 当前已加载，请先关闭后再执行输出：" + scenePath;
                    return false;
                }
            }

            string transactionId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string backupRoot = Path.Combine(Directory.GetCurrentDirectory(), "Library", "ESWorkbench", "Backups", transactionId);
            List<FileBackup> backups;
            try
            {
                Directory.CreateDirectory(backupRoot);
                backups = new List<FileBackup>
                {
                    CaptureBackup(sourceAssetPath, backupRoot),
                    CaptureBackup(terrainDataPath, backupRoot),
                    CaptureBackup(scenePath, backupRoot),
                    CaptureBackup(navigationPath, backupRoot)
                };
            }
            catch (Exception exception)
            {
                error = "无法建立正式输出事务备份，未执行任何写入：" + exception.Message;
                return false;
            }
            string stagingScenePath = Path.ChangeExtension(scenePath, null)
                + ".__es_staging_" + Guid.NewGuid().ToString("N") + ".unity";
            Scene stagingScene = default;
            Scene previousActiveScene = SceneManager.GetActiveScene();
            try
            {
                EnsureAssetFolder(terrainDataPath);
                EnsureAssetFolder(scenePath);
                EnsureAssetFolder(navigationPath);
                AssetDatabase.Refresh();

                if (!UnityTerrainBackend.TryBakePersistent(asset, terrainDataPath, string.Empty, out error))
                    throw new InvalidOperationException(error);
                TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataPath);
                if (terrainData == null) throw new InvalidOperationException("TerrainData 提交后无法重读。");

                stagingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                stagingScene.name = asset.name + "_Staging";
                var contentRoot = new GameObject("ES World Content");
                SceneManager.MoveGameObjectToScene(contentRoot, stagingScene);
                GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
                terrainObject.name = asset.name + "_Terrain";
                terrainObject.transform.position = new Vector3(
                    asset.Definition.worldMin.x, 0f, asset.Definition.worldMin.y);
                TerrainCollider terrainCollider = terrainObject.GetComponent<TerrainCollider>();
                if (terrainCollider != null)
                    terrainCollider.enabled = asset.Definition.collision?.terrainCollider != false;
                SceneManager.MoveGameObjectToScene(terrainObject, stagingScene);
                terrainObject.transform.SetParent(contentRoot.transform, true);
                PopulatePrefabPlacements(asset.Definition, stagingScene, contentRoot.transform);

                NavMeshData navigationData = null;
                if (asset.Definition.navigation?.enabled == true && asset.Definition.build?.includeNavigation == true)
                {
                    navigationData = BuildNavigationData(asset.Definition, contentRoot.transform);
                    NavMeshData existingNavigation = AssetDatabase.LoadAssetAtPath<NavMeshData>(navigationPath);
                    if (existingNavigation == null) AssetDatabase.CreateAsset(navigationData, navigationPath);
                    else
                    {
                        EditorUtility.CopySerialized(navigationData, existingNavigation);
                        UnityEngine.Object.DestroyImmediate(navigationData);
                        navigationData = existingNavigation;
                        EditorUtility.SetDirty(navigationData);
                    }
                    AssetDatabase.SaveAssetIfDirty(navigationData);
                }
                if (navigationData != null)
                {
                    var navigationRoot = new GameObject("ES World Navigation");
                    SceneManager.MoveGameObjectToScene(navigationRoot, stagingScene);
                    navigationRoot.AddComponent<ESWorldBakedNavMeshReference>()
                        .SetNavigationData(navigationData);
                }
                EditorSceneManager.MarkSceneDirty(stagingScene);
                if (!EditorSceneManager.SaveScene(stagingScene, stagingScenePath, false))
                    throw new InvalidOperationException("正式 Scene staging 保存失败。");
                if (!EditorSceneManager.CloseScene(stagingScene, true))
                    throw new InvalidOperationException("正式 Scene staging 关闭失败。");
                stagingScene = default;
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);

                CommitStagedScene(stagingScenePath, scenePath);
                AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceUpdate);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                    throw new InvalidOperationException("正式 Scene 提交后无法重读。");
                if (navigationData != null && AssetDatabase.LoadAssetAtPath<NavMeshData>(navigationPath) == null)
                    throw new InvalidOperationException("NavMeshData 提交后无法重读。");

                asset.Definition.terrainDataAssetPath = terrainDataPath;
                asset.Definition.terrainAssetKey = terrainDataPath;
                asset.Definition.build.formalSceneAssetPath = scenePath;
                asset.Definition.build.navigationDataAssetPath = navigationData == null ? string.Empty : navigationPath;
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
                AssetDatabase.Refresh();
                return true;
            }
            catch (Exception exception)
            {
                if (stagingScene.IsValid()) EditorSceneManager.CloseScene(stagingScene, true);
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);
                string rollbackError = string.Empty;
                try { RestoreBackups(backups); }
                catch (Exception rollbackException) { rollbackError = "；回滚异常：" + rollbackException.Message; }
                AssetDatabase.Refresh();
                if (string.IsNullOrEmpty(rollbackError))
                    AssetDatabase.ImportAsset(sourceAssetPath, ImportAssetOptions.ForceUpdate);
                error = "正式 World 输出事务失败，已恢复提交前文件：" + exception.Message + rollbackError;
                return false;
            }
            finally
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(stagingScenePath) != null)
                    AssetDatabase.DeleteAsset(stagingScenePath);
            }
        }

        internal static bool TryValidateOutputPaths(
            ESWorldMapAsset asset,
            string terrainDataPath,
            string scenePath,
            out string navigationPath,
            out string error)
        {
            navigationPath = string.Empty;
            error = string.Empty;
            if (asset == null || asset.Definition == null)
            {
                error = "地图资产或定义为空。";
                return false;
            }
            if (!asset.Validate(out error)) return false;
            if (!IsSafeAssetPath(terrainDataPath, ".asset"))
            {
                error = "TerrainData 路径必须是 Assets/ 下的 .asset 文件。";
                return false;
            }
            if (!IsSafeAssetPath(scenePath, ".unity"))
            {
                error = "正式 Scene 路径必须是 Assets/ 下的 .unity 文件。";
                return false;
            }
            navigationPath = Path.ChangeExtension(scenePath, null) + "_NavMesh.asset";
            if (string.Equals(terrainDataPath, navigationPath, StringComparison.OrdinalIgnoreCase))
            {
                error = "TerrainData 与 NavMeshData 输出路径不能相同。";
                return false;
            }
            return true;
        }

        private static bool IsSafeAssetPath(string path, string extension)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string normalized = path.Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal)
                || normalized.Contains("/../") || normalized.EndsWith("/..", StringComparison.Ordinal)
                || !string.Equals(Path.GetExtension(normalized), extension, StringComparison.OrdinalIgnoreCase))
                return false;
            try
            {
                string assetsRoot = Path.GetFullPath("Assets")
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(normalized);
                return full.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static NavMeshData BuildNavigationData(ESWorldMapDefinition definition, Transform contentRoot)
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            if (settings.agentTypeID < 0)
                throw new InvalidOperationException("项目没有可用的 Unity NavMesh Agent Settings。");
            var sources = new List<NavMeshBuildSource>();
            UnityEngine.AI.NavMeshBuilder.CollectSources(
                contentRoot,
                ~0,
                NavMeshCollectGeometry.RenderMeshes,
                0,
                new List<NavMeshBuildMarkup>(),
                sources);
            if (sources.Count == 0)
                throw new InvalidOperationException("正式 Scene 中没有可用于 NavMesh 的 Terrain 或 Collider。" );
            Bounds bounds = new Bounds(
                new Vector3(
                    (definition.worldMin.x + definition.worldMax.x) * 0.5f,
                    definition.terrainHeightScale * 0.5f,
                    (definition.worldMin.y + definition.worldMax.y) * 0.5f),
                new Vector3(
                    Mathf.Max(1f, definition.worldMax.x - definition.worldMin.x),
                    Mathf.Max(1f, definition.terrainHeightScale * 2f),
                    Mathf.Max(1f, definition.worldMax.y - definition.worldMin.y)));
            NavMeshData data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(
                settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null) throw new InvalidOperationException("Unity NavMeshBuilder 未生成可持久化数据。");
            data.name = "ES World NavMesh";
            return data;
        }

        private static void PopulatePrefabPlacements(
            ESWorldMapDefinition definition,
            Scene scene,
            Transform contentRoot)
        {
            if (definition.prefabPlacements == null) return;
            for (int i = 0; i < definition.prefabPlacements.Count; i++)
            {
                ESWorldMapPrefabPlacement placement = definition.prefabPlacements[i];
                if (placement == null || !placement.enabled || string.IsNullOrWhiteSpace(placement.editorPrefabGuid))
                    continue;
                string prefabPath = AssetDatabase.GUIDToAssetPath(placement.editorPrefabGuid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null) continue;
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance == null) continue;
                instance.name = prefab.name;
                instance.transform.SetPositionAndRotation(placement.position, Quaternion.Euler(placement.rotationEuler));
                instance.transform.localScale = placement.scale;
                instance.transform.SetParent(contentRoot, true);
            }
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("输出目录为空：" + assetPath);
            Directory.CreateDirectory(Path.GetFullPath(directory));
        }

        private static void CommitStagedScene(string stagingPath, string targetPath)
        {
            string stagingFull = Path.GetFullPath(stagingPath);
            string targetFull = Path.GetFullPath(targetPath);
            if (File.Exists(targetFull)) File.Copy(stagingFull, targetFull, true);
            else
            {
                string moveError = AssetDatabase.MoveAsset(stagingPath, targetPath);
                if (!string.IsNullOrEmpty(moveError)) throw new IOException(moveError);
            }
        }

        private static FileBackup CaptureBackup(string assetPath, string backupRoot)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return null;
            string full = Path.GetFullPath(assetPath);
            string safeName = assetPath.Replace(':', '_').Replace('\\', '_').Replace('/', '_');
            var backup = new FileBackup
            {
                assetPath = assetPath,
                fullPath = full,
                backupPath = Path.Combine(backupRoot, safeName),
                metaPath = full + ".meta",
                metaBackupPath = Path.Combine(backupRoot, safeName + ".meta"),
                existed = File.Exists(full),
                metaExisted = File.Exists(full + ".meta")
            };
            if (backup.existed) File.Copy(backup.fullPath, backup.backupPath, true);
            if (backup.metaExisted) File.Copy(backup.metaPath, backup.metaBackupPath, true);
            return backup;
        }

        private static void RestoreBackups(IReadOnlyList<FileBackup> backups)
        {
            for (int i = 0; i < backups.Count; i++)
            {
                FileBackup backup = backups[i];
                if (backup == null) continue;
                if (backup.existed) File.Copy(backup.backupPath, backup.fullPath, true);
                else if (File.Exists(backup.fullPath)) File.Delete(backup.fullPath);
                if (backup.metaExisted) File.Copy(backup.metaBackupPath, backup.metaPath, true);
                else if (File.Exists(backup.metaPath)) File.Delete(backup.metaPath);
            }
        }
    }
}
#endif
