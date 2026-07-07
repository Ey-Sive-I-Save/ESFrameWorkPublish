#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 保存并恢复已加载场景中 GameObject 在 Hierarchy 面板里的展开状态。
/// 本脚本刻意保持独立：不依赖 ES、Odin、asmdef 或任何项目工具类。
/// </summary>
[InitializeOnLoad]
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
    private static readonly bool LogTiming = true;

    private const string MenuRoot = "Tools/Scene Hierarchy Expansion/";
    private const string StoragePrefix = "Standalone.SceneHierarchyExpansionState.";

    private static int restoreRetryCount;
    private static int pendingRestoreDelayTicks;
    private static bool restoreScheduled;

    static SceneHierarchyExpansionState()
    {
        if (AutoSaveOnSceneSaving)
            EditorSceneManager.sceneSaving += OnSceneSaving;

        if (AutoLoadOnSceneOpened)
            EditorSceneManager.sceneOpened += OnSceneOpened;

        if (AutoSaveBeforeSceneClosing)
            EditorSceneManager.sceneClosing += OnSceneClosing;

        if (AutoSaveBeforeAssemblyReload)
            AssemblyReloadEvents.beforeAssemblyReload += SaveLoadedScenesExpansionState;

        if (AutoRestoreAfterPlayMode)
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        EditorApplication.quitting += SaveLoadedScenesExpansionState;
        EditorApplication.delayCall += () => ScheduleRestoreLoadedScenes(RestoreDelayTicks);
    }

    [MenuItem(MenuRoot + "Save Loaded Scenes Expansion")]
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

            // 保存到 EditorPrefs：不创建资产文件，不影响版本库，按项目和场景 GUID 隔离。
            EditorPrefs.SetString(GetStorageKey(scene), JsonUtility.ToJson(data));
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

    [MenuItem(MenuRoot + "Load Loaded Scenes Expansion")]
    public static void LoadLoadedScenesExpansionState()
    {
        restoreRetryCount = 0;
        ScheduleRestoreLoadedScenes(0);
    }

    [MenuItem(MenuRoot + "Clear Loaded Scenes Saved State")]
    public static void ClearLoadedScenesSavedState()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (CanStoreScene(scene))
                EditorPrefs.DeleteKey(GetStorageKey(scene));
        }

        Debug.Log("[SceneHierarchyExpansionState] Cleared saved hierarchy expansion state for loaded scenes.");
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        SaveLoadedScenesExpansionState();
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        restoreRetryCount = 0;
        ScheduleRestoreLoadedScenes(RestoreDelayTicks);
    }

    private static void OnSceneClosing(Scene scene, bool removingScene)
    {
        SaveLoadedScenesExpansionState();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
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

            string json = EditorPrefs.GetString(GetStorageKey(scene), string.Empty);
            if (string.IsNullOrEmpty(json))
                continue;

            var data = JsonUtility.FromJson<SceneExpansionData>(json);
            if (data == null || data.expandedTransformPaths == null)
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
                method.Invoke(owner, new object[] { instanceId, expanded });
                return true;
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
            var field = HierarchyWindowType.GetField("m_SceneHierarchy", InstanceFlags);
            return field != null ? field.GetValue(window) : window;
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
