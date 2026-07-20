using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditor.Animations;


namespace ES
{

    #region 动画器批量设置工具
    [Serializable]
    public class Page_AnimationBatchSetting : ESWindowPageBase
    {
        [Serializable]
        public class SettingsData
        {
            public bool includeChildren;
            public bool addAnimatorIfMissing;
            public RuntimeAnimatorController animatorController;
            public AnimationClip defaultAnimationClip;
            public string animatorControllerGuid;
            public string animatorControllerPath;
            public string defaultAnimationClipGuid;
            public string defaultAnimationClipPath;
            public ControllerNullAction controllerNullAction;
            public ClipNullAction clipNullAction;
            public string assetGroupName;
            public bool enableApplySettings;
            public AnimatorUpdateMode updateMode;
            public AnimatorCullingMode cullingMode;
            public bool applyRootMotion;
            public string newClipName;
        }

        [Serializable]
        public class CreatedAnimationAssetRecord
        {
            [DisplayAsString, LabelText("类型")]
            public string assetType;

            [DisplayAsString, LabelText("路径")]
            public string assetPath;

            [DisplayAsString, LabelText("来源")]
            public string source;
        }
        #region 公共设置
        [Title("动画器批量设置工具", "批量设置Animator属性", bold: true, titleAlignment: TitleAlignments.Centered)]

        [DisplayAsString(fontSize: 13), HideLabel, GUIColor(0.72f, 0.86f, 0.86f)]
        public string readMe = "选择带有Animator的GameObject，\n设置动画属性，\n点击应用按钮批量修改";

        [ShowInInspector, ReadOnly, DisplayAsString, HideLabel, PropertyOrder(-10)]
        private string PanelSummary
        {
            get
            {
                int selectedCount = Selection.gameObjects != null ? Selection.gameObjects.Length : 0;
                var targets = SimpleToolsSafetyUtility.CollectTargets(Selection.gameObjects, includeChildren);
                int animatorCount = targets.Count(obj => obj != null && obj.GetComponent<Animator>() != null);
                int missingAnimatorCount = targets.Count - animatorCount;
                return $"当前选择: {selectedCount} 个对象 | 实际目标: {targets.Count} 个 | 已有 Animator: {animatorCount} | 可新增: {(addAnimatorIfMissing ? missingAnimatorCount : 0)}";
            }
        }

        [LabelText("包含子对象"), Space(5)]
        [PropertyTooltip("启用后，批量操作将递归应用到选中对象的子对象。")]
        public bool includeChildren = true;

        [LabelText("如果没有Animator则添加"), Space(5)]
        [PropertyTooltip("如果对象没有 Animator 组件，自动添加一个。")]
        public bool addAnimatorIfMissing = true;



        [LabelText("动画控制器"), AssetsOnly, Space(5)]
        [PropertyTooltip("指定要应用的 AnimatorController。如果为空，根据下方选项处理。")]
        public RuntimeAnimatorController animatorController;

        [LabelText("默认动画剪辑"), AssetsOnly, Space(5)]
        [PropertyTooltip("默认的 AnimationClip，用于创建新的 Controller。")]
        public AnimationClip defaultAnimationClip;

        public enum ControllerNullAction
        {
            [LabelText("忽略")]
            Ignore,
            [LabelText("创建新Controller（共用）")]
            CreateShared,
            [LabelText("创建新Controller（独立）")]
            CreateIndividual
        }

        [LabelText("Controller为空时"), Space(5)]
        [PropertyTooltip("当 AnimatorController 为空时，选择如何处理：忽略、创建共享或独立的新 Controller。")]
        public ControllerNullAction controllerNullAction = ControllerNullAction.CreateShared;

        public enum ClipNullAction
        {
            [LabelText("忽略")]
            Ignore,
            [LabelText("共享新AnimationClip")]
            CreateShared,
            [LabelText("独立的AnimationClip")]
            CreateIndividual
        }

        [LabelText("AnimationClip为空时"), Space(5)]
        [PropertyTooltip("当 AnimationClip 为空时，选择如何处理：忽略、创建共享或独立的 Clip。")]
        public ClipNullAction clipNullAction = ClipNullAction.CreateShared;

        [LabelText("资产分组"), Space(5)]
        [PropertyTooltip("新创建的资产将分组到此文件夹下，避免资源混乱。")]
        public string assetGroupName = "默认";

        [ShowInInspector, ReadOnly, LabelText("预览将应用的对象"), ListDrawerSettings(DraggableItems = false)]
        [PropertyTooltip("显示将要应用设置的对象列表（包括添加 Animator 的对象）。")]
        public List<string> previewObjects = new List<string>();

        [FoldoutGroup("资产创建记录"), ShowInInspector, ReadOnly, LabelText("最近创建"), ListDrawerSettings(DraggableItems = false, NumberOfItemsPerPage = 6)]
        public List<CreatedAnimationAssetRecord> createdAssetRecords = new List<CreatedAnimationAssetRecord>();

        private string lastResultSummary = "";
        private string lastResultDetail = "";

        [OnInspectorGUI]
        private void DrawResultPanel()
        {
            SimpleToolsPanelUtility.DrawResultSummary("最近 Animator 操作", lastResultSummary, lastResultDetail);
        }
        #endregion
        #region 辅助方法
        private string GetAnimationAssetFolder(string subFolder)
        {
            string root = ESGlobalEditorDefaultConfi.Instance?.Path_ResourceParent;
            if (string.IsNullOrWhiteSpace(root) || !SimpleToolsSafetyUtility.IsAssetPath(root))
                root = "Assets";

            string group = SanitizeAssetName(string.IsNullOrWhiteSpace(assetGroupName) ? "默认" : assetGroupName);
            return $"{SimpleToolsSafetyUtility.NormalizeAssetPath(root)}/{subFolder}/{group}";
        }

        private string SanitizeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "NewAsset";

            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');

            return value.Trim();
        }

        private AnimatorController CreateAnimatorControllerAsset(string baseName, string source)
        {
            string folder = GetAnimationAssetFolder("AnimationControllers");
            if (!SimpleToolsSafetyUtility.EnsureAssetFolder(folder, out var error))
            {
                EditorUtility.DisplayDialog("创建失败", error, "知道了");
                return null;
            }

            string path = SimpleToolsSafetyUtility.GetUniqueAssetPath($"{folder}/{SanitizeAssetName(baseName)}.controller");
            var controller = new AnimatorController();
            AssetDatabase.CreateAsset(controller, path);
            controller.name = Path.GetFileNameWithoutExtension(path);
            EnsureControllerHasBaseLayer(controller);
            AssetDatabase.SaveAssets();
            RecordCreatedAsset("AnimatorController", path, source);
            return controller;
        }

        private AnimationClip CreateAnimationClipAsset(string baseName, string source)
        {
            string folder = GetAnimationAssetFolder("Animations");
            if (!SimpleToolsSafetyUtility.EnsureAssetFolder(folder, out var error))
            {
                EditorUtility.DisplayDialog("创建失败", error, "知道了");
                return null;
            }

            string path = SimpleToolsSafetyUtility.GetUniqueAssetPath($"{folder}/{SanitizeAssetName(baseName)}.anim");
            var clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
            clip.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.SaveAssets();
            RecordCreatedAsset("AnimationClip", path, source);
            return clip;
        }

        private void EnsureControllerHasBaseLayer(AnimatorController controller)
        {
            if (controller != null && controller.layers.Length == 0)
                controller.AddLayer("Base Layer");
        }

        private void RecordCreatedAsset(string type, string path, string source)
        {
            createdAssetRecords.Add(new CreatedAnimationAssetRecord
            {
                assetType = type,
                assetPath = path,
                source = source
            });
        }

        private List<GameObject> GetSelectedTargets()
        {
            return SimpleToolsSafetyUtility.CollectTargets(Selection.gameObjects, includeChildren);
        }

        private bool ConfirmTargetOperation(string title, string action, List<GameObject> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                EditorUtility.DisplayDialog("没有可处理对象", "当前选区下没有可处理的 GameObject。", "知道了");
                return false;
            }

            string preview = SimpleToolsSafetyUtility.JoinPreview(targets.Select(obj => obj != null ? obj.name : "<丢失对象>"), 10);
            return EditorUtility.DisplayDialog(title,
                $"将{action} {targets.Count} 个对象。\n\n{preview}\n\n支持 Ctrl+Z 撤销。继续吗？",
                "开始处理", "取消");
        }
        #endregion

        #region 应用Animator设置
        [BoxGroup("应用Animator设置", ShowLabel = false)]
        [Title("应用Animator设置", titleAlignment: TitleAlignments.Centered, bold: true)]
        [ToggleLeft, LabelText("启用"), LabelWidth(120)]
        public bool enableApplySettings = false;

        [HorizontalGroup("应用Animator设置/ApplySettings1"), EnableIf("enableApplySettings"), LabelText("更新模式"), LabelWidth(120)]
        public AnimatorUpdateMode updateMode = AnimatorUpdateMode.Normal;

        [HorizontalGroup("应用Animator设置/ApplySettings1"), EnableIf("enableApplySettings"), LabelText("剔除模式"), LabelWidth(120)]
        public AnimatorCullingMode cullingMode = AnimatorCullingMode.AlwaysAnimate;

        [HorizontalGroup("应用Animator设置/ApplySettings2"), ToggleLeft, LabelText("应用根运动"), LabelWidth(120)]
        public bool applyRootMotion = false;



        [Space(10)]
        [BoxGroup("配套AnimationClip", ShowLabel = false)]
        [Title("配套AnimationClip", titleAlignment: TitleAlignments.Centered, bold: true)]



        [HorizontalGroup("配套AnimationClip/CreateClip1"), ShowIf("@clipNullAction != ClipNullAction.Ignore"), LabelText("Clip名称"), LabelWidth(120)]
        public string newClipName = "NewAnimation";

        [BoxGroup("按钮组", showLabel: false)]
        [HorizontalGroup("按钮组/Row1")]
        [Button("应用 Animator 设置", ButtonHeight = 34), GUIColor(0.28f, 0.52f, 0.85f)]
        public void ApplyAnimatorSettings()
        {
            createdAssetRecords.Clear();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("批量应用Animator设置");

            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }
            var allObjects = SimpleToolsSafetyUtility.CollectTargets(selectedObjects, includeChildren);
            var affectedTargets = allObjects
                .Where(obj => obj != null && (obj.GetComponent<Animator>() != null || addAnimatorIfMissing))
                .ToList();

            // 填充预览列表
            previewObjects.Clear();
            foreach (var obj in affectedTargets)
            {
                previewObjects.Add(obj.name);
            }

            if (!ConfirmTargetOperation("确认应用Animator设置", "应用 Animator 设置到", affectedTargets))
                return;

            RuntimeAnimatorController sharedController = null;
            AnimationClip sharedClip = null;

            // 检查是否需要创建sharedController
            bool needSharedController = false;
            foreach (var obj in affectedTargets)
            {
                var animator = obj.GetComponent<Animator>();
                if ((animator == null && addAnimatorIfMissing) ||
                    (animator != null && animator.runtimeAnimatorController == null))
                {
                    needSharedController = true;
                    break;
                }
            }

            if (animatorController == null && controllerNullAction == ControllerNullAction.CreateShared && needSharedController)
            {
                var controller = CreateAnimatorControllerAsset("NewAnimatorController", "应用Animator设置-共享Controller");
                if (controller == null)
                    return;

                sharedController = controller;

                // 根据 clipNullAction 创建剪辑
                if (clipNullAction == ClipNullAction.CreateShared)
                {
                    if (defaultAnimationClip != null)
                    {
                        sharedClip = defaultAnimationClip;
                    }
                    else
                    {
                        sharedClip = CreateAnimationClipAsset("SharedAnimationClip", "应用Animator设置-共享Clip");
                        if (sharedClip == null)
                            return;
                    }

                    var rootStateMachine = (sharedController as AnimatorController).layers[0].stateMachine;
                    var defaultState = rootStateMachine.AddState(sharedClip.name);
                    defaultState.motion = sharedClip;
                    AssetDatabase.SaveAssets();
                }
                else if (clipNullAction == ClipNullAction.Ignore)
                {
                    // 不创建剪辑
                }
            }

            if (animatorController == null &&
                controllerNullAction == ControllerNullAction.CreateIndividual &&
                clipNullAction == ClipNullAction.CreateShared)
            {
                sharedClip = defaultAnimationClip != null
                    ? defaultAnimationClip
                    : CreateAnimationClipAsset("SharedAnimationClip", "应用Animator设置-独立Controller共享Clip");
                if (sharedClip == null)
                    return;
            }

            int modifiedCount = 0;
            EditorUtility.DisplayProgressBar("应用Animator设置", "开始处理...", 0f);
            try
            {
                for (int i = 0; i < affectedTargets.Count; i++)
                {
                    var obj = affectedTargets[i];
                    float progress = (float)i / affectedTargets.Count;
                    EditorUtility.DisplayProgressBar("应用Animator设置", $"正在处理 {obj.name}...", progress);

                    var animator = obj.GetComponent<Animator>();
                    if (addAnimatorIfMissing && animator == null)
                    {
                        animator = Undo.AddComponent<Animator>(obj);
                    }
                    if (animator != null)
                    {
                        Undo.RecordObject(animator, "Modify Animator");

                        // 应用settings
                        if (enableApplySettings)
                        {
                            animator.updateMode = updateMode;
                            animator.cullingMode = cullingMode;
                            animator.applyRootMotion = applyRootMotion;
                        }

                        // 如果Controller为null，则设置
                        if (animator.runtimeAnimatorController == null)
                        {
                            RuntimeAnimatorController controllerToUse = animatorController;
                            if (controllerToUse == null)
                            {
                                if (controllerNullAction == ControllerNullAction.CreateShared)
                                {
                                    controllerToUse = sharedController;
                                }
                                else if (controllerNullAction == ControllerNullAction.CreateIndividual)
                                {
                                    var controller = CreateAnimatorControllerAsset($"NewAnimatorController_{obj.name}", $"应用Animator设置-独立Controller:{obj.name}");
                                    if (controller == null)
                                        continue;

                                    controllerToUse = controller;

                                    // 根据 clipNullAction 创建剪辑
                                    if (clipNullAction == ClipNullAction.CreateIndividual)
                                    {
                                        AnimationClip clipToAdd;
                                        if (defaultAnimationClip != null)
                                        {
                                            clipToAdd = defaultAnimationClip;
                                        }
                                        else
                                        {
                                            clipToAdd = CreateAnimationClipAsset($"AnimationClip_{obj.name}", $"应用Animator设置-独立Clip:{obj.name}");
                                            if (clipToAdd == null)
                                                continue;
                                        }

                                        var rootStateMachine = (controllerToUse as AnimatorController).layers[0].stateMachine;
                                        var defaultState = rootStateMachine.AddState(clipToAdd.name);
                                        defaultState.motion = clipToAdd;
                                        AssetDatabase.SaveAssets();
                                    }
                                    else if (clipNullAction == ClipNullAction.CreateShared && sharedClip != null)
                                    {
                                        var rootStateMachine = (controllerToUse as AnimatorController).layers[0].stateMachine;
                                        var defaultState = rootStateMachine.AddState(sharedClip.name);
                                        defaultState.motion = sharedClip;
                                        AssetDatabase.SaveAssets();
                                    }
                                    else if (clipNullAction == ClipNullAction.Ignore)
                                    {
                                        // 不创建剪辑
                                    }
                                }
                            }

                            if (controllerToUse != null)
                            {
                                animator.runtimeAnimatorController = controllerToUse;
                            }
                        }

                        EditorUtility.SetDirty(animator);
                        modifiedCount++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(undoGroup);
            }

            MarkScenesDirty(affectedTargets);
            lastResultSummary = $"Animator 设置完成: 修改 {modifiedCount} 个 | 目标 {affectedTargets.Count} 个 | 新建资产 {createdAssetRecords.Count} 个";
            lastResultDetail = BuildAnimatorResultDetail(affectedTargets);
            EditorUtility.DisplayDialog("成功", $"成功修改 {modifiedCount} 个Animator组件！", "确定");
        }

        [HorizontalGroup("按钮组/Row1")]
        [Button("添加 Animator 组件", ButtonHeight = 34), GUIColor(0.25f, 0.62f, 0.45f)]
        public void AddAnimatorComponents()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            var allObjects = GetSelectedTargets();
            var targets = allObjects.Where(obj => obj != null && obj.GetComponent<Animator>() == null).ToList();
            if (!ConfirmTargetOperation("确认批量添加Animator", "添加 Animator 到", targets))
                return;

            int addedCount = 0;
            foreach (var obj in targets)
            {
                var animator = Undo.AddComponent<Animator>(obj);
                if (animatorController != null)
                {
                    animator.runtimeAnimatorController = animatorController;
                }
                EditorUtility.SetDirty(animator);
                addedCount++;
            }

            MarkScenesDirty(targets);
            lastResultSummary = $"添加 Animator 完成: 添加 {addedCount} 个 | 目标 {targets.Count} 个";
            lastResultDetail = BuildAnimatorResultDetail(targets);
            EditorUtility.DisplayDialog("成功", $"成功添加 {addedCount} 个Animator组件！", "确定");
        }

        [HorizontalGroup("按钮组/Row2")]
        [Button("移除 Animator 组件", ButtonHeight = 34), GUIColor(0.82f, 0.38f, 0.30f)]
        public void RemoveAnimatorComponents()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            var allObjects = SimpleToolsSafetyUtility.CollectTargets(selectedObjects, includeChildren);
            var targets = allObjects.Where(obj => obj != null && obj.GetComponent<Animator>() != null).ToList();
            if (!ConfirmTargetOperation("确认批量移除Animator", "移除 Animator 从", targets))
                return;

            int removedCount = 0;
            foreach (var obj in targets)
            {
                var animator = obj.GetComponent<Animator>();
                if (animator != null)
                {
                    Undo.DestroyObjectImmediate(animator);
                    removedCount++;
                }
            }

            MarkScenesDirty(targets);
            lastResultSummary = $"移除 Animator 完成: 移除 {removedCount} 个 | 目标 {targets.Count} 个";
            lastResultDetail = BuildAnimatorResultDetail(targets);
            EditorUtility.DisplayDialog("成功", $"成功移除 {removedCount} 个Animator组件！", "确定");
        }

        private void MarkScenesDirty(IEnumerable<GameObject> targets)
        {
            if (targets == null)
                return;

            foreach (var scene in targets
                .Where(obj => obj != null && obj.scene.IsValid())
                .Select(obj => obj.scene)
                .Distinct())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        [HorizontalGroup("按钮组/Row2")]
        [Button("替换 AnimatorController", ButtonHeight = 34), GUIColor(0.75f, 0.58f, 0.25f)]
        public void ReplaceAnimatorControllers()
        {
            createdAssetRecords.Clear();
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            var allObjects = SimpleToolsSafetyUtility.CollectTargets(selectedObjects, includeChildren);
            var targets = allObjects
                .Where(obj => obj != null)
                .Where(obj =>
                {
                    var animator = obj.GetComponent<Animator>();
                    return animator != null && animator.runtimeAnimatorController == null;
                })
                .ToList();

            RuntimeAnimatorController controllerToUse = null;
            if (animatorController != null)
            {
                controllerToUse = animatorController;
            }
            bool willCreateController = animatorController == null && controllerNullAction == ControllerNullAction.CreateShared;

            int replacedCount = 0;
            if (controllerToUse == null && !willCreateController)
            {
                EditorUtility.DisplayDialog("没有可用Controller", "当前没有指定 Controller，且没有创建新的 Controller。", "知道了");
                return;
            }

            if (!ConfirmTargetOperation("确认替换AnimatorController", "设置 Controller 到", targets))
                return;

            if (willCreateController)
            {
                var controller = CreateAnimatorControllerAsset("NewAnimatorController", "替换AnimatorController-共享Controller");
                if (controller == null)
                    return;

                controllerToUse = controller;
            }

            foreach (var obj in targets)
            {
                var animator = obj.GetComponent<Animator>();
                if (animator != null && animator.runtimeAnimatorController == null && controllerToUse != null)
                {
                    Undo.RecordObject(animator, "Replace Animator Controller");
                    animator.runtimeAnimatorController = controllerToUse;
                    EditorUtility.SetDirty(animator);
                    replacedCount++;
                }
            }

            MarkScenesDirty(targets);
            lastResultSummary = $"替换 Controller 完成: 替换 {replacedCount} 个 | 目标 {targets.Count} 个 | 新建资产 {createdAssetRecords.Count} 个";
            lastResultDetail = BuildAnimatorResultDetail(targets);
            EditorUtility.DisplayDialog("成功", $"成功替换 {replacedCount} 个空的AnimatorController！", "确定");
        }

        [HorizontalGroup("按钮组/Row3")]
        [Button("重置为默认设置", ButtonHeight = 34), GUIColor("@ESDesignUtility.ColorSelector.Color_02")]
        public void ResetToDefaultSettings()
        {
            includeChildren = true;
            addAnimatorIfMissing = true;
            animatorController = null;
            defaultAnimationClip = null;
            controllerNullAction = ControllerNullAction.CreateShared;
            clipNullAction = ClipNullAction.CreateShared;
            enableApplySettings = false;
            updateMode = AnimatorUpdateMode.Normal;
            cullingMode = AnimatorCullingMode.AlwaysAnimate;
            applyRootMotion = false;
            newClipName = "NewAnimation";
            assetGroupName = "默认";
        }

        [HorizontalGroup("按钮组/Row4")]
        [Button("导出设置", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
        public void ExportSettings()
        {
            string path = EditorUtility.SaveFilePanel("导出设置", "", "AnimationBatchSettings.json", "json");
            if (!string.IsNullOrEmpty(path))
            {
                var settings = new SettingsData
                {
                    includeChildren = this.includeChildren,
                    addAnimatorIfMissing = this.addAnimatorIfMissing,
                    animatorController = this.animatorController,
                    defaultAnimationClip = this.defaultAnimationClip,
                    animatorControllerPath = AssetDatabase.GetAssetPath(this.animatorController),
                    animatorControllerGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(this.animatorController)),
                    defaultAnimationClipPath = AssetDatabase.GetAssetPath(this.defaultAnimationClip),
                    defaultAnimationClipGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(this.defaultAnimationClip)),
                    controllerNullAction = this.controllerNullAction,
                    clipNullAction = this.clipNullAction,
                    assetGroupName = this.assetGroupName,
                    enableApplySettings = this.enableApplySettings,
                    updateMode = this.updateMode,
                    cullingMode = this.cullingMode,
                    applyRootMotion = this.applyRootMotion,
                    newClipName = this.newClipName
                };
                try
                {
                    string json = JsonUtility.ToJson(settings, true);
                    File.WriteAllText(path, json, Encoding.UTF8);
                    lastResultSummary = "Animator 设置导出完成";
                    lastResultDetail = path;
                    EditorUtility.DisplayDialog("成功", "设置已导出！", "确定");
                }
                catch (Exception ex)
                {
                    lastResultSummary = "Animator 设置导出失败";
                    lastResultDetail = ex.Message;
                    EditorUtility.DisplayDialog("导出失败", $"无法写入 Animator 设置：\n{ex.Message}", "知道了");
                }
            }
        }

        [HorizontalGroup("按钮组/Row4")]
        [Button("导入设置", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
        public void ImportSettings()
        {
            string path = EditorUtility.OpenFilePanel("导入设置", "", "json");
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    var settings = JsonUtility.FromJson<SettingsData>(json);
                    if (settings == null)
                    {
                        EditorUtility.DisplayDialog("导入失败", "设置文件为空或格式无效。", "知道了");
                        return;
                    }

                    includeChildren = settings.includeChildren;
                    addAnimatorIfMissing = settings.addAnimatorIfMissing;
                    animatorController = LoadAssetFromGuidOrPath<RuntimeAnimatorController>(settings.animatorControllerGuid, settings.animatorControllerPath) ?? settings.animatorController;
                    defaultAnimationClip = LoadAssetFromGuidOrPath<AnimationClip>(settings.defaultAnimationClipGuid, settings.defaultAnimationClipPath) ?? settings.defaultAnimationClip;
                    controllerNullAction = settings.controllerNullAction;
                    clipNullAction = settings.clipNullAction;
                    assetGroupName = settings.assetGroupName;
                    enableApplySettings = settings.enableApplySettings;
                    updateMode = settings.updateMode;
                    cullingMode = settings.cullingMode;
                    applyRootMotion = settings.applyRootMotion;
                    newClipName = settings.newClipName;
                    lastResultSummary = "Animator 设置导入完成";
                    lastResultDetail = path;
                    EditorUtility.DisplayDialog("成功", "设置已导入！", "确定");
                }
                catch (Exception ex)
                {
                    lastResultSummary = "Animator 设置导入失败";
                    lastResultDetail = ex.Message;
                    EditorUtility.DisplayDialog("导入失败", $"无法读取 Animator 设置：\n{ex.Message}", "知道了");
                }
            }
        }

        private T LoadAssetFromGuidOrPath<T>(string guid, string path) where T : UnityEngine.Object
        {
            if (!string.IsNullOrWhiteSpace(guid))
            {
                string guidPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(guidPath))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<T>(guidPath);
                    if (asset != null)
                        return asset;
                }
            }

            if (!string.IsNullOrWhiteSpace(path))
                return AssetDatabase.LoadAssetAtPath<T>(path);

            return null;
        }
        #endregion

        #region 创建AnimationClip
        [ShowIf("@clipNullAction != ClipNullAction.Ignore")]
        [HorizontalGroup("按钮组/Row3")]
        [Button("创建并应用 AnimationClip", ButtonHeight = 34), GUIColor(0.25f, 0.62f, 0.45f)]
        public void CreateAndApplyAnimationClip()
        {
            createdAssetRecords.Clear();
            var selectedObjects = Selection.gameObjects;
            List<GameObject> allObjects = SimpleToolsSafetyUtility.CollectTargets(selectedObjects, includeChildren);
            if (selectedObjects != null && selectedObjects.Length > 0 &&
                !ConfirmTargetOperation("确认创建并应用AnimationClip", "应用 AnimationClip/Controller 到", allObjects))
                return;

            // 确保Controller存在
            if (animatorController == null)
            {
                var controller_ = CreateAnimatorControllerAsset("NewAnimatorController", "创建并应用Clip-自动Controller");
                if (controller_ == null)
                    return;

                animatorController = controller_;
            }

            // 获取所有对象

            if (clipNullAction == ClipNullAction.CreateIndividual)
            {
                // 为每个对象创建独立的clip
                foreach (var obj in allObjects)
                {
                    var clip = CreateAnimationClipAsset($"{newClipName}_{obj.name}", $"创建并应用Clip-独立:{obj.name}");
                    if (clip == null)
                        continue;

                    // 添加到controller
                    var controller = animatorController as AnimatorController;
                    if (controller != null)
                    {
                        var rootStateMachine = controller.layers[0].stateMachine;
                        var state = rootStateMachine.AddState(clip.name);
                        state.motion = clip;
                    }
                }
                AssetDatabase.SaveAssets();
            }

            AnimationClip clipToUse = defaultAnimationClip;
            if (clipToUse == null && clipNullAction != ClipNullAction.CreateIndividual)
            {
                if (string.IsNullOrEmpty(newClipName))
                {
                    EditorUtility.DisplayDialog("错误", "请输入新Clip名称！", "确定");
                    return;
                }

                if (clipNullAction == ClipNullAction.CreateShared)
                {
                    clipToUse = CreateAnimationClipAsset(newClipName, "创建并应用Clip-共享");
                }
                else
                {
                    // 默认情况，假设CreateShared
                    clipToUse = CreateAnimationClipAsset(newClipName, "创建并应用Clip-默认共享");
                }

                if (clipToUse == null)
                    return;
            }

            // 添加到Controller（仅对非CreateIndividual的情况）
            if (clipToUse != null && clipNullAction != ClipNullAction.CreateIndividual)
            {
                var controller = animatorController as AnimatorController;
                if (controller != null)
                {
                    var rootStateMachine = controller.layers[0].stateMachine;
                    var state = rootStateMachine.AddState(clipToUse.name);
                    state.motion = clipToUse;
                    AssetDatabase.SaveAssets();
                }
            }

            // 应用到选中对象
            if (selectedObjects != null && selectedObjects.Length > 0)
            {
                int appliedCount = 0;
                foreach (var obj in allObjects)
                {
                    var animator = obj.GetComponent<Animator>();
                    if (addAnimatorIfMissing && animator == null)
                    {
                        animator = Undo.AddComponent<Animator>(obj);
                    }
                    if (animator != null)
                    {
                        Undo.RecordObject(animator, "Set Animator Controller");
                        animator.runtimeAnimatorController = animatorController;
                        EditorUtility.SetDirty(animator);
                        appliedCount++;
                    }
                }

                MarkScenesDirty(allObjects);
                lastResultSummary = $"AnimationClip 创建并应用完成: 应用 {appliedCount} 个对象 | 新建资产 {createdAssetRecords.Count} 个";
                lastResultDetail = BuildAnimatorResultDetail(allObjects);
                EditorUtility.DisplayDialog("成功", $"AnimationClip 已创建并应用到 {appliedCount} 个对象！", "确定");
            }
            else
            {
                lastResultSummary = $"AnimationClip 已应用到 Controller | 新建资产 {createdAssetRecords.Count} 个";
                lastResultDetail = BuildAnimatorResultDetail(null);
                EditorUtility.DisplayDialog("成功", $"AnimationClip 已应用到Controller！", "确定");
            }
        }

        private string BuildAnimatorResultDetail(IEnumerable<GameObject> targets)
        {
            var sections = new List<string>();
            if (targets != null)
                sections.Add("对象:\n" + SimpleToolsSafetyUtility.JoinPreview(targets.Select(obj => obj != null ? obj.name : null), 12));

            if (createdAssetRecords.Count > 0)
                sections.Add("新建资产:\n" + SimpleToolsSafetyUtility.JoinPreview(createdAssetRecords.Select(record => $"{record.assetType}: {record.assetPath}"), 12));

            return sections.Count == 0 ? "无详细项" : string.Join("\n\n", sections);
        }
        #endregion
    }
    #endregion

}
