using UnityEngine;

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace ES
{
    /// <summary>
    /// 通过 GUID 持有 Prefab 身份，并在当前场景中维护一个可编辑但不会保存的临时实例。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("【ES】/开发与验证/编辑器/Prefab Tester")]
    public sealed class ESPrefabTester : MonoBehaviour
    {
        [SerializeField, HideInInspector]
        private string prefabGuid = string.Empty;

        [SerializeField, HideInInspector]
        private bool createAutomatically = true;

        [SerializeField, HideInInspector]
        private Transform previewParent;

#if UNITY_EDITOR
        private const string PreviewContainerName = "[ES Prefab Tester Preview]";
        private const string PreviewMarkerOwner = "ESPrefabTester";
        private const HideFlags EditableDontSaveFlags =
            HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        [NonSerialized]
        private GameObject previewContainer;

        [NonSerialized]
        private GameObject previewInstance;

        [NonSerialized]
        private string lastStatus = "尚未创建测试实例。";

        [NonSerialized]
        private string previewSourceDependencyHash = string.Empty;

        public string PrefabGuid => prefabGuid;
        public GameObject PreviewInstance => previewInstance;
        public string LastStatus => lastStatus;
        internal bool CreateAutomatically => createAutomatically;

        private void OnEnable()
        {
            if (Application.isPlaying)
                return;

            RegisterEditorCallbacks();
            EditorApplication.delayCall -= RebuildAfterEnable;
            if (createAutomatically)
                EditorApplication.delayCall += RebuildAfterEnable;
        }

        private void OnDisable()
        {
            UnregisterEditorCallbacks();
            DestroyPreviewForLifecycle("组件禁用", "组件已禁用，测试实例已清理。");
        }

        private void OnDestroy()
        {
            UnregisterEditorCallbacks();
            DestroyPreviewForLifecycle("组件销毁", "组件已销毁，测试实例已清理。");
        }

        internal GameObject ResolvePrefabAsset()
        {
            if (string.IsNullOrEmpty(prefabGuid))
                return null;

            string path = AssetDatabase.GUIDToAssetPath(prefabGuid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        internal bool RebuildPreview()
        {
            return RebuildPreview(true);
        }

        internal bool RebuildPreviewWithoutDiscardConfirmation()
        {
            return RebuildPreview(false);
        }

        private bool RebuildPreview(bool confirmDiscardOverrides)
        {
            if (!CanMutatePreview())
            {
                lastStatus = "PlayMode、编译或 PlayMode 切换期间不能重建测试实例。";
                return false;
            }

            if (IsInsidePrefabTesterPreview())
            {
                UnregisterEditorCallbacks();
                lastStatus = "Prefab 测试实例内部禁止再次创建测试实例，以避免递归预览。";
                return false;
            }

            if (PrefabStageUtility.GetPrefabStage(gameObject) != null)
            {
                lastStatus = "Prefab Stage 中禁止创建测试实例，请把 ESPrefabTester 放在普通测试场景中。";
                return false;
            }

            Scene hostScene = gameObject.scene;
            if (!hostScene.IsValid() || !hostScene.isLoaded)
            {
                lastStatus = "Prefab 测试器当前不属于有效且已加载的场景。";
                return false;
            }

            if (!TryGetPreviewParent(hostScene, out Transform targetParent, out string parentReason))
            {
                lastStatus = parentReason;
                return false;
            }

            if (confirmDiscardOverrides && !ConfirmDiscardPreview("重建测试实例"))
                return false;

            DestroyPreviewInstance(null);

            GameObject prefab = ResolvePrefabAsset();
            if (!IsSupportedPrefabAsset(prefab, out string reason))
            {
                lastStatus = reason;
                return false;
            }

            try
            {
                previewContainer = new GameObject(PreviewContainerName);
                previewContainer.hideFlags = EditableDontSaveFlags;
                SceneManager.MoveGameObjectToScene(previewContainer, hostScene);
                previewContainer.transform.SetParent(targetParent, false);

                EditorPreviewGameObjectSign marker = previewContainer.AddComponent<EditorPreviewGameObjectSign>();
                marker.Setup(PreviewMarkerOwner, "Temporary editable Prefab test container.");
                marker.hideFlags = HideFlags.HideAndDontSave;

                previewInstance = PrefabUtility.InstantiatePrefab(prefab, hostScene) as GameObject;
                if (previewInstance == null)
                {
                    DestroyPreviewInstance("Prefab 实例创建失败。");
                    return false;
                }

                previewInstance.transform.SetParent(previewContainer.transform, false);
                SetEditableDontSaveFlagsRecursive(previewInstance.transform);
                previewSourceDependencyHash = GetSourceDependencyHash(prefab);
                lastStatus = "测试实例已创建。修改子实例后可应用到源 Prefab。";
                return true;
            }
            catch (Exception exception)
            {
                DestroyPreviewInstance("Prefab 实例创建失败：" + exception.Message);
                Debug.LogException(exception, this);
                return false;
            }
        }

        internal void ClearPreview()
        {
            if (!ConfirmDiscardPreview("清理测试实例"))
                return;

            DestroyPreviewInstance("测试实例已清理。");
        }

        internal void ClearPreviewWithoutDiscardConfirmation()
        {
            DestroyPreviewInstance("测试实例已清理。");
        }

        internal void NotifyManualCreationReady()
        {
            DestroyPreviewInstance("Prefab 已绑定。当前为手动模式，点击“创建实例”开始测试。");
        }

        internal void HandleAutomaticCreationChanged()
        {
            EditorApplication.delayCall -= RebuildAfterEnable;
            if (createAutomatically && previewInstance == null && !string.IsNullOrEmpty(prefabGuid))
                EditorApplication.delayCall += RebuildAfterEnable;
        }

        internal bool ConfirmDiscardPreview(string actionName)
        {
            if (!TryGetUserOverrideState(out bool hasOverrides, out string error))
            {
                if (!string.IsNullOrEmpty(error))
                {
                    lastStatus = error;
                    return false;
                }
                return true;
            }

            if (!hasOverrides)
                return true;

            bool confirmed = EditorUtility.DisplayDialog(
                "丢弃 Prefab 测试修改",
                "当前测试实例存在尚未应用的 Override。\n\n继续“" + actionName + "”将永久丢弃这些修改。",
                "丢弃并继续",
                "取消");
            if (!confirmed)
                lastStatus = "已取消“" + actionName + "”，尚未应用的修改仍保留。";
            return confirmed;
        }

        internal bool CanApply(out string reason)
        {
            reason = string.Empty;
            if (!CanMutatePreview())
            {
                reason = "PlayMode、编译或 PlayMode 切换期间不能应用 Prefab。";
                return false;
            }

            if (!TryGetValidPreviewInstance(out GameObject instance, out reason))
                return false;

            if (!TryValidateSourceRevision(instance, out reason))
                return false;

            if (!TryGetUserOverrideState(out bool hasOverrides, out reason))
                return false;

            if (!hasOverrides)
            {
                reason = "当前实例没有可应用的 Prefab Override。";
                return false;
            }

            return true;
        }

        internal bool ApplyToPrefab()
        {
            return ApplyToPrefabInternal(true);
        }

        private bool ApplyToPrefabInternal(bool requireConfirmation)
        {
            if (!CanApply(out string reason))
            {
                lastStatus = reason;
                return false;
            }

            string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(previewInstance);
            if (HasTransformOverride(previewInstance))
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "应用包含 Transform Override",
                    "当前测试实例包含位置/旋转/缩放 Override。\n\n"
                    + "若你移动的是预览实例，而不是测试容器或 Tester 宿主，这些场景摆放值也会被写入源 Prefab。\n\n"
                    + "目标：\n" + sourcePath,
                    "仍要全部应用",
                    "取消",
                    "查看说明");
                if (choice != 0)
                {
                    lastStatus = "已取消应用：拒绝将 Transform Override 写入源 Prefab。";
                    return false;
                }
            }

            if (requireConfirmation && !EditorUtility.DisplayDialog(
                    "应用 Prefab 测试修改",
                    "将当前测试实例的全部 Override 应用到：\n\n" + sourcePath + "\n\n此操作会修改源 Prefab。",
                    "应用",
                    "取消"))
            {
                lastStatus = "已取消应用。";
                return false;
            }

            try
            {
                ApplyPrefabWithoutPreviewHideFlags(previewInstance);
                bool rebuilt = RebuildPreview(false);
                lastStatus = rebuilt
                    ? "修改已应用到源 Prefab，测试实例已从源资产重建。"
                    : "修改已应用到源 Prefab，但测试实例重建失败。";
                return true;
            }
            catch (Exception exception)
            {
                lastStatus = "应用 Prefab 修改失败：" + exception.Message;
                Debug.LogException(exception, this);
                return false;
            }
            finally
            {
                if (previewInstance != null)
                    SetEditableDontSaveFlagsRecursive(previewInstance.transform);
            }
        }

        internal bool RevertPreview()
        {
            if (!CanMutatePreview())
            {
                lastStatus = "PlayMode、编译或 PlayMode 切换期间不能还原测试实例。";
                return false;
            }

            if (!TryGetValidPreviewInstance(out GameObject instance, out string reason))
            {
                lastStatus = reason;
                return false;
            }

            try
            {
                PrefabUtility.RevertPrefabInstance(instance, InteractionMode.UserAction);
                lastStatus = "测试实例已还原为源 Prefab 状态。";
                return true;
            }
            catch (Exception exception)
            {
                lastStatus = "还原 Prefab 实例失败：" + exception.Message;
                Debug.LogException(exception, this);
                return false;
            }
            finally
            {
                if (instance != null)
                    SetEditableDontSaveFlagsRecursive(instance.transform);
            }
        }

        private void RebuildAfterEnable()
        {
            EditorApplication.delayCall -= RebuildAfterEnable;
            if (this == null
                || !isActiveAndEnabled
                || !createAutomatically
                || Application.isPlaying
                || string.IsNullOrEmpty(prefabGuid))
                return;

            if (IsInsidePrefabTesterPreview())
            {
                UnregisterEditorCallbacks();
                lastStatus = "Prefab 测试实例内部的 ESPrefabTester 已停止自动重建。";
                return;
            }

            RebuildPreview();
        }

        private void RegisterEditorCallbacks()
        {
            UnregisterEditorCallbacks();
            AssemblyReloadEvents.beforeAssemblyReload += CleanupBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += CleanupOnPlayModeChanged;
            EditorSceneManager.sceneClosing += CleanupOnSceneClosing;
        }

        private void UnregisterEditorCallbacks()
        {
            EditorApplication.delayCall -= RebuildAfterEnable;
            AssemblyReloadEvents.beforeAssemblyReload -= CleanupBeforeAssemblyReload;
            EditorApplication.playModeStateChanged -= CleanupOnPlayModeChanged;
            EditorSceneManager.sceneClosing -= CleanupOnSceneClosing;
        }

        private void CleanupBeforeAssemblyReload()
        {
            DestroyPreviewForLifecycle("ReloadDomain", "ReloadDomain 前已清理测试实例。");
        }

        private void CleanupOnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                DestroyPreviewForLifecycle("进入 PlayMode", "进入 PlayMode 前已清理测试实例。");
        }

        private void CleanupOnSceneClosing(Scene scene, bool removingScene)
        {
            if (scene == gameObject.scene)
                DestroyPreviewForLifecycle("场景关闭", "场景关闭前已清理测试实例。");
        }

        private void DestroyPreviewForLifecycle(string actionName, string status)
        {
            if (TryGetUserOverrideState(out bool hasOverrides, out _) && hasOverrides)
            {
                Debug.LogWarning(
                    "[ESPrefabTester] " + actionName + " 必须清理测试实例，尚未应用的 Prefab Override 已丢弃。",
                    this);
            }

            DestroyPreviewInstance(status);
        }

        private void DestroyPreviewInstance(string status)
        {
            GameObject ownedContainer = FindOwnedPreviewContainer();
            GameObject instance = previewInstance;
            previewInstance = null;
            previewContainer = null;
            previewSourceDependencyHash = string.Empty;

            if (instance != null
                && (ownedContainer == null || !instance.transform.IsChildOf(ownedContainer.transform)))
            {
                if (Application.isPlaying)
                    Destroy(instance);
                else
                    DestroyImmediate(instance);
            }

            if (ownedContainer != null)
            {
                if (Application.isPlaying)
                    Destroy(ownedContainer);
                else
                    DestroyImmediate(ownedContainer);
            }

            if (!string.IsNullOrEmpty(status))
                lastStatus = status;
        }

        private GameObject FindOwnedPreviewContainer()
        {
            if (previewContainer != null)
                return previewContainer;

            Transform candidateParent = previewParent != null ? previewParent : transform;
            GameObject candidate = FindOwnedPreviewContainerUnder(candidateParent);
            if (candidate != null)
                return candidate;

            return candidateParent == transform
                ? null
                : FindOwnedPreviewContainerUnder(transform);
        }

        private static GameObject FindOwnedPreviewContainerUnder(Transform candidateParent)
        {
            if (candidateParent == null)
                return null;

            for (int i = candidateParent.childCount - 1; i >= 0; i--)
            {
                Transform child = candidateParent.GetChild(i);
                if (child == null)
                    continue;

                EditorPreviewGameObjectSign marker = child.GetComponent<EditorPreviewGameObjectSign>();
                if (marker != null && string.Equals(marker.Owner, PreviewMarkerOwner, StringComparison.Ordinal))
                    return child.gameObject;
            }

            return null;
        }

        private bool TryGetPreviewParent(Scene hostScene, out Transform targetParent, out string reason)
        {
            targetParent = previewParent != null ? previewParent : transform;
            if (targetParent == null
                || !targetParent.gameObject.scene.IsValid()
                || !targetParent.gameObject.scene.isLoaded)
            {
                reason = "指定父节点不属于有效且已加载的场景。";
                return false;
            }

            if (targetParent.gameObject.scene != hostScene)
            {
                reason = "指定父节点必须与 Prefab 测试器位于同一场景。";
                return false;
            }

            if (IsInsideOwnedPreview(targetParent))
            {
                reason = "指定父节点不能位于另一个 Prefab 测试预览内部。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static bool IsInsideOwnedPreview(Transform target)
        {
            Transform current = target;
            while (current != null)
            {
                EditorPreviewGameObjectSign marker = current.GetComponent<EditorPreviewGameObjectSign>();
                if (marker != null && string.Equals(marker.Owner, PreviewMarkerOwner, StringComparison.Ordinal))
                    return true;
                current = current.parent;
            }

            return false;
        }

        private static bool HasTransformOverride(GameObject instance)
        {
            if (instance == null)
                return false;

            PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(instance);
            if (modifications == null)
                return false;

            for (int i = 0; i < modifications.Length; i++)
            {
                PropertyModification modification = modifications[i];
                if (modification == null || string.IsNullOrEmpty(modification.propertyPath))
                    continue;

                if (modification.propertyPath.StartsWith("m_LocalPosition", StringComparison.Ordinal)
                    || modification.propertyPath.StartsWith("m_LocalRotation", StringComparison.Ordinal)
                    || modification.propertyPath.StartsWith("m_LocalScale", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private bool TryGetValidPreviewInstance(out GameObject instance, out string reason)
        {
            instance = previewInstance;
            reason = string.Empty;
            if (instance == null || !PrefabUtility.IsPartOfPrefabInstance(instance))
            {
                reason = "当前没有有效的 Prefab 测试实例。";
                return false;
            }

            if (PrefabUtility.GetOutermostPrefabInstanceRoot(instance) != instance)
            {
                reason = "测试实例不是最外层 Prefab 根，已拒绝应用以避免修改错误目标。";
                return false;
            }

            string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);
            string sourceGuid = string.IsNullOrEmpty(sourcePath) ? string.Empty : AssetDatabase.AssetPathToGUID(sourcePath);
            if (!string.Equals(sourceGuid, prefabGuid, StringComparison.Ordinal))
            {
                reason = "实例来源与保存的 GUID 不一致，已拒绝修改源 Prefab。";
                return false;
            }

            if (PrefabUtility.GetPrefabAssetType(instance) == PrefabAssetType.Model)
            {
                reason = "模型 Prefab 不支持应用修改。";
                return false;
            }

            return true;
        }

        private bool TryValidateSourceRevision(GameObject instance, out string reason)
        {
            string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);
            string currentHash = string.IsNullOrEmpty(sourcePath)
                ? string.Empty
                : AssetDatabase.GetAssetDependencyHash(sourcePath).ToString();
            if (string.IsNullOrEmpty(previewSourceDependencyHash) || string.IsNullOrEmpty(currentHash))
            {
                reason = "无法验证源 Prefab 版本，请重建测试实例后再应用。";
                return false;
            }

            if (!string.Equals(currentHash, previewSourceDependencyHash, StringComparison.Ordinal))
            {
                reason = "源 Prefab 在测试实例创建后已经变化。为避免覆盖其他修改，请先重建测试实例。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool TryGetUserOverrideState(out bool hasOverrides, out string reason)
        {
            hasOverrides = false;
            reason = string.Empty;
            GameObject instance = previewInstance;
            if (instance == null || !PrefabUtility.IsPartOfPrefabInstance(instance))
                return false;

            if (PrefabUtility.GetOutermostPrefabInstanceRoot(instance) != instance)
            {
                reason = "测试实例不是最外层 Prefab 根，无法安全检查 Override。";
                return false;
            }

            List<PreviewHideFlagState> states = null;
            try
            {
                states = ReplaceWithSourceHideFlags(instance.transform);
                hasOverrides = PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false);
                return true;
            }
            catch (Exception exception)
            {
                reason = "检查 Prefab Override 失败：" + exception.Message;
                Debug.LogException(exception, this);
                return false;
            }
            finally
            {
                if (states != null)
                    RestoreHideFlags(states);
            }
        }

        private static void ApplyPrefabWithoutPreviewHideFlags(GameObject instance)
        {
            List<PreviewHideFlagState> states = null;
            try
            {
                states = ReplaceWithSourceHideFlags(instance.transform);
                PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.UserAction);
            }
            finally
            {
                if (states != null)
                    RestoreHideFlags(states);
            }
        }

        private static List<PreviewHideFlagState> ReplaceWithSourceHideFlags(Transform root)
        {
            var states = new List<PreviewHideFlagState>(32);
            try
            {
                ReplaceWithSourceHideFlagsRecursive(root, states);
                return states;
            }
            catch
            {
                RestoreHideFlags(states);
                throw;
            }
        }

        private static void ReplaceWithSourceHideFlagsRecursive(
            Transform current,
            List<PreviewHideFlagState> states)
        {
            if (current == null)
                return;

            GameObject gameObject = current.gameObject;
            states.Add(new PreviewHideFlagState(gameObject, gameObject.hideFlags));
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            gameObject.hideFlags = source != null ? source.hideFlags : HideFlags.None;
            if (source != null)
            {
                var serializedGameObject = new SerializedObject(gameObject);
                SerializedProperty hideFlagsProperty = serializedGameObject.FindProperty("m_ObjectHideFlags");
                if (hideFlagsProperty != null
                    && HasPrefabPropertyModification(gameObject, source, hideFlagsProperty.propertyPath))
                {
                    PrefabUtility.RevertPropertyOverride(
                        hideFlagsProperty,
                        InteractionMode.AutomatedAction);
                }
            }
            for (int i = 0; i < current.childCount; i++)
                ReplaceWithSourceHideFlagsRecursive(current.GetChild(i), states);
        }

        private static bool HasPrefabPropertyModification(
            GameObject instance,
            GameObject source,
            string propertyPath)
        {
            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(instance);
            PropertyModification[] modifications = root == null
                ? null
                : PrefabUtility.GetPropertyModifications(root);
            if (modifications == null)
                return false;

            for (int i = 0; i < modifications.Length; i++)
            {
                PropertyModification modification = modifications[i];
                if (modification != null
                    && modification.target == source
                    && string.Equals(modification.propertyPath, propertyPath, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void RestoreHideFlags(List<PreviewHideFlagState> states)
        {
            for (int i = 0; i < states.Count; i++)
            {
                PreviewHideFlagState state = states[i];
                if (state.gameObject != null)
                    state.gameObject.hideFlags = state.hideFlags;
            }
        }

        private bool IsInsidePrefabTesterPreview()
        {
            Transform current = transform.parent;
            while (current != null)
            {
                EditorPreviewGameObjectSign marker = current.GetComponent<EditorPreviewGameObjectSign>();
                if (marker != null && string.Equals(marker.Owner, PreviewMarkerOwner, StringComparison.Ordinal))
                    return true;
                current = current.parent;
            }

            return false;
        }

        private static string GetSourceDependencyHash(GameObject prefab)
        {
            string sourcePath = prefab == null ? string.Empty : AssetDatabase.GetAssetPath(prefab);
            return string.IsNullOrEmpty(sourcePath)
                ? string.Empty
                : AssetDatabase.GetAssetDependencyHash(sourcePath).ToString();
        }

        private static bool IsSupportedPrefabAsset(GameObject prefab, out string reason)
        {
            if (prefab == null)
            {
                reason = "GUID 未绑定有效 Prefab，或源资产已经丢失。";
                return false;
            }

            if (!PrefabUtility.IsPartOfPrefabAsset(prefab))
            {
                reason = "所选资产不是 Prefab。";
                return false;
            }

            if (PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Model)
            {
                reason = "模型 Prefab 不支持此可应用修改的测试流程。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static bool CanMutatePreview()
        {
            return !Application.isPlaying
                   && !EditorApplication.isPlayingOrWillChangePlaymode
                   && !EditorApplication.isCompiling
                   && !EditorApplication.isUpdating;
        }

        private static void SetEditableDontSaveFlagsRecursive(Transform root)
        {
            if (root == null)
                return;

            root.gameObject.hideFlags = EditableDontSaveFlags;
            for (int i = 0; i < root.childCount; i++)
                SetEditableDontSaveFlagsRecursive(root.GetChild(i));
        }

        private readonly struct PreviewHideFlagState
        {
            public readonly GameObject gameObject;
            public readonly HideFlags hideFlags;

            public PreviewHideFlagState(GameObject gameObject, HideFlags hideFlags)
            {
                this.gameObject = gameObject;
                this.hideFlags = hideFlags;
            }
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ESPrefabTester))]
    internal sealed class ESPrefabTesterEditor : Editor
    {
        private static readonly string[] CreationModeLabels =
        {
            "自动维护",
            "手动创建"
        };

        private SerializedProperty prefabGuidProperty;
        private SerializedProperty createAutomaticallyProperty;
        private SerializedProperty previewParentProperty;
        private string resolvedGuid;
        private GameObject resolvedPrefab;

        private void OnEnable()
        {
            prefabGuidProperty = serializedObject.FindProperty("prefabGuid");
            createAutomaticallyProperty = serializedObject.FindProperty("createAutomatically");
            previewParentProperty = serializedObject.FindProperty("previewParent");
            RefreshResolvedPrefab();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            RefreshResolvedPrefab();

            ESPrefabTester tester = (ESPrefabTester)target;
            EditorGUILayout.LabelField("Prefab 测试器", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(tester.LastStatus, MessageType.Info);

            int currentCreationMode = createAutomaticallyProperty.boolValue ? 0 : 1;
            int selectedCreationMode = EditorGUILayout.Popup(
                "创建模式",
                currentCreationMode,
                CreationModeLabels);
            if (selectedCreationMode != currentCreationMode)
            {
                bool switchToAutomatic = selectedCreationMode == 0;
                string title = switchToAutomatic ? "切换为自动维护" : "切换为手动创建";
                string message = switchToAutomatic
                    ? "自动维护适合持续编辑 Prefab：绑定源、启用组件或 ReloadDomain 后会自动创建测试实例。\n\n"
                      + "若当前已绑定 Prefab 且没有实例，确认后会安排一次自动创建。"
                    : "手动创建适合受控检查：之后绑定源、启用组件或 ReloadDomain 都不会自动创建。\n\n"
                      + "当前已有实例会保留，仍可使用创建、重建、Apply、Revert 和清理按钮。";
                if (EditorUtility.DisplayDialog(title, message, "确认切换", "取消"))
                {
                    createAutomaticallyProperty.boolValue = switchToAutomatic;
                    serializedObject.ApplyModifiedProperties();
                    tester.HandleAutomaticCreationChanged();
                }
            }

            EditorGUILayout.HelpBox(
                createAutomaticallyProperty.boolValue
                    ? "自动维护：用于持续修改。编辑态会主动保持一个可编辑测试实例；进入 PlayMode 前仍会销毁。"
                    : "手动创建：用于受控检查。只有点击“创建实例/重建实例”才会生成；ReloadDomain 后保持为空。",
                MessageType.None);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                previewParentProperty,
                new GUIContent("实例父节点", "为空时使用当前 Tester。父节点必须位于同一已加载场景。"));
            if (EditorGUI.EndChangeCheck())
            {
                if (tester.ConfirmDiscardPreview("更改实例父节点"))
                {
                    serializedObject.ApplyModifiedProperties();
                    if (tester.PreviewInstance != null
                        || (tester.CreateAutomatically && resolvedPrefab != null))
                        tester.RebuildPreviewWithoutDiscardConfirmation();
                }
                else
                {
                    serializedObject.Update();
                }
            }

            EditorGUI.BeginChangeCheck();
            GameObject selectedPrefab = (GameObject)EditorGUILayout.ObjectField(
                "源 Prefab",
                resolvedPrefab,
                typeof(GameObject),
                false);
            if (EditorGUI.EndChangeCheck())
                SetPrefabSelection(selectedPrefab, tester);

            string assetPath = resolvedPrefab == null ? string.Empty : AssetDatabase.GetAssetPath(resolvedPrefab);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(assetPath)))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("资产路径");
                EditorGUILayout.SelectableLabel(assetPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(resolvedPrefab == null))
            {
                if (GUILayout.Button(tester.PreviewInstance == null ? "创建实例" : "重建实例"))
                    tester.RebuildPreview();
            }
            using (new EditorGUI.DisabledScope(tester.PreviewInstance == null))
            {
                if (GUILayout.Button("清理"))
                    tester.ClearPreview();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(tester.PreviewInstance == null))
            {
                if (GUILayout.Button("应用到源 Prefab"))
                    tester.ApplyToPrefab();
            }
            using (new EditorGUI.DisabledScope(tester.PreviewInstance == null))
            {
                if (GUILayout.Button("还原实例"))
                    tester.RevertPreview();
            }
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(resolvedPrefab == null))
            {
                if (GUILayout.Button("在 Project 中定位源 Prefab"))
                {
                    Selection.activeObject = resolvedPrefab;
                    EditorGUIUtility.PingObject(resolvedPrefab);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void SetPrefabSelection(GameObject prefab, ESPrefabTester tester)
        {
            string guid = string.Empty;
            if (prefab != null)
            {
                string path = AssetDatabase.GetAssetPath(prefab);
                if (string.IsNullOrEmpty(path) || !PrefabUtility.IsPartOfPrefabAsset(prefab))
                {
                    EditorUtility.DisplayDialog("Prefab 测试器", "只能选择 Project 中的 Prefab 资产。", "确定");
                    return;
                }

                guid = AssetDatabase.AssetPathToGUID(path);
            }

            if (!tester.ConfirmDiscardPreview("切换源 Prefab"))
                return;

            prefabGuidProperty.stringValue = guid;
            serializedObject.ApplyModifiedProperties();
            resolvedGuid = null;
            RefreshResolvedPrefab();

            if (prefab == null)
                tester.ClearPreviewWithoutDiscardConfirmation();
            else if (!tester.CreateAutomatically)
                tester.NotifyManualCreationReady();
            else
                tester.RebuildPreviewWithoutDiscardConfirmation();
        }

        private void RefreshResolvedPrefab()
        {
            if (prefabGuidProperty == null)
                return;

            string guid = prefabGuidProperty.stringValue ?? string.Empty;
            if (string.Equals(guid, resolvedGuid, StringComparison.Ordinal))
                return;

            resolvedGuid = guid;
            string path = string.IsNullOrEmpty(guid) ? string.Empty : AssetDatabase.GUIDToAssetPath(guid);
            resolvedPrefab = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
    }
#endif
}
