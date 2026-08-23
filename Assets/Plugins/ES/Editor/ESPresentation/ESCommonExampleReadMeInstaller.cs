using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES.EditorInternal
{
    /// <summary>
    /// 为 ES 常见案例补齐 Inspector 内可见的 <see cref="ESReadMeNote"/>。
    ///
    /// 这是明确触发的案例维护命令：不在初始化、窗口打开或 OnGUI 中修改场景、Prefab 或资产。
    /// 每次执行只更新下面列出的稳定案例入口，并且不会触及用户当前已打开的场景。
    /// </summary>
    internal static class ESCommonExampleReadMeInstaller
    {
        private const string MenuPath = "【ES】/自动化与开发/文档与示例/编辑器案例/接入或更新常见案例 ReadMe";
        private const string ReadMeRootPrefix = "【ES】ReadMe · ";

        private const string SimpleToolsScenePath =
            "Assets/Plugins/ES/3_Examples/1_Runtime/Example_SimpleTools/New Scene.unity";

        private const string RuntimeWatchScenePath =
            "Assets/Plugins/ES/3_Examples/1_Runtime/Example_SimpleTools/New Scene 1.unity";

        private const string ItemMotionScenePath =
            "Assets/Plugins/ES/3_Examples/1_Runtime/Example_ItemMotion/ES_Item_Shot_Template_Demo.unity";

        private const string AssetFlowScenePath =
            "Assets/Plugins/ES/3_Examples/1_Runtime/Example_AssetFlowHotUpdate/ESAssetGameCoreFlowHotUpdateTest.unity";

        private const string ResourcePrefabPath =
            "Assets/Plugins/ES/3_Examples/1_Runtime/Example_Res/GameObject (3).prefab";

        private const string EditorExtensionFolder =
            "Assets/Plugins/ES/3_Examples/2_Editor/Example_EditorTools";

        private const string EditorExtensionScenePath =
            EditorExtensionFolder + "/ES_EditorExtension_Demo.unity";

        [MenuItem(MenuPath, false, 42)]
        private static void InstallOrUpdateCommonExampleReadMes()
        {
            const string message =
                "将为下列常见案例创建或更新专用 ESReadMeNote：\n\n"
                + "• SimpleTools 基础场景\n"
                + "• RuntimeWatch 场景\n"
                + "• ItemMotion 场景\n"
                + "• Asset + GameCore 热更新场景\n"
                + "• 资源引用 Prefab\n"
                + "• ES 编辑器扩展案例场景（Section / 双目录 / 多态引用 / 边界测试）\n\n"
                + "此操作只在你点击后写入这些指定资产；不会修改当前打开的场景，也不会在编译或打开窗口时自动执行。";

            if (!EditorUtility.DisplayDialog("接入常见案例 ReadMe", message, "接入 / 更新", "取消"))
                return;

            var changedPaths = new List<string>(6);
            try
            {
                UpdateSceneReadMe(
                    SimpleToolsScenePath,
                    "SimpleTools 基础入口",
                    new ReadMeContent(
                        "ES SimpleTools 基础场景",
                        "简单工具集的空白、低风险入口；先从菜单打开工具，再按任务选择范围并手动执行。",
                        "使用步骤：\n"
                        + "1. 打开本场景，确认当前项目没有需要保留的临时测试对象。\n"
                        + "2. 从【ES】/自动化与开发/编辑器扩展/打开简单工具集。\n"
                        + "3. 先确认范围、规则和预览，再点击页面唯一的主操作。\n"
                        + "4. 需要写入、清理、迁移或重建时，阅读风险说明并在结果区核对反馈。\n\n"
                        + "这个场景不承载自动扫描任务；工具打开、切页和重绘不会自动改动项目资产。",
                        new[]
                        {
                            "本场景仅作为 SimpleTools 的低干扰入口；实际工具范围由工具页内明确选择。",
                            "主相机和方向光可保留用于需要 SceneView/PlayMode 的小型验证。",
                        },
                        new[]
                        {
                            "不要把项目级清理、发布或迁移绑定到打开本场景。",
                            "若工具显示风险或缺失前置条件，应先在提示指定的位置修复。",
                        },
                        "ES Editor / SimpleTools"),
                    changedPaths);

                UpdateSceneReadMe(
                    RuntimeWatchScenePath,
                    "RuntimeWatch",
                    new ReadMeContent(
                        "ES RuntimeWatch 运行时观察案例",
                        "在 Play Mode 中观察带有 ESRuntimeWatch 标记的对象；数据采集只在 RuntimeWatch 前台当前页时发生。",
                        "使用步骤：\n"
                        + "1. 打开本场景并进入 Play Mode。\n"
                        + "2. 从【ES】/验证与诊断/运行时监视/RuntimeWatch/打开运行时观察打开观察页。\n"
                        + "3. 选择目标或分类；重新回到前台时会请求一次刷新，自动刷新仍受开关控制。\n"
                        + "4. 观察“分类 → 对象 → 条目”层级；需要留证时再开始录制。\n\n"
                        + "离开 RuntimeWatch、切换其他工具页或让 SimpleTools 失焦时，自动采集会暂停；录制只在用户明确开始后持续采样。",
                        new[]
                        {
                            "保留 RW_* 示例对象及其 RuntimeWatch 观察字段，才能看到完整分类、对象和操作案例。",
                            "运行时数据仅供观察和测试；不要将临时调试值当作正式配置来源。",
                        },
                        new[]
                        {
                            "慢 Getter 或读取异常会标记为状态，不应在高频自动刷新中执行昂贵业务逻辑。",
                            "需要性能测试时，关闭自动刷新或切离 RuntimeWatch 即可停止普通采集。",
                        },
                        "ES Editor / RuntimeWatch"),
                    changedPaths,
                    ConfigureRuntimeWatchObjectReadMes);

                UpdateSceneReadMe(
                    ItemMotionScenePath,
                    "ItemMotion",
                    new ReadMeContent(
                        "ES ItemMotion 发射物模板案例",
                        "用于验证道具/发射物生成、命中目标与可选场景阻挡的最小可运行场景。",
                        "使用步骤：\n"
                        + "1. 打开场景，选择根对象“ES_Item_Shot_通用模板_场景样例”。\n"
                        + "2. 在 Inspector 中检查发射物来源、目标和启动策略。\n"
                        + "3. 进入 Play Mode 验证正常命中。\n"
                        + "4. 将“Wall_可选阻挡”移动到弹道中，验证阻挡分支。\n\n"
                        + "本案例用于说明运行时行为链路；不要把测试对象或临时资源路径直接复制进正式关卡。",
                        new[]
                        {
                            "ES_Item_Shot_通用模板_场景样例：案例启动与配置入口。",
                            "Shot_Target_必中目标：正常命中验证目标。",
                            "Ground_场景阻挡_地面：基础场景碰撞。",
                        },
                        new[]
                        {
                            "Wall_可选阻挡默认是可移动的测试条件，不是正式关卡布景。",
                            "运行前若缺失 Item、目标或资源引用，请先在根对象的配置中修复。",
                        },
                        "ES Runtime / ItemMotion"),
                    changedPaths,
                    ConfigureItemMotionObjectReadMes);

                UpdateSceneReadMe(
                    AssetFlowScenePath,
                    "AssetFlow",
                    new ReadMeContent(
                        "ES Asset + GameCore 热更新流程案例",
                        "验证 Asset 流程和 GameCore 配置在明确初始化后被消费的测试入口。",
                        "使用步骤：\n"
                        + "1. 先完成本项目资源与配置的必要构建/准备。\n"
                        + "2. 打开场景，选择“ES Asset + GameCore Flow Test”根对象。\n"
                        + "3. 根据测试脚本选择 testKey，并决定是否在启动时自动运行。\n"
                        + "4. 进入 Play Mode 后在 Console 和运行时面板核对结果。\n\n"
                        + "资源构建、发布和扫描属于显式工作流；打开本场景本身不会执行这些高影响操作。",
                        new[]
                        {
                            "ES Asset + GameCore Flow Test：测试控制器和可见入口。",
                            "测试所需资源、包配置和 GameCore 配置必须与当前分支一致。",
                        },
                        new[]
                        {
                            "热更新或资源清理前请先确认目标环境，避免误操作本地开发资产。",
                            "测试失败时先检查缺失资源和配置键，而不是反复重建全部资产。",
                        },
                        "ES Resource Delivery / GameCore"),
                    changedPaths,
                    ConfigureAssetFlowObjectReadMe);

                UpdatePrefabReadMe(
                    ResourcePrefabPath,
                    new ReadMeContent(
                        "ES 资源引用 Prefab 案例",
                        "用于观察资源引用、依赖关系与缺失引用诊断的最小 Prefab 入口。",
                        "使用步骤：\n"
                        + "1. 在 Project 中选中本 Prefab。\n"
                        + "2. 在 Inspector 中检查 SampleSO 与 refer 等引用字段。\n"
                        + "3. 更换或清空引用后，使用资源工具的显式检查/收集动作验证结果。\n\n"
                        + "本 ReadMe 仅保存说明文字，不参与运行时逻辑，也不会在选中 Prefab 时自动扫描资源。",
                        new[]
                        {
                            "保留 SampleSO 和 refer 字段，用于验证对象引用与依赖关系。",
                        },
                        new[]
                        {
                            "不要把该 Prefab 当作正式运行时配置；它是资源链路的测试样本。",
                            "资源扫描、写入和发布必须通过工具中的明确操作触发。",
                        },
                        "ES Resource / Example_Res"),
                    changedPaths);

                UpdateEditorExtensionDemo(changedPaths);
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "接入常见案例 ReadMe 失败",
                    "已停止后续写入。请查看 Console 中的异常和已处理的资产路径。",
                    "知道了");
                return;
            }

            string result = "已接入或更新 " + changedPaths.Count + " 个常见案例 ReadMe：\n\n- "
                            + string.Join("\n- ", changedPaths);
            Debug.Log("[ES] " + result);
            EditorUtility.DisplayDialog("常见案例 ReadMe 已接入", result, "知道了");
        }

        private static void UpdateSceneReadMe(
            string scenePath,
            string rootLabel,
            ReadMeContent content,
            List<string> changedPaths,
            Action<Scene> configureCaseObjects = null)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                throw new FileNotFoundException("找不到常见案例场景。", scenePath);

            Scene loadedScene = SceneManager.GetSceneByPath(scenePath);
            bool openedByTool = !loadedScene.IsValid() || !loadedScene.isLoaded;
            Scene scene = openedByTool
                ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive)
                : loadedScene;

            try
            {
                GameObject root = FindOrCreateSceneReadMeRoot(scene, rootLabel);
                ApplyContent(EnsureReadMe(root), content);
                configureCaseObjects?.Invoke(scene);
                EditorSceneManager.MarkSceneDirty(scene);

                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("无法保存案例场景：" + scenePath);

                changedPaths.Add(scenePath);
            }
            finally
            {
                if (openedByTool && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void UpdatePrefabReadMe(
            string prefabPath,
            ReadMeContent content,
            List<string> changedPaths)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                throw new FileNotFoundException("找不到常见案例 Prefab。", prefabPath);

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ApplyContent(EnsureReadMe(prefabRoot), content);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                changedPaths.Add(prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void UpdateEditorExtensionDemo(List<string> changedPaths)
        {
            bool sceneExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(EditorExtensionScenePath) != null;
            if (!AssetDatabase.IsValidFolder(EditorExtensionFolder))
                throw new DirectoryNotFoundException("找不到编辑器案例目录：" + EditorExtensionFolder);

            Scene loadedScene = SceneManager.GetSceneByPath(EditorExtensionScenePath);
            bool openedByTool = !loadedScene.IsValid() || !loadedScene.isLoaded;
            Scene scene;
            if (sceneExists)
            {
                scene = openedByTool
                    ? EditorSceneManager.OpenScene(EditorExtensionScenePath, OpenSceneMode.Additive)
                    : loadedScene;
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                openedByTool = true;
            }

            try
            {
                GameObject root = FindOrCreateSceneReadMeRoot(scene, "编辑器扩展案例");
                ApplyContent(
                    EnsureReadMe(root),
                    new ReadMeContent(
                        "ES 编辑器扩展案例",
                        "集中验证 ESEditorSection、双配置目录、多态引用与多目标边界的可见 Inspector 入口。",
                        "使用步骤：\n"
                        + "1. 打开本场景，在 Hierarchy 中依次选中三个案例对象。\n"
                        + "2. “配置目录案例”验证 Begin / Continue / End、目录外字段和普通 Odin 分组共存。\n"
                        + "3. “双配置目录案例”验证同一宿主上的两个独立目录。\n"
                        + "4. “多态引用案例”验证类型选择、清除、嵌套、集合和缺失状态。\n"
                        + "5. “多目标边界案例”验证 2～10 个对象编辑、11 个对象保护、空值和深层嵌套。\n\n"
                        + "这些组件只用于编辑器展示验证；打开 Inspector 不会扫描资产、重建类型目录或修改场景数据。",
                        new[]
                        {
                            "四个 ES 编辑器扩展案例对象及其对应案例组件。",
                            "项目中已启用 Odin Inspector，才能看到完整的属性绘制效果。",
                        },
                        new[]
                        {
                            "不要把案例字段误当作正式角色或技能数据。",
                            "多态引用的类型切换会修改当前字段；测试前可先复制对象或使用 Undo。",
                        },
                        "ES Editor / Presentation Suite"));

                EnsureCaseObject<ESEditorSectionNavigatorCase>(
                    scene,
                    "01 · 配置目录案例",
                    new ReadMeContent(
                        "ESEditorSection 配置目录案例",
                        "验证密集信息在 Inspector 中按业务目录、标题、分隔和折叠层级被组织。",
                        "选择本对象后，优先从“核心配置”开始，依次查看身体能力、控制来源、状态表现、资源引用和诊断。\n\n"
                        + "无参数 [ESEditorSection] 会延续上一个分区；Begin/End 用于连续字段，不会把目录外字段强行塞进分区。",
                        new[] { "ESEditorSectionNavigatorCase 组件。" },
                        new[] { "这是排版案例，不应替代真实业务数据资产。" },
                        "ES Editor / ESEditorSection"));

                EnsureCaseObject<ESEditorSectionDualNavigatorCase>(
                    scene,
                    "02 · 双配置目录案例",
                    new ReadMeContent(
                        "ESEditorSection 双配置目录案例",
                        "验证同一宿主内的作者配置与运行时配置可以保留各自独立的业务目录。",
                        "选择本对象后，在 Inspector 中分别操作作者配置与运行时配置目录；目录外字段和普通 Odin Foldout 应保持独立。",
                        new[] { "ESEditorSectionDualNavigatorCase 组件。" },
                        new[] { "两个目录用于验证隔离能力，不代表必须在所有页面拆成双目录。" },
                        "ES Editor / ESEditorSection"));

                EnsureCaseObject<ESPolymorphicReferenceCase>(
                    scene,
                    "03 · 多态引用案例",
                    new ReadMeContent(
                        "ESPolymorphicReference 多态引用案例",
                        "验证 SerializeReference 的类型选择、嵌套层次、集合元素、清除和可恢复的重选体验。",
                        "选择本对象后，先查看单体引用，再展开嵌套引用和效果序列。\n\n"
                        + "类型切换、清除和集合修改都是有状态操作；请用 Undo 回退测试，或重打开场景恢复示例初始值。",
                        new[] { "ESPolymorphicReferenceCase 组件及其 SerializeReference 字段。" },
                        new[] { "更换类型会替换当前字段实例；不要在未确认内容时覆盖真实配置。" },
                        "ES Editor / ESPolymorphicReference"));

                EnsureCaseObject<ESPresentationBoundaryCase>(
                    scene,
                    "04 · 多目标边界案例",
                    new ReadMeContent(
                        "ES 多目标与嵌套边界案例",
                        "验证多目标编辑的 10 对象上限、类型不一致、空槽和深层 SerializeReference 嵌套。",
                        "使用步骤：\n"
                        + "1. 先选择单个对象，确认基础多态和集合绘制。\n"
                        + "2. 在 Hierarchy 中多选 2～10 个同类案例对象，观察共同字段编辑。\n"
                        + "3. 使用【ES】/验证与诊断/测试与验收/编辑器扩展/ES 编辑器扩展中的多目标边界测试命令创建 11 个独立对象，验证上限保护。\n\n"
                        + "超过 10 个目标不是普通编辑场景；ES 应明确提示并拒绝高风险批量写入，而不是悄悄产生不一致结果。",
                        new[] { "ESPresentationBoundaryCase 组件。" },
                        new[]
                        {
                            "多目标编辑会同时修改选中的所有对象；测试前确认 Hierarchy 的实际选中范围。",
                            "11 个对象边界层级由专用菜单显式创建，不会在打开本场景时自动生成。",
                        },
                        "ES Editor / Presentation Boundary"));

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, EditorExtensionScenePath))
                    throw new InvalidOperationException("无法保存编辑器扩展案例场景：" + EditorExtensionScenePath);

                changedPaths.Add(EditorExtensionScenePath);
            }
            finally
            {
                if (openedByTool && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static GameObject FindOrCreateSceneReadMeRoot(Scene scene, string rootLabel)
        {
            string rootName = ReadMeRootPrefix + rootLabel;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root != null && string.Equals(root.name, rootName, StringComparison.Ordinal))
                    return root;
            }

            GameObject created = new GameObject(rootName);
            SceneManager.MoveGameObjectToScene(created, scene);
            return created;
        }

        private static T EnsureCaseObject<T>(Scene scene, string objectName, ReadMeContent content)
            where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            GameObject target = null;
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && string.Equals(roots[i].name, objectName, StringComparison.Ordinal))
                {
                    target = roots[i];
                    break;
                }
            }

            if (target == null)
            {
                target = new GameObject(objectName);
                SceneManager.MoveGameObjectToScene(target, scene);
            }

            T component = target.GetComponent<T>();
            if (component == null)
                component = target.AddComponent<T>();

            ApplyContent(EnsureReadMe(target), content);
            return component;
        }

        private static ESReadMeNote EnsureReadMe(GameObject host)
        {
            ESReadMeNote readMe = host.GetComponent<ESReadMeNote>();
            return readMe != null ? readMe : host.AddComponent<ESReadMeNote>();
        }

        /// <summary>
        /// RuntimeWatch 场景的四个 RW_* 对象分别代表一种观察能力。
        /// ReadMe 必须跟着对象走，而不是只藏在场景根，用户选中任一对象即可知道它的测试边界。
        /// </summary>
        private static void ConfigureRuntimeWatchObjectReadMes(Scene scene)
        {
            ApplyObjectReadMe(
                scene,
                "RW_01_基础类型",
                new ReadMeContent(
                    "RuntimeWatch · 基础类型",
                    "验证 bool、string、int、float 等基础值在运行时观察器中的采样、格式化与变化显示。",
                    "职责：提供最小且稳定的基础值样本。进入 Play Mode 后，在 RuntimeWatch 中确认数值更新、状态文本和空值展示。\n\n"
                    + "不要把高开销查询塞进这类高频样本；每个 Getter 都应只读取已存在的数据。",
                    new[] { "本对象上的 RuntimeWatchCase_1_BasicTypes 测试组件。" },
                    new[] { "这是采样展示对象，不是正式角色配置。" },
                    "ES Editor / RuntimeWatch"));

            ApplyObjectReadMe(
                scene,
                "RW_02_方法调用",
                new ReadMeContent(
                    "RuntimeWatch · 方法调用",
                    "验证 RuntimeWatch 对无副作用读取方法、操作入口和结果状态的展示。",
                    "职责：区分“读取信息”和“明确执行操作”。先观察返回结果，再手动触发允许的测试操作。\n\n"
                    + "观察 Getter 不应写入场景或资产；会改变状态的测试必须由按钮明确触发。",
                    new[] { "本对象上的 RuntimeWatchCase_2_Methods 测试组件。" },
                    new[] { "不要把真实发布、清理或资源写入操作注册为自动刷新 Getter。" },
                    "ES Editor / RuntimeWatch"));

            ApplyObjectReadMe(
                scene,
                "RW_03_筛选嵌套",
                new ReadMeContent(
                    "RuntimeWatch · 筛选与嵌套",
                    "验证分类筛选、嵌套对象和条目层级在运行时观察器中的稳定排序。",
                    "职责：提供有层次的数据样本。使用 RuntimeWatch 的分类和搜索筛选确认“分类 → 对象 → 条目”的阅读顺序。\n\n"
                    + "嵌套仅用于验证层级表现；不要把无关大对象图挂入自动刷新路径。",
                    new[] { "本对象上的 RuntimeWatchCase_3_FilterAndNested 测试组件。" },
                    new[] { "层级过深或对象过多时，应缩小观察范围而不是盲目提升刷新频率。" },
                    "ES Editor / RuntimeWatch"));

            ApplyObjectReadMe(
                scene,
                "RW_04_Unity类型",
                new ReadMeContent(
                    "RuntimeWatch · Unity 类型",
                    "验证 GameObject、Component、Transform 等 Unity 对象引用的显示、定位和空引用语义。",
                    "职责：提供 UnityEngine.Object 引用样本。进入 Play Mode 后检查对象名称、引用存在性和定位操作是否与实际场景一致。\n\n"
                    + "丢失引用应显示可理解的状态；不要在 Getter 内使用全场景 Find 作为隐式修复。",
                    new[] { "本对象上的 RuntimeWatchCase_4_UnityTypes 测试组件。" },
                    new[] { "移动或删除被引用对象后，应以“缺失引用”验证诊断，而不是保留错误的旧缓存。" },
                    "ES Editor / RuntimeWatch"));
        }

        private static void ConfigureItemMotionObjectReadMes(Scene scene)
        {
            ApplyObjectReadMe(
                scene,
                "ES_Item_Shot_通用模板_场景样例",
                new ReadMeContent(
                    "ItemMotion · 发射流程入口",
                    "控制本案例的发射物来源、启动策略和验证流程，是 ItemMotion 行为链路的主入口。",
                    "职责：在进入 Play Mode 后创建或驱动发射物测试。先检查 Item、目标和启动参数，再开始运行。\n\n"
                    + "这里是案例控制器，不应直接承载目标、阻挡或地面碰撞职责。",
                    new[] { "ItemMotion 案例控制组件及其必要资源引用。" },
                    new[] { "缺少 Item 或目标引用时，先修复本对象配置再运行。" },
                    "ES Runtime / ItemMotion"));

            ApplyObjectReadMe(
                scene,
                "Shot_Target_必中目标",
                new ReadMeContent(
                    "ItemMotion · 命中目标",
                    "发射物正常路径的确定命中目标，用于验证命中回调、伤害或结束条件。",
                    "职责：提供稳定的命中对象。保持它处于默认弹道上，用于确认无阻挡时的基准结果。",
                    new[] { "目标对象的 Collider / 命中相关组件。" },
                    new[] { "移动目标会改变基准结果；记录测试前先确认其位置。" },
                    "ES Runtime / ItemMotion"));

            ApplyObjectReadMe(
                scene,
                "Ground_场景阻挡_地面",
                new ReadMeContent(
                    "ItemMotion · 基础地面",
                    "提供稳定的场景接触面，用于验证发射物与普通场景碰撞的基础分支。",
                    "职责：作为默认地面碰撞。它与可选墙体共同区分“落地/接触”和“中途阻挡”测试。",
                    new[] { "地面 Collider。" },
                    new[] { "不要把地面改成动态测试障碍；额外阻挡请使用专用墙体对象。" },
                    "ES Runtime / ItemMotion"));

            ApplyObjectReadMe(
                scene,
                "Wall_可选阻挡_移动到弹道中测试",
                new ReadMeContent(
                    "ItemMotion · 可选弹道阻挡",
                    "用于手动插入弹道，验证发射物被环境提前阻挡的分支。",
                    "职责：默认不干扰基准命中；需要测试阻挡时，手动移动到发射物和目标之间。\n\n"
                    + "这是条件对象，改变它的位置就是改变当前测试条件。",
                    new[] { "墙体 Collider。" },
                    new[] { "完成阻挡测试后请移回默认位置，避免污染后续基准测试。" },
                    "ES Runtime / ItemMotion"));
        }

        private static void ConfigureAssetFlowObjectReadMe(Scene scene)
        {
            ApplyObjectReadMe(
                scene,
                "ES Asset + GameCore Flow Test",
                new ReadMeContent(
                    "AssetFlow · 测试控制器",
                    "热更新资源和 GameCore 配置消费链路的唯一场景测试入口。",
                    "职责：读取当前 testKey 和运行策略，在明确启动后执行流程测试并输出结果。\n\n"
                    + "它不负责构建、发布或全量扫描；这些高影响操作必须从对应工具中单独触发。",
                    new[] { "AssetFlow 测试控制器、有效 testKey 及对应资源/配置。" },
                    new[] { "执行前确认目标环境，避免把本地测试配置当作发布配置。" },
                    "ES Resource Delivery / GameCore"));
        }

        private static void ApplyObjectReadMe(Scene scene, string objectName, ReadMeContent content)
        {
            GameObject target = FindSceneObject(scene, objectName);
            if (target == null)
            {
                Debug.LogWarning(
                    "[ES] 常见案例 ReadMe 未找到预期对象："
                    + objectName + "（场景：" + scene.path + "）。未创建替代对象，避免掩盖场景结构变化。");
                return;
            }

            ApplyContent(EnsureReadMe(target), content);
        }

        private static GameObject FindSceneObject(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                    continue;

                Transform[] hierarchy = root.GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < hierarchy.Length; j++)
                {
                    Transform candidate = hierarchy[j];
                    if (candidate != null && string.Equals(candidate.name, objectName, StringComparison.Ordinal))
                        return candidate.gameObject;
                }
            }

            return null;
        }

        private static void ApplyContent(ESReadMeNote readMe, ReadMeContent content)
        {
            readMe.title = content.Title;
            readMe.summary = content.Summary;
            readMe.readMe = content.ReadMe;
            if (readMe.requiredItems == null)
                readMe.requiredItems = new List<string>();
            if (readMe.notes == null)
                readMe.notes = new List<string>();
            CopyList(readMe.requiredItems, content.RequiredItems);
            CopyList(readMe.notes, content.Notes);
            readMe.ownerSystem = content.OwnerSystem;
            readMe.lastUpdated = "2026-08-02";
            EditorUtility.SetDirty(readMe);
        }

        private static void CopyList(List<string> destination, string[] source)
        {
            if (destination == null)
                return;

            destination.Clear();
            if (source == null)
                return;

            destination.AddRange(source);
        }

        private sealed class ReadMeContent
        {
            public readonly string Title;
            public readonly string Summary;
            public readonly string ReadMe;
            public readonly string[] RequiredItems;
            public readonly string[] Notes;
            public readonly string OwnerSystem;

            public ReadMeContent(
                string title,
                string summary,
                string readMe,
                string[] requiredItems,
                string[] notes,
                string ownerSystem)
            {
                Title = title;
                Summary = summary;
                ReadMe = readMe;
                RequiredItems = requiredItems;
                Notes = notes;
                OwnerSystem = ownerSystem;
            }
        }
    }
}
