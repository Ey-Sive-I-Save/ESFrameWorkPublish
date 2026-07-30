using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using System.Text;
using Stopwatch = System.Diagnostics.Stopwatch;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Marks an ESSO type that must be available immediately after the editor assembly stream.
    /// The marker is collected before Level0 by <see cref="ESAS_Register_ESSOEditorPreLoad"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ESSOEditorPreLoadAttribute : Attribute
    {
    }

    /// <summary>
    /// 编辑器用的 SO 索引容器。
    /// - <c>SOS</c>：按类型分组的已注册 <see cref="ESSO"/> 实例集合。
    /// - <c>AllSoNames</c>：映射显示名到 ScriptableObject 类型的双向字典，便于菜单/反射使用。
    /// </summary>
    public class ESEditorSO
    {
        /// <summary>按类型分组的已注册 ESSO 实例。</summary>
        public static TypeMatchKeyGroup<ESSO> SOS = new TypeMatchKeyGroup<ESSO>();



        /// <summary>显示名 ↔ 类型 的双向映射（编辑器使用）。</summary>
        public static BidirectionalDictionary<string,Type> AllSoNames=new BidirectionalDictionary<string, Type>();

        /// <summary>仅包含实现 <see cref="IESGlobalData"/> 的全局数据类型映射（显示名 ↔ 类型）。</summary>
        public static BidirectionalDictionary<string, Type> AllGlobalSoNames = new BidirectionalDictionary<string, Type>();

#if UNITY_EDITOR
        private static readonly HashSet<Type> LoadedTypes = new();
        private static readonly HashSet<Type> LoadingTypes = new();
        private static readonly HashSet<string> LoadedAssetPaths = new(StringComparer.Ordinal);
        private static bool allTypesLoaded;
        private static bool editorApplyPhaseCompleted;

        /// <summary>Returns whether every asset of this exact ESSO type has been loaded into SOS this domain.</summary>
        public static bool IsTypeLoaded(Type type)
        {
            return type != null && LoadedTypes.Contains(type);
        }

        public static void EnsureTypeLoaded<T>() where T : ESSO
        {
            EnsureTypeLoaded(typeof(T));
        }

        /// <summary>
        /// Returns the current exact-type group after ensuring a concrete ESSO type is available.
        /// Abstract generic bases intentionally retain the previous index-only lookup behavior.
        /// </summary>
        public static List<T> GetGroupOfType<T>()
        {
            Type type = typeof(T);
            if (!type.IsAbstract && typeof(ESSO).IsAssignableFrom(type))
                EnsureTypeLoaded(type);
            return SOS.GetNewGroupOfType<T>();
        }

        /// <summary>Ensures an exact ESSO type before querying it through an interface/base view.</summary>
        public static List<T> GetGroup<T>(Type exactEssoType) where T : class
        {
            if (exactEssoType != null && !exactEssoType.IsAbstract && typeof(ESSO).IsAssignableFrom(exactEssoType))
                EnsureTypeLoaded(exactEssoType);
            return SOS.GetGroup<T>(exactEssoType);
        }

        /// <summary>
        /// Ensures every concrete ESSO type assignable to <typeparamref name="T"/> before
        /// returning a combined view. Use this only when a caller intentionally accepts a base
        /// type rather than one exact ESSO type.
        /// </summary>
        public static List<T> GetAssignableGroupOfType<T>() where T : class
        {
            Type targetType = typeof(T);
            EnsureTypesAssignableTo(targetType);
            var result = new List<T>();
            foreach (KeyValuePair<Type, List<ESSO>> pair in SOS.Groups)
            {
                if (pair.Key == null || !targetType.IsAssignableFrom(pair.Key) || pair.Value == null)
                    continue;

                for (int i = 0; i < pair.Value.Count; i++)
                {
                    if (pair.Value[i] is T value)
                        result.Add(value);
                }
            }

            return result;
        }

        /// <summary>
        /// Loads and registers every asset of one exact ESSO type once. This is the future-safe
        /// replacement for assuming SOS was globally hydrated at editor startup.
        /// </summary>
        public static void EnsureTypeLoaded(Type type)
        {
            if (type == null || type.IsAbstract || !typeof(ESSO).IsAssignableFrom(type))
                throw new ArgumentException("The requested type must be a non-abstract ESSO type.", nameof(type));
            if (LoadedTypes.Contains(type))
            {
                EnsureLoadedTypeApplied(type);
                return;
            }
            if (!LoadingTypes.Add(type))
                return;

            try
            {
                string[] guids;
                using (ESEditorSOInitializationTiming.MeasurePhase(ESEditorSOInitializationTiming.Phase.FindAssets))
                    guids = AssetDatabase.FindAssets("t:" + type.Name);
                ESEditorSOInitializationTiming.AddFoundGuidCount(guids.Length);
                var paths = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < guids.Length; i++)
                {
                    string assetPath;
                    using (ESEditorSOInitializationTiming.MeasurePhase(ESEditorSOInitializationTiming.Phase.ResolveAssetPath))
                        assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrEmpty(assetPath) || !paths.Add(assetPath))
                        continue;

                    EnsureAssetPathLoaded(assetPath);
                }

                // Only the explicit search above proves this exact type is complete.
                // A sibling sub-asset encountered while loading another type's file does not.
                LoadedTypes.Add(type);
            }
            finally
            {
                LoadingTypes.Remove(type);
            }
        }

        /// <summary>Ensures concrete ESSO implementations of a base ESSO type are loaded once.</summary>
        public static void EnsureTypesAssignableTo(Type baseType)
        {
            if (baseType == null || !typeof(ESSO).IsAssignableFrom(baseType))
                return;

            if (!baseType.IsAbstract && !baseType.ContainsGenericParameters)
                EnsureTypeLoaded(baseType);

            foreach (Type type in TypeCache.GetTypesDerivedFrom(baseType))
            {
                if (type == null || type.IsAbstract || type.ContainsGenericParameters || !typeof(ESSO).IsAssignableFrom(type))
                    continue;

                EnsureTypeLoaded(type);
            }
        }

        /// <summary>
        /// Explicit full-hydration escape hatch for editor operations that genuinely enumerate
        /// every SOS group. Normal windows should prefer <see cref="GetGroupOfType{T}"/>.
        /// </summary>
        public static void EnsureAllTypesLoaded()
        {
            if (allTypesLoaded)
            {
                if (editorApplyPhaseCompleted)
                {
                    foreach (KeyValuePair<Type, List<ESSO>> pair in SOS.Groups)
                    {
                        List<ESSO> group = pair.Value;
                        for (int i = 0; group != null && i < group.Count; i++)
                            EnsureAppliedIfPhaseCompleted(group[i]);
                    }
                }
                return;
            }

            string[] guids = AssetDatabase.FindAssets($"t:{nameof(ESSO)}");
            var paths = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(assetPath) || !paths.Add(assetPath))
                    continue;

                EnsureAssetPathLoaded(assetPath);
            }

            // This is the sole bulk enumeration path. At this point all registered concrete
            // types are complete, unlike a single path load that can merely expose a sibling.
            foreach (KeyValuePair<Type, List<ESSO>> pair in SOS.Groups)
                LoadedTypes.Add(pair.Key);
            allTypesLoaded = true;
        }

        internal static void ResetLoadedIndex()
        {
            SOS.Clear();
            LoadedTypes.Clear();
            LoadingTypes.Clear();
            LoadedAssetPaths.Clear();
            allTypesLoaded = false;
            editorApplyPhaseCompleted = false;
        }

        internal static void MarkTypeLoaded(Type type)
        {
            if (type != null)
                LoadedTypes.Add(type);
        }

        internal static void CompleteEditorApplyPhase()
        {
            editorApplyPhaseCompleted = true;
        }

        private static void EnsureLoadedTypeApplied(Type type)
        {
            if (!editorApplyPhaseCompleted || !SOS.Groups.TryGetValue(type, out List<ESSO> group))
                return;

            for (int i = 0; i < group.Count; i++)
                EnsureAppliedIfPhaseCompleted(group[i]);
        }

        private static void EnsureAppliedIfPhaseCompleted(ESSO soAsset)
        {
            if (editorApplyPhaseCompleted && soAsset != null)
                soAsset.Editor_EnsureApplied();
        }

        /// <summary>
        /// A path is Unity's real deserialization boundary. Register every ESSO found in the path
        /// on its first load, so a later request for a sibling sub-asset type never repeats
        /// LoadAllAssetsAtPath for the same file.
        /// </summary>
        private static void EnsureAssetPathLoaded(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !LoadedAssetPaths.Add(assetPath))
                return;

            ESEditorSOInitializationTiming.AddLoadedPathCount();
            UnityEngine.Object[] assets;
            using (ESEditorSOInitializationTiming.MeasurePhase(ESEditorSOInitializationTiming.Phase.LoadAllAssetsAtPath))
                assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is not ESSO soAsset)
                    continue;

                Type type = soAsset.GetType();
                using (ESEditorSOInitializationTiming.Measure(type, assetPath))
                    soAsset.Editor_EnsureInitializedAndRegistered();
                EnsureAppliedIfPhaseCompleted(soAsset);
            }
        }
#endif
    }
#if UNITY_EDITOR
    /// <summary>Assembly-stream registry of ESSO types marked for editor preload.</summary>
    internal static class ESSOEditorPreLoadRegistry
    {
        private static readonly HashSet<Type> TypeSet = new();
        private static readonly List<Type> Types = new(16);

        public static IReadOnlyList<Type> RegisteredTypes => Types;

        public static void Register(Type type)
        {
            if (type != null && TypeSet.Add(type))
                Types.Add(type);
        }
    }

    /// <summary>
    /// Collects ESSOEditorPreLoad types before Level0 so SoEditorIniter can consume an already
    /// complete preload registry. It intentionally runs before normal assembly initializers.
    /// </summary>
    public sealed class ESAS_Register_ESSOEditorPreLoad
        : EditorRegister_FOR_ClassAttribute<ESSOEditorPreLoadAttribute>
    {
        public override int Order => -1;

        public override void Handle(ESSOEditorPreLoadAttribute attribute, Type type)
        {
            if (type != null && !type.IsAbstract && typeof(ESSO).IsAssignableFrom(type))
                ESSOEditorPreLoadRegistry.Register(type);
        }
    }

    /// <summary>
    /// 编辑器启动时扫描并初始化所有 <see cref="ESSO"/> 资产。
    /// 调用时机：Editor 初始化阶段（继承自 <c>EditorInvoker_Level0</c>）。
    /// </summary>
    public class SoEditorIniter : EditorInvoker_Level0
    {
        /// <summary>查找项目中所有 ESSO 类型资产并调用 <c>OnEditorInitialized</c>。</summary>
        public override void InitInvoke()
        {
            ESEditorSOInitializationTiming.Begin();
            try
            {
                // 清空旧索引后，仅加载程序集流声明的编辑器常驻 ESSO。
                ESEditorSO.ResetLoadedIndex();
                IReadOnlyList<Type> preloadTypes = ESSOEditorPreLoadRegistry.RegisteredTypes;
                ESEditorSOInitializationTiming.SetPreloadTypeCount(preloadTypes.Count);
                for (int i = 0; i < preloadTypes.Count; i++)
                {
                    ESEditorSO.EnsureTypeLoaded(preloadTypes[i]);
                }
            }
            finally
            {
                ESEditorSOInitializationTiming.LogReport();
            }
        }
    }

    /// <summary>
    /// 专用于 <see cref="SoEditorIniter"/> 的编辑器初始化计时器。
    /// 以 ESSO 实际类型聚合，记录总耗时、调用次数、最大单项和最慢资产路径；不参与 Player。
    /// </summary>
    internal static class ESEditorSOInitializationTiming
    {
        internal enum Phase : byte
        {
            FindAssets,
            ResolveAssetPath,
            LoadAllAssetsAtPath
        }

        private sealed class Entry
        {
            public readonly Type Type;
            public int Count;
            public long TotalTicks;
            public long MaxTicks;
            public string SlowestAssetPath;

            public Entry(Type type)
            {
                Type = type;
            }
        }

        internal readonly struct MeasureScope : IDisposable
        {
            private readonly Type type;
            private readonly string assetPath;
            private readonly long startedAt;

            internal MeasureScope(Type type, string assetPath, long startedAt)
            {
                this.type = type;
                this.assetPath = assetPath;
                this.startedAt = startedAt;
            }

            public void Dispose()
            {
                if (startedAt != 0)
                    Record(type, assetPath, Stopwatch.GetTimestamp() - startedAt);
            }
        }

        internal readonly struct PhaseMeasureScope : IDisposable
        {
            private readonly Phase phase;
            private readonly long startedAt;

            internal PhaseMeasureScope(Phase phase, long startedAt)
            {
                this.phase = phase;
                this.startedAt = startedAt;
            }

            public void Dispose()
            {
                if (startedAt != 0)
                    RecordPhase(phase, Stopwatch.GetTimestamp() - startedAt);
            }
        }

        private static readonly Dictionary<Type, Entry> Entries = new(64);
        private static readonly List<Entry> SortedEntries = new(64);
        private static readonly StringBuilder ReportBuilder = new(2048);
        private static long findAssetsTicks;
        private static long resolveAssetPathTicks;
        private static long loadAllAssetsAtPathTicks;
        private static int foundGuidCount;
        private static int loadedPathCount;
        private static int initializedInstanceCount;
        private static int preloadTypeCount;
        private static long startedAt;
        private static bool isCollecting;

        public static void Begin()
        {
            Entries.Clear();
            SortedEntries.Clear();
            ReportBuilder.Clear();
            findAssetsTicks = 0;
            resolveAssetPathTicks = 0;
            loadAllAssetsAtPathTicks = 0;
            foundGuidCount = 0;
            loadedPathCount = 0;
            initializedInstanceCount = 0;
            preloadTypeCount = 0;
            startedAt = Stopwatch.GetTimestamp();
            isCollecting = true;
        }

        public static MeasureScope Measure(Type type, string assetPath)
        {
            return isCollecting && type != null
                ? new MeasureScope(type, assetPath, Stopwatch.GetTimestamp())
                : default;
        }

        public static PhaseMeasureScope MeasurePhase(Phase phase)
        {
            return isCollecting ? new PhaseMeasureScope(phase, Stopwatch.GetTimestamp()) : default;
        }

        public static void AddFoundGuidCount(int count)
        {
            if (isCollecting)
                foundGuidCount += Math.Max(0, count);
        }

        public static void AddLoadedPathCount()
        {
            if (isCollecting)
                loadedPathCount++;
        }

        public static void SetPreloadTypeCount(int count)
        {
            if (isCollecting)
                preloadTypeCount = Math.Max(0, count);
        }

        public static void LogReport()
        {
            if (!isCollecting)
                return;

            isCollecting = false;
            SortedEntries.Clear();
            foreach (Entry entry in Entries.Values)
                SortedEntries.Add(entry);

            SortedEntries.Sort((left, right) => right.TotalTicks.CompareTo(left.TotalTicks));
            ReportBuilder.Clear();
            long totalTicks = Stopwatch.GetTimestamp() - startedAt;
            ReportBuilder.Append("[ESEditorSOInit] completed | Total=")
                .Append(ToMilliseconds(totalTicks).ToString("F3"))
                .Append(" ms | Types=").Append(SortedEntries.Count)
                .Append(" | PreLoadTypes=").Append(preloadTypeCount)
                .Append(" | FindAssetsGuids=").Append(foundGuidCount)
                .Append(" | LoadedPaths=").Append(loadedPathCount)
                .Append(" | LoadAllCalls=").Append(loadedPathCount)
                .Append(" | ESSOInstances=").Append(initializedInstanceCount)
                .Append(" | InstancesPerPath=")
                .Append(loadedPathCount == 0 ? "0.000" : ((double)initializedInstanceCount / loadedPathCount).ToString("F3"))
                .Append('\n');
            AppendPhase("AssetDatabase.FindAssets", findAssetsTicks);
            AppendPhase("AssetDatabase.GUIDToAssetPath", resolveAssetPathTicks);
            AppendPhase("AssetDatabase.LoadAllAssetsAtPath", loadAllAssetsAtPathTicks);

            for (int i = 0; i < SortedEntries.Count; i++)
            {
                Entry entry = SortedEntries[i];
                ReportBuilder.Append("[ESEditorSOInit] #").Append(i + 1)
                    .Append(" | Type=").Append(entry.Type.FullName)
                    .Append(" | Total=").Append(ToMilliseconds(entry.TotalTicks).ToString("F3"))
                    .Append(" ms | Count=").Append(entry.Count)
                    .Append(" | Max=").Append(ToMilliseconds(entry.MaxTicks).ToString("F3"))
                    .Append(" ms | Slowest=").Append(entry.SlowestAssetPath ?? string.Empty)
                    .Append('\n');
            }

            Debug.Log(ReportBuilder.ToString());
        }

        private static void Record(Type type, string assetPath, long elapsedTicks)
        {
            if (!isCollecting || type == null)
                return;

            if (!Entries.TryGetValue(type, out Entry entry))
            {
                entry = new Entry(type);
                Entries.Add(type, entry);
            }

            entry.Count++;
            initializedInstanceCount++;
            entry.TotalTicks += elapsedTicks;
            if (elapsedTicks <= entry.MaxTicks)
                return;

            entry.MaxTicks = elapsedTicks;
            entry.SlowestAssetPath = assetPath;
        }

        private static void RecordPhase(Phase phase, long elapsedTicks)
        {
            if (!isCollecting)
                return;

            switch (phase)
            {
                case Phase.FindAssets:
                    findAssetsTicks += elapsedTicks;
                    break;
                case Phase.ResolveAssetPath:
                    resolveAssetPathTicks += elapsedTicks;
                    break;
                case Phase.LoadAllAssetsAtPath:
                    loadAllAssetsAtPathTicks += elapsedTicks;
                    break;
            }
        }

        private static void AppendPhase(string name, long ticks)
        {
            ReportBuilder.Append("[ESEditorSOInit] Phase | ").Append(name)
                .Append(" | ").Append(ToMilliseconds(ticks).ToString("F3")).Append(" ms\n");
        }

        private static double ToMilliseconds(long ticks)
        {
            return ticks * 1000d / Stopwatch.Frequency;
        }
    }
    
    /// <summary>
    /// 在编辑器应用阶段调用所有已注册 ESSO 的 <c>OnEditorApply</c> 钩子（例如在保存或刷新时）。
    /// </summary>
    public class SoEditorApplier : EditorInvoker_SoApply
    {
        /// <summary>对已注册的每个 ESSO 实例执行一次 Apply 操作。</summary>
        public override void InitInvoke()
        {
            try
            {
                var keys = new List<Type>(ESEditorSO.SOS.Groups.Keys);
                foreach (var i in keys)
                {
                    var group = new List<ESSO>(ESEditorSO.SOS.GetGroupDirectly(i));
                    foreach (var g in group)
                    {
                        if (g != null)
                            g.Editor_EnsureApplied();
                    }
                }
            }
            finally
            {
                ESEditorSO.CompleteEditorApplyPhase();
            }
        }
    }


    /// <summary>
    /// 编辑器注册器：当检测到 ScriptableObject 子类时，将其显示名登记到 <see cref="ESEditorSO.AllSoNames"/>。
    /// 用于在编辑器菜单或选择器中显示友好的类型名。
    /// </summary>
    public class ER_So_SubClass : EditorRegister_FOR_AsSubclass<ScriptableObject>
    {
        public override int Order => 10;

        /// <summary>处理检测到的子类类型并登记显示名与类型的映射。</summary>
        public override void Handle(Type SubClassType)
        {
            var Disname = SubClassType._GetTypeDisplayName();
            ESEditorSO.AllSoNames.Add(Disname, SubClassType);
            // 如果该类型实现了 IESGlobalData，则同时登记到 AllGlobalSoNames（便于区分全局配置类）
            if (typeof(IESGlobalData).IsAssignableFrom(SubClassType))
            {
                ESEditorSO.AllGlobalSoNames.Add(Disname, SubClassType);
            }
        }
    }
    
#endif

}
