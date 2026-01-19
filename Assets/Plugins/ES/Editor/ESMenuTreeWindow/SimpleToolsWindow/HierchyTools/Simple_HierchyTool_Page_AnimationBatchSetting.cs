using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
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
            public ControllerNullAction controllerNullAction;
            public ClipNullAction clipNullAction;
            public string assetGroupName;
            public bool enableApplySettings;
            public AnimatorUpdateMode updateMode;
            public AnimatorCullingMode cullingMode;
            public bool applyRootMotion;
            public string newClipName;
        }
        #region 公共设置
        [Title("动画器批量设置工具", "批量设置Animator属性", bold: true, titleAlignment: TitleAlignments.Centered)]

        [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
        public string readMe = "选择带有Animator的GameObject，\n设置动画属性，\n点击应用按钮批量修改";

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
        #endregion
        #region 辅助方法
        private string GenerateUniqueAssetName(string baseName, string folder, string extension)
        {
            string fullFolder = $"{folder}/{assetGroupName}";
            string fullPath = $"{ESGlobalEditorDefaultConfi.Instance.Path_ResourceParent}/{fullFolder}/{baseName}{extension}";
            if (!AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(fullPath))
            {
                return baseName;
            }
            int random;
            do
            {
                random = UnityEngine.Random.Range(0, 10000);
                fullPath = $"{ESGlobalEditorDefaultConfi.Instance.Path_ResourceParent}/{fullFolder}/{baseName}_{random}{extension}";
            } while (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(fullPath));
            return $"{baseName}_{random}";
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
        [Button("应用Animator设置", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
        public void ApplyAnimatorSettings()
        {
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("批量应用Animator设置");

            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }
            var allObjects = new List<GameObject>();
            foreach (var obj in selectedObjects)
            {
                allObjects.Add(obj);
                if (includeChildren)
                {
                    allObjects.AddRange(obj.GetComponentsInChildren<Transform>().Select(t => t.gameObject));
                }
            }

            // 填充预览列表
            previewObjects.Clear();
            foreach (var obj in allObjects)
            {
                if (obj.GetComponent<Animator>() != null || addAnimatorIfMissing)
                {
                    previewObjects.Add(obj.name);
                }
            }


            RuntimeAnimatorController sharedController = null;
            AnimationClip sharedClip = null;

            // 检查是否需要创建sharedController
            bool needSharedController = false;
            foreach (var obj in allObjects)
            {
                var animator = obj.GetComponent<Animator>();
                if (animator != null && animator.runtimeAnimatorController == null)
                {
                    needSharedController = true;
                    break;
                }
            }

            if (animatorController == null && controllerNullAction == ControllerNullAction.CreateShared && needSharedController)
            {
                var controller = new AnimatorController();
                controller.name = GenerateUniqueAssetName("NewAnimatorController", "AnimationControllers", ".controller");
                string controllerPath = $"{ESGlobalEditorDefaultConfi.Instance.Path_ResourceParent}/AnimationControllers/{assetGroupName}/{controller.name}.controller";
                Directory.CreateDirectory(Path.GetDirectoryName(controllerPath));
                AssetDatabase.CreateAsset(controller, controllerPath);
                AssetDatabase.SaveAssets();
                sharedController = controller;

                // 确保Controller有Layer
                if ((sharedController as AnimatorController).layers.Length == 0)
                {
                    (sharedController as AnimatorController).AddLayer("Base Layer");
                    AssetDatabase.SaveAssets();
                }

                // 根据 clipNullAction 创建剪辑
                if (clipNullAction == ClipNullAction.CreateShared)
                {
                    if (defaultAnimationClip != null)
                    {
                        sharedClip = defaultAnimationClip;
                    }
                    else
                    {
                        sharedClip = new AnimationClip();
                        sharedClip.name = GenerateUniqueAssetName("SharedAnimationClip", "Animations", ".anim");
                        string clipPath = $"{ESGlobalEditorDefaultConfi.Instance.Path_ResourceParent}/Animations/{assetGroupName}/{sharedClip.name}.anim";
                        Directory.CreateDirectory(Path.GetDirectoryName(clipPath));
                        AssetDatabase.CreateAsset(sharedClip, clipPath);
                        AssetDatabase.SaveAssets();
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

            int modifiedCount = 0;
            EditorUtility.DisplayProgressBar("应用Animator设置", "开始处理...", 0f);
            for (int i = 0; i < allObjects.Count; i++)
            {
                var obj = allObjects[i];
                float progress = (float)i / allObjects.Count;
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
                                var controller = new AnimatorController();
                                controller.name = GenerateUniqueAssetName($"NewAnimatorController_{obj.name}", "AnimationControllers", ".controller");
                                string controllerPath = $"{ESGlobalEditorDefaultConfi.Instance.Path_ResourceParent}/AnimationControllers/{assetGroupName}/{controller.name}.controller";
                                Directory.CreateDirectory(Path.GetDirectoryName(controllerPath));
                                AssetDatabase.CreateAsset(controller, controllerPath);
                                AssetDatabase.SaveAssets();
                                controllerToUse = controller;

                                // 确保Controller有Layer
                                if ((controllerToUse as AnimatorController).layers.Length == 0)
                                {
                                    (controllerToUse as AnimatorController).AddLayer("Base Layer");
                                    AssetDatabase.SaveAssets();
                                }

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
                                        clipToAdd = new AnimationClip();
                                        clipToAdd.name = GenerateUniqueAssetName($"AnimationClip_{obj.name}", "Animations", ".anim");
                                        string clipPath = $"{ESGlobalEditorDefaultConfi.Instance.Path_ResourceParent}/Animations/{assetGroupName}/{clipToAdd.name}.anim";
                                        Directory.CreateDirectory(Path.GetDirectoryName(clipPath));
                                        AssetDatabase.CreateAsset(clipToAdd, clipPath);
                                        AssetDatabase.SaveAssets();
                                    }

                                    var rootStateMachine = (controllerToUse as AnimatorController).layers[0].stateMachine;
                                    var defaultState = rootStateMachine.AddState(clipToAdd.name);
                                    defaultState.motion = clipToAdd;
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
            EditorUtility.ClearProgressBar();

            EditorUtility.DisplayDialog("成功", $"成功修改 {modifiedCount} 个Animator组件！", "确定");
        }

        [HorizontalGroup("按钮组/Row1")]
        [Button("批量添加Animator组件", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
        public void AddAnimatorComponents()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            int addedCount = 0;
            foreach (var obj in selectedObjects)
            {
                if (obj.GetComponent<Animator>() == null)
                {
                    var animator = Undo.AddComponent<Animator>(obj);
                    if (animatorController != null)
                    {
                        animator.runtimeAnimatorController = animatorController;
                    }
                    addedCount++;
                }
            }

            EditorUtility.DisplayDialog("成功", $"成功添加 {addedCount} 个Animator组件！", "确定");
        }

        [HorizontalGroup("按钮组/Row2")]
        [Button("批量移除Animator组件", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_05")]
        public void RemoveAnimatorComponents()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            var allObjects = new List<GameObject>();
            foreach (var obj in selectedObjects)
            {
                allObjects.Add(obj);
                if (includeChildren)
                {
                    allObjects.AddRange(obj.GetComponentsInChildren<Transform>().Select(t => t.gameObject));
                }
            }

            int removedCount = 0;
            foreach (var obj in allObjects)
            {
                var animator = obj.GetComponent<Animator>();
                if (animator != null)
                {
                    Undo.DestroyObjectImmediate(animator);
                    removedCount++;
                }
            }

            EditorUtility.DisplayDialog("成功", $"成功移除 {removedCount} 个Animator组件！", "确定");
        }

        [HorizontalGroup("按钮组/Row2")]
        [Button("替换AnimatorController", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
        public void ReplaceAnimatorControllers()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            var allObjects = new List<GameObject>();
            foreach (var obj in selectedObjects)
            {
                allObjects.Add(obj);
                if (includeChildren)
                {
                    allObjects.AddRange(obj.GetComponentsInChildren<Transform>().Select(t => t.gameObject));
                }
            }

            RuntimeAnimatorController controllerToUse = null;
            if (animatorController != null)
            {
                controllerToUse = animatorController;
            }
            else if (controllerNullAction == ControllerNullAction.CreateShared)
            {
                var controller = new AnimatorController();
                controller.name = GenerateUniqueAssetName("NewAnimatorController", "AnimationControllers", ".controller");
                string controllerPath = $"{ESGlobalEditorDefaultConfi.Instance.Path_ResourceParent}/AnimationControllers/{assetGroupName}/{controller.name}.controller";
                Directory.CreateDirectory(Path.GetDirectoryName(controllerPath));
                AssetDatabase.CreateAsset(controller, controllerPath);
                AssetDatabase.SaveAssets();
                controllerToUse = controller;

                // 确保Controller有Layer
                if ((controllerToUse as AnimatorController).layers.Length == 0)
                {
                    (controllerToUse as AnimatorController).AddLayer("Base Layer");
                    AssetDatabase.SaveAssets();
                }
            }

            int replacedCount = 0;
            foreach (var obj in allObjects)
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

            EditorUtility.DisplayDialog("成功", $"成功替换 {replacedCount} 个空的AnimatorController！", "确定");
        }

        [HorizontalGroup("按钮组/Row3")]
        [Button("重置为默认设置", ButtonHeight = 40), GUIColor("@ESDesignUtility.ColorSelector.Color_02")]
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
                    controllerNullAction = this.controllerNullAction,
                    clipNullAction = this.clipNullAction,
                    assetGroupName = this.assetGroupName,
                    enableApplySettings = this.enableApplySettings,
                    updateMode = this.updateMode,
                    cullingMode = this.cullingMode,
                    applyRootMotion = this.applyRootMotion,
                    newClipName = this.newClipName
                };
                string json = JsonUtility.ToJson(settings);
                File.WriteAllText(path, json);
                EditorUtility.DisplayDialog("成功", "设置已导出！", "确定");
            }
        }

        [HorizontalGroup("按钮组/Row4")]
        [Button("导入设置", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
        public void ImportSettings()
        {
            string path = EditorUtility.OpenFilePanel("导入设置", "", "json");
            if (!string.IsNullOrEmpty(path))
            {
                string json = File.ReadAllText(path);
                var settings = JsonUtility.FromJson<SettingsData>(json);
                includeChildren = settings.includeChildren;
                addAnimatorIfMissing = settings.addAnimatorIfMissing;
                animatorController = settings.animatorController;
                defaultAnimationClip = settings.defaultAnimationClip;
                controllerNullAction = settings.controllerNullAction;
                clipNullAction = settings.clipNullAction;
                assetGroupName = settings.assetGroupName;
                enableApplySettings = settings.enableApplySettings;
                updateMode = settings.updateMode;
                cullingMode = settings.cullingMode;
                applyRootMotion = settings.applyRootMotion;
                newClipName = settings.newClipName;
                EditorUtility.DisplayDialog("成功", "设置已导入！", "确定");
            }
        }
        #endregion

        #region 创建AnimationClip
        [ShowIf("@clipNullAction != ClipNullAction.Ignore")]
        [HorizontalGroup("按钮组/Row3")]
        [Button("创建并应用AnimationClip", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_06")]
        public void CreateAndApplyAnimationClip()
        {
            // 确保Controller存在
            if (animatorController == null)
            {
                var controller_ = new AnimatorController();
                controller_.name = GenerateUniqueAssetName("NewAnimatorController", "AnimationControllers", ".controller");
                string controllerPath = $"{ESGlobalEditorDefaultConfi.Instance.Path_ResourceParent}/AnimationControllers/{assetGroupName}/{controller_.name}.controller";
                Directory.CreateDirectory(Path.GetDirectoryName(controllerPath));
                AssetDatabase.CreateAsset(controller_, controllerPath);
                AssetDatabase.SaveAssets();
                animatorController = controller_;

                // 确保Controller有Layer
                if ((animatorController as AnimatorController).layers.Length == 0)
                {
                    (animatorController as AnimatorController).AddLayer("Base Layer");
                    AssetDatabase.SaveAssets();
                }
            }

            // 获取所有对象
            var selectedObjects = Selection.gameObjects;
            List<GameObject> allObjects = new List<GameObject>();
            if (selectedObjects != null && selectedObjects.Length > 0)
            {
                foreach (var obj in selectedObjects)
                {
                    allObjects.Add(obj);
                    if (includeChildren)
                    {
                        allObjects.AddRange(obj.GetComponentsInChildren<Transform>().Select(t => t.gameObject));
                    }
                }
            }

            if (clipNullAction == ClipNullAction.CreateIndividual)
            {
                // 为每个对象创建独立的clip
                foreach (var obj in allObjects)
                {
                    string uniqueClipName = GenerateUniqueAssetName($"{newClipName}_{obj.name}", "Animations", ".anim");
                    var clip = new AnimationClip();
                    clip.name = uniqueClipName;
                    string clipPath = $"{ESGlobalEditorDefaultConfi.Instance.Path_ResourceParent}/Animations/{assetGroupName}/{uniqueClipName}.anim";
                    Directory.CreateDirectory(Path.GetDirectoryName(clipPath));
                    AssetDatabase.CreateAsset(clip, clipPath);

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

                // 设置clipPath
                string clipPath;
                if (clipNullAction == ClipNullAction.CreateShared)
                {
                    string uniqueClipName = GenerateUniqueAssetName(newClipName, "Animations", ".anim");
                    clipPath = $"{ESGlobalEditorDefaultConfi.Instance.Path_ResourceParent}/Animations/{assetGroupName}/{uniqueClipName}.anim";
                    clipToUse = new AnimationClip();
                    clipToUse.name = uniqueClipName;
                }
                else
                {
                    // 默认情况，假设CreateShared
                    string uniqueClipName = GenerateUniqueAssetName(newClipName, "Animations", ".anim");
                    clipPath = $"{ESGlobalEditorDefaultConfi.Instance.Path_ResourceParent}/Animations/{assetGroupName}/{uniqueClipName}.anim";
                    clipToUse = new AnimationClip();
                    clipToUse.name = uniqueClipName;
                }

                // 创建目录和资产
                Directory.CreateDirectory(Path.GetDirectoryName(clipPath));
                AssetDatabase.CreateAsset(clipToUse, clipPath);
                AssetDatabase.SaveAssets();
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

                EditorUtility.DisplayDialog("成功", $"AnimationClip 已创建并应用到 {appliedCount} 个对象！", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("成功", $"AnimationClip 已应用到Controller！", "确定");
            }
        }
        #endregion
    }
    #endregion

}