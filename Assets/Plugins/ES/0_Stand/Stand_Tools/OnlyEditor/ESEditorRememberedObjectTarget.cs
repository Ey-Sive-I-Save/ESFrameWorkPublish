#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    public enum ESEditorRememberedTargetFallbackStrategy
    {
        /// <summary>只使用 GlobalObjectId 和 InstanceID。适合误绑定成本很高的工具。</summary>
        StrictOnly,

        /// <summary>允许使用场景 GUID/路径 + 层级路径恢复。适合层级稳定的场景对象。</summary>
        SceneAndPath,

        /// <summary>允许在同场景下使用对象名 + 类型兜底恢复。默认策略，适合大多数编辑器预览工具。</summary>
        NameAndType,

        /// <summary>最后允许“唯一候选对象”兜底。只适合测试工具或单对象场景。</summary>
        AnySingleCandidate
    }

    /// <summary>
    /// 编辑器目标对象记忆工具。
    /// 用于保存预览、调试等编辑器工具的上一次目标对象；这是个人编辑器状态，使用 EditorPrefs，不写入项目资产。
    ///
    /// 主要恢复顺序：
    /// 1. GlobalObjectId：最稳定，优先用于场景对象和资产对象。
    /// 2. InstanceID：同一次编辑器会话内有效。
    /// 3. Scene GUID/Path + HierarchyPath：对象被重新加载后仍可找回。
    /// 4. IsFallbackCandidate：默认使用同场景下的层级路径、对象名和类型兜底。
    ///
    /// 子类扩展方式：
    /// - 重写 CreateRecord：保存业务额外信息，例如 Prefab GUID、Entity 稳定 ID、配置表 ID。
    /// - 重写 IsFallbackCandidate：定义业务自己的兜底匹配规则，例如只允许 Entity、排除 _ESPreview、检查阵营或组件标记。
    /// - 构造时选择 fallbackStrategy：误绑定成本高用 StrictOnly；普通预览用 NameAndType；单对象测试可用 AnySingleCandidate。
    ///
    /// 示例：
    /// sealed class RememberedEntityTarget : ESEditorRememberedObjectTarget&lt;Entity&gt;
    /// {
    ///     public RememberedEntityTarget(string key)
    ///         : base(key, ESEditorRememberedTargetFallbackStrategy.NameAndType, 30) { }
    ///
    ///     protected override bool IsFallbackCandidate(Entity target, TargetRecord record)
    ///     {
    ///         if (!base.IsFallbackCandidate(target, record))
    ///             return false;
    ///
    ///         return target.gameObject.activeInHierarchy
    ///             &amp;&amp; !target.name.EndsWith("_ESPreview", StringComparison.Ordinal);
    ///     }
    /// }
    /// </summary>
    public class ESEditorRememberedObjectTarget<T> where T : UnityEngine.Object
    {
        private const int DefaultMaxRecordAgeDays = 30;

        [Serializable]
        protected class TargetRecord
        {
            public string globalObjectId;
            public int instanceId;
            public string objectName;
            public string typeName;
            public string sceneGuid;
            public string scenePath;
            public string hierarchyPath;
            public long savedUtcTicks;
        }

        private readonly string editorPrefsKey;
        private readonly int maxRecordAgeDays;
        private readonly ESEditorRememberedTargetFallbackStrategy fallbackStrategy;
        private TargetRecord cachedRecord;
        private bool hasLoadedRecord;

        public ESEditorRememberedObjectTarget(
            string editorPrefsKey,
            ESEditorRememberedTargetFallbackStrategy fallbackStrategy = ESEditorRememberedTargetFallbackStrategy.NameAndType,
            int maxRecordAgeDays = DefaultMaxRecordAgeDays)
        {
            this.editorPrefsKey = editorPrefsKey;
            this.fallbackStrategy = fallbackStrategy;
            this.maxRecordAgeDays = maxRecordAgeDays;
        }

        public void Remember(T target)
        {
            if (target == null)
                return;

            TargetRecord record = CreateRecord(target);
            cachedRecord = record;
            hasLoadedRecord = true;
            EditorPrefs.SetString(editorPrefsKey, JsonUtility.ToJson(record));
        }

        public void Clear()
        {
            cachedRecord = null;
            hasLoadedRecord = true;
            EditorPrefs.DeleteKey(editorPrefsKey);
        }

        public bool TryResolve(out T target)
        {
            target = null;
            TargetRecord record = LoadRecord();
            if (record == null)
                return false;

            if (IsRecordExpired(record))
            {
                Clear();
                return false;
            }

            target = ResolveByGlobalObjectId(record);
            if (target != null)
                return true;

            target = ResolveByInstanceId(record);
            if (target != null)
                return true;

            if (fallbackStrategy == ESEditorRememberedTargetFallbackStrategy.StrictOnly)
                return false;

            target = ResolveBySceneAndHierarchy(record);
            if (target != null)
                return true;

            if (fallbackStrategy == ESEditorRememberedTargetFallbackStrategy.SceneAndPath)
                return false;

            target = ResolveByFallbackSearch(record);
            if (target != null)
                return true;

            if (fallbackStrategy != ESEditorRememberedTargetFallbackStrategy.AnySingleCandidate)
                return false;

            target = ResolveBySingleCandidate(record);
            return target != null;
        }

        public string GetDisplayName()
        {
            TargetRecord record = LoadRecord();
            if (record == null)
                return "<无>";

            return string.IsNullOrEmpty(record.objectName) ? "<未命名>" : record.objectName;
        }

        /// <summary>
        /// 业务兜底匹配入口。
        /// 子类可以在 base 结果之上继续收紧规则；不要在这里做重型扫描，本方法会被候选对象循环调用。
        /// </summary>
        protected virtual bool IsFallbackCandidate(T target, TargetRecord record)
        {
            if (target == null || record == null)
                return false;

            if (!SceneMatches(target, record))
                return false;

            if (!string.IsNullOrEmpty(record.hierarchyPath) && GetHierarchyPath(target) == record.hierarchyPath)
                return true;

            return target.name == record.objectName && target.GetType().FullName == record.typeName;
        }

        /// <summary>
        /// 创建可序列化记忆记录。
        /// 子类需要更多业务字段时，优先继承 TargetRecord 并重写本方法返回自定义记录类型。
        /// 注意 JsonUtility 只序列化字段，不序列化属性。
        /// </summary>
        protected virtual TargetRecord CreateRecord(T target)
        {
            string globalObjectId = string.Empty;
            try
            {
                GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(target);
                globalObjectId = id.ToString();
            }
            catch
            {
                globalObjectId = string.Empty;
            }

            return new TargetRecord
            {
                globalObjectId = globalObjectId,
                instanceId = target.GetInstanceID(),
                objectName = target.name,
                typeName = target.GetType().FullName,
                sceneGuid = GetSceneGuid(target),
                scenePath = GetScenePath(target),
                hierarchyPath = GetHierarchyPath(target),
                savedUtcTicks = DateTime.UtcNow.Ticks
            };
        }

        protected static string GetSceneGuid(UnityEngine.Object target)
        {
            string scenePath = GetScenePath(target);
            return string.IsNullOrEmpty(scenePath) ? string.Empty : AssetDatabase.AssetPathToGUID(scenePath);
        }

        protected static string GetScenePath(UnityEngine.Object target)
        {
            GameObject gameObject = GetGameObject(target);
            if (gameObject == null)
                return string.Empty;

            Scene scene = gameObject.scene;
            return scene.IsValid() ? scene.path : string.Empty;
        }

        protected static string GetHierarchyPath(UnityEngine.Object target)
        {
            GameObject gameObject = GetGameObject(target);
            if (gameObject == null)
                return string.Empty;

            Transform current = gameObject.transform;
            string path = current.name;
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + "/" + path;
            }

            return path;
        }

        protected static GameObject GetGameObject(UnityEngine.Object target)
        {
            if (target is GameObject gameObject)
                return gameObject;

            if (target is Component component)
                return component.gameObject;

            return null;
        }

        private bool IsRecordExpired(TargetRecord record)
        {
            if (record == null || maxRecordAgeDays <= 0 || record.savedUtcTicks <= 0)
                return false;

            DateTime savedUtc = new DateTime(record.savedUtcTicks, DateTimeKind.Utc);
            return DateTime.UtcNow - savedUtc > TimeSpan.FromDays(maxRecordAgeDays);
        }

        private TargetRecord LoadRecord()
        {
            if (hasLoadedRecord)
                return cachedRecord;

            hasLoadedRecord = true;
            string json = EditorPrefs.GetString(editorPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                cachedRecord = JsonUtility.FromJson<TargetRecord>(json);
            }
            catch
            {
                cachedRecord = null;
            }

            return cachedRecord;
        }

        private static T ResolveByGlobalObjectId(TargetRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.globalObjectId))
                return null;

            if (!GlobalObjectId.TryParse(record.globalObjectId, out GlobalObjectId id))
                return null;

            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as T;
        }

        private static T ResolveByInstanceId(TargetRecord record)
        {
            if (record == null || record.instanceId == 0)
                return null;

            return EditorUtility.InstanceIDToObject(record.instanceId) as T;
        }

        private T ResolveBySceneAndHierarchy(TargetRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.hierarchyPath))
                return null;

            T[] targets = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < targets.Length; i++)
            {
                T candidate = targets[i];
                if (candidate == null || EditorUtility.IsPersistent(candidate))
                    continue;

                if (SceneMatches(candidate, record) && GetHierarchyPath(candidate) == record.hierarchyPath)
                    return candidate;
            }

            return null;
        }

        private T ResolveByFallbackSearch(TargetRecord record)
        {
            T[] targets = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < targets.Length; i++)
            {
                T candidate = targets[i];
                if (candidate == null || EditorUtility.IsPersistent(candidate))
                    continue;

                if (IsFallbackCandidate(candidate, record))
                    return candidate;
            }

            return null;
        }

        private T ResolveBySingleCandidate(TargetRecord record)
        {
            T result = null;
            T[] targets = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < targets.Length; i++)
            {
                T candidate = targets[i];
                if (candidate == null || EditorUtility.IsPersistent(candidate))
                    continue;

                if (!SceneMatches(candidate, record))
                    continue;

                if (result != null)
                    return null;

                result = candidate;
            }

            if (result != null)
                return result;

            for (int i = 0; i < targets.Length; i++)
            {
                T candidate = targets[i];
                if (candidate == null || EditorUtility.IsPersistent(candidate))
                    continue;

                if (result != null)
                    return null;

                result = candidate;
            }

            return result;
        }

        private static bool SceneMatches(UnityEngine.Object target, TargetRecord record)
        {
            if (target == null || record == null)
                return false;

            bool hasSceneGuid = !string.IsNullOrEmpty(record.sceneGuid);
            bool hasScenePath = !string.IsNullOrEmpty(record.scenePath);

            if (!hasSceneGuid && !hasScenePath)
                return true;

            if (hasSceneGuid && GetSceneGuid(target) == record.sceneGuid)
                return true;

            return hasScenePath && GetScenePath(target) == record.scenePath;
        }
    }
}
#endif
