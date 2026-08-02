using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    /// <summary>
    /// 可重复生成的玩家控制器验收场景。
    /// 相机和环境属于场景，不向正式角色 Prefab 增加任何测试 MonoBehaviour。
    /// </summary>
    public static class ESPlayerControllerTestSceneBuilder
    {
        public const string SceneFolder = "Assets/Scenes/Tests";
        public const string ScenePath = SceneFolder + "/ESPlayerControllerTest.unity";

        private const string PlayerPrefabPath = ESFormalHertaPlayerVariantBuilder.VariantPath;
        private const string CarPrefabPath = "Assets/ESNormalAssets/VehiclePrototypes/BlockCar.prefab";
        private const string BicyclePrefabPath = "Assets/ESNormalAssets/VehiclePrototypes/BlockBicycle.prefab";
        private const string HelicopterPrefabPath = "Assets/ESNormalAssets/VehiclePrototypes/BlockHelicopter.prefab";

        [MenuItem("【ES】/示例与测试/角色/一键准备正式资产并创建 玩家控制器测试场景", false, 99)]
        public static void PrepareAssetsAndCreateOrRefreshMenu()
        {
            SceneAsset scene = PrepareAssetsAndCreateOrRefresh();
            if (scene == null)
                return;

            Selection.activeObject = scene;
            EditorGUIUtility.PingObject(scene);
        }

        [MenuItem("【ES】/示例与测试/角色/创建或刷新 玩家控制器测试场景", false, 100)]
        public static void CreateOrRefreshMenu()
        {
            SceneAsset scene = CreateOrRefresh();
            if (scene == null)
                return;

            Selection.activeObject = scene;
            EditorGUIUtility.PingObject(scene);
        }

        /// <summary>
        /// 面向首次搭建和 CI 的显式完整流程：重建正式玩家、升级载具探针、生成测试场景。
        /// 该入口的资产写入是菜单名称承诺的一部分；纯场景入口 <see cref="CreateOrRefresh"/> 始终只读前置资产。
        /// 可由 Unity 使用 -executeMethod ES.ESPlayerControllerTestSceneBuilder.PrepareAssetsAndCreateOrRefreshBatch 调用，
        /// 不需要人工依次打开角色、载具和场景再执行三个菜单。
        /// </summary>
        public static SceneAsset PrepareAssetsAndCreateOrRefresh()
        {
            ESFormalHertaPlayerVariantBuilder.RebuildHertaPlayerVariant();
            ESFormalHertaPlayerVariantBuilder.UpgradeVehicleMountProbes();
            return CreateOrRefresh();
        }

        /// <summary>Unity 命令行批处理入口，供 CI 或本地一键准备调用。</summary>
        public static void PrepareAssetsAndCreateOrRefreshBatch()
        {
            PrepareAssetsAndCreateOrRefresh();
        }

        /// <summary>
        /// 只读取并验证已准备好的正式资产，再生成独立的控制器验收场景。
        /// 不得在测试场景工具中隐式重建角色或改写载具 Prefab。
        /// </summary>
        public static SceneAsset CreateOrRefresh()
        {
            GameObject playerPrefab = LoadRequiredPrefab(PlayerPrefabPath);
            GameObject carPrefab = LoadRequiredPrefab(CarPrefabPath);
            GameObject bicyclePrefab = LoadRequiredPrefab(BicyclePrefabPath);
            GameObject helicopterPrefab = LoadRequiredPrefab(HelicopterPrefabPath);
            ValidatePreparedDependencies(playerPrefab, carPrefab, bicyclePrefab, helicopterPrefab);

            Directory.CreateDirectory(SceneFolder);
            Scene previousActiveScene = SceneManager.GetActiveScene();
            // Unity batch mode starts from an unsaved empty scene. The same state is also common
            // for an Editor started through MCP before any scene is opened. Unity refuses to add
            // another scene beside that scratch scene. Unity marks a freshly created EmptyScene as
            // dirty even though it contains no hierarchy content, so rootCount is the authoritative
            // safety boundary here: an authored unsaved scene with any root object is never discarded.
            bool replaceEmptyUntitledScratchScene = !Application.isBatchMode
                                                    && previousActiveScene.IsValid()
                                                    && string.IsNullOrEmpty(previousActiveScene.path)
                                                    && previousActiveScene.rootCount == 0;
            if (!Application.isBatchMode
                && previousActiveScene.IsValid()
                && string.IsNullOrEmpty(previousActiveScene.path)
                && !replaceEmptyUntitledScratchScene)
            {
                throw new InvalidOperationException(
                    "当前存在未保存的场景内容。为避免覆盖制作数据，请先保存或关闭该场景，再创建玩家控制器测试场景。");
            }

            // Interactive authoring retains an already saved scene; batch mode and a clean empty
            // scratch scene both create the test scene as the sole active scene.
            NewSceneMode mode = Application.isBatchMode || replaceEmptyUntitledScratchScene
                ? NewSceneMode.Single
                : NewSceneMode.Additive;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            try
            {
                // Single creation already makes the sole new scene active. Calling SetActiveScene
                // again may return false in that state, although the scene is valid and active.
                // Additive creation still needs the explicit switch for all generated objects.
                if (mode == NewSceneMode.Additive && !SceneManager.SetActiveScene(scene))
                    throw new InvalidOperationException("无法激活用于生成的临时测试场景。");

                scene.name = "ESPlayerControllerTest";
                GameObject root = new GameObject("ES Player Controller Test");
                CreateRuntimeBootstrap(root.transform);
                CreateLighting(root.transform);
                PlayerControllerTestLandmarks landmarks = CreateTraversalEnvironment(root.transform);

                Entity player = CreatePlayer(playerPrefab, scene, root.transform);
                Camera playerCamera = CreatePlayerCamera(player, root.transform);
                VehicleController[] vehicles = CreateVehicles(carPrefab, bicyclePrefab, helicopterPrefab, scene, root.transform);
                CreateTestGuide(root.transform, player, playerCamera, landmarks, vehicles);

                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("保存玩家控制器测试场景失败：" + ScenePath);

                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);
                SceneAsset saved = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
                if (saved == null)
                    throw new InvalidOperationException("无法重新加载玩家控制器测试场景：" + ScenePath);

                Debug.Log("[玩家控制器测试] 场景已生成：" + ScenePath, saved);
                return saved;
            }
            finally
            {
                // An additive generation must restore the authoring scene and leave no temporary
                // scene loaded. A Single generation intentionally leaves the saved test scene open
                // so both MCP and an interactive user can inspect it immediately.
                if (mode == NewSceneMode.Additive)
                {
                    if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                        SceneManager.SetActiveScene(previousActiveScene);
                    if (scene.IsValid() && scene.isLoaded)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void CreateRuntimeBootstrap(Transform root)
        {
            GameObject managerObject = new GameObject("ESGameManager");
            managerObject.transform.SetParent(root, false);
            ESGameManager manager = managerObject.AddComponent<ESGameManager>();
            // 测试场景不跨场景保留 Manager，避免从该场景切出后留下本地控制权和输入模块。
            manager.dontDestroyOnLoad = false;
            manager.autoCreateInputModule = true;
            manager.autoCreatePhysicsQueryModule = true;
        }

        private static void ValidatePreparedDependencies(
            GameObject playerPrefab,
            GameObject carPrefab,
            GameObject bicyclePrefab,
            GameObject helicopterPrefab)
        {
            if (!ESCharacterTemplateReleaseGate.ValidateFormalCharacterPrefab(PlayerPrefabPath, out string playerReport))
            {
                throw new InvalidOperationException(
                    "玩家测试场景需要已通过正式门禁的大黑塔 Variant。请先显式执行“重建正式玩家 Variant/大黑塔（新版通用模板）”。\n"
                    + playerReport);
            }

            ValidateVehicleMountProbe(carPrefab, CarPrefabPath);
            ValidateVehicleMountProbe(bicyclePrefab, BicyclePrefabPath);
            ValidateVehicleMountProbe(helicopterPrefab, HelicopterPrefabPath);

            if (!ESCameraDefaultContentBuilder.TryLoadDefaultPlayerCameraContent(out _, out _, out string cameraError))
                throw new InvalidOperationException("玩家测试场景需要已准备好的 ES Camera 内容：" + cameraError);
            if (!ESCameraDefaultContentBuilder.TryLoadDefaultVehicleCameraContent(out _, out _, out string vehicleCameraError))
                throw new InvalidOperationException("玩家测试场景需要已准备好的载具 Camera 内容：" + vehicleCameraError);
        }

        private static void ValidateVehicleMountProbe(GameObject vehiclePrefab, string path)
        {
            EntityMountable mountable = vehiclePrefab.GetComponent<EntityMountable>();
            Collider bodyCollider = vehiclePrefab.GetComponent<Collider>();
            VehicleController controller = vehiclePrefab.GetComponent<VehicleController>();
            if (mountable == null || mountable.matchPoint == null || bodyCollider == null || controller == null)
                throw new InvalidOperationException("载具测试前置不完整：" + path + " 缺少 EntityMountable、VehicleController、DriverSeat 或根 Collider。");

            Transform probe = mountable.matchPoint.Find("MountInteractionProbe");
            SphereCollider probeCollider = probe != null ? probe.GetComponent<SphereCollider>() : null;
            if (vehiclePrefab.layer != ESPhysicsLayers.WorldDynamic || bodyCollider.isTrigger
                || probe == null || probe.gameObject.layer != ESPhysicsLayers.Interaction
                || probeCollider == null || !probeCollider.isTrigger)
            {
                throw new InvalidOperationException(
                    "载具测试前置不完整：" + path
                    + " 未通过 WorldDynamic 车体与 Interaction 座位探针约定。"
                    + "请先显式执行“升级方块载具骑乘探针”。");
            }

            if (!string.Equals(controller.driverCameraProfileKey, ESCameraDefaultContentBuilder.VehicleChaseProfileKey, StringComparison.Ordinal)
                || controller.driverCameraFollow != vehiclePrefab.transform)
            {
                throw new InvalidOperationException(
                    "载具测试前置不完整：" + path
                    + " 未配置驾驶镜头 Profile/Follow。请先显式执行“升级方块载具骑乘探针”。");
            }
        }

        private static void CreateLighting(Transform root)
        {
            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.52f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.28f, 0.31f, 0.35f);
            RenderSettings.ambientGroundColor = new Color(0.15f, 0.16f, 0.17f);
        }

        private static void CreateTestGuide(
            Transform root,
            Entity player,
            Camera playerCamera,
            PlayerControllerTestLandmarks landmarks,
            VehicleController[] vehicles)
        {
            // 场景专用导视：不向正式角色或载具 Prefab 增加任何测试组件。
            GameObject guideObject = new GameObject("验收导视（ES Scene Validation Guide）");
            guideObject.transform.SetParent(root, false);
            ESSceneValidationGuide guide = guideObject.AddComponent<ESSceneValidationGuide>();
            // 显式绑定测试场景的 Player 与输出 Camera。Guide 不会回退到 Camera.main，
            // 因此不会与角色、载具或正式 Camera Prefab 形成隐藏耦合。
            guide.routeObserver = player != null ? player.transform : null;
            guide.worldGuideCamera = playerCamera;

            VehicleController car = vehicles != null && vehicles.Length > 0 ? vehicles[0] : null;
            VehicleController bicycle = vehicles != null && vehicles.Length > 1 ? vehicles[1] : null;
            VehicleController helicopter = vehicles != null && vehicles.Length > 2 ? vehicles[2] : null;
            guide.ConfigureForAuthoring(
                "ES 玩家控制器 · 综合验收场",
                "按编号路线操作；每一步均给出实际输入、预期结果与当前 ES 运行状态。",
                new[]
                {
                    new ESSceneValidationStage
                    {
                        id = "boot",
                        title = "启动与本地控制",
                        landmark = player != null ? player.transform : null,
                        objective = "确认玩家生成于起点，并尝试移动与转向。",
                        expectedResult = "本地控制权、输入模块和 MainView 均已就绪。",
                        failureHint = "依次检查 ESGameManager、LocalControl、Input 与 Camera SceneBinding。",
                        inputActions = new[] { ESInputActionId.Move, ESInputActionId.Look },
                        checkIds = new[] { "framework", "input", "local-control", "main-camera" },
                    },
                    new ESSceneValidationStage
                    {
                        id = "vault",
                        title = "移动、跳跃与翻越",
                        landmark = landmarks.lowWall,
                        objective = "前往右前方低墙，完成移动、跳跃和翻越。",
                        expectedResult = "KCC 运动连续；翻越结束后角色重新回到地面状态。",
                        failureHint = "检查 KCC、ClimbableSurface、输入调度与玩家状态包。",
                        inputActions = new[] { ESInputActionId.Move, ESInputActionId.Jump, ESInputActionId.Climb },
                        checkIds = new[] { "movement-observation" },
                    },
                    new ESSceneValidationStage
                    {
                        id = "climb",
                        title = "攀爬与翻上",
                        landmark = landmarks.climbWall,
                        objective = "前往正前方攀爬墙，完成附着、上行与翻上平台。",
                        expectedResult = "攀爬状态进入与退出正确，翻上后落在后方平台。",
                        failureHint = "检查攀爬层、墙体标记、状态支持标记与 KCC 运动顺序。",
                        inputActions = new[] { ESInputActionId.Move, ESInputActionId.Jump, ESInputActionId.Climb },
                        checkIds = new[] { "climb-observation" },
                    },
                    new ESSceneValidationStage
                    {
                        id = "ground-vehicle",
                        title = "汽车与自行车骑乘",
                        landmark = car != null ? car.transform : null,
                        objective = "靠近左侧座位探针，分别上车、驾驶并离座。",
                        expectedResult = "Mounted 封锁角色地面行动；驾驶权与车辆输入只属于当前座位。",
                        failureHint = "检查 Interaction Layer、EntityMountable、VehicleController 与 Mounted 状态配置。",
                        inputActions = new[] { ESInputActionId.Move, ESInputActionId.Look, ESInputActionId.Mount },
                        checkIds = new[] { "player-mounted", "car-ready", "car-driver", "bicycle-ready", "bicycle-driver" },
                    },
                    new ESSceneValidationStage
                    {
                        id = "air-vehicle",
                        title = "直升机驾驶与镜头恢复",
                        landmark = helicopter != null ? helicopter.transform : null,
                        objective = "骑乘右侧直升机，验证水平、垂直驾驶输入并离座。",
                        expectedResult = "驾驶镜头获得主视角；离座或禁用 Controller 后角色与镜头完整恢复。",
                        failureHint = "检查驾驶权仲裁、输入过期保护、VehicleController 禁用补偿与 Camera Lease。",
                        inputActions = new[] { ESInputActionId.Move, ESInputActionId.Look, ESInputActionId.FlyVertical, ESInputActionId.Mount },
                        checkIds = new[] { "helicopter-ready", "helicopter-driver", "main-camera" },
                    },
                },
                new[]
                {
                    new ESSceneValidationCheck { id = "framework", title = "ESGameManager", kind = ESSceneValidationCheckKind.FrameworkReady },
                    new ESSceneValidationCheck { id = "input", title = "输入模块", kind = ESSceneValidationCheckKind.InputReady },
                    new ESSceneValidationCheck { id = "local-control", title = "本地控制权", kind = ESSceneValidationCheckKind.LocalControlOwner, target = player },
                    new ESSceneValidationCheck { id = "main-camera", title = "MainView 输出", kind = ESSceneValidationCheckKind.CameraOutputReady, cameraViewKey = ESCameraViewId.Main.Key, target = playerCamera },
                    new ESSceneValidationCheck { id = "movement-observation", title = "KCC 移动观察", kind = ESSceneValidationCheckKind.ManualObservation, manualHint = "观察运动是否平滑、跳跃是否按缓冲时间消费。" },
                    new ESSceneValidationCheck { id = "climb-observation", title = "攀爬流程观察", kind = ESSceneValidationCheckKind.ManualObservation, manualHint = "观察附着、上行、翻上与状态退出是否连续。" },
                    new ESSceneValidationCheck { id = "player-mounted", title = "玩家 Mounted", kind = ESSceneValidationCheckKind.EntityMounted, target = player },
                    new ESSceneValidationCheck { id = "car-ready", title = "汽车控制器", kind = ESSceneValidationCheckKind.VehicleReady, target = car },
                    new ESSceneValidationCheck { id = "car-driver", title = "汽车驾驶权", kind = ESSceneValidationCheckKind.VehicleDriverOwner, target = car, expectedEntity = player },
                    new ESSceneValidationCheck { id = "bicycle-ready", title = "自行车控制器", kind = ESSceneValidationCheckKind.VehicleReady, target = bicycle },
                    new ESSceneValidationCheck { id = "bicycle-driver", title = "自行车驾驶权", kind = ESSceneValidationCheckKind.VehicleDriverOwner, target = bicycle, expectedEntity = player },
                    new ESSceneValidationCheck { id = "helicopter-ready", title = "直升机控制器", kind = ESSceneValidationCheckKind.VehicleReady, target = helicopter },
                    new ESSceneValidationCheck { id = "helicopter-driver", title = "直升机驾驶权", kind = ESSceneValidationCheckKind.VehicleDriverOwner, target = helicopter, expectedEntity = player },
                });
        }

        private static PlayerControllerTestLandmarks CreateTraversalEnvironment(Transform root)
        {
            var landmarks = new PlayerControllerTestLandmarks();
            Transform environment = new GameObject("Traversal Environment").transform;
            environment.SetParent(root, false);

            CreateCube(environment, "Ground", new Vector3(0f, -0.5f, 6f), new Vector3(34f, 1f, 34f), ESPhysicsLayers.Ground);

            GameObject climbWall = CreateCube(
                environment,
                "Climb Wall",
                new Vector3(0f, 2f, 10f),
                new Vector3(7f, 4f, 0.5f),
                ESPhysicsLayers.Wall);
            ConfigureClimbSurface(climbWall, ClimbableSurfaceType.Wall);
            landmarks.climbWall = climbWall.transform;

            // 位于攀爬墙后方的可站立平台，用于验证攀上/翻越后的落点。
            CreateCube(
                environment,
                "Climb Wall Top Platform",
                new Vector3(0f, 3.75f, 12.25f),
                new Vector3(7f, 0.5f, 4f),
                ESPhysicsLayers.Ground);

            GameObject lowWall = CreateCube(
                environment,
                "Vault Low Wall",
                new Vector3(6f, 0.55f, 7f),
                new Vector3(3.2f, 1.1f, 0.65f),
                ESPhysicsLayers.Wall);
            ConfigureClimbSurface(lowWall, ClimbableSurfaceType.LowWall);
            landmarks.lowWall = lowWall.transform;

            return landmarks;
        }

        private static Entity CreatePlayer(GameObject prefab, Scene scene, Transform root)
        {
            GameObject playerObject = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (playerObject == null)
                throw new InvalidOperationException("实例化正式玩家失败：" + PlayerPrefabPath);

            playerObject.name = "Player_大黑塔";
            playerObject.transform.SetParent(root, true);
            playerObject.transform.SetPositionAndRotation(new Vector3(0f, 0.02f, 0f), Quaternion.identity);

            Entity player = playerObject.GetComponent<Entity>();
            EntityCharacterProfile profile = playerObject.GetComponent<EntityCharacterProfile>();
            if (player == null || profile == null || profile.prefabRole != EntityCharacterPrefabRole.CharacterVariant)
                throw new InvalidOperationException("玩家测试场景只能实例化正式 CharacterVariant。" );

            return player;
        }

        private static Camera CreatePlayerCamera(Entity player, Transform sceneRoot)
        {
            EntityTransformMapping mapping = player.GetComponent<EntityTransformMapping>();
            Transform cameraAnchor = mapping != null ? mapping.Resolve("CameraTarget") : null;
            if (cameraAnchor == null && mapping != null)
                cameraAnchor = mapping.Resolve(DefaultTransformKey.Camera);
            if (cameraAnchor == null)
                throw new InvalidOperationException("正式玩家缺少 CameraTarget Mapping，无法创建测试相机。");

            return ESCameraDefaultContentBuilder.CreateDefaultMainViewForAuthoring(sceneRoot);

        }

        private static VehicleController[] CreateVehicles(
            GameObject carPrefab,
            GameObject bicyclePrefab,
            GameObject helicopterPrefab,
            Scene scene,
            Transform root)
        {
            Transform vehicles = new GameObject("Vehicle Mount Tests").transform;
            vehicles.SetParent(root, false);

            return new[]
            {
                CreateVehicle(carPrefab, scene, vehicles, "Car", new Vector3(-7f, 0.1f, 2f), Quaternion.Euler(0f, 90f, 0f)),
                CreateVehicle(bicyclePrefab, scene, vehicles, "Bicycle", new Vector3(-7f, 0.1f, -3f), Quaternion.Euler(0f, 90f, 0f)),
                CreateVehicle(helicopterPrefab, scene, vehicles, "Helicopter", new Vector3(8f, 0.1f, 3f), Quaternion.Euler(0f, -90f, 0f)),
            };
        }

        private static VehicleController CreateVehicle(
            GameObject prefab,
            Scene scene,
            Transform parent,
            string displayName,
            Vector3 position,
            Quaternion rotation)
        {
            GameObject vehicle = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (vehicle == null)
                throw new InvalidOperationException("实例化载具失败：" + prefab.name);

            vehicle.name = "Mount Test - " + displayName;
            vehicle.transform.SetParent(parent, true);
            vehicle.transform.SetPositionAndRotation(position, rotation);

            VehicleController controller = vehicle.GetComponent<VehicleController>();
            if (vehicle.GetComponent<EntityMountable>() == null || controller == null)
                throw new InvalidOperationException("载具原型缺少 EntityMountable 或 VehicleController：" + displayName);

            return controller;
        }

        private static GameObject CreateCube(Transform parent, string name, Vector3 position, Vector3 scale, int layer)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            cube.layer = layer;
            return cube;
        }

        private static void ConfigureClimbSurface(GameObject surfaceObject, ClimbableSurfaceType surfaceType)
        {
            ClimbableSurface surface = surfaceObject.AddComponent<ClimbableSurface>();
            surface.surfaceType = surfaceType;
            surface.areaCenter = Vector3.zero;
            surface.areaSize = Vector3.one;
            surface.enableBilateral = true;
            surface.showGizmo = true;
        }

        private static GameObject LoadRequiredPrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                throw new InvalidOperationException("缺少测试场景需要的 Prefab：" + path);
            return prefab;
        }

        private sealed class PlayerControllerTestLandmarks
        {
            public Transform climbWall;
            public Transform lowWall;
        }
    }
}
