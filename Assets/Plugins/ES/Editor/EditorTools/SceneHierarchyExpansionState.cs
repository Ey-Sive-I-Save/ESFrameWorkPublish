#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ES;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 保存并恢复已加载场景中 GameObject 在 Hierarchy 面板里的展开状态。
/// 展开状态使用 ESGlobalProjectAssetGuideData；上次场景、SceneView 相机等轻量编辑器状态使用 EditorPrefs。
/// </summary>
public static class SceneHierarchyExpansionState
{
    // 最大记录层级。默认 5 层可以覆盖多数编辑需求，同时避免深层大场景产生过多路径。
    private const int MaxDepth = 5;

    // 自动恢复失败后的重试上限。Hierarchy 窗口刚创建时，内部 TreeView 可能还没初始化完成。
    private const int RetryLimit = 5;

    // 单次最多保存的展开对象数量。用于限制大场景中展开节点过多导致的保存/恢复开销。
    private const int MaxStoredExpandedObjects = 250;

    // 自动恢复前额外等待的 Editor Update 次数，用于避开场景刚打开时 Hierarchy 尚未稳定的阶段。
    private const int RestoreDelayTicks = 2;

    // 自动保存/加载开关。需要完全手动控制时，可以把这些常量改为 false。
    private const bool AutoSaveOnSceneSaving = true;
    private const bool AutoSaveBeforeSceneClosing = true;
    private const bool AutoLoadOnSceneOpened = true;
    private const bool AutoSaveBeforeAssemblyReload = true;
    private const bool AutoRestoreAfterPlayMode = true;
    private const bool AutoOpenLastSceneOnStartupDefault = true;
    private const bool AutoRestoreLastSelectedSceneObjectOnLastSceneOpen = true;
    private const bool AutoRestoreSceneViewCameraOnLastSceneOpen = true;
    private const int SceneViewCameraRecordMaxAgeDays = 30;
    private const float SceneViewCameraBaselineTimeoutSeconds = 5f;
    private static readonly bool LogTiming = true;

    private const string MenuRoot = MenuItemPathDefine.SCENE_TOOLS_PATH + "层级展开/";
    private const string StoragePrefix = "Standalone.SceneHierarchyExpansionState.";
    private const string LastOpenedScenePathKeyPrefix = StoragePrefix + "LastOpenedScenePath.";
    private const string LastOpenedSceneAutoOpenedKeyPrefix = StoragePrefix + "LastOpenedSceneAutoOpened.";
    private const string AutoOpenLastSceneOnStartupKeyPrefix = StoragePrefix + "AutoOpenLastSceneOnStartup.";
    private const string SceneViewCameraStateKeyPrefix = StoragePrefix + "SceneViewCamera.";

    private static int restoreRetryCount;
    private static int pendingRestoreDelayTicks;
    private static bool restoreScheduled;
    private static int lastSceneOpenRetryCount;
    private static int lastSceneOpenDelayTicks;
    private static bool lastSceneOpenScheduled;
    private static string pendingLastScenePath = string.Empty;
    private static bool restoreSelectionAfterLastSceneOpen;
    private static bool selectionRestoreScheduled;
    private static int selectionRestoreRetryCount;
    private static int selectionRestoreDelayTicks;
    private static ESEditorRememberedObjectTarget<GameObject> lastSelectedSceneObject;
    private static bool restoreCameraAfterLastSceneOpen;
    private static bool cameraRestoreScheduled;
    private static int cameraRestoreRetryCount;
    private static int cameraRestoreDelayTicks;
    private static bool hasPendingSceneViewCameraBaseline;
    private static Scene pendingSceneViewCameraBaselineScene;
    private static double sceneViewCameraBaselineStartedAt;

    internal static void RegisterEditorCallbacks()
    {
        if (AutoSaveOnSceneSaving)
        {
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaving += OnSceneSaving;
        }

        if (AutoLoadOnSceneOpened)
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChangedInEditMode;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChangedInEditMode;

        if (AutoSaveBeforeSceneClosing)
        {
            EditorSceneManager.sceneClosing -= OnSceneClosing;
            EditorSceneManager.sceneClosing += OnSceneClosing;
        }

        if (AutoSaveBeforeAssemblyReload)
        {
            AssemblyReloadEvents.beforeAssemblyReload -= SaveLoadedScenesExpansionState;
            AssemblyReloadEvents.beforeAssemblyReload += SaveLoadedScenesExpansionState;
            AssemblyReloadEvents.beforeAssemblyReload -= RememberActiveSceneViewCameraState;
            AssemblyReloadEvents.beforeAssemblyReload += RememberActiveSceneViewCameraState;
            AssemblyReloadEvents.beforeAssemblyReload -= CancelSceneViewCameraBaseline;
            AssemblyReloadEvents.beforeAssemblyReload += CancelSceneViewCameraBaseline;
        }

        if (AutoRestoreAfterPlayMode)
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        EditorApplication.quitting -= SaveLoadedScenesExpansionState;
        EditorApplication.quitting += SaveLoadedScenesExpansionState;
        EditorApplication.quitting -= RememberActiveSceneAsLastOpened;
        EditorApplication.quitting += RememberActiveSceneAsLastOpened;
        EditorApplication.quitting -= RememberActiveSceneViewCameraState;
        EditorApplication.quitting += RememberActiveSceneViewCameraState;
        EditorApplication.quitting -= CancelSceneViewCameraBaseline;
        EditorApplication.quitting += CancelSceneViewCameraBaseline;
        Selection.selectionChanged -= RememberLastSelectedSceneObject;
        Selection.selectionChanged += RememberLastSelectedSceneObject;
        EditorApplication.delayCall -= ScheduleInitialRestoreLoadedScenes;
        EditorApplication.delayCall += ScheduleInitialRestoreLoadedScenes;
    }

    private static void ScheduleInitialRestoreLoadedScenes()
    {
        TryScheduleAutoOpenLastSceneOnStartup();
        ScheduleRestoreLoadedScenes(RestoreDelayTicks);
    }

    [MenuItem(MenuRoot + "打开上次打开场景", false, 5)]
    public static void OpenLastOpenedScene()
    {
        string lastScenePath = GetLastOpenedScenePath();
        if (string.IsNullOrWhiteSpace(lastScenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(lastScenePath) == null)
        {
            Debug.LogWarning("[SceneHierarchyExpansionState] 未找到上次打开的场景记录。");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        RememberActiveSceneViewCameraState();
        restoreSelectionAfterLastSceneOpen = AutoRestoreLastSelectedSceneObjectOnLastSceneOpen;
        restoreCameraAfterLastSceneOpen = AutoRestoreSceneViewCameraOnLastSceneOpen;
        try
        {
            EditorSceneManager.OpenScene(lastScenePath, OpenSceneMode.Single);
        }
        catch
        {
            restoreSelectionAfterLastSceneOpen = false;
            restoreCameraAfterLastSceneOpen = false;
            throw;
        }
    }

    [MenuItem(MenuRoot + "打开上次打开场景", true, 5)]
    private static bool ValidateOpenLastOpenedScene()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem(MenuRoot + "恢复上次选中对象", false, 6)]
    public static void RestoreLastSelectedSceneObject()
    {
        if (GetLastSelectedSceneObjectTarget().TryResolve(out GameObject target) && target != null)
            Selection.activeGameObject = target;
    }

    [MenuItem(MenuRoot + "恢复上次选中对象", true, 6)]
    private static bool ValidateRestoreLastSelectedSceneObject()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem(MenuRoot + "保存当前 SceneView 相机位置", false, 7)]
    public static void SaveCurrentSceneViewCameraState()
    {
        RememberActiveSceneViewCameraState();
        Debug.Log("[SceneHierarchyExpansionState] 已保存当前场景 SceneView 相机位置。");
    }

    [MenuItem(MenuRoot + "保存当前 SceneView 相机位置", true, 7)]
    private static bool ValidateSaveCurrentSceneViewCameraState()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem(MenuRoot + "恢复当前场景 SceneView 相机位置", false, 8)]
    public static void RestoreCurrentSceneViewCameraState()
    {
        if (!RestoreActiveSceneViewCameraNow())
            Debug.LogWarning("[SceneHierarchyExpansionState] 当前场景没有可恢复的 SceneView 相机位置。");
    }

    [MenuItem(MenuRoot + "恢复当前场景 SceneView 相机位置", true, 8)]
    private static bool ValidateRestoreCurrentSceneViewCameraState()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem(MenuRoot + "启动时自动打开上次场景", false, 4)]
    public static void ToggleAutoOpenLastSceneOnStartup()
    {
        bool enabled = !GetAutoOpenLastSceneOnStartup();
        EditorPrefs.SetBool(GetAutoOpenLastSceneOnStartupKey(), enabled);
        Debug.Log("[SceneHierarchyExpansionState] 启动时自动打开上次场景：" + (enabled ? "已开启" : "已关闭"));
    }

    [MenuItem(MenuRoot + "启动时自动打开上次场景", true, 4)]
    private static bool ValidateToggleAutoOpenLastSceneOnStartup()
    {
        return !Application.isBatchMode;
    }

    public class SceneHierarchyExpansionStateInitializer : EditorInvoker_Level2
    {
        public override void InitInvoke()
        {
            RegisterEditorCallbacks();
        }
    }

    [MenuItem(MenuRoot + "保存已加载场景展开状态", false, 0)]
    public static void SaveLoadedScenesExpansionState()
    {
        double totalStart = EditorApplication.timeSinceStartup;
        double readExpandedStart = EditorApplication.timeSinceStartup;

        // Unity 没有公开 Hierarchy 展开状态 API，这里通过反射读取当前展开的 InstanceID。
        var expandedIds = SceneHierarchyReflection.GetExpandedInstanceIds();
        double readExpandedMs = ToMilliseconds(EditorApplication.timeSinceStartup - readExpandedStart);
        if (expandedIds.Count == 0)
        {
            LogSaveTiming(0, 0, 0, expandedIds.Count, readExpandedMs, ToMilliseconds(EditorApplication.timeSinceStartup - totalStart));
            return;
        }

        int storedSceneCount = 0;
        int scannedTransformCount = 0;
        int storedExpandedCount = 0;

        // 分场景保存，避免多场景编辑时互相污染。
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!CanStoreScene(scene))
                continue;

            var data = new SceneExpansionData();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (data.expandedTransformPaths.Count >= MaxStoredExpandedObjects)
                    break;

                CollectExpandedPaths(root.transform, expandedIds, data.expandedTransformPaths, ref scannedTransformCount);
            }

            data.expandedTransformPaths.Sort(StringComparer.Ordinal);

            SaveSceneExpansionData(scene, data);
            storedSceneCount++;
            storedExpandedCount += data.expandedTransformPaths.Count;
        }

        LogSaveTiming(
            storedSceneCount,
            scannedTransformCount,
            storedExpandedCount,
            expandedIds.Count,
            readExpandedMs,
            ToMilliseconds(EditorApplication.timeSinceStartup - totalStart));
    }

    [MenuItem(MenuRoot + "恢复已加载场景展开状态", false, 10)]
    public static void LoadLoadedScenesExpansionState()
    {
        restoreRetryCount = 0;
        ScheduleRestoreLoadedScenes(0);
    }

    [MenuItem(MenuRoot + "清除已加载场景展开记录", false, 20)]
    public static void ClearLoadedScenesSavedState()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (CanStoreScene(scene))
            {
                EditorPrefs.DeleteKey(GetStorageKey(scene));
                ClearSceneExpansionData(scene);
            }
        }

        SaveProjectGuideDataIfNeeded();
        Debug.Log("[SceneHierarchyExpansionState] Cleared saved hierarchy expansion state for loaded scenes.");
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        RememberLastOpenedScenePath(path);
        RememberSceneViewCameraState(scene);
        SaveLoadedScenesExpansionState();
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        RememberLastOpenedScene(scene);
        if (restoreSelectionAfterLastSceneOpen)
        {
            restoreSelectionAfterLastSceneOpen = false;
            ScheduleRestoreLastSelectedSceneObject();
        }
        if (restoreCameraAfterLastSceneOpen)
        {
            restoreCameraAfterLastSceneOpen = false;
            ScheduleRestoreSceneViewCamera();
        }
        restoreRetryCount = 0;
        ScheduleRestoreLoadedScenes(RestoreDelayTicks);
    }

    private static void OnActiveSceneChangedInEditMode(Scene previousActiveScene, Scene newActiveScene)
    {
        SaveSceneViewCameraState(previousActiveScene);
        ScheduleSceneViewCameraBaseline(newActiveScene);
    }

    private static void OnSceneClosing(Scene scene, bool removingScene)
    {
        RememberSceneViewCameraState(scene);
        SaveLoadedScenesExpansionState();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            // SceneView 基线只允许由编辑模式下场景切换后的首帧建立。
            // 若此时尚未绘制就进入 PlayMode，必须撤销待捕获回调，
            // 避免把运行中的 SceneView 相机误保存为编辑器基线。
            CancelSceneViewCameraBaseline();
            RememberActiveSceneViewCameraState();
            SaveLoadedScenesExpansionState();
            return;
        }

        if (state == PlayModeStateChange.EnteredEditMode)
        {
            restoreRetryCount = 0;
            ScheduleRestoreLoadedScenes(RestoreDelayTicks);
        }
    }

    private static void ScheduleRestoreLoadedScenes(int delayTicks)
    {
        // 合并短时间内的重复恢复请求，并从最后一次请求后重新等待。
        pendingRestoreDelayTicks = Mathf.Max(0, delayTicks);

        if (restoreScheduled)
            return;

        restoreScheduled = true;
        EditorApplication.update += RestoreLoadedScenesWhenReady;
    }

    private static void RestoreLoadedScenesWhenReady()
    {
        if (pendingRestoreDelayTicks > 0)
        {
            pendingRestoreDelayTicks--;
            return;
        }

        // 编译、资源刷新、播放模式切换期间不应用，避免和 Unity 自身重建 Hierarchy 的时机冲突。
        if (!IsEditorReadyForRestore())
        {
            RetryRestore();
            return;
        }

        // 如果 Hierarchy 内部对象还没准备好，延迟到后续 editor tick 再试。
        if (!SceneHierarchyReflection.CanSetExpandedState)
        {
            RetryRestore();
            return;
        }

        EditorApplication.update -= RestoreLoadedScenesWhenReady;
        restoreScheduled = false;

        double totalStart = EditorApplication.timeSinceStartup;
        double resolveMs = 0d;
        double applyMs = 0d;
        int loadedSceneCount = 0;
        int candidatePathCount = 0;
        int resolvedPathCount = 0;
        int restoredCount = 0;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!CanStoreScene(scene))
                continue;

            if (!TryLoadSceneExpansionData(scene, out SceneExpansionData data))
                continue;

            loadedSceneCount++;
            candidatePathCount += data.expandedTransformPaths.Count;

            // 先恢复浅层，再恢复深层，避免父节点未展开时子节点恢复失败或不可见。
            data.expandedTransformPaths.Sort(ComparePathDepthThenName);
            foreach (string transformPath in data.expandedTransformPaths)
            {
                double resolveStart = EditorApplication.timeSinceStartup;
                var transform = ResolveTransformPath(scene, transformPath);
                resolveMs += ToMilliseconds(EditorApplication.timeSinceStartup - resolveStart);
                if (transform == null)
                    continue;

                resolvedPathCount++;

                double applyStart = EditorApplication.timeSinceStartup;
                if (SceneHierarchyReflection.SetExpanded(transform.gameObject.GetInstanceID(), true))
                    restoredCount++;
                applyMs += ToMilliseconds(EditorApplication.timeSinceStartup - applyStart);
            }
        }

        if (restoredCount == 0 && restoreRetryCount < RetryLimit)
        {
            RetryRestore();
            return;
        }

        EditorApplication.RepaintHierarchyWindow();
        restoreRetryCount = 0;

        LogRestoreTiming(
            loadedSceneCount,
            candidatePathCount,
            resolvedPathCount,
            restoredCount,
            resolveMs,
            applyMs,
            ToMilliseconds(EditorApplication.timeSinceStartup - totalStart));
    }

    private static void RetryRestore()
    {
        restoreRetryCount++;
        if (restoreRetryCount <= RetryLimit)
            ScheduleRestoreLoadedScenes(RestoreDelayTicks);
        else
        {
            EditorApplication.update -= RestoreLoadedScenesWhenReady;
            restoreScheduled = false;
            pendingRestoreDelayTicks = 0;
        }
    }

    private static bool IsEditorReadyForRestore()
    {
        return !EditorApplication.isCompiling
            && !EditorApplication.isUpdating
            && !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static void TryScheduleAutoOpenLastSceneOnStartup()
    {
        if (!GetAutoOpenLastSceneOnStartup() || Application.isBatchMode)
            return;

        if (SessionState.GetBool(GetLastOpenedSceneAutoOpenedSessionKey(), false))
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(activeScene.path))
        {
            SessionState.SetBool(GetLastOpenedSceneAutoOpenedSessionKey(), true);
            return;
        }

        string lastScenePath = GetLastOpenedScenePath();
        if (string.IsNullOrWhiteSpace(lastScenePath))
        {
            SessionState.SetBool(GetLastOpenedSceneAutoOpenedSessionKey(), true);
            return;
        }

        ScheduleOpenLastScene(lastScenePath);
    }

    private static void ScheduleOpenLastScene(string scenePath)
    {
        if (lastSceneOpenScheduled)
            return;

        lastSceneOpenScheduled = true;
        pendingLastScenePath = scenePath;
        lastSceneOpenRetryCount = 0;
        lastSceneOpenDelayTicks = RestoreDelayTicks;
        EditorApplication.update += TryOpenLastSceneWhenReady;
    }

    private static void TryOpenLastSceneWhenReady()
    {
        if (lastSceneOpenDelayTicks > 0)
        {
            lastSceneOpenDelayTicks--;
            return;
        }

        if (!IsEditorReadyForRestore())
        {
            RetryOpenLastScene();
            return;
        }

        EditorApplication.update -= TryOpenLastSceneWhenReady;
        lastSceneOpenScheduled = false;

        try
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(pendingLastScenePath) == null)
                throw new InvalidOperationException("上次场景资产已不存在：" + pendingLastScenePath);

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                SessionState.SetBool(GetLastOpenedSceneAutoOpenedSessionKey(), true);
                return;
            }

            RememberActiveSceneViewCameraState();
            restoreSelectionAfterLastSceneOpen = AutoRestoreLastSelectedSceneObjectOnLastSceneOpen;
            restoreCameraAfterLastSceneOpen = AutoRestoreSceneViewCameraOnLastSceneOpen;
            EditorSceneManager.OpenScene(pendingLastScenePath, OpenSceneMode.Single);
        }
        catch (Exception exception)
        {
            restoreSelectionAfterLastSceneOpen = false;
            restoreCameraAfterLastSceneOpen = false;
            Debug.LogWarning("[SceneHierarchyExpansionState] 自动打开上次场景失败：" + exception.Message);
        }
        finally
        {
            pendingLastScenePath = string.Empty;
            SessionState.SetBool(GetLastOpenedSceneAutoOpenedSessionKey(), true);
        }
    }

    private static void RetryOpenLastScene()
    {
        lastSceneOpenRetryCount++;
        if (lastSceneOpenRetryCount <= RetryLimit)
        {
            lastSceneOpenDelayTicks = RestoreDelayTicks;
            return;
        }

        EditorApplication.update -= TryOpenLastSceneWhenReady;
        lastSceneOpenScheduled = false;
        pendingLastScenePath = string.Empty;
        SessionState.SetBool(GetLastOpenedSceneAutoOpenedSessionKey(), true);
    }

    private static void CollectExpandedPaths(Transform transform, HashSet<int> expandedIds, List<string> paths, ref int scannedTransformCount)
    {
        if (transform == null)
            return;

        scannedTransformCount++;

        // 超过限制层级就不继续递归，控制保存和恢复成本。
        if (GetDepth(transform) > MaxDepth)
            return;

        if (transform.childCount > 0 && expandedIds.Contains(transform.gameObject.GetInstanceID()))
        {
            if (paths.Count >= MaxStoredExpandedObjects)
                return;

            paths.Add(BuildTransformPath(transform));
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            if (paths.Count >= MaxStoredExpandedObjects)
                break;

            CollectExpandedPaths(transform.GetChild(i), expandedIds, paths, ref scannedTransformCount);
        }
    }

    private static void LogSaveTiming(
        int sceneCount,
        int scannedTransformCount,
        int storedExpandedCount,
        int editorExpandedIdCount,
        double readExpandedMs,
        double totalMs)
    {
        if (!LogTiming)
            return;

        Debug.Log(
            $"[SceneHierarchyExpansionState] Save timing: total={totalMs:F2}ms, readExpandedIds={readExpandedMs:F2}ms, " +
            $"scenes={sceneCount}, scannedObjects={scannedTransformCount}, savedExpanded={storedExpandedCount}, " +
            $"editorExpandedIds={editorExpandedIdCount}, maxSaved={MaxStoredExpandedObjects}, maxDepth={MaxDepth}.");
    }

    private static void LogRestoreTiming(
        int sceneCount,
        int candidatePathCount,
        int resolvedPathCount,
        int restoredCount,
        double resolveMs,
        double applyMs,
        double totalMs)
    {
        if (!LogTiming)
            return;

        Debug.Log(
            $"[SceneHierarchyExpansionState] Restore timing: total={totalMs:F2}ms, resolvePaths={resolveMs:F2}ms, applyExpanded={applyMs:F2}ms, " +
            $"scenes={sceneCount}, savedPaths={candidatePathCount}, resolvedPaths={resolvedPathCount}, restored={restoredCount}, " +
            $"retry={restoreRetryCount}/{RetryLimit}.");
    }

    private static double ToMilliseconds(double seconds)
    {
        return seconds * 1000d;
    }

    private static string BuildTransformPath(Transform transform)
    {
        var segments = new List<string>(MaxDepth + 1);
        var current = transform;

        while (current != null)
        {
            segments.Add(BuildPathSegment(current));
            current = current.parent;
        }

        segments.Reverse();
        return string.Join("/", segments);
    }

    private static string BuildPathSegment(Transform transform)
    {
        int sameNameIndex = GetSameNameIndex(transform);
        int siblingIndex = transform.GetSiblingIndex();

        // name + 同名序号 + siblingIndex 共同组成完整路径段。恢复时必须全部匹配，不做模糊降级。
        return Uri.EscapeDataString(transform.name) + "#" + sameNameIndex + "@" + siblingIndex;
    }

    private static Transform ResolveTransformPath(Scene scene, string transformPath)
    {
        if (string.IsNullOrEmpty(transformPath))
            return null;

        string[] segments = transformPath.Split('/');
        if (segments.Length == 0 || segments.Length > MaxDepth + 1)
            return null;

        Transform current = null;
        var roots = scene.GetRootGameObjects();

        for (int i = 0; i < segments.Length; i++)
        {
            if (!TryParsePathSegment(segments[i], out string name, out int sameNameIndex, out int siblingIndex))
                return null;

            // 严格按完整路径段匹配，避免同名对象被误展开。
            current = i == 0
                ? FindRoot(roots, name, sameNameIndex, siblingIndex)
                : FindChild(current, name, sameNameIndex, siblingIndex);

            if (current == null)
                return null;
        }

        return current;
    }

    private static Transform FindRoot(GameObject[] roots, string name, int sameNameIndex, int siblingIndex)
    {
        if (siblingIndex < 0 || siblingIndex >= roots.Length)
            return null;

        var rootAtSibling = roots[siblingIndex];
        if (rootAtSibling == null || rootAtSibling.name != name)
            return null;

        int seenSameName = 0;
        for (int i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root == null || root.name != name)
                continue;

            if (root == rootAtSibling)
                return seenSameName == sameNameIndex ? root.transform : null;

            seenSameName++;
        }

        return null;
    }

    private static Transform FindChild(Transform parent, string name, int sameNameIndex, int siblingIndex)
    {
        if (parent == null)
            return null;

        if (siblingIndex < 0 || siblingIndex >= parent.childCount)
            return null;

        var childAtSibling = parent.GetChild(siblingIndex);
        if (childAtSibling == null || childAtSibling.name != name)
            return null;

        int seenSameName = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name != name)
                continue;

            if (child == childAtSibling)
                return seenSameName == sameNameIndex ? child : null;

            seenSameName++;
        }

        return null;
    }

    private static bool TryParsePathSegment(string segment, out string name, out int sameNameIndex, out int siblingIndex)
    {
        name = string.Empty;
        sameNameIndex = 0;
        siblingIndex = -1;

        int hashIndex = segment.LastIndexOf('#');
        int atIndex = segment.LastIndexOf('@');
        if (hashIndex <= 0 || atIndex <= hashIndex)
            return false;

        name = Uri.UnescapeDataString(segment.Substring(0, hashIndex));
        return int.TryParse(segment.Substring(hashIndex + 1, atIndex - hashIndex - 1), out sameNameIndex)
            && int.TryParse(segment.Substring(atIndex + 1), out siblingIndex);
    }

    private static int GetSameNameIndex(Transform transform)
    {
        int index = 0;

        if (transform.parent == null)
        {
            var roots = transform.gameObject.scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == transform.gameObject)
                    return index;

                if (root != null && root.name == transform.name)
                    index++;
            }

            return index;
        }

        for (int i = 0; i < transform.parent.childCount; i++)
        {
            var child = transform.parent.GetChild(i);
            if (child == transform)
                return index;

            if (child.name == transform.name)
                index++;
        }

        return index;
    }

    private static int GetDepth(Transform transform)
    {
        int depth = 0;
        var current = transform;
        while (current.parent != null)
        {
            depth++;
            current = current.parent;
        }

        return depth;
    }

    private static bool CanStoreScene(Scene scene)
    {
        return scene.IsValid() && scene.isLoaded && !string.IsNullOrEmpty(scene.path);
    }

    private static string GetStorageKey(Scene scene)
    {
        string sceneId = AssetDatabase.AssetPathToGUID(scene.path);
        if (string.IsNullOrEmpty(sceneId))
            sceneId = scene.path;

        // 同一个工程复制到不同目录时，project hash 可以避免 EditorPrefs 键冲突。
        return StoragePrefix + GetProjectHash() + "." + sceneId;
    }

    private static string GetSceneGuid(Scene scene)
    {
        string sceneGuid = AssetDatabase.AssetPathToGUID(scene.path);
        return string.IsNullOrEmpty(sceneGuid) ? scene.path : sceneGuid;
    }

    private static void SaveSceneExpansionData(Scene scene, SceneExpansionData data)
    {
        // This method also runs from AssemblyReloadEvents.beforeAssemblyReload.
        // Creating an asset there fires a ProjectBrowser refresh while Unity is rebuilding its TreeView,
        // which can produce a Unity-internal NullReferenceException. Persist only when the global data
        // asset already exists; normal editor commands create it on a safe event-loop turn.
        if (!ESGlobalProjectAssetGuideData.TryFindExistingData(out ESGlobalProjectAssetGuideData globalData)
            || globalData == null)
            return;

        globalData.SetSceneExpansion(GetSceneGuid(scene), scene.path, data.expandedTransformPaths);
        if (globalData.saveSceneExpansionAssetImmediately)
            SaveProjectGuideDataIfNeeded();
    }

    private static bool TryLoadSceneExpansionData(Scene scene, out SceneExpansionData data)
    {
        data = null;
        string sceneGuid = GetSceneGuid(scene);

        if (ESGlobalProjectAssetGuideData.TryFindExistingData(out ESGlobalProjectAssetGuideData globalData)
            && globalData.TryGetSceneExpansion(sceneGuid, out ESGlobalProjectAssetGuideData.SceneHierarchyExpansionRecord record)
            && record != null
            && record.expandedTransformPaths != null)
        {
            data = new SceneExpansionData();
            data.expandedTransformPaths.AddRange(record.expandedTransformPaths);
            return true;
        }

        string json = EditorPrefs.GetString(GetStorageKey(scene), string.Empty);
        if (string.IsNullOrEmpty(json))
            return false;

        data = JsonUtility.FromJson<SceneExpansionData>(json);
        if (data == null || data.expandedTransformPaths == null)
            return false;

        SaveSceneExpansionData(scene, data);
        return true;
    }

    private static void ClearSceneExpansionData(Scene scene)
    {
        if (!ESGlobalProjectAssetGuideData.TryFindExistingData(out ESGlobalProjectAssetGuideData globalData) || globalData == null)
            return;

        globalData.ClearSceneExpansion(GetSceneGuid(scene));
    }

    private static void SaveProjectGuideDataIfNeeded()
    {
        if (!ESGlobalProjectAssetGuideData.TryFindExistingData(out ESGlobalProjectAssetGuideData globalData) || globalData == null)
            return;

        EditorUtility.SetDirty(globalData);
        AssetDatabase.SaveAssetIfDirty(globalData);
    }

    private static string GetProjectHash()
    {
        using (var md5 = MD5.Create())
        {
            byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(Application.dataPath));
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
                builder.Append(value.ToString("x2"));

            return builder.ToString();
        }
    }

    private static string GetLastOpenedScenePathKey()
    {
        return LastOpenedScenePathKeyPrefix + GetProjectHash();
    }

    private static string GetLastOpenedSceneAutoOpenedSessionKey()
    {
        return LastOpenedSceneAutoOpenedKeyPrefix + GetProjectHash();
    }

    private static string GetLastOpenedScenePath()
    {
        return EditorPrefs.GetString(GetLastOpenedScenePathKey(), string.Empty);
    }

    private static bool GetAutoOpenLastSceneOnStartup()
    {
        return EditorPrefs.GetBool(GetAutoOpenLastSceneOnStartupKey(), AutoOpenLastSceneOnStartupDefault);
    }

    private static string GetAutoOpenLastSceneOnStartupKey()
    {
        return AutoOpenLastSceneOnStartupKeyPrefix + GetProjectHash();
    }

    private static void RememberLastOpenedScene(Scene scene)
    {
        if (CanStoreScene(scene))
            EditorPrefs.SetString(GetLastOpenedScenePathKey(), scene.path);
    }

    private static void RememberLastOpenedScenePath(string scenePath)
    {
        if (!string.IsNullOrWhiteSpace(scenePath))
            EditorPrefs.SetString(GetLastOpenedScenePathKey(), scenePath);
    }

    private static void RememberActiveSceneAsLastOpened()
    {
        RememberLastOpenedScene(SceneManager.GetActiveScene());
    }

    private static ESEditorRememberedObjectTarget<GameObject> GetLastSelectedSceneObjectTarget()
    {
        if (lastSelectedSceneObject == null)
        {
            lastSelectedSceneObject = new ESEditorRememberedObjectTarget<GameObject>(
                StoragePrefix + "LastSelectedSceneObject." + GetProjectHash(),
                ESEditorRememberedTargetFallbackStrategy.SceneAndPath,
                30);
        }

        return lastSelectedSceneObject;
    }

    private static void RememberLastSelectedSceneObject()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null || EditorUtility.IsPersistent(selected))
            return;

        Scene scene = selected.scene;
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        GetLastSelectedSceneObjectTarget().Remember(selected);
    }

    private static void ScheduleRestoreLastSelectedSceneObject()
    {
        if (selectionRestoreScheduled)
            return;

        selectionRestoreScheduled = true;
        selectionRestoreRetryCount = 0;
        selectionRestoreDelayTicks = RestoreDelayTicks;
        EditorApplication.update += RestoreLastSelectedSceneObjectWhenReady;
    }

    private static void RestoreLastSelectedSceneObjectWhenReady()
    {
        if (selectionRestoreDelayTicks > 0)
        {
            selectionRestoreDelayTicks--;
            return;
        }

        if (!IsEditorReadyForRestore())
        {
            RetryRestoreLastSelectedSceneObject();
            return;
        }

        EditorApplication.update -= RestoreLastSelectedSceneObjectWhenReady;
        selectionRestoreScheduled = false;

        if (GetLastSelectedSceneObjectTarget().TryResolve(out GameObject target) && target != null)
            Selection.activeGameObject = target;
    }

    private static void RetryRestoreLastSelectedSceneObject()
    {
        selectionRestoreRetryCount++;
        if (selectionRestoreRetryCount <= RetryLimit)
        {
            selectionRestoreDelayTicks = RestoreDelayTicks;
            return;
        }

        EditorApplication.update -= RestoreLastSelectedSceneObjectWhenReady;
        selectionRestoreScheduled = false;
    }

    private static string GetSceneViewCameraStateKey(Scene scene)
    {
        return SceneViewCameraStateKeyPrefix + GetProjectHash() + "." + GetSceneGuid(scene);
    }

    private static bool IsSceneActive(Scene scene)
    {
        return scene.IsValid() && scene.isLoaded && scene == SceneManager.GetActiveScene();
    }

    private static void RememberSceneViewCameraState(Scene scene)
    {
        if (!IsSceneActive(scene))
            return;

        SaveSceneViewCameraState(scene);
    }

    private static void SaveSceneViewCameraState(Scene scene)
    {
        SaveSceneViewCameraState(scene, SceneView.lastActiveSceneView);
    }

    private static void SaveSceneViewCameraState(Scene scene, SceneView sceneView)
    {
        if (!CanStoreScene(scene))
            return;

        if (sceneView == null || sceneView.camera == null)
            return;

        var record = new SceneViewCameraRecord
        {
            scenePath = scene.path,
            savedUtcTicks = DateTime.UtcNow.Ticks,
            pivot = sceneView.pivot,
            rotation = sceneView.rotation,
            size = sceneView.size,
            isOrtho = sceneView.orthographic,
            fieldOfView = sceneView.cameraSettings.fieldOfView
        };

        EditorPrefs.SetString(GetSceneViewCameraStateKey(scene), JsonUtility.ToJson(record));
    }

    private static void ScheduleSceneViewCameraBaseline(Scene scene)
    {
        CancelSceneViewCameraBaseline();

        if (!CanStoreScene(scene) || TryGetSceneViewCameraRecord(scene, out _))
            return;

        // 等活动场景真正进入第一个 SceneView 绘制帧后再记基线，避免把切换前的旧视角写进新场景。
        pendingSceneViewCameraBaselineScene = scene;
        hasPendingSceneViewCameraBaseline = true;
        sceneViewCameraBaselineStartedAt = EditorApplication.timeSinceStartup;
        SceneView.duringSceneGui -= CaptureSceneViewCameraBaseline;
        SceneView.duringSceneGui += CaptureSceneViewCameraBaseline;
        EditorApplication.update -= CheckSceneViewCameraBaselineTimeout;
        EditorApplication.update += CheckSceneViewCameraBaselineTimeout;
    }

    private static void CaptureSceneViewCameraBaseline(SceneView sceneView)
    {
        if (!hasPendingSceneViewCameraBaseline
            || !IsSceneActive(pendingSceneViewCameraBaselineScene)
            || (SceneView.lastActiveSceneView != null && sceneView != SceneView.lastActiveSceneView))
            return;

        if (!TryGetSceneViewCameraRecord(pendingSceneViewCameraBaselineScene, out _))
            SaveSceneViewCameraState(pendingSceneViewCameraBaselineScene, sceneView);

        CancelSceneViewCameraBaseline();
    }

    private static void CancelSceneViewCameraBaseline()
    {
        SceneView.duringSceneGui -= CaptureSceneViewCameraBaseline;
        EditorApplication.update -= CheckSceneViewCameraBaselineTimeout;
        hasPendingSceneViewCameraBaseline = false;
        pendingSceneViewCameraBaselineScene = default;
    }

    private static void CheckSceneViewCameraBaselineTimeout()
    {
        if (!hasPendingSceneViewCameraBaseline)
        {
            EditorApplication.update -= CheckSceneViewCameraBaselineTimeout;
            return;
        }

        if (EditorApplication.timeSinceStartup - sceneViewCameraBaselineStartedAt < SceneViewCameraBaselineTimeoutSeconds)
            return;

        bool hasSceneView = SceneView.sceneViews != null && SceneView.sceneViews.Count > 0;
        CancelSceneViewCameraBaseline();
        if (hasSceneView)
        {
            // A SceneView can exist without receiving a draw callback while Unity is
            // restoring layouts, entering PlayMode, or the view is hidden behind a
            // different dock. This is an expected, retryable condition rather than
            // a user-facing warning; keep the cancellation observable without
            // making the Console look like the scene or camera state is corrupted.
            Debug.Log("[SceneHierarchyExpansionState] 当前未收到 SceneView 绘制帧，已取消本次相机基线捕获；下次场景视图绘制时将重试。");
        }
    }

    private static void RememberActiveSceneViewCameraState()
    {
        RememberSceneViewCameraState(SceneManager.GetActiveScene());
    }

    private static bool TryGetSceneViewCameraRecord(Scene scene, out SceneViewCameraRecord record)
    {
        record = null;
        if (!CanStoreScene(scene))
            return false;

        string json = EditorPrefs.GetString(GetSceneViewCameraStateKey(scene), string.Empty);
        if (string.IsNullOrEmpty(json))
            return false;

        record = JsonUtility.FromJson<SceneViewCameraRecord>(json);
        if (record == null || record.savedUtcTicks <= 0)
            return false;

        DateTime savedUtc = new DateTime(record.savedUtcTicks, DateTimeKind.Utc);
        return SceneViewCameraRecordMaxAgeDays <= 0
            || DateTime.UtcNow - savedUtc <= TimeSpan.FromDays(SceneViewCameraRecordMaxAgeDays);
    }

    private static bool RestoreActiveSceneViewCameraNow()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!TryGetSceneViewCameraRecord(activeScene, out SceneViewCameraRecord record))
            return false;

        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
            return false;

        sceneView.pivot = record.pivot;
        sceneView.rotation = record.rotation;
        sceneView.size = record.size;
        if (sceneView.orthographic != record.isOrtho)
            sceneView.orthographic = record.isOrtho;
        if (record.fieldOfView > 0f)
            sceneView.cameraSettings.fieldOfView = record.fieldOfView;

        SceneView.RepaintAll();
        return true;
    }

    private static void ScheduleRestoreSceneViewCamera()
    {
        if (cameraRestoreScheduled)
            return;

        cameraRestoreScheduled = true;
        cameraRestoreRetryCount = 0;
        cameraRestoreDelayTicks = RestoreDelayTicks;
        EditorApplication.update += RestoreSceneViewCameraWhenReady;
    }

    private static void RestoreSceneViewCameraWhenReady()
    {
        if (cameraRestoreDelayTicks > 0)
        {
            cameraRestoreDelayTicks--;
            return;
        }

        if (!IsEditorReadyForRestore())
        {
            RetryRestoreSceneViewCamera();
            return;
        }

        EditorApplication.update -= RestoreSceneViewCameraWhenReady;
        cameraRestoreScheduled = false;

        if (!RestoreActiveSceneViewCameraNow())
            Debug.LogWarning("[SceneHierarchyExpansionState] 当前场景没有可恢复的 SceneView 相机位置。");
    }

    private static void RetryRestoreSceneViewCamera()
    {
        cameraRestoreRetryCount++;
        if (cameraRestoreRetryCount <= RetryLimit)
        {
            cameraRestoreDelayTicks = RestoreDelayTicks;
            return;
        }

        EditorApplication.update -= RestoreSceneViewCameraWhenReady;
        cameraRestoreScheduled = false;
    }

    private static int ComparePathDepthThenName(string a, string b)
    {
        int depthCompare = GetPathDepth(a).CompareTo(GetPathDepth(b));
        return depthCompare != 0 ? depthCompare : string.CompareOrdinal(a, b);
    }

    private static int GetPathDepth(string path)
    {
        if (string.IsNullOrEmpty(path))
            return 0;

        int depth = 0;
        for (int i = 0; i < path.Length; i++)
        {
            if (path[i] == '/')
                depth++;
        }

        return depth;
    }

    [Serializable]
    private sealed class SceneExpansionData
    {
        public List<string> expandedTransformPaths = new List<string>();
    }

    [Serializable]
    private sealed class SceneViewCameraRecord
    {
        public string scenePath = string.Empty;
        public long savedUtcTicks;
        public Vector3 pivot;
        public Quaternion rotation;
        public float size;
        public bool isOrtho;
        public float fieldOfView;
    }

    private static class SceneHierarchyReflection
    {
        private const int ReflectionSearchDepth = 6;

        private static readonly Type HierarchyWindowType =
            typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");

        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static bool CanSetExpandedState
        {
            get
            {
                object hierarchyObject = GetSceneHierarchyObject();
                return FindMethodOwner(hierarchyObject, "SetExpanded", ReflectionSearchDepth, new HashSet<object>(ReferenceComparer.Instance), out _, out _)
                    || TryFindExpandedIds(hierarchyObject, ReflectionSearchDepth, new HashSet<object>(ReferenceComparer.Instance), out _);
            }
        }

        public static HashSet<int> GetExpandedInstanceIds()
        {
            var result = new HashSet<int>();
            object hierarchyObject = GetSceneHierarchyObject();
            if (hierarchyObject == null)
                return result;

            if (!TryFindExpandedIds(hierarchyObject, ReflectionSearchDepth, new HashSet<object>(ReferenceComparer.Instance), out IList expandedIds))
                return result;

            foreach (object item in expandedIds)
            {
                if (item is int id)
                    result.Add(id);
            }

            return result;
        }

        public static bool SetExpanded(int instanceId, bool expanded)
        {
            object hierarchyObject = GetSceneHierarchyObject();
            if (hierarchyObject == null)
                return false;

            if (FindMethodOwner(hierarchyObject, "SetExpanded", ReflectionSearchDepth, new HashSet<object>(ReferenceComparer.Instance), out object owner, out MethodInfo method))
            {
                try
                {
                    method.Invoke(owner, new object[] { instanceId, expanded });
                    return true;
                }
                catch (Exception exception)
                {
                    Debug.LogException(new InvalidOperationException(
                        "SceneHierarchy SetExpanded 反射调用失败，已安全跳过。", exception));
                    return false;
                }
            }

            return TrySetExpandedId(hierarchyObject, instanceId, expanded);
        }

        private static object GetSceneHierarchyObject()
        {
            if (HierarchyWindowType == null)
                return null;

            var windows = Resources.FindObjectsOfTypeAll(HierarchyWindowType);
            if (windows == null || windows.Length == 0)
                return null;

            // Unity 2022 的 SceneHierarchyWindow 内部通常持有 m_SceneHierarchy。
            // 如果字段名变化，则回退到 window 自身继续搜索，降低版本差异导致的失败概率。
            object window = windows[0];
            try
            {
                var field = HierarchyWindowType.GetField("m_SceneHierarchy", InstanceFlags);
                return field != null ? field.GetValue(window) : window;
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    "SceneHierarchy 内部对象解析失败，已安全跳过。", exception));
                return null;
            }
        }

        private static bool TryFindExpandedIds(object source, int depth, HashSet<object> visited, out IList expandedIds)
        {
            expandedIds = null;
            if (!CanInspect(source, depth, visited))
                return false;

            Type type = source.GetType();
            foreach (var field in type.GetFields(InstanceFlags))
            {
                object value = SafeGet(() => field.GetValue(source));
                if (IsExpandedIdsMember(field.Name, value, out expandedIds))
                    return true;

                if (ShouldTraverseMember(field.Name) && TryFindExpandedIds(value, depth - 1, visited, out expandedIds))
                    return true;
            }

            foreach (var property in type.GetProperties(InstanceFlags))
            {
                if (property.GetIndexParameters().Length > 0)
                    continue;

                object value = SafeGet(() => property.GetValue(source, null));
                if (IsExpandedIdsMember(property.Name, value, out expandedIds))
                    return true;

                if (ShouldTraverseMember(property.Name) && TryFindExpandedIds(value, depth - 1, visited, out expandedIds))
                    return true;
            }

            return false;
        }

        private static bool TrySetExpandedId(object hierarchyObject, int instanceId, bool expanded)
        {
            if (!TryFindExpandedIds(hierarchyObject, ReflectionSearchDepth, new HashSet<object>(ReferenceComparer.Instance), out IList expandedIds))
                return false;

            bool contains = false;
            foreach (object item in expandedIds)
            {
                if (item is int id && id == instanceId)
                {
                    contains = true;
                    break;
                }
            }

            if (expanded)
            {
                if (!contains)
                    expandedIds.Add(instanceId);

                return true;
            }

            if (contains)
                expandedIds.Remove(instanceId);

            return true;
        }

        private static bool FindMethodOwner(object source, string methodName, int depth, HashSet<object> visited, out object owner, out MethodInfo method)
        {
            owner = null;
            method = null;
            if (!CanInspect(source, depth, visited))
                return false;

            Type type = source.GetType();
            method = type.GetMethod(methodName, InstanceFlags, null, new[] { typeof(int), typeof(bool) }, null);
            if (method != null)
            {
                owner = source;
                return true;
            }

            foreach (var field in type.GetFields(InstanceFlags))
            {
                if (!ShouldTraverseMember(field.Name))
                    continue;

                object value = SafeGet(() => field.GetValue(source));
                if (FindMethodOwner(value, methodName, depth - 1, visited, out owner, out method))
                    return true;
            }

            foreach (var property in type.GetProperties(InstanceFlags))
            {
                if (!ShouldTraverseMember(property.Name) || property.GetIndexParameters().Length > 0)
                    continue;

                object value = SafeGet(() => property.GetValue(source, null));
                if (FindMethodOwner(value, methodName, depth - 1, visited, out owner, out method))
                    return true;
            }

            return false;
        }

        private static bool IsExpandedIdsMember(string memberName, object value, out IList expandedIds)
        {
            expandedIds = null;
            if (!string.Equals(memberName, "expandedIDs", StringComparison.OrdinalIgnoreCase))
                return false;

            if (value is IList list)
            {
                expandedIds = list;
                return true;
            }

            return false;
        }

        private static bool ShouldTraverseMember(string memberName)
        {
            if (string.IsNullOrEmpty(memberName))
                return false;

            // 限制反射搜索范围，只进入可能承载 TreeView 状态的成员，避免扫描整个编辑器对象图。
            string lower = memberName.ToLowerInvariant();
            return lower.Contains("scenehierarchy")
                || lower.Contains("treeview")
                || lower.Contains("state")
                || lower.Contains("data")
                || lower == "m_rootitem";
        }

        private static bool CanInspect(object source, int depth, HashSet<object> visited)
        {
            if (source == null || depth < 0)
                return false;

            Type type = source.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string))
                return false;

            return visited.Add(source);
        }

        private static object SafeGet(Func<object> getter)
        {
            try
            {
                return getter();
            }
            catch
            {
                return null;
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
#endif
