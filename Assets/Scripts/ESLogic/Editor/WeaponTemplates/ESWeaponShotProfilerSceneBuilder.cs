using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    /// <summary>
    /// Weapon/Shot 专用 Profiler 场景的唯一布局权威。只读取正式 Definition、Prefab
    /// 和 ResourcePlan，不在生成场景时修改它们。
    /// </summary>
    public static class ESWeaponShotProfilerSceneBuilder
    {
        public const string SceneFolder = "Assets/Scenes/Tests";
        public const string ScenePath = SceneFolder + "/ESWeaponShotProfiler.unity";

        private const string WeaponInfoPath = "Assets/ESNormalAssets/Data/Group/Item/基础步枪_ItemDataInfo.asset";
        private const string ShotInfoPath = "Assets/ESNormalAssets/Data/Group/Item/基础步枪弹丸_ItemDataInfo.asset";
        private const string ResourcePlanPath = "Assets/ESNormalAssets/Data/ResourcePlan/基础步枪_ResourcePlan.asset";
        private const string WeaponPrefabPath = "Assets/ESNormalAssets/WeaponPrototypes/基础步枪.prefab";
        private const string ShotPrefabPath = "Assets/ESNormalAssets/ProjectilePrototypes/基础步枪弹丸.prefab";

        [MenuItem("【ES】/验证与诊断/验证环境/战斗/创建或刷新 Weapon Shot Profiler 场景", false, 130)]
        public static void CreateOrRefreshMenu()
        {
            SceneAsset scene = CreateOrRefresh();
            Selection.activeObject = scene;
            EditorGUIUtility.PingObject(scene);
        }

        public static SceneAsset CreateOrRefresh()
        {
            EnsureEditorCanGenerate();
            ItemDataInfo weaponInfo = LoadRequiredAsset<ItemDataInfo>(WeaponInfoPath);
            ItemDataInfo shotInfo = LoadRequiredAsset<ItemDataInfo>(ShotInfoPath);
            ESResourcePlanInfo resourcePlan = LoadRequiredAsset<ESResourcePlanInfo>(ResourcePlanPath);
            GameObject weaponPrefab = LoadRequiredAsset<GameObject>(WeaponPrefabPath);
            GameObject shotPrefab = LoadRequiredAsset<GameObject>(ShotPrefabPath);
            ValidateDependencies(weaponInfo, shotInfo, weaponPrefab, shotPrefab);

            Scene previousActiveScene = SceneManager.GetActiveScene();
            NewSceneMode mode = ResolveCreationMode(previousActiveScene);
            BackupExistingSceneIfNeeded();
            EnsureSceneFolder();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            try
            {
                if (mode == NewSceneMode.Additive && !SceneManager.SetActiveScene(scene))
                    throw new InvalidOperationException("无法激活 Weapon/Shot Profiler 临时场景。");

                scene.name = "ESWeaponShotProfiler";
                GameObject root = new GameObject("ES Weapon Shot Profiler");
                CreateLighting(root.transform);
                CreateGround(root.transform);
                ESResourcePlanBinder binder = CreateRuntimeBootstrap(root.transform, resourcePlan);
                Entity target = CreateTarget(root.transform);
                Entity shooter = CreateShooter(root.transform, target.transform, weaponInfo, out int weaponSlotIndex);
                Camera camera = CreateCamera(root.transform, shooter.transform, target.transform);
                CreateDiagnostics(
                    root.transform,
                    binder,
                    weaponInfo,
                    shotInfo,
                    shooter,
                    target,
                    camera,
                    weaponSlotIndex);

                ValidateGeneratedScene(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("保存 Weapon/Shot Profiler 场景失败：" + ScenePath);

                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);
                SceneAsset saved = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
                if (saved == null)
                    throw new InvalidOperationException("保存后无法重新加载 Weapon/Shot Profiler 场景：" + ScenePath);

                Debug.Log("[Weapon/Shot Profiler] 场景已生成：" + ScenePath, saved);
                return saved;
            }
            finally
            {
                if (mode == NewSceneMode.Additive)
                {
                    if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                        SceneManager.SetActiveScene(previousActiveScene);
                    if (scene.IsValid() && scene.isLoaded)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void EnsureEditorCanGenerate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling
                || EditorApplication.isUpdating)
            {
                throw new InvalidOperationException(
                    "Weapon/Shot Profiler 场景只能在 EditMode、编译完成且 AssetDatabase 空闲时生成。");
            }
        }

        private static NewSceneMode ResolveCreationMode(Scene previousActiveScene)
        {
            bool replaceEmptyScratch = previousActiveScene.IsValid()
                                       && string.IsNullOrEmpty(previousActiveScene.path)
                                       && previousActiveScene.rootCount == 0;
            if (previousActiveScene.IsValid()
                && string.IsNullOrEmpty(previousActiveScene.path)
                && !replaceEmptyScratch)
            {
                throw new InvalidOperationException(
                    "当前存在未保存场景内容。请先保存或关闭，再生成 Weapon/Shot Profiler 场景。");
            }

            return Application.isBatchMode || replaceEmptyScratch
                ? NewSceneMode.Single
                : NewSceneMode.Additive;
        }

        private static void ValidateDependencies(
            ItemDataInfo weaponInfo,
            ItemDataInfo shotInfo,
            GameObject weaponPrefab,
            GameObject shotPrefab)
        {
            if (weaponInfo.ValidateConfiguration(false) != ESItemDataValidationCode.Valid
                || !(weaponInfo.kindData is ItemWeaponDataBlock weaponBlock)
                || weaponBlock.key == null
                || !weaponBlock.key.IsConfigured)
            {
                throw new InvalidOperationException("基础步枪 Weapon ItemDataInfo 未通过正式配置门禁。");
            }
            if (shotInfo.ValidateConfiguration(false) != ESItemDataValidationCode.Valid
                || !(shotInfo.kindData is ItemShotDataBlock shotBlock)
                || shotBlock.key == null
                || !shotBlock.key.IsConfigured)
            {
                throw new InvalidOperationException("基础步枪弹丸 Shot ItemDataInfo 未通过正式配置门禁。");
            }
            if (weaponBlock.sharedData == null
                || weaponBlock.sharedData.defaultShot == null
                || !ESConfigKeyMatch.Matches(
                    weaponBlock.sharedData.defaultShot.EnumKeyInt,
                    weaponBlock.sharedData.defaultShot.StringKey,
                    shotBlock.key.EnumKeyInt,
                    shotBlock.key.StringKey))
            {
                throw new InvalidOperationException("基础步枪 WeaponDefinition 未引用当前 ShotDefinition。");
            }

            ValidateWeaponPrefab(weaponPrefab);
            ValidateShotPrefab(shotPrefab);
        }

        private static void ValidateWeaponPrefab(GameObject prefab)
        {
            if (prefab.GetComponentsInChildren<Item>(true).Length != 1
                || prefab.GetComponentsInChildren<EntityWeaponBinding>(true).Length != 1)
            {
                throw new InvalidOperationException("基础步枪 Prefab 必须在完整子树中恰好包含一个 Item 和一个 EntityWeaponBinding。");
            }
            ValidateNoMissingScripts(prefab, WeaponPrefabPath);
        }

        private static void ValidateShotPrefab(GameObject prefab)
        {
            Item[] items = prefab.GetComponentsInChildren<Item>(true);
            if (items.Length != 1 || items[0].basicDomain?.FindMyModule<ItemShotModule>() == null)
                throw new InvalidOperationException("基础步枪弹丸 Prefab 必须在根 Item 中提供 ItemShotModule。");
            ValidateNoMissingScripts(prefab, ShotPrefabPath);
        }

        private static ESResourcePlanBinder CreateRuntimeBootstrap(
            Transform root,
            ESResourcePlanInfo resourcePlan)
        {
            GameObject runtime = new GameObject("Runtime Bootstrap");
            runtime.transform.SetParent(root, false);
            ESGameManager manager = runtime.AddComponent<ESGameManager>();
            manager.dontDestroyOnLoad = false;
            manager.autoCreateInputModule = false;
            manager.autoCreatePhysicsQueryModule = true;

            ESResourcePlanBinder binder = runtime.AddComponent<ESResourcePlanBinder>();
            // ES-EDITOR-VALIDATOR: intentional-no-undo
            // The binder belongs to a generated profiler scene, not the user's
            // current scene; do not put this temporary fixture in Undo history.
            using (var serializedBinder = new SerializedObject(binder))
            {
                SerializedProperty planProperty = serializedBinder.FindProperty("plan");
                if (planProperty == null)
                    throw new MissingFieldException(typeof(ESResourcePlanBinder).FullName, "plan");
                planProperty.objectReferenceValue = resourcePlan;
                serializedBinder.ApplyModifiedPropertiesWithoutUndo();
            }
            return binder;
        }

        private static Entity CreateShooter(
            Transform root,
            Transform target,
            ItemDataInfo weaponInfo,
            out int weaponSlotIndex)
        {
            GameObject shooterObject = new GameObject("Weapon Profiler Shooter");
            shooterObject.transform.SetParent(root, false);
            shooterObject.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.identity);
            Entity shooter = shooterObject.AddComponent<Entity>();
            shooter.EnsureEntityStructure();

            var combat = new EntityBasicCombatModule
            {
                enableGunFire = true,
                fireOnAttackInput = false,
                warnWhenRecoilIKUnavailable = false,
                debugDrawFireRay = false,
                debugFireLog = false,
                startWeaponIndex = 0,
                startWithWeaponInHand = true,
                defaultAimTarget = target
            };
            shooter.basicDomain.MyModules.Add(combat);
            shooter.basicDomain.MyModules.Add(new EntityBasicHealthModule { maxHealth = 100f });
            shooter.basicDomain.MyModules.ApplyBuffers(true);

            var inventory = new EntityEquipmentInventoryModule { capacity = 4 };
            var slots = new EntityEquipmentSlotModule();
            ItemWeaponDataBlock weaponBlock = (ItemWeaponDataBlock)weaponInfo.kindData;
            slots.weaponSlots.Add(new EntityEquipmentWeaponSlot
            {
                displayName = "基础步枪",
                weaponKey = new ESWeaponConfigKey
                {
                    enumKey = weaponBlock.key.enumKey,
                    stringKey = weaponBlock.key.stringKey
                }
            });
            weaponSlotIndex = 0;
            shooter.equipmentDomain.MyModules.Add(inventory);
            shooter.equipmentDomain.MyModules.Add(slots);
            shooter.equipmentDomain.MyModules.Add(new EntityEquipmentAttachmentModule());
            shooter.equipmentDomain.MyModules.Add(new EntityEquipmentEffectModule());
            shooter.equipmentDomain.MyModules.ApplyBuffers(true);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Shooter Visual";
            body.transform.SetParent(shooterObject.transform, false);
            body.transform.localPosition = new Vector3(0f, 0f, 0f);
            Collider bodyCollider = body.GetComponent<Collider>();
            if (bodyCollider != null)
                UnityEngine.Object.DestroyImmediate(bodyCollider);
            return shooter;
        }

        private static Entity CreateTarget(Transform root)
        {
            GameObject targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObject.name = "Damage Target";
            targetObject.layer = ESPhysicsLayers.WorldDynamic;
            targetObject.transform.SetParent(root, false);
            targetObject.transform.position = new Vector3(0f, 1.5f, 32f);
            targetObject.transform.localScale = new Vector3(5f, 3f, 1f);
            Entity target = targetObject.AddComponent<Entity>();
            target.EnsureEntityStructure();
            target.basicDomain.MyModules.Add(new EntityBasicHealthModule
            {
                maxHealth = 100000f,
                resetToFullOnSpawn = true
            });
            target.basicDomain.MyModules.ApplyBuffers(true);
            return target;
        }

        private static void CreateDiagnostics(
            Transform root,
            ESResourcePlanBinder binder,
            ItemDataInfo weaponInfo,
            ItemDataInfo shotInfo,
            Entity shooter,
            Entity target,
            Camera camera,
            int weaponSlotIndex)
        {
            GameObject diagnostics = new GameObject("Diagnostics");
            diagnostics.transform.SetParent(root, false);
            ESSceneValidationGuide guide = diagnostics.AddComponent<ESSceneValidationGuide>();
            guide.routeObserver = shooter.transform;
            guide.worldGuideCamera = camera;

            var checks = new List<ESSceneValidationCheck>
            {
                ExternalCheck("weapon-plan-ready", "ResourcePlan Ready"),
                ExternalCheck("weapon-definitions-ready", "Prepared Definition"),
                ExternalCheck("weapon-runtime-view-equipped", "Weapon 运行时视图"),
                ExternalCheck("weapon-damage-consumed", "Damage 消费"),
                ExternalCheck("weapon-capacity-stable", "容量与溢出"),
                ExternalCheck("weapon-view-recycled", "卸下与回池")
            };
            var stages = new List<ESSceneValidationStage>
            {
                new ESSceneValidationStage
                {
                    id = "weapon-prepare",
                    title = "Definition 与运行时视图准备",
                    landmark = shooter.transform,
                    routeColor = new Color(0.25f, 0.75f, 1f, 1f),
                    objective = "等待正式 ResourcePlan，随后注入 Shot/Weapon Definition 并创建装备视图。",
                    expectedResult = "Prepared Data Ready，Weapon Prefab 经 Pool 借出并绑定 Item 实例与装备槽。",
                    failureHint = "依次检查 Provider、ResourcePlan、Definition 门禁、ActivePlan 和 Pool。",
                    checkIds = new[] { "weapon-plan-ready", "weapon-definitions-ready", "weapon-runtime-view-equipped" }
                },
                new ESSceneValidationStage
                {
                    id = "weapon-sample",
                    title = "Weapon/Shot Profiler 采样",
                    landmark = target.transform,
                    routeColor = new Color(0.2f, 0.9f, 0.45f, 1f),
                    objective = "在 Unity Profiler 中采样 ES.Weapon.Fire、ES.Shot.Simulation.Batch 与 ES.Weapon.Damage.Apply。",
                    expectedResult = "预热后连续开火、真实伤害消费，且容量拒绝和命中缓存溢出均为零。",
                    failureHint = "检查 ProfilerMarker 调用、Shot Batch 峰值、图案容量和命中缓存溢出统计。",
                    checkIds = new[] { "weapon-damage-consumed", "weapon-capacity-stable" }
                },
                new ESSceneValidationStage
                {
                    id = "weapon-recycle",
                    title = "装备卸下与 Pool 回收",
                    landmark = diagnostics.transform,
                    routeColor = new Color(1f, 0.75f, 0.2f, 1f),
                    objective = "等待活动 Shot 清零，随后通过 EquipmentDomain 卸下并移除 Item 实例。",
                    expectedResult = "装备槽解除绑定，Weapon 运行时视图归还对象池。",
                    failureHint = "检查 TryUnequipItem、Inventory Remove、Slot View Release 和 Pool Push。",
                    checkIds = new[] { "weapon-view-recycled" }
                }
            };
            guide.ConfigureForAuthoring(
                "ES Weapon / Shot Profiler",
                "本场景只消费正式 Definition、ResourcePlan、Prefab、Equipment、Damage 与 Pool 权威链。",
                stages,
                checks);

            ESWeaponShotProfilerScenario scenario = diagnostics.AddComponent<ESWeaponShotProfilerScenario>();
            scenario.ConfigureForAuthoring(
                binder,
                guide,
                shotInfo,
                weaponInfo,
                shooter,
                target,
                weaponSlotIndex);
        }

        private static ESSceneValidationCheck ExternalCheck(string id, string title)
        {
            return new ESSceneValidationCheck
            {
                id = id,
                title = title,
                kind = ESSceneValidationCheckKind.External
            };
        }

        private static void CreateGround(Transform root)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Profiler Ground";
            ground.layer = ESPhysicsLayers.Ground;
            ground.transform.SetParent(root, false);
            ground.transform.position = new Vector3(0f, -0.5f, 16f);
            ground.transform.localScale = new Vector3(24f, 1f, 48f);
        }

        private static void CreateLighting(Transform root)
        {
            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
        }

        private static Camera CreateCamera(Transform root, Transform shooter, Transform target)
        {
            GameObject cameraObject = new GameObject("Profiler Camera");
            cameraObject.transform.SetParent(root, false);
            cameraObject.transform.position = new Vector3(12f, 8f, -10f);
            cameraObject.transform.rotation = Quaternion.LookRotation(
                Vector3.Lerp(shooter.position, target.position, 0.35f) - cameraObject.transform.position,
                Vector3.up);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 250f;
            camera.fieldOfView = 50f;
            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        private static void ValidateGeneratedScene(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < transforms.Length; index++)
                {
                    GameObject target = transforms[index].gameObject;
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target) > 0)
                        throw new InvalidOperationException("Profiler 场景存在 Missing Script：" + target.name);
                }
            }
        }

        private static void ValidateNoMissingScripts(GameObject prefab, string path)
        {
            Transform[] transforms = prefab.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transforms[index].gameObject) > 0)
                    throw new InvalidOperationException("Prefab 存在 Missing Script：" + path + " | " + transforms[index].name);
            }
        }

        private static T LoadRequiredAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new FileNotFoundException("缺少 Weapon/Shot Profiler 前置资产。", path);
            return asset;
        }

        private static void EnsureSceneFolder()
        {
            string absolute = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                SceneFolder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(absolute);
        }

        private static void BackupExistingSceneIfNeeded()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string source = Path.Combine(projectRoot, ScenePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(source))
                return;

            string taskKey = "WeaponShotProfilerScene_" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture);
            string backupFolder = Path.Combine(projectRoot, "ES", "Bak", "Local", taskKey);
            Directory.CreateDirectory(backupFolder);
            string backupScene = Path.Combine(backupFolder, Path.GetFileName(source));
            File.Copy(source, backupScene, false);
            string sourceMeta = source + ".meta";
            if (File.Exists(sourceMeta))
                File.Copy(sourceMeta, backupScene + ".meta", false);

            string manifest = "# Weapon Shot Profiler Scene Local Backup\n\n"
                              + "- Source: `" + ScenePath + "`\n"
                              + "- UTC: `" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "`\n"
                              + "- Bytes: `" + new FileInfo(source).Length + "`\n"
                              + "- SHA-256: `" + ComputeSha256(source) + "`\n";
            File.WriteAllText(
                Path.Combine(backupFolder, "BACKUP_MANIFEST.md"),
                manifest,
                new UTF8Encoding(false));
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty);
        }
    }
}
