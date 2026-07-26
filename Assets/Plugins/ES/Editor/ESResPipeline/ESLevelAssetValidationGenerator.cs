#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    /// <summary>可重复执行的三关资源生命周期验收集生成器。</summary>
    public static class ESLevelAssetValidationGenerator
    {
        private const string Root = "Assets/ESNormalAssets/ESValidation/LevelAssetFlow";
        private static readonly Color[] Colors = { Color.red, Color.green, Color.blue, Color.yellow, Color.magenta, Color.cyan };
        private static readonly string[] ColorNames = { "Red", "Green", "Blue", "Yellow", "Magenta", "Cyan" };
        private static readonly PrimitiveType[] Shapes = { PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Cylinder };
        private static readonly string[] LevelNames = { "Level01_Blocks", "Level02_Spheres", "Level03_Cylinders" };

        [MenuItem("ES/资源/生成关卡资源卸载验收集")]
        public static void Generate()
        {
            GenerateInternal(true);
        }

        /// <summary>供 Unity BatchMode 调用；不会弹窗，适合 CI 或框架自检。</summary>
        public static void GenerateBatchMode()
        {
            GenerateInternal(false);
        }

        /// <summary>仅修复旧版入口场景的 Missing Script，不触碰关卡、Library 或 Consumer。</summary>
        internal static void RebuildEntrySceneForControllerFix()
        {
            string path = Root + "/Scenes/ESLevelAssetValidation.unity";
            Scene openedEntryScene = SceneManager.GetSceneByPath(path);
            if (openedEntryScene.IsValid() && openedEntryScene.isLoaded)
            {
                GameObject controller = null;
                foreach (GameObject root in openedEntryScene.GetRootGameObjects())
                    if (root.name == "ESLevelAssetValidationController") { controller = root; break; }
                if (controller == null) controller = new GameObject("ESLevelAssetValidationController");

                // 旧场景中该组件的 MonoScript 是无 GUID 的临时对象。程序集已恢复后它可能
                // 暂时能解析为正确类型，却依旧会在下次重开时失效，因此必须无条件替换。
                foreach (ESLevelAssetValidationSceneController oldController in controller.GetComponents<ESLevelAssetValidationSceneController>())
                    UnityEngine.Object.DestroyImmediate(oldController);
                Type entryType = Type.GetType("ES.ESHotUpdateSceneEntry, ES_Stand", false);
                if (entryType != null)
                    foreach (Component oldEntry in controller.GetComponents(entryType))
                        UnityEngine.Object.DestroyImmediate(oldEntry);
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(controller);
                AddHotUpdateSceneEntry(controller);
                EditorSceneManager.MarkSceneDirty(openedEntryScene);
                EditorSceneManager.SaveScene(openedEntryScene);
            }
            else
            {
                CreateOrUpdateScene();
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ESValidation] 已修复入口场景控制器脚本引用。");
        }

        private static void GenerateInternal(bool allowOverwritePrompt)
        {
            if (allowOverwritePrompt && AssetDatabase.LoadAssetAtPath<ESLevelAssetValidationGameCore>(Root + "/Data/ESLevelAssetValidationGameCore.asset") != null
                && !EditorUtility.DisplayDialog("生成验收集", "验收集已存在。将覆盖生成的 Prefab、材质、计划、场景与配置，是否继续？", "继续", "取消"))
                return;

            EnsureFolder(Root);
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Data");
            EnsureFolder(Root + "/Scenes");

            var levelPrefabs = new List<GameObject>[3];
            for (int level = 0; level < 3; level++) levelPrefabs[level] = CreateLevelPrefabs(level);

            ESAssetReferScene[] levelScenes = CreateOrUpdateLevelScenes(levelPrefabs);
            ESLevelAssetValidationGameCore gameCore = CreateOrReplaceGameCore(levelPrefabs, levelScenes);
            ESAssetLibrary library = CreateOrUpdateLibrary(levelPrefabs, gameCore);
            ESAssetLibraryConsumer consumer = CreateOrUpdateConsumer(library, gameCore);
            CreateOrUpdateScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = gameCore;
            EditorGUIUtility.PingObject(gameCore);
            Debug.Log("[ESValidation] 关卡资源卸载验收集已生成。接下来：在资源面板确认 Consumer，依次执行烘焙、规划、构建、发布；运行 ESLevelAssetValidation.unity。", gameCore);
        }

        private static List<GameObject> CreateLevelPrefabs(int level)
        {
            var result = new List<GameObject>(Colors.Length);
            string levelPath = Root + "/Prefabs/" + LevelNames[level];
            string materialPath = Root + "/Materials/" + LevelNames[level];
            EnsureFolder(levelPath);
            EnsureFolder(materialPath);
            for (int i = 0; i < Colors.Length; i++)
            {
                string baseName = LevelNames[level] + "_" + ColorNames[i];
                Material material = CreateMaterial(materialPath + "/" + baseName + ".mat", Colors[i]);
                GameObject temporary = GameObject.CreatePrimitive(Shapes[level]);
                temporary.name = baseName;
                temporary.transform.localScale = Vector3.one * (0.65f + i * 0.18f);
                temporary.GetComponent<Renderer>().sharedMaterial = material;
                if (level == 0)
                {
                    switch (i % 3)
                    {
                        case 0: AddHotUpdateMotion(temporary, "ES.ESHotUpdateLoopMoveHorizontal"); break;
                        case 1: AddHotUpdateMotion(temporary, "ES.ESHotUpdateLoopMoveVertical"); break;
                        default: AddHotUpdateMotion(temporary, "ES.ESHotUpdateLoopMoveDepth"); break;
                    }
                }
                string prefabPath = levelPath + "/" + baseName + ".prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporary, prefabPath);
                UnityEngine.Object.DestroyImmediate(temporary);
                result.Add(prefab);
            }
            return result;
        }

        private static Material CreateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static ESLevelAssetValidationGameCore CreateOrReplaceGameCore(IReadOnlyList<GameObject>[] prefabsByLevel, ESAssetReferScene[] levelScenes)
        {
            const string path = Root + "/Data/ESLevelAssetValidationGameCore.asset";
            ESLevelAssetValidationGameCore gameCore = AssetDatabase.LoadAssetAtPath<ESLevelAssetValidationGameCore>(path);
            if (gameCore == null)
            {
                gameCore = ScriptableObject.CreateInstance<ESLevelAssetValidationGameCore>();
                AssetDatabase.CreateAsset(gameCore, path);
            }
            gameCore.levels.Clear();
            for (int level = 0; level < 3; level++)
            {
                ESResourcePlanInfo plan = CreateOrReplacePlan(level, prefabsByLevel[level]);
                var definition = new ESLevelAssetValidationLevel { levelName = LevelNames[level], resourcePlan = plan, scene = levelScenes[level] };
                gameCore.levels.Add(definition);
            }
            EditorUtility.SetDirty(gameCore);
            return gameCore;
        }

        private static ESResourcePlanInfo CreateOrReplacePlan(int level, IReadOnlyList<GameObject> prefabs)
        {
            string path = Root + "/Data/" + LevelNames[level] + "_ResourcePlan.asset";
            ESResourcePlanInfo plan = AssetDatabase.LoadAssetAtPath<ESResourcePlanInfo>(path);
            if (plan == null)
            {
                plan = ScriptableObject.CreateInstance<ESResourcePlanInfo>();
                AssetDatabase.CreateAsset(plan, path);
            }
            plan.targetKind = ESResourcePlanTargetKind.Level;
            plan.targetInfoKey = LevelNames[level];
            plan.releaseOnExit = true;
            plan.releaseDelaySeconds = 0f;
            plan.prefabs.Clear();
            plan.prefabPrewarms.Clear();
            for (int i = 0; i < prefabs.Count; i++)
            {
                string pathOfPrefab = AssetDatabase.GetAssetPath(prefabs[i]);
                plan.prefabs.Add(new ESResourcePlanPrefabEntry
                {
                    required = true,
                    key = new ESAssetReferPrefabConfigKey
                    {
                        stringKey = GetKey(level, i),
                        guid = AssetDatabase.AssetPathToGUID(pathOfPrefab),
                        localFileId = 0,
                        assetTypeName = typeof(GameObject).FullName,
                        editorPath = pathOfPrefab
                    }
                });
            }
            EditorUtility.SetDirty(plan);
            return plan;
        }

        private static ESAssetReferScene[] CreateOrUpdateLevelScenes(IReadOnlyList<GameObject>[] prefabsByLevel)
        {
            var result = new ESAssetReferScene[LevelNames.Length];
            for (int level = 0; level < LevelNames.Length; level++)
            {
                string path = Root + "/Scenes/" + LevelNames[level] + ".unity";
                // 验收资产生成绝不能替换开发者当前正在编辑的场景。
                // 以临时 Additive Scene 生成、保存、关闭，当前 Scene Setup 完全保留。
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                try
                {
                    var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                    camera.tag = "MainCamera";
                    camera.transform.position = new Vector3(0f, 8f, -12f);
                    camera.transform.rotation = Quaternion.Euler(33f, 0f, 0f);
                    camera.GetComponent<Camera>().backgroundColor = new Color(.1f, .13f, .19f);
                    var light = new GameObject("Directional Light", typeof(Light));
                    light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                    light.GetComponent<Light>().type = LightType.Directional;
                    for (int i = 0; i < prefabsByLevel[level].Count; i++)
                    {
                        var instance = PrefabUtility.InstantiatePrefab(prefabsByLevel[level][i], scene) as GameObject;
                        if (instance == null) throw new InvalidOperationException("无法创建验收 Prefab 实例：" + prefabsByLevel[level][i].name);
                        instance.transform.position = new Vector3((i % 3) * 3f - 3f, 0f, (i / 3) * 3f);
                    }
                    EditorSceneManager.SaveScene(scene, path);
                    result[level] = new ESAssetReferScene();
                    result[level].InitializeGeneratedReference(AssetDatabase.AssetPathToGUID(path), 0, ESAssetReferKind.Scene, 0, "level_validation_scene_" + (level + 1));
                }
                finally { EditorSceneManager.CloseScene(scene, true); }
            }
            return result;
        }

        private static ESAssetLibrary CreateOrUpdateLibrary(IReadOnlyList<GameObject>[] levelPrefabs, ESLevelAssetValidationGameCore gameCore)
        {
            const string path = Root + "/Data/ESLevelAssetValidationLibrary.asset";
            ESAssetLibrary library = AssetDatabase.LoadAssetAtPath<ESAssetLibrary>(path);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<ESAssetLibrary>();
                library.Name = "关卡资源卸载验收库";
                library.LibFolderName = "es_level_asset_validation";
                library.ContainsBuild = true;
                library.IsNet = true;
                AssetDatabase.CreateAsset(library, path);
            }
            var assets = new List<UnityEngine.Object>();
            assets.Add(gameCore);
            for (int level = 0; level < 3; level++)
            {
                assets.AddRange(levelPrefabs[level]);
                assets.Add(AssetDatabase.LoadAssetAtPath<SceneAsset>(Root + "/Scenes/" + LevelNames[level] + ".unity"));
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Root + "/Materials" }))
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material != null) assets.Add(material);
            }
            // 验收资产需要先写入稳定测试 Key，再进行首次 Registry 注入。
            // 通用 EditorOnly_DragAssetsToBooks 会立即注入，随后改 Key 会被正确但无意义地报告为“资产自覆盖”。
            foreach (UnityEngine.Object asset in assets)
            {
                if (asset == null) continue;
                ESAssetReferKind kind = ESAssetPage.DetermineKind(asset);
                ESAssetBook targetBook = library.GetDefaultBookByKind(kind);
                if (targetBook == null)
                    throw new InvalidOperationException("验收资产类型不受 AssetLibrary 支持：" + asset.name + " / " + kind);
                targetBook.EditorOnly_DragAtArea(new[] { asset });
            }
            library.MarkFastIndexDirty();
            library.NormalizePagesEditor();
            foreach (ESAssetPage page in library.GetPagesByKind(ESAssetReferKind.Prefab))
            {
                for (int level = 0; level < 3; level++)
                    for (int index = 0; index < levelPrefabs[level].Count; index++)
                        if (page.OB == levelPrefabs[level][index]) page.StringKey = GetKey(level, index);
            }
            foreach (ESAssetPage page in library.GetPagesByKind(ESAssetReferKind.Scene))
                for (int level = 0; level < LevelNames.Length; level++)
                    if (page?.OB != null && page.OB.name == LevelNames[level])
                        page.StringKey = "level_validation_scene_" + (level + 1);
            foreach (ESAssetPage page in library.GetPagesByKind(ESAssetReferKind.Material))
                if (page?.OB != null)
                    page.StringKey = "level_validation_material_" + page.OB.name.ToLowerInvariant();
            EditorUtility.SetDirty(library);
            library.InjectToAssetRegistryEditor();
            return library;
        }

        private static ESAssetLibraryConsumer CreateOrUpdateConsumer(ESAssetLibrary library, ESLevelAssetValidationGameCore gameCore)
        {
            const string path = Root + "/Data/ESLevelAssetValidationConsumer.asset";
            ESAssetLibraryConsumer consumer = AssetDatabase.LoadAssetAtPath<ESAssetLibraryConsumer>(path);
            if (consumer == null)
            {
                consumer = ScriptableObject.CreateInstance<ESAssetLibraryConsumer>();
                consumer.Name = "关卡资源卸载验收 Consumer";
                consumer.ConsumerId = "es_level_asset_validation";
                consumer.Version = "1.0.0";
                consumer.Desc = "三关独占几何 Prefab 与 ResourcePlan/AB 释放验收。";
                AssetDatabase.CreateAsset(consumer, path);
            }
            consumer.ConsumerLibFolders.Clear();
            consumer.ConsumerLibFolders.Add(library);
            // GameCore 同时保留在普通 Assets 和 Consumer 收集链中；Baker 按 GUID 静默去重。
            consumer.ManualGameCoreAssets.Clear();
            EditorUtility.SetDirty(consumer);
            return consumer;
        }

        private static void CreateOrUpdateScene()
        {
            string path = Root + "/Scenes/ESLevelAssetValidation.unity";
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                // 入口场景只承载控制器。相机与 AudioListener 属于 Additive 关卡场景，
                // 否则入口壳与关卡会同时渲染并产生双 AudioListener。
                GameObject controller = new GameObject("ESLevelAssetValidationController");
                AddHotUpdateSceneEntry(controller);
                EditorSceneManager.SaveScene(scene, path);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static string GetKey(int level, int index) => "level_validation_" + (level + 1) + "_" + ColorNames[index].ToLowerInvariant();

        private static void AddHotUpdateMotion(GameObject target, string fullTypeName)
        {
            Type type = Type.GetType(fullTypeName + ", ES_Logic", false);
            if (type == null)
                throw new InvalidOperationException("热更新移动脚本尚未完成编译：" + fullTypeName);
            target.AddComponent(type);
        }

        private static void AddHotUpdateSceneEntry(GameObject controller)
        {
            Type entryType = Type.GetType("ES.ESHotUpdateSceneEntry, ES_Stand", false);
            if (entryType == null)
                throw new InvalidOperationException("ESHotUpdateSceneEntry 尚未完成编译，请等待 Unity 脚本刷新后重试。");
            Component entry = controller.AddComponent(entryType);
            var serializedEntry = new SerializedObject(entry);
            serializedEntry.FindProperty("componentTypeName").stringValue = "ES.ESLevelAssetValidationSceneController, ES_Logic";
            serializedEntry.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
