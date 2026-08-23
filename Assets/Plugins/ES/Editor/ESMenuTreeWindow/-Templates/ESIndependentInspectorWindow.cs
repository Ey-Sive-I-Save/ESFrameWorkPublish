using System;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ES.EditorInternal;

namespace ES
{
    [Serializable]
    internal struct ESEditorAssetIdentity
    {
        [SerializeField] private string guid;
        [SerializeField] private string path;
        [SerializeField] private string subAssetName;
        [SerializeField] private long localFileId;
        [SerializeField] private string typeName;

        internal bool IsValid => !string.IsNullOrEmpty(guid) || !string.IsNullOrEmpty(path);

        internal static bool TryCapture(UnityEngine.Object asset, out ESEditorAssetIdentity identity)
        {
            identity = default;
            if (asset == null)
                return false;

            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
                return false;

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string assetGuid, out long assetLocalFileId);
            identity.guid = assetGuid ?? string.Empty;
            identity.path = assetPath;
            identity.subAssetName = asset.name ?? string.Empty;
            identity.localFileId = assetLocalFileId;
            identity.typeName = asset.GetType().AssemblyQualifiedName ?? asset.GetType().FullName;
            return identity.IsValid;
        }

        internal bool TryResolve(out UnityEngine.Object asset)
        {
            asset = null;
            string resolvedPath = !string.IsNullOrEmpty(guid)
                ? AssetDatabase.GUIDToAssetPath(guid)
                : path;
            if (string.IsNullOrEmpty(resolvedPath))
                return false;

            UnityEngine.Object[] candidates = AssetDatabase.LoadAllAssetsAtPath(resolvedPath);
            for (int i = 0; i < candidates.Length; i++)
            {
                UnityEngine.Object candidate = candidates[i];
                if (candidate == null)
                    continue;

                if (localFileId != 0
                    && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out _, out long candidateLocalFileId)
                    && candidateLocalFileId == localFileId)
                {
                    asset = candidate;
                    return true;
                }
            }

            // 已记录 LocalFileId 时，它就是子资产/主资产的强身份。
            // 找不到必须视为引用丢失，不能再按同名同类型猜测并误绑到另一份资产。
            if (localFileId != 0)
                return false;

            Type expectedType = !string.IsNullOrEmpty(typeName) ? Type.GetType(typeName, false) : null;
            for (int i = 0; i < candidates.Length; i++)
            {
                UnityEngine.Object candidate = candidates[i];
                if (candidate == null)
                    continue;
                if (expectedType != null && !expectedType.IsInstanceOfType(candidate))
                    continue;
                if (!string.IsNullOrEmpty(subAssetName)
                    && !string.Equals(candidate.name, subAssetName, StringComparison.Ordinal))
                    continue;

                asset = candidate;
                return true;
            }

            return false;
        }
    }

    internal static class ESIndependentInspectorAsset
    {
        internal static VisualGUIDrawerSO CreateManagedReferenceAsset(object data, string name)
        {
            if (data == null)
                return null;

            VisualGUIDrawerSO asset = ScriptableObject.CreateInstance<VisualGUIDrawerSO>();
            asset.name = string.IsNullOrWhiteSpace(name) ? "ES 独立检查器桥接资产" : name;
            asset.hideFlags = HideFlags.HideAndDontSave;
            asset.drawerData = data;
            return asset;
        }

        internal static void DestroyManagedReferenceAsset(VisualGUIDrawerSO asset)
        {
            if (asset == null)
                return;

            asset.drawerData = null;
            UnityEngine.Object.DestroyImmediate(asset);
        }
    }

    /// <summary>
    /// ES 编辑器统一的独立检查器弹窗基类。
    /// 每个窗口拥有自己的 HideAndDontSave 桥接资产；ReloadDomain 只保存稳定身份，
    /// 不保存活对象、SerializedObject、OdinEditor 或委托。
    /// </summary>
    public abstract class ESIndependentInspectorWindow<TWindow> : ESOdinMenuTreeWindow<TWindow>
        where TWindow : ESIndependentInspectorWindow<TWindow>
    {
        [SerializeField] private bool configured;
        [SerializeField] private bool targetIsSourceAsset;
        [SerializeField] private bool closeActionInvoked;
        [SerializeField] private bool closeBecauseTargetLost;
        [SerializeField] private string targetStableKey = string.Empty;
        [SerializeField] private string windowTitle = "独立检查器";
        [SerializeField] private string pageName = "编辑";
        [SerializeField] private ESEditorAssetIdentity sourceIdentity;

        [NonSerialized] private UnityEngine.Object sourceAsset;
        [NonSerialized] private object inspectorData;
        [NonSerialized] private UnityEngine.Object inspectorObject;
        [NonSerialized] private VisualGUIDrawerSO ownedBridgeAsset;
        [NonSerialized] private Page_ESIndependentInspector inspectorPage;
        [NonSerialized] private VisualElement shellRoot;
        [NonSerialized] private bool domainReloading;
        [NonSerialized] private bool restoreScheduled;
        [NonSerialized] private bool validationScheduled;
        [NonSerialized] private bool closeScheduled;

        public object CurrentInspectorData => inspectorData;
        public UnityEngine.Object CurrentSourceAsset => sourceAsset;
        internal UnityEngine.Object CurrentInspectorObject => inspectorObject;
        internal bool OwnsIndependentBridgeAsset => ownedBridgeAsset != null;

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent(windowTitle, "ES 独立检查器");
        }

        public override bool UseScrollView => false;

        protected static TWindow OpenIndependent(
            object data,
            UnityEngine.Object source,
            string stableTargetKey,
            string title,
            string page,
            EditorWindow sleepOwner = null)
        {
            if (data == null || source == null)
                return null;

            bool alreadyOpen = HasOpenInstances<TWindow>();
            TWindow window = GetWindow<TWindow>(true, title, false);
            UsingWindow = window;
            try
            {
                if (alreadyOpen && window.configured)
                    window.InvokeCloseActionOnce();
                window.ESWindow_SetSleepOwnerOverride(sleepOwner);
                if (!window.Configure(data, source, stableTargetKey, title, page))
                {
                    window.Close();
                    return null;
                }

                window.titleContent = window.ESWindow_GetWindowGUIContent();
                window.MenuWidth = 0f;
                window.ShowUtility();
                window.Focus();
                if (!alreadyOpen)
                    window.ApplyDefaultWindowBounds();
                // GetWindow 首次创建时会先触发 OnEnable；此时显式 owner 可能尚未存在，
                // 或窗口声明的动态 getter 仍指向另一实例。窗口真正显示并完成
                // Presentation 绑定后，再提交一次显式关系，确保 OpenFor(..., owner)
                // 的参数成为唯一生效的父窗口身份，并清掉可能残留的 Pending 记录。
                if (sleepOwner != null
                    && window.ESWindow_SleepLinkMode != ESWindowSleepLinkMode.Independent)
                {
                    ESWindowFoundation.SetSleepOwner(
                        window,
                        sleepOwner,
                        window.ESWindow_SleepLinkMode);
                }
                window.ForceMenuTreeRebuild();
                window.BuildIndependentInspectorShell();
                window.Repaint();
                return window;
            }
            catch (Exception exception)
            {
                window.ScheduleCloseBecauseInspectorError("打开独立检查器", exception);
                return null;
            }
        }

        public static void CloseCurrentWindow()
        {
            if (UsingWindow == null)
                return;

            TWindow window = UsingWindow;
            UsingWindow = null;
            window.Close();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            UsingWindow = this as TWindow;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;

            if (configured)
                ScheduleRestoreAfterReload();
        }

        protected override void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.projectChanged -= OnProjectChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            CancelDelayedCalls();
            ReleaseTransientResources(domainReloading);
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.projectChanged -= OnProjectChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            CancelDelayedCalls();

            try
            {
                if (!domainReloading)
                    InvokeCloseActionOnce();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                ReleaseTransientResources(true);
                if (ReferenceEquals(UsingWindow, this))
                    UsingWindow = null;
                base.OnDestroy();
            }
        }

        protected override void ES_OnBuildMenuTree(OdinMenuTree tree)
        {
            base.ES_OnBuildMenuTree(tree);
            inspectorPage = new Page_ESIndependentInspector(this);
            RegisterAndAddPage(tree, pageName, inspectorPage, SdfIconType.Pencil);
        }

        protected override void DrawEditors()
        {
            // 字段只由独立的 IMGUIContainer 绘制，禁止 Odin 宿主重复绘制整页。
        }

        private void CreateGUI()
        {
            BuildIndependentInspectorShell();
        }

        protected virtual string InspectorSubtitle => "Odin 业务字段桥接 · ES 独立检查器";

        protected virtual void DrawIndependentInspectorSummary(UnityEngine.Object target)
        {
            string displayName = inspectorData != null
                ? inspectorData.GetType()._GetTypeDisplayName()
                : target != null ? target.name : "未绑定目标";
            EditorGUILayout.LabelField(pageName, ESEditorPresentation.MetaStyle);
            EditorGUILayout.LabelField(displayName, ESEditorPresentation.HeaderStyle);
            EditorGUILayout.Space(4f);
        }

        protected virtual IDisposable BeginIndependentInspectorBody()
        {
            return EmptyScope.Instance;
        }

        protected abstract bool TryResolveManagedInspectorData(
            UnityEngine.Object resolvedSourceAsset,
            string stableTargetKey,
            out object data);

        protected virtual void OnIndependentInspectorChanged(UnityEngine.Object resolvedSourceAsset, object data)
        {
        }

        protected virtual void OnIndependentInspectorClosed(
            UnityEngine.Object resolvedSourceAsset,
            object data,
            bool targetLost)
        {
        }

        protected virtual void OnIndependentInspectorBound(bool restoredAfterReload)
        {
        }

        private bool Configure(
            object data,
            UnityEngine.Object source,
            string stableTargetKey,
            string title,
            string page)
        {
            if (!ESEditorAssetIdentity.TryCapture(source, out sourceIdentity))
            {
                // 非持久对象允许在当前 Domain 编辑；ReloadDomain 后无法可靠恢复，将自动关闭。
                sourceIdentity = default;
            }

            configured = true;
            targetIsSourceAsset = ReferenceEquals(data, source);
            targetStableKey = stableTargetKey?.Trim() ?? string.Empty;
            windowTitle = string.IsNullOrWhiteSpace(title) ? "独立检查器" : title.Trim();
            pageName = string.IsNullOrWhiteSpace(page) ? "编辑" : page.Trim();
            closeActionInvoked = false;
            closeBecauseTargetLost = false;
            return BindInspectorTarget(source, data, false);
        }

        private bool BindInspectorTarget(UnityEngine.Object resolvedSourceAsset, object data, bool restoredAfterReload)
        {
            if (resolvedSourceAsset == null || data == null)
                return false;

            ReleaseTransientResources(true);
            sourceAsset = resolvedSourceAsset;
            inspectorData = data;
            if (data is UnityEngine.Object unityObject)
            {
                inspectorObject = unityObject;
            }
            else
            {
                ownedBridgeAsset = ESIndependentInspectorAsset.CreateManagedReferenceAsset(
                    data,
                    windowTitle + " · 临时桥接");
                inspectorObject = ownedBridgeAsset;
            }

            if (inspectorObject == null)
                return false;

            OnIndependentInspectorBound(restoredAfterReload);
            return true;
        }

        private void ScheduleRestoreAfterReload()
        {
            if (restoreScheduled)
                return;

            restoreScheduled = true;
            EditorApplication.delayCall -= RestoreAfterReloadDelayed;
            EditorApplication.delayCall += RestoreAfterReloadDelayed;
        }

        private void RestoreAfterReloadDelayed()
        {
            EditorApplication.delayCall -= RestoreAfterReloadDelayed;
            restoreScheduled = false;
            if (this == null || !configured)
                return;

            try
            {
                domainReloading = false;
                if (!sourceIdentity.IsValid || !sourceIdentity.TryResolve(out UnityEngine.Object resolvedSource))
                {
                    ScheduleCloseBecauseTargetLost("源资产引用在 ReloadDomain 后无法恢复");
                    return;
                }

                if (!TryResolveCurrentData(resolvedSource, out object resolvedData))
                {
                    ScheduleCloseBecauseTargetLost("检查器目标在 ReloadDomain 后无法恢复");
                    return;
                }

                if (!BindInspectorTarget(resolvedSource, resolvedData, true))
                {
                    ScheduleCloseBecauseTargetLost("独立检查器桥接资产重建失败");
                    return;
                }

                titleContent = ESWindow_GetWindowGUIContent();
                ForceMenuTreeRebuild();
                BuildIndependentInspectorShell();
                Repaint();
            }
            catch (Exception exception)
            {
                ScheduleCloseBecauseInspectorError("ReloadDomain 恢复", exception);
            }
        }

        private bool TryResolveCurrentData(UnityEngine.Object resolvedSource, out object data)
        {
            if (targetIsSourceAsset)
            {
                data = resolvedSource;
                return data != null;
            }

            if (string.IsNullOrWhiteSpace(targetStableKey))
            {
                data = null;
                return false;
            }

            return TryResolveManagedInspectorData(resolvedSource, targetStableKey, out data) && data != null;
        }

        private void OnBeforeAssemblyReload()
        {
            domainReloading = true;
            ReleaseTransientResources(true);
        }

        private void OnProjectChanged()
        {
            ScheduleValidation();
        }

        private void OnUndoRedoPerformed()
        {
            ScheduleValidation();
        }

        private void ScheduleValidation()
        {
            if (!configured || validationScheduled || !sourceIdentity.IsValid)
                return;

            validationScheduled = true;
            EditorApplication.delayCall -= ValidateTargetDelayed;
            EditorApplication.delayCall += ValidateTargetDelayed;
        }

        private void ValidateTargetDelayed()
        {
            EditorApplication.delayCall -= ValidateTargetDelayed;
            validationScheduled = false;
            if (this == null || !configured)
                return;

            try
            {
                if (!sourceIdentity.TryResolve(out UnityEngine.Object resolvedSource)
                    || !TryResolveCurrentData(resolvedSource, out object resolvedData))
                {
                    ScheduleCloseBecauseTargetLost("源资产或检查器目标已被删除");
                    return;
                }

                if (!ReferenceEquals(sourceAsset, resolvedSource) || !ReferenceEquals(inspectorData, resolvedData))
                {
                    if (!BindInspectorTarget(resolvedSource, resolvedData, false))
                    {
                        ScheduleCloseBecauseTargetLost("检查器目标重新绑定失败");
                        return;
                    }

                    ForceMenuTreeRebuild();
                    BuildIndependentInspectorShell();
                }

                Repaint();
            }
            catch (Exception exception)
            {
                ScheduleCloseBecauseInspectorError("目标校验", exception);
            }
        }

        private void ScheduleCloseBecauseTargetLost(string reason)
        {
            if (closeScheduled)
                return;

            closeBecauseTargetLost = true;
            closeScheduled = true;
            Debug.LogWarning("[ES 独立检查器] " + reason + "，窗口将自动关闭。标题：" + windowTitle);
            EditorApplication.delayCall -= CloseBecauseTargetLostDelayed;
            EditorApplication.delayCall += CloseBecauseTargetLostDelayed;
        }

        private void ScheduleCloseBecauseInspectorError(string stage, Exception exception)
        {
            if (closeScheduled)
                return;

            Debug.LogException(exception);
            ScheduleCloseBecauseTargetLost(stage + "发生异常：" + exception.GetType().Name);
        }

        private void CloseBecauseTargetLostDelayed()
        {
            EditorApplication.delayCall -= CloseBecauseTargetLostDelayed;
            closeScheduled = false;
            if (this != null)
                Close();
        }

        private void InvokeCloseActionOnce()
        {
            if (closeActionInvoked)
                return;

            closeActionInvoked = true;
            OnIndependentInspectorClosed(sourceAsset, inspectorData, closeBecauseTargetLost);
        }

        private void HandleInspectorChanged()
        {
            if (sourceAsset == null || inspectorData == null)
            {
                ScheduleCloseBecauseTargetLost("编辑过程中资产引用失效");
                return;
            }

            EditorUtility.SetDirty(sourceAsset);
            OnIndependentInspectorChanged(sourceAsset, inspectorData);
        }

        private void ReleaseTransientResources(bool clearSessionReferences)
        {
            inspectorPage?.ReleaseEditor();
            inspectorPage = null;
            shellRoot = null;
            inspectorObject = null;
            ESIndependentInspectorAsset.DestroyManagedReferenceAsset(ownedBridgeAsset);
            ownedBridgeAsset = null;
            if (!clearSessionReferences)
                return;

            inspectorData = null;
            sourceAsset = null;
        }

        private void CancelDelayedCalls()
        {
            EditorApplication.delayCall -= RestoreAfterReloadDelayed;
            EditorApplication.delayCall -= ValidateTargetDelayed;
            EditorApplication.delayCall -= CloseBecauseTargetLostDelayed;
            restoreScheduled = false;
            validationScheduled = false;
            closeScheduled = false;
        }

        private void ApplyDefaultWindowBounds()
        {
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            float width = Mathf.Clamp(main.width * 0.32f, 420f, 720f);
            float height = Mathf.Clamp(main.height * 0.72f, 520f, 1200f);
            minSize = new Vector2(420f, 520f);
            maxSize = new Vector2(1200f, 1600f);
            position = new Rect(
                main.x + (main.width - width) * 0.5f,
                main.y + (main.height - height) * 0.5f,
                width,
                height);
        }

        private void BuildIndependentInspectorShell()
        {
            if (rootVisualElement == null)
                return;
            if (inspectorPage == null)
            {
                rootVisualElement.schedule.Execute(BuildIndependentInspectorShell);
                return;
            }

            if (shellRoot != null)
                shellRoot.RemoveFromHierarchy();

            shellRoot = new VisualElement { name = "ESIndependentInspectorShell" };
            shellRoot.style.flexGrow = 1f;
            shellRoot.style.flexDirection = FlexDirection.Column;
            shellRoot.style.backgroundColor = ESEditorPresentation.GetDepthBackground(3);
            shellRoot.style.borderLeftWidth = 1f;
            shellRoot.style.borderLeftColor = ESEditorPresentation.DividerColor;

            VisualElement header = new VisualElement { name = "ESIndependentInspectorHeader" };
            header.style.paddingLeft = 13f;
            header.style.paddingRight = 12f;
            header.style.paddingTop = 9f;
            header.style.paddingBottom = 9f;
            header.style.minHeight = 67f;
            header.style.backgroundColor = ESEditorPresentation.GetDepthBackground(1);
            header.style.borderLeftWidth = 4f;
            header.style.borderLeftColor = ESEditorPresentation.GetDepthAccent(0);
            header.style.borderBottomWidth = 1f;
            header.style.borderBottomColor = ESEditorPresentation.DividerColor;

            Label context = new Label(pageName);
            context.style.fontSize = 9f;
            context.style.unityFontStyleAndWeight = FontStyle.Bold;
            context.style.color = ESEditorPresentation.SectionMutedTextColor;
            Label title = new Label(windowTitle);
            title.style.fontSize = 14f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = ESEditorPresentation.SectionSelectedTextColor;
            title.style.whiteSpace = WhiteSpace.NoWrap;
            title.style.overflow = Overflow.Hidden;
            title.style.textOverflow = TextOverflow.Ellipsis;
            Label subtitle = new Label(InspectorSubtitle);
            subtitle.style.fontSize = 10f;
            subtitle.style.color = ESEditorPresentation.EmptyTextColor;
            subtitle.style.whiteSpace = WhiteSpace.NoWrap;
            subtitle.style.overflow = Overflow.Hidden;
            subtitle.style.textOverflow = TextOverflow.Ellipsis;

            VisualElement headingRow = new VisualElement { name = "ESIndependentInspectorHeadingRow" };
            headingRow.style.flexDirection = FlexDirection.Row;
            headingRow.style.alignItems = Align.Center;
            headingRow.style.flexShrink = 0f;

            VisualElement headingText = new VisualElement { name = "ESIndependentInspectorHeadingText" };
            headingText.style.flexGrow = 1f;
            headingText.style.flexShrink = 1f;
            headingText.style.minWidth = 0f;
            headingText.Add(context);
            headingText.Add(title);

            Button closeButton = new Button(Close)
            {
                name = "ESIndependentInspectorQuickCloseButton",
                text = "关闭",
                tooltip = "保存当前修改并关闭独立检查器。"
            };
            closeButton.style.flexShrink = 0f;
            closeButton.style.minWidth = 64f;
            closeButton.style.height = 26f;
            closeButton.style.marginLeft = 10f;
            closeButton.style.paddingLeft = 12f;
            closeButton.style.paddingRight = 12f;
            closeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            closeButton.style.color = ESEditorPresentation.SectionSelectedTextColor;
            closeButton.style.backgroundColor = ESEditorPresentation.GetDepthBackground(2);
            closeButton.style.borderLeftColor = ESEditorPresentation.DividerColor;
            closeButton.style.borderRightColor = ESEditorPresentation.DividerColor;
            closeButton.style.borderTopColor = ESEditorPresentation.DividerColor;
            closeButton.style.borderBottomColor = ESEditorPresentation.DividerColor;
            closeButton.style.borderLeftWidth = 1f;
            closeButton.style.borderRightWidth = 1f;
            closeButton.style.borderTopWidth = 1f;
            closeButton.style.borderBottomWidth = 1f;

            headingRow.Add(headingText);
            headingRow.Add(closeButton);
            header.Add(headingRow);
            header.Add(subtitle);
            shellRoot.Add(header);

            ScrollView details = new ScrollView(ScrollViewMode.Vertical);
            details.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            details.verticalScrollerVisibility = ScrollerVisibility.Auto;
            details.style.flexGrow = 1f;
            details.style.flexShrink = 1f;
            details.style.overflow = Overflow.Hidden;
            details.style.paddingLeft = 7f;
            details.style.paddingRight = 7f;
            details.style.paddingTop = 5f;
            details.style.paddingBottom = 7f;
            IMGUIContainer body = new IMGUIContainer(inspectorPage.DrawInspectorContents);
            body.style.flexGrow = 1f;
            body.style.flexShrink = 1f;
            body.style.minWidth = 0f;
            body.style.width = Length.Percent(100f);
            body.style.marginLeft = 2f;
            body.style.marginRight = 2f;
            details.Add(body);
            shellRoot.Add(details);
            rootVisualElement.Add(shellRoot);
        }

        [Serializable]
        private sealed class Page_ESIndependentInspector : ESWindowPageBase
        {
            [NonSerialized] private readonly ESIndependentInspectorWindow<TWindow> owner;
            [NonSerialized] private OdinEditor editor;
            [NonSerialized] private UnityEngine.Object editorTarget;

            internal Page_ESIndependentInspector(ESIndependentInspectorWindow<TWindow> owner)
            {
                this.owner = owner;
            }

            internal void DrawInspectorContents()
            {
                try
                {
                    UnityEngine.Object target = owner != null ? owner.CurrentInspectorObject : null;
                    if (target == null)
                    {
                        if (owner != null && owner.restoreScheduled)
                        {
                            EditorGUILayout.HelpBox("正在恢复独立检查器目标…", MessageType.Info);
                            return;
                        }

                        EditorGUILayout.HelpBox("检查器目标已经失效，窗口将自动关闭。", MessageType.Warning);
                        owner?.ScheduleCloseBecauseTargetLost("绘制时检查器目标为空");
                        return;
                    }

                    owner.DrawIndependentInspectorSummary(target);
                    if (!ReferenceEquals(editorTarget, target) || editor == null)
                    {
                        ReleaseEditor();
                        editorTarget = target;
                        editor = OdinEditor.CreateEditor(target, typeof(OdinEditor)) as OdinEditor;
                    }

                    if (editor == null)
                    {
                        owner.ScheduleCloseBecauseTargetLost("Odin 检查器创建失败");
                        return;
                    }

                    RecordUndoBeforeInspectorInput(owner.CurrentSourceAsset, "编辑独立检查器资产");
                    bool changed;
                    EditorGUI.BeginChangeCheck();
                    try
                    {
                        using (owner.BeginIndependentInspectorBody())
                        {
                            editor.DrawDefaultInspector();
                        }
                    }
                    finally
                    {
                        changed = EditorGUI.EndChangeCheck();
                    }

                    if (changed)
                        owner.HandleInspectorChanged();
                }
                catch (Exception exception)
                {
                    owner?.ScheduleCloseBecauseInspectorError("检查器绘制", exception);
                }
            }

            private static void RecordUndoBeforeInspectorInput(UnityEngine.Object target, string label)
            {
                if (target == null || Event.current == null)
                    return;

                EventType type = Event.current.type;
                if (type == EventType.MouseDown
                    || type == EventType.DragPerform
                    || type == EventType.ExecuteCommand)
                {
                    Undo.RecordObject(target, label);
                }
                else if (type == EventType.KeyDown && !EditorGUIUtility.editingTextField)
                {
                    Undo.RecordObject(target, label);
                }
            }

            internal void ReleaseEditor()
            {
                if (editor != null)
                    UnityEngine.Object.DestroyImmediate(editor);
                editor = null;
                editorTarget = null;
            }

            public override void OnPageDisable()
            {
                ReleaseEditor();
            }
        }

        private sealed class EmptyScope : IDisposable
        {
            internal static readonly EmptyScope Instance = new EmptyScope();

            public void Dispose()
            {
            }
        }
    }
}
