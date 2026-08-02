using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES.EditorInternal
{
    /// <summary>
    /// One-click scene fixture for ESPresentationBoundaryCase.
    /// Each case is an independent child object so Hierarchy multi-selection can be used
    /// directly for the 2-10 and over-10 target checks.
    /// </summary>
    internal static class ESPresentationBoundaryMenu
    {
        private const string RootName = "ES 边界测试层级";
        private const int MultiTargetCaseCount = 11;

        private const string CreateMenuPath
            = MenuItemPathDefine.TEST_TOOLS_PATH + "ES 编辑器扩展/创建多态边界测试层级";
        private const string SelectMenuPath
            = MenuItemPathDefine.TEST_TOOLS_PATH + "ES 编辑器扩展/定位多态边界测试层级";
        private const string SelectMixedPairMenuPath
            = MenuItemPathDefine.TEST_TOOLS_PATH + "ES 编辑器扩展/选择 2 个不一致对象";
        private const string SelectAllMenuPath
            = MenuItemPathDefine.TEST_TOOLS_PATH + "ES 编辑器扩展/选择全部多目标对象（11 个）";
        private const string SelectTenMenuPath
            = MenuItemPathDefine.TEST_TOOLS_PATH + "ES 编辑器扩展/选择 10 个多目标对象";
        private const string ClearMenuPath
            = MenuItemPathDefine.TEST_TOOLS_PATH + "ES 编辑器扩展/删除多态边界测试层级";

        [MenuItem(CreateMenuPath, false, 40)]
        private static void CreateBoundaryHierarchy()
        {
            if (!EnsureCanEditScene())
                return;

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("无法创建测试层级", "当前没有有效的活动场景。", "知道了");
                return;
            }

            GameObject existing = FindRoot(scene);
            if (existing != null
                && !EditorUtility.DisplayDialog(
                    "重建 ES 边界测试层级",
                    "当前场景已经存在测试层级。重建会通过 Undo 删除旧层级并创建新的 12 个独立测试对象。",
                    "重建",
                    "取消"))
                return;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("创建 ES 边界测试层级");

            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "创建 ES 边界测试层级");
            SceneManager.MoveGameObjectToScene(root, scene);
            ApplyReadMe(
                root,
                "ES 多目标边界测试层级",
                "承载 1 个基准对象和 11 个独立对象，用于验证多目标编辑上限、类型不一致、空值与深层嵌套。",
                "职责：这是边界测试的组织根，不是业务对象。\n\n"
                + "先选择 2～10 个子对象验证共同编辑；再选择全部 11 个子对象，确认 ES 明确进入保护而不是静默批量写入。\n\n"
                + "本层级只由【ES】/示例与测试/编辑器案例/ES 编辑器扩展中的专用菜单创建或删除。",
                "ES Editor / Presentation Boundary");

            CreateCaseObject(root.transform, "01 基准案例", false, false);
            for (int i = 0; i < MultiTargetCaseCount; i++)
                CreateCaseObject(
                    root.transform,
                    "多目标 " + (i + 1).ToString("00"),
                    i == MultiTargetCaseCount - 1,
                    i == MultiTargetCaseCount - 1);

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log(
                "[ES] 已创建边界测试层级：" + RootName
                + "。包含 1 个基准对象和 " + MultiTargetCaseCount
                + " 个独立多目标对象；可直接在 Hierarchy 中多选验证 10 个上限。",
                root);
        }

        [MenuItem(CreateMenuPath, true)]
        private static bool ValidateCreateBoundaryHierarchy()
        {
            return CanEditScene();
        }

        [MenuItem(SelectMenuPath, false, 50)]
        private static void SelectBoundaryHierarchy()
        {
            GameObject root = FindRoot(SceneManager.GetActiveScene());
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "没有找到测试层级",
                    "请先执行“创建多态边界测试层级”。",
                    "知道了");
                return;
            }

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
        }

        [MenuItem(SelectAllMenuPath, false, 55)]
        private static void SelectAllBoundaryTargets()
        {
            SelectBoundaryTargets(MultiTargetCaseCount);
        }

        [MenuItem(SelectTenMenuPath, false, 54)]
        private static void SelectTenBoundaryTargets()
        {
            SelectBoundaryTargets(10);
        }

        [MenuItem(SelectMixedPairMenuPath, false, 53)]
        private static void SelectMixedBoundaryTargets()
        {
            GameObject root = FindRoot(SceneManager.GetActiveScene());
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "没有找到测试层级",
                    "请先执行“创建多态边界测试层级”。",
                    "知道了");
                return;
            }

            // Child 0 is the baseline. The first and final multi-target children are designed
            // to differ in both concrete type and null state.
            Transform first = root.transform.childCount > 1 ? root.transform.GetChild(1) : null;
            Transform last = root.transform.childCount > MultiTargetCaseCount
                ? root.transform.GetChild(MultiTargetCaseCount)
                : null;
            if (first == null || last == null)
                return;

            Selection.objects = new Object[] { first.gameObject, last.gameObject };
            EditorGUIUtility.PingObject(first.gameObject);
        }

        private static void SelectBoundaryTargets(int requestedCount)
        {
            GameObject root = FindRoot(SceneManager.GetActiveScene());
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "没有找到测试层级",
                    "请先执行“创建多态边界测试层级”。",
                    "知道了");
                return;
            }

            var targets = new List<Object>(requestedCount);
            int end = Mathf.Min(root.transform.childCount, requestedCount + 1);
            for (int i = 1; i < end; i++)
            {
                Transform child = root.transform.GetChild(i);
                if (child != null && child.GetComponent<ESPresentationBoundaryCase>() != null)
                    targets.Add(child.gameObject);
            }

            if (targets.Count == 0)
                return;

            Selection.objects = targets.ToArray();
            EditorGUIUtility.PingObject(targets[0]);
        }

        [MenuItem(SelectAllMenuPath, true)]
        [MenuItem(SelectTenMenuPath, true)]
        [MenuItem(SelectMixedPairMenuPath, true)]
        private static bool ValidateSelectAllBoundaryTargets()
        {
            return FindRoot(SceneManager.GetActiveScene()) != null;
        }

        [MenuItem(SelectMenuPath, true)]
        private static bool ValidateSelectBoundaryHierarchy()
        {
            return FindRoot(SceneManager.GetActiveScene()) != null;
        }

        [MenuItem(ClearMenuPath, false, 60)]
        private static void ClearBoundaryHierarchy()
        {
            if (!EnsureCanEditScene())
                return;

            Scene scene = SceneManager.GetActiveScene();
            GameObject root = FindRoot(scene);
            if (root == null)
                return;

            if (!EditorUtility.DisplayDialog(
                    "删除 ES 边界测试层级",
                    "将通过 Undo 删除“" + RootName + "”及其全部子对象。",
                    "删除",
                    "取消"))
                return;

            Undo.DestroyObjectImmediate(root);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        [MenuItem(ClearMenuPath, true)]
        private static bool ValidateClearBoundaryHierarchy()
        {
            return CanEditScene() && FindRoot(SceneManager.GetActiveScene()) != null;
        }

        private static void CreateCaseObject(
            Transform parent,
            string name,
            bool useDifferentSharedType,
            bool useEmptyMixedNode)
        {
            GameObject child = new GameObject("ES 边界 · " + name);
            child.transform.SetParent(parent, false);
            var boundaryCase = child.AddComponent<ESPresentationBoundaryCase>();
            if (useDifferentSharedType)
            {
                boundaryCase.sharedNode = new ESPresentationBoundaryCase.TextNode
                {
                    name = "用于复现多目标类型不一致"
                };
            }

            if (useEmptyMixedNode)
                boundaryCase.mixedNode = null;

            ApplyReadMe(
                child,
                "多目标边界对象 · " + name,
                useDifferentSharedType || useEmptyMixedNode
                    ? "故意包含类型或空值差异，用于验证多目标编辑的风险提示和保护语义。"
                    : "基准多态与嵌套对象，用于验证 2～10 个目标的正常共同编辑。",
                "职责：这是可独立选中的多目标测试样本。\n\n"
                + "多选时，本对象会与其他样本共同参与编辑；任何字段写入都会作用到实际选中的每个对象。\n\n"
                + (useDifferentSharedType
                    ? "本对象故意使用不同的 sharedNode 具体类型，应触发类型不一致状态。"
                    : useEmptyMixedNode
                        ? "本对象故意保留空 mixedNode，应触发空槽混合状态。"
                        : "本对象是正常基准样本。"),
                "ES Editor / Presentation Boundary");

            Undo.RegisterCreatedObjectUndo(child, "创建 ES 边界测试对象");
        }

        private static void ApplyReadMe(
            GameObject target,
            string title,
            string summary,
            string readMe,
            string ownerSystem)
        {
            ESReadMeNote note = target.GetComponent<ESReadMeNote>();
            if (note == null)
                note = target.AddComponent<ESReadMeNote>();

            note.title = title;
            note.summary = summary;
            note.readMe = readMe;
            note.requiredItems.Clear();
            note.requiredItems.Add("ESPresentationBoundaryCase 组件。");
            note.notes.Clear();
            note.notes.Add("测试前请确认 Hierarchy 的实际多选范围；多目标编辑会同时改写每个选中的对象。");
            note.ownerSystem = ownerSystem;
            note.lastUpdated = "2026-08-02";
        }

        private static GameObject FindRoot(Scene scene)
        {
            if (!scene.IsValid())
                return null;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && string.Equals(roots[i].name, RootName, System.StringComparison.Ordinal))
                    return roots[i];
            }

            return null;
        }

        private static bool CanEditScene()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static bool EnsureCanEditScene()
        {
            if (CanEditScene())
                return true;

            Debug.LogWarning("[ES] 播放模式中不能创建或删除边界测试层级。");
            return false;
        }
    }
}
