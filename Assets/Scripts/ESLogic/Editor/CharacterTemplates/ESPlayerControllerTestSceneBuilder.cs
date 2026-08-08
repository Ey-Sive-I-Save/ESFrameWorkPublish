using System;
using System.Collections.Generic;
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
                PlayerControllerTestAreaLayout areaLayout = CreateTestEnvironment(root.transform);

                Entity player = CreatePlayer(playerPrefab, scene, root.transform);
                Camera playerCamera = CreatePlayerCamera(player, root.transform);
                VehicleController[] vehicles = CreateVehicles(carPrefab, bicyclePrefab, helicopterPrefab, scene, root.transform);
                CreateTestGuide(root.transform, player, playerCamera, areaLayout, vehicles);

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

            if (controller.driverCameraDefinition != ESCameraDefaultContentBuilder.VehicleChaseDefinition
                || controller.driverCameraFollow != vehiclePrefab.transform)
            {
                throw new InvalidOperationException(
                    "载具测试前置不完整：" + path
                    + " 未配置驾驶镜头 Definition/Follow。请先显式执行“升级方块载具骑乘探针”。");
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
            PlayerControllerTestAreaLayout areaLayout,
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
            var stages = new List<ESSceneValidationStage>(areaLayout.Specs.Count);
            var checks = new List<ESSceneValidationCheck>(areaLayout.Specs.Count + 16)
            {
                new ESSceneValidationCheck { id = "framework", title = "ESGameManager", kind = ESSceneValidationCheckKind.FrameworkReady },
                new ESSceneValidationCheck { id = "input", title = "输入模块", kind = ESSceneValidationCheckKind.InputReady },
                new ESSceneValidationCheck { id = "local-control", title = "本地控制权", kind = ESSceneValidationCheckKind.LocalControlOwner, target = player },
                new ESSceneValidationCheck { id = "main-camera", title = "MainView 输出", kind = ESSceneValidationCheckKind.CameraOutputReady, cameraViewKey = ESCameraViewId.Main.Key, target = playerCamera },
                new ESSceneValidationCheck { id = "player-mounted", title = "玩家 Mounted", kind = ESSceneValidationCheckKind.EntityMounted, target = player },
                new ESSceneValidationCheck { id = "car-ready", title = "汽车控制器", kind = ESSceneValidationCheckKind.VehicleReady, target = car },
                new ESSceneValidationCheck { id = "car-driver", title = "汽车驾驶权", kind = ESSceneValidationCheckKind.VehicleDriverOwner, target = car, expectedEntity = player },
                new ESSceneValidationCheck { id = "bicycle-ready", title = "自行车控制器", kind = ESSceneValidationCheckKind.VehicleReady, target = bicycle },
                new ESSceneValidationCheck { id = "bicycle-driver", title = "自行车驾驶权", kind = ESSceneValidationCheckKind.VehicleDriverOwner, target = bicycle, expectedEntity = player },
                new ESSceneValidationCheck { id = "helicopter-ready", title = "直升机控制器", kind = ESSceneValidationCheckKind.VehicleReady, target = helicopter },
                new ESSceneValidationCheck { id = "helicopter-driver", title = "直升机驾驶权", kind = ESSceneValidationCheckKind.VehicleDriverOwner, target = helicopter, expectedEntity = player },
            };

            for (int i = 0; i < areaLayout.Specs.Count; i++)
            {
                PlayerControllerTestAreaSpec spec = areaLayout.Specs[i];
                string observationCheckId = "area-" + spec.id + "-observation";
                var stageCheckIds = new List<string>(6);
                if (i == 0)
                {
                    stageCheckIds.Add("framework");
                    stageCheckIds.Add("input");
                    stageCheckIds.Add("local-control");
                    stageCheckIds.Add("main-camera");
                }
                else
                {
                    stageCheckIds.Add(observationCheckId);
                    checks.Add(new ESSceneValidationCheck
                    {
                        id = observationCheckId,
                        title = spec.title + (spec.status == ESDemoTestAreaStatus.Ready ? "观察" : "预留确认"),
                        kind = ESSceneValidationCheckKind.ManualObservation,
                        manualHint = spec.status == ESDemoTestAreaStatus.Ready
                            ? spec.expectedResult
                            : "该区域当前为 " + spec.status + "，仅确认预留边界和说明清晰，不得记录为功能通过。",
                    });
                }

                if (string.Equals(spec.id, "22-ground-vehicle", StringComparison.Ordinal))
                {
                    stageCheckIds.Add("player-mounted");
                    stageCheckIds.Add("car-ready");
                    stageCheckIds.Add("car-driver");
                    stageCheckIds.Add("bicycle-ready");
                    stageCheckIds.Add("bicycle-driver");
                }
                else if (string.Equals(spec.id, "23-helicopter", StringComparison.Ordinal))
                {
                    stageCheckIds.Add("player-mounted");
                    stageCheckIds.Add("helicopter-ready");
                    stageCheckIds.Add("helicopter-driver");
                    stageCheckIds.Add("main-camera");
                }

                stages.Add(new ESSceneValidationStage
                {
                    id = spec.id,
                    title = spec.title,
                    landmark = areaLayout.Require(spec.id).transform,
                    routeColor = GetAreaColor(spec.status),
                    objective = spec.objective,
                    expectedResult = spec.expectedResult,
                    failureHint = spec.failureHint,
                    inputActions = spec.inputActions,
                    checkIds = stageCheckIds.ToArray(),
                });
            }

            guide.ConfigureForAuthoring(
                "ES 玩家控制器 · 24 区综合验收场",
                "蓝色为可测试设施，黄色为明确预留，红色为阻断区；预留区不得记录为功能通过。",
                stages,
                checks);
        }

        private static PlayerControllerTestAreaLayout CreateTestEnvironment(Transform root)
        {
            Transform environment = new GameObject("综合测试环境").transform;
            environment.SetParent(root, false);
            CreateCube(environment, "综合测试地面", new Vector3(0f, -0.5f, 45f), new Vector3(72f, 1f, 108f), ESPhysicsLayers.Ground);

            Transform areaRoot = new GameObject("测试区域（24）").transform;
            areaRoot.SetParent(environment, false);
            ESDemoTestAreaSet areaSet = areaRoot.gameObject.AddComponent<ESDemoTestAreaSet>();
            List<PlayerControllerTestAreaSpec> specs = CreateAreaSpecs();
            var markers = new List<ESDemoTestAreaMarker>(specs.Count);
            var markerById = new Dictionary<string, ESDemoTestAreaMarker>(StringComparer.Ordinal);

            for (int i = 0; i < specs.Count; i++)
            {
                PlayerControllerTestAreaSpec spec = specs[i];
                GameObject areaObject = new GameObject($"区域_{spec.number:00}_{spec.title}");
                areaObject.transform.SetParent(areaRoot, false);
                areaObject.transform.localPosition = spec.position;
                ESDemoTestAreaMarker marker = areaObject.AddComponent<ESDemoTestAreaMarker>();
                marker.ConfigureForAuthoring(
                    spec.number,
                    spec.id,
                    spec.title,
                    spec.category,
                    spec.status,
                    spec.objective,
                    GetAreaColor(spec.status),
                    new Vector3(14f, 4f, 14f));
                CreateAreaPadAndSign(marker.transform, spec);
                markers.Add(marker);
                markerById.Add(spec.id, marker);
            }

            areaSet.ConfigureForAuthoring(20, markers);
            if (!areaSet.ValidateForAuthoring(out string areaReport))
                throw new InvalidOperationException("玩家控制器测试区域配置无效：\n" + areaReport);

            var layout = new PlayerControllerTestAreaLayout(specs, markerById);
            CreateAreaFacilities(layout);
            Debug.Log("[玩家控制器测试] " + areaReport, areaSet);
            return layout;
        }

        private static List<PlayerControllerTestAreaSpec> CreateAreaSpecs()
        {
            return new List<PlayerControllerTestAreaSpec>
            {
                Area(1, "01-boot", "启动与本地控制", "Control", ESDemoTestAreaStatus.Ready, -24f, 0f,
                    "确认角色生成、LocalControl、输入模块和 MainView 均就绪。", "WASD 与鼠标可直接控制正式玩家角色。", "检查 ESGameManager、LocalControl、Input 与 Camera SceneBinding。", ESInputActionId.Move, ESInputActionId.Look),
                Area(2, "02-linear-move", "基础直线移动", "Movement", ESDemoTestAreaStatus.Ready, -8f, 0f,
                    "沿标线往返移动，观察起步、停止和连续输入。", "角色沿地面稳定移动，无输入丢失或异常漂移。", "检查 Player Writer、EntityAIDomain 输入消费和 KCC。", ESInputActionId.Move),
                Area(3, "03-turning", "转向与镜头相对移动", "Movement", ESDemoTestAreaStatus.Ready, 8f, 0f,
                    "绕过蛇形路标并持续旋转镜头。", "移动方向与当前 MainView 朝向一致，转向连续。", "检查 Look 输入、相机朝向读取和运动转向。", ESInputActionId.Move, ESInputActionId.Look),
                Area(4, "04-jump-buffer", "跳跃与输入缓冲", "Movement", ESDemoTestAreaStatus.Ready, 24f, 0f,
                    "跨越低障碍，并在落地前后测试跳跃输入。", "跳跃只在合法时机消费，落地和再跳状态正确。", "检查 Jump 脉冲、缓冲窗口和接地状态。", ESInputActionId.Move, ESInputActionId.Jump),
                Area(5, "05-slope", "斜坡移动", "Movement", ESDemoTestAreaStatus.Ready, -24f, 18f,
                    "从两个方向上下斜坡并在坡面停住。", "坡面移动、接地和速度变化连续。", "检查 KCC 坡度、Ground Layer 与接地法线。", ESInputActionId.Move),
                Area(6, "06-stairs", "台阶通过", "Movement", ESDemoTestAreaStatus.Ready, -8f, 18f,
                    "低速和高速通过连续台阶。", "角色可稳定跨阶，不持续弹跳或卡边。", "检查 KCC Step 配置和碰撞体尺寸。", ESInputActionId.Move),
                Area(7, "07-low-vault", "低墙翻越", "Traversal", ESDemoTestAreaStatus.Ready, 8f, 18f,
                    "接近低墙并触发翻越。", "翻越结束后恢复地面运动，位置无穿透。", "检查 LowWall、Climb 输入和状态退出。", ESInputActionId.Move, ESInputActionId.Jump, ESInputActionId.Climb),
                Area(8, "08-high-vault", "高翻越预留", "Traversal", ESDemoTestAreaStatus.Planned, 24f, 18f,
                    "确认高翻越设施和接口边界预留。", "仅确认区域可见；当前不得记录高翻越通过。", "等待高翻越能力进入 Integrating 后接真实设施。", ESInputActionId.Move, ESInputActionId.Climb),
                Area(9, "09-front-climb", "正面攀爬与翻上", "Traversal", ESDemoTestAreaStatus.Ready, -24f, 36f,
                    "从正面附着墙体、上行并翻上平台。", "攀爬进入、移动、翻上和退出连续。", "检查 ClimbableSurface、状态支持标记与 KCC 顺序。", ESInputActionId.Move, ESInputActionId.Jump, ESInputActionId.Climb),
                Area(10, "10-side-climb", "双向与侧向攀爬", "Traversal", ESDemoTestAreaStatus.Ready, -8f, 36f,
                    "从墙体两面附着并尝试横向移动。", "双面识别和侧向输入方向一致。", "检查 enableBilateral、墙面法线和攀爬切线。", ESInputActionId.Move, ESInputActionId.Climb),
                Area(11, "11-top-clearance", "攀爬顶部净空", "Traversal", ESDemoTestAreaStatus.Ready, 8f, 36f,
                    "在不同顶部净空下尝试翻上。", "可翻区域正常完成，受阻区域不会穿透顶障碍。", "检查顶部探测、翻上落点和碰撞查询层。", ESInputActionId.Move, ESInputActionId.Climb),
                Area(12, "12-narrow-corridor", "窄通道", "Collision", ESDemoTestAreaStatus.Ready, 24f, 36f,
                    "通过不同宽度的窄通道并转身。", "角色不穿墙、不抖动，镜头仍可观察。", "检查角色胶囊体、KCC 解穿透和 Camera Collider。", ESInputActionId.Move, ESInputActionId.Look),
                Area(13, "13-ledge-drop", "边缘与落差", "Movement", ESDemoTestAreaStatus.Ready, -24f, 54f,
                    "从高台边缘离地并落到地面。", "离地、空中、落地状态按顺序切换。", "检查接地探测、竖直速度和落地恢复。", ESInputActionId.Move, ESInputActionId.Jump),
                Area(14, "14-moving-platform", "移动平台预留", "Traversal", ESDemoTestAreaStatus.Planned, -8f, 54f,
                    "确认移动平台承载与速度继承测试边界。", "仅确认静态预留平台和路线，不宣称移动平台支持。", "待移动平台正式实现后替换静态占位设施。", ESInputActionId.Move, ESInputActionId.Jump),
                Area(15, "15-interaction", "交互预留", "Interaction", ESDemoTestAreaStatus.Planned, 8f, 54f,
                    "确认通用交互目标、距离和提示位置预留。", "仅确认交互测试台可见，不宣称交互闭环。", "待正式 Interactable 契约接入后绑定真实目标。", ESInputActionId.Interact),
                Area(16, "16-combat", "战斗预留", "Combat", ESDemoTestAreaStatus.Planned, 24f, 54f,
                    "确认轻击、重击、格挡目标和安全距离预留。", "仅确认战斗靶场布局，不宣称伤害闭环。", "待战斗 Domain 和目标契约稳定后接真实靶子。", ESInputActionId.Attack, ESInputActionId.HeavyAttack, ESInputActionId.Block),
                Area(17, "17-weapon", "武器切换预留", "Combat", ESDemoTestAreaStatus.Planned, -24f, 72f,
                    "确认装备、收起、切换和武器槽位测试台。", "仅确认武器架预留，不宣称装备链路通过。", "待武器实体与库存接口稳定后接入真实武器。", ESInputActionId.EquipWeapon, ESInputActionId.HolsterWeapon, ESInputActionId.SwitchWeapon),
                Area(18, "18-skill", "技能预留", "Skill", ESDemoTestAreaStatus.Planned, -8f, 72f,
                    "确认三个技能输入、目标距离和范围标记预留。", "仅确认技能靶场布局，不宣称技能执行通过。", "待技能配置和效果目标进入 Integrating 后接入。", ESInputActionId.Skill1, ESInputActionId.Skill2, ESInputActionId.Skill3),
                Area(19, "19-permit", "Control Permit 阻断预留", "Control", ESDemoTestAreaStatus.Planned, 8f, 72f,
                    "确认剧情、网络或 AI 临时阻断玩家意图的验收位置。", "契约保留；无真实消费者前不得宣称控制源切换完成。", "接入真实 Permit 消费者、恢复路径和测试后再升级状态。", ESInputActionId.Move, ESInputActionId.Look),
                Area(20, "20-camera-occlusion", "相机遮挡与恢复", "Camera", ESDemoTestAreaStatus.Ready, 24f, 72f,
                    "绕柱、贴墙和进入遮挡棚，观察 Cinemachine 镜头。", "镜头避障连续，离开遮挡后平滑恢复且无额外相机激活。", "检查 Cinemachine Collider、Rig 活性和 MainView Lease。", ESInputActionId.Move, ESInputActionId.Look),
                Area(21, "21-lock-camera", "锁敌镜头预留", "Camera", ESDemoTestAreaStatus.Planned, -24f, 90f,
                    "确认锁定目标、切换目标和距离边界预留。", "仅确认目标桩与环形路线，不宣称锁敌镜头存在。", "待锁敌服务与 Camera Request 接入后绑定真实目标。", ESInputActionId.Look, ESInputActionId.Aim),
                Area(22, "22-ground-vehicle", "汽车与自行车骑乘", "Vehicle", ESDemoTestAreaStatus.Ready, -8f, 90f,
                    "分别上车、驾驶、离座并观察角色状态。", "Mounted 封锁地面行动；驾驶权和输入只属于当前座位。", "检查 Interaction Layer、EntityMountable、VehicleController 和 Camera Lease。", ESInputActionId.Move, ESInputActionId.Look, ESInputActionId.Mount),
                Area(23, "23-helicopter", "直升机驾驶与镜头恢复", "Vehicle", ESDemoTestAreaStatus.Ready, 8f, 90f,
                    "骑乘直升机，验证水平、垂直输入并离座。", "驾驶镜头获得主视角；离座后角色和 MainView 完整恢复。", "检查驾驶权仲裁、输入过期保护和 Vehicle Camera Lease。", ESInputActionId.Move, ESInputActionId.Look, ESInputActionId.FlyVertical, ESInputActionId.Mount),
                Area(24, "24-pool-control", "池化与控制权切换预留", "Lifecycle", ESDemoTestAreaStatus.Planned, 24f, 90f,
                    "确认回池、复用、本地控制权转移和恢复测试位置。", "仅确认三个生命周期测试槽位，不宣称池化切换已通过。", "待可控测试实体和自动验收驱动接入后执行完整复用测试。", ESInputActionId.Move, ESInputActionId.Look),
            };
        }

        private static PlayerControllerTestAreaSpec Area(
            int number,
            string id,
            string title,
            string category,
            ESDemoTestAreaStatus status,
            float x,
            float z,
            string objective,
            string expectedResult,
            string failureHint,
            params ESInputActionId[] inputActions)
        {
            return new PlayerControllerTestAreaSpec(
                number,
                id,
                title,
                category,
                status,
                new Vector3(x, 0f, z),
                objective,
                expectedResult,
                failureHint,
                inputActions);
        }

        private static void CreateAreaPadAndSign(Transform area, PlayerControllerTestAreaSpec spec)
        {
            CreateVisualCube(area, "区域边界底板", new Vector3(0f, 0.03f, 0f), new Vector3(14f, 0.06f, 14f));
            CreateVisualCube(area, "编号标识柱", new Vector3(-6.2f, 1.25f, -6.2f), new Vector3(0.35f, 2.5f, 0.35f));

            GameObject labelObject = new GameObject("区域标牌");
            labelObject.transform.SetParent(area, false);
            labelObject.transform.localPosition = new Vector3(-5.8f, 2.7f, -5.8f);
            labelObject.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = $"{spec.number:00}  {spec.title}\n{spec.status}";
            label.anchor = TextAnchor.LowerLeft;
            label.alignment = TextAlignment.Left;
            label.characterSize = 0.22f;
            label.fontSize = 48;
            label.color = GetAreaColor(spec.status);
        }

        private static void CreateAreaFacilities(PlayerControllerTestAreaLayout layout)
        {
            Transform area02 = layout.Require("02-linear-move").transform;
            for (int i = -2; i <= 2; i++)
                CreateVisualCube(area02, "直线检查点_" + (i + 3), new Vector3(0f, 0.04f, i * 2.5f), new Vector3(2.5f, 0.08f, 0.18f));

            Transform area03 = layout.Require("03-turning").transform;
            for (int i = 0; i < 5; i++)
                CreateCube(area03, "蛇形路标_" + (i + 1), new Vector3(i % 2 == 0 ? -2.2f : 2.2f, 0.55f, -4f + i * 2f), new Vector3(0.65f, 1.1f, 0.65f), ESPhysicsLayers.WorldDynamic);

            Transform area04 = layout.Require("04-jump-buffer").transform;
            CreateCube(area04, "跳跃障碍_低", new Vector3(0f, 0.35f, -2.5f), new Vector3(5f, 0.7f, 0.45f), ESPhysicsLayers.Wall);
            CreateCube(area04, "跳跃障碍_中", new Vector3(0f, 0.55f, 2.5f), new Vector3(5f, 1.1f, 0.45f), ESPhysicsLayers.Wall);

            Transform area05 = layout.Require("05-slope").transform;
            GameObject slope = CreateCube(area05, "双向斜坡", new Vector3(0f, 0.9f, 0f), new Vector3(7f, 0.5f, 8f), ESPhysicsLayers.Ground);
            slope.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);

            Transform area06 = layout.Require("06-stairs").transform;
            for (int i = 0; i < 6; i++)
                CreateCube(area06, "台阶_" + (i + 1), new Vector3(0f, 0.15f + i * 0.25f, -4f + i * 1.25f), new Vector3(5f, 0.3f + i * 0.5f, 1.25f), ESPhysicsLayers.Ground);

            Transform area07 = layout.Require("07-low-vault").transform;
            GameObject lowWall = CreateCube(area07, "真实低墙翻越设施", new Vector3(0f, 0.55f, 0f), new Vector3(5f, 1.1f, 0.65f), ESPhysicsLayers.Wall);
            ConfigureClimbSurface(lowWall, ClimbableSurfaceType.LowWall);

            Transform area08 = layout.Require("08-high-vault").transform;
            CreateCube(area08, "PLANNED_高翻越占位墙", new Vector3(0f, 1.15f, 0f), new Vector3(5f, 2.3f, 0.7f), ESPhysicsLayers.Wall);

            Transform area09 = layout.Require("09-front-climb").transform;
            GameObject climbWall = CreateCube(area09, "真实正面攀爬墙", new Vector3(0f, 2f, 0f), new Vector3(7f, 4f, 0.5f), ESPhysicsLayers.Wall);
            ConfigureClimbSurface(climbWall, ClimbableSurfaceType.Wall);
            CreateCube(area09, "攀爬墙顶部平台", new Vector3(0f, 3.75f, 2.25f), new Vector3(7f, 0.5f, 4f), ESPhysicsLayers.Ground);

            Transform area10 = layout.Require("10-side-climb").transform;
            GameObject bilateralWall = CreateCube(area10, "真实双面侧向攀爬墙", new Vector3(0f, 2f, 0f), new Vector3(8f, 4f, 0.5f), ESPhysicsLayers.Wall);
            ConfigureClimbSurface(bilateralWall, ClimbableSurfaceType.Wall);

            Transform area11 = layout.Require("11-top-clearance").transform;
            GameObject clearanceWall = CreateCube(area11, "顶部净空攀爬墙", new Vector3(0f, 1.75f, 0f), new Vector3(7f, 3.5f, 0.5f), ESPhysicsLayers.Wall);
            ConfigureClimbSurface(clearanceWall, ClimbableSurfaceType.Wall);
            CreateCube(area11, "可翻上平台", new Vector3(-2f, 3.4f, 1.8f), new Vector3(3f, 0.4f, 3f), ESPhysicsLayers.Ground);
            CreateCube(area11, "顶部受阻对照块", new Vector3(2.2f, 4.35f, 0.8f), new Vector3(2.5f, 0.5f, 2.5f), ESPhysicsLayers.Wall);

            Transform area12 = layout.Require("12-narrow-corridor").transform;
            CreateCube(area12, "窄通道左墙", new Vector3(-1.15f, 1.5f, 0f), new Vector3(0.35f, 3f, 10f), ESPhysicsLayers.Wall);
            CreateCube(area12, "窄通道右墙", new Vector3(1.15f, 1.5f, 0f), new Vector3(0.35f, 3f, 10f), ESPhysicsLayers.Wall);

            Transform area13 = layout.Require("13-ledge-drop").transform;
            CreateCube(area13, "落差高台", new Vector3(0f, 1.5f, -1f), new Vector3(8f, 3f, 7f), ESPhysicsLayers.Ground);
            CreateVisualCube(area13, "落地区", new Vector3(0f, 0.04f, 4.5f), new Vector3(8f, 0.08f, 3f));

            Transform area14 = layout.Require("14-moving-platform").transform;
            CreateCube(area14, "PLANNED_静态移动平台占位", new Vector3(0f, 1.2f, 0f), new Vector3(5f, 0.4f, 5f), ESPhysicsLayers.Ground);
            CreateVisualCube(area14, "PLANNED_运动轨迹", new Vector3(0f, 0.08f, 0f), new Vector3(11f, 0.12f, 1f));

            Transform area15 = layout.Require("15-interaction").transform;
            for (int i = -1; i <= 1; i++)
                CreateCube(area15, "PLANNED_交互台_" + (i + 2), new Vector3(i * 3f, 0.75f, 0f), new Vector3(1.2f, 1.5f, 1.2f), ESPhysicsLayers.WorldDynamic);

            Transform area16 = layout.Require("16-combat").transform;
            for (int i = -1; i <= 1; i++)
                CreateCube(area16, "PLANNED_战斗靶_" + (i + 2), new Vector3(i * 3f, 1.2f, 2f), new Vector3(0.9f, 2.4f, 0.9f), ESPhysicsLayers.WorldDynamic);

            Transform area17 = layout.Require("17-weapon").transform;
            CreateCube(area17, "PLANNED_武器架", new Vector3(0f, 1f, 2f), new Vector3(8f, 2f, 0.6f), ESPhysicsLayers.WorldDynamic);
            for (int i = -2; i <= 2; i++)
                CreateVisualCube(area17, "PLANNED_武器槽_" + (i + 3), new Vector3(i * 1.4f, 1.1f, 1.6f), new Vector3(0.18f, 1.6f, 0.18f));

            Transform area18 = layout.Require("18-skill").transform;
            for (int i = 0; i < 3; i++)
                CreateCube(area18, "PLANNED_技能目标_" + (i + 1), new Vector3(-3f + i * 3f, 0.8f, 2.5f), new Vector3(1f, 1.6f, 1f), ESPhysicsLayers.WorldDynamic);
            CreateVisualCube(area18, "PLANNED_范围边界", new Vector3(0f, 0.06f, 0f), new Vector3(10f, 0.1f, 10f));

            Transform area19 = layout.Require("19-permit").transform;
            CreateCube(area19, "PLANNED_ControlPermit闸门_左", new Vector3(-2.3f, 1.5f, 0f), new Vector3(0.5f, 3f, 5f), ESPhysicsLayers.Wall);
            CreateCube(area19, "PLANNED_ControlPermit闸门_右", new Vector3(2.3f, 1.5f, 0f), new Vector3(0.5f, 3f, 5f), ESPhysicsLayers.Wall);
            CreateVisualCube(area19, "PLANNED_阻断线", new Vector3(0f, 0.06f, 0f), new Vector3(4.2f, 0.1f, 0.5f));

            Transform area20 = layout.Require("20-camera-occlusion").transform;
            CreateCube(area20, "镜头遮挡墙", new Vector3(0f, 1.8f, 1f), new Vector3(7f, 3.6f, 0.5f), ESPhysicsLayers.Wall);
            CreateCube(area20, "镜头遮挡柱_左", new Vector3(-3.5f, 1.5f, -2.5f), new Vector3(1f, 3f, 1f), ESPhysicsLayers.Wall);
            CreateCube(area20, "镜头遮挡柱_右", new Vector3(3.5f, 1.5f, -2.5f), new Vector3(1f, 3f, 1f), ESPhysicsLayers.Wall);
            CreateCube(area20, "镜头遮挡棚顶", new Vector3(0f, 3.2f, -2.5f), new Vector3(8f, 0.4f, 4f), ESPhysicsLayers.Wall);

            Transform area21 = layout.Require("21-lock-camera").transform;
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f;
                CreateCube(area21, "PLANNED_锁敌目标_" + (i + 1), new Vector3(Mathf.Cos(angle) * 4f, 1f, Mathf.Sin(angle) * 4f), new Vector3(0.8f, 2f, 0.8f), ESPhysicsLayers.WorldDynamic);
            }

            Transform area24 = layout.Require("24-pool-control").transform;
            for (int i = -1; i <= 1; i++)
            {
                Transform slot = new GameObject("PLANNED_生命周期槽位_" + (i + 2)).transform;
                slot.SetParent(area24, false);
                slot.localPosition = new Vector3(i * 3.5f, 0f, 1f);
                CreateVisualCube(slot, "槽位底板", new Vector3(0f, 0.08f, 0f), new Vector3(2.5f, 0.12f, 2.5f));
                CreateVisualCube(slot, "实体占位", new Vector3(0f, 1f, 0f), new Vector3(0.8f, 2f, 0.8f));
            }
        }

        private static Color GetAreaColor(ESDemoTestAreaStatus status)
        {
            switch (status)
            {
                case ESDemoTestAreaStatus.Planned:
                    return new Color(1f, 0.72f, 0.18f, 1f);
                case ESDemoTestAreaStatus.Blocked:
                    return new Color(1f, 0.24f, 0.22f, 1f);
                default:
                    return new Color(0.28f, 0.82f, 1f, 1f);
            }
        }

        private static Entity CreatePlayer(GameObject prefab, Scene scene, Transform root)
        {
            GameObject playerObject = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (playerObject == null)
                throw new InvalidOperationException("实例化正式玩家失败：" + PlayerPrefabPath);

            playerObject.name = "Player_大黑塔";
            playerObject.transform.SetParent(root, true);
            playerObject.transform.SetPositionAndRotation(new Vector3(-24f, 0.02f, -2f), Quaternion.identity);

            Entity player = playerObject.GetComponent<Entity>();
            EntityCharacterIdentity profile = playerObject.GetComponent<EntityCharacterIdentity>();
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
                CreateVehicle(carPrefab, scene, vehicles, "Car", new Vector3(-11f, 0.1f, 89f), Quaternion.Euler(0f, 90f, 0f)),
                CreateVehicle(bicyclePrefab, scene, vehicles, "Bicycle", new Vector3(-5f, 0.1f, 89f), Quaternion.Euler(0f, 90f, 0f)),
                CreateVehicle(helicopterPrefab, scene, vehicles, "Helicopter", new Vector3(8f, 0.1f, 90f), Quaternion.Euler(0f, -90f, 0f)),
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

        private static GameObject CreateVisualCube(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            GameObject cube = CreateCube(parent, name, position, scale, 0);
            Collider collider = cube.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);
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

        private sealed class PlayerControllerTestAreaSpec
        {
            public readonly int number;
            public readonly string id;
            public readonly string title;
            public readonly string category;
            public readonly ESDemoTestAreaStatus status;
            public readonly Vector3 position;
            public readonly string objective;
            public readonly string expectedResult;
            public readonly string failureHint;
            public readonly ESInputActionId[] inputActions;

            public PlayerControllerTestAreaSpec(
                int number,
                string id,
                string title,
                string category,
                ESDemoTestAreaStatus status,
                Vector3 position,
                string objective,
                string expectedResult,
                string failureHint,
                ESInputActionId[] inputActions)
            {
                this.number = number;
                this.id = id;
                this.title = title;
                this.category = category;
                this.status = status;
                this.position = position;
                this.objective = objective;
                this.expectedResult = expectedResult;
                this.failureHint = failureHint;
                this.inputActions = inputActions ?? Array.Empty<ESInputActionId>();
            }
        }

        private sealed class PlayerControllerTestAreaLayout
        {
            private readonly Dictionary<string, ESDemoTestAreaMarker> markerById;

            public IReadOnlyList<PlayerControllerTestAreaSpec> Specs { get; }

            public PlayerControllerTestAreaLayout(
                IReadOnlyList<PlayerControllerTestAreaSpec> specs,
                Dictionary<string, ESDemoTestAreaMarker> markers)
            {
                Specs = specs ?? throw new ArgumentNullException(nameof(specs));
                markerById = markers ?? throw new ArgumentNullException(nameof(markers));
            }

            public ESDemoTestAreaMarker Require(string id)
            {
                if (!string.IsNullOrEmpty(id) && markerById.TryGetValue(id, out ESDemoTestAreaMarker marker) && marker != null)
                    return marker;

                throw new InvalidOperationException("测试场景缺少区域标识：" + id);
            }
        }
    }
}
