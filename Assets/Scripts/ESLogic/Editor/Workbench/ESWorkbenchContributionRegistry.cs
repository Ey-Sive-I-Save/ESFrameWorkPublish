#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace ES
{
    /// <summary>工作台可启用的标准模块类型；具体工作台通过默认列表和调整钩子决定启用顺序。</summary>
    public enum ESWorkbenchModuleKind : byte
    {
        Overview,
        Terrain,
        Material,
        Vegetation,
        Prefab,
        Navigation,
        WaterWeather,
        Streaming,
        Collision,
        UGC
    }

    /// <summary>工作台贡献所属的编辑器语义分类。</summary>
    public enum ESWorkbenchContributionCategory : byte
    {
        General,
        Terrain,
        Material,
        Vegetation,
        Prefab,
        Navigation,
        WaterWeather,
        Streaming,
        Collision,
        UGC,
        Validation,
        Build
    }

    /// <summary>工作台模块向底座声明的可注入能力。</summary>
    public sealed class ESWorkbenchContributionDescriptor
    {
        public string WorkbenchId { get; }
        public string ContributionId { get; }
        public string DisplayName { get; }
        public string Tooltip { get; }
        public string Owner { get; }
        public int Priority { get; }
        public int Revision { get; }
        public ESWorkbenchContributionCategory Category { get; }
        public IReadOnlyList<string> Dependencies { get; }
        public Func<ESWorkbenchContributionContext, bool> IsEnabled { get; }
        public Func<ESWorkbenchContributionContext, IDisposable> Inject { get; }

        public ESWorkbenchContributionDescriptor(
            string workbenchId,
            string contributionId,
            string displayName,
            ESWorkbenchContributionCategory category,
            Func<ESWorkbenchContributionContext, IDisposable> inject,
            string tooltip = null,
            string owner = null,
            int priority = 0,
            int revision = 1,
            IEnumerable<string> dependencies = null,
            Func<ESWorkbenchContributionContext, bool> isEnabled = null)
        {
            if (string.IsNullOrWhiteSpace(workbenchId)) throw new ArgumentException("WorkbenchId 不能为空。", nameof(workbenchId));
            if (string.IsNullOrWhiteSpace(contributionId)) throw new ArgumentException("ContributionId 不能为空。", nameof(contributionId));
            if (inject == null) throw new ArgumentNullException(nameof(inject));
            WorkbenchId = workbenchId.Trim();
            ContributionId = contributionId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? ContributionId : displayName.Trim();
            Tooltip = tooltip ?? string.Empty;
            Owner = string.IsNullOrWhiteSpace(owner) ? "ES" : owner.Trim();
            Priority = priority;
            Revision = Math.Max(1, revision);
            Category = category;
            Dependencies = (dependencies ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            IsEnabled = isEnabled;
            Inject = inject;
        }

        internal string StableKey => WorkbenchId + ":" + ContributionId;
    }

    /// <summary>一次工作台打开过程中的注入上下文；不把委托或 Editor 对象序列化到资产。</summary>
    public sealed class ESWorkbenchContributionContext
    {
        private readonly Action<ESWorkbenchPageDefinition> registerPage;
        private readonly Action<ESWorkbenchAssetRegistrationSlot> registerAssetSlot;
        private readonly Action<ESWorkbenchContributionEntry> registerEntry;
        private readonly Action<ESWorkbenchViewportDescriptor> registerViewport;
        private readonly Action<ESWorkbenchObjectDescriptor> registerObject;
        private readonly Action<ESWorkbenchCollectionSource<ESWorkbenchObjectDescriptor>> registerObjectSource;
        private readonly Action<ESWorkbenchHierarchyDescriptor> registerHierarchy;
        private readonly Action<ESWorkbenchCollectionSource<ESWorkbenchHierarchyDescriptor>> registerHierarchySource;
        private readonly Action<ESWorkbenchAuthoringAdapterDescriptor> registerAuthoringAdapter;
        private readonly Action<ESWorkbenchInspectorDescriptor> registerInspector;
        private readonly Action<ESWorkbenchToolDescriptor> registerTool;
        private readonly Action<ESWorkbenchCommandDescriptor> registerCommand;
        private readonly Action<ESWorkbenchCollectionSource<ESWorkbenchIssueDescriptor>> registerIssueSource;
        private readonly Action<string> reportDiagnostic;

        internal ESWorkbenchContributionContext(
            string workbenchId,
            object window,
            Action<ESWorkbenchPageDefinition> registerPage,
            Action<ESWorkbenchAssetRegistrationSlot> registerAssetSlot,
            Action<ESWorkbenchContributionEntry> registerEntry,
            Action<ESWorkbenchViewportDescriptor> registerViewport,
            Action<ESWorkbenchObjectDescriptor> registerObject,
            Action<ESWorkbenchCollectionSource<ESWorkbenchObjectDescriptor>> registerObjectSource,
            Action<ESWorkbenchHierarchyDescriptor> registerHierarchy,
            Action<ESWorkbenchCollectionSource<ESWorkbenchHierarchyDescriptor>> registerHierarchySource,
            Action<ESWorkbenchAuthoringAdapterDescriptor> registerAuthoringAdapter,
            Action<ESWorkbenchInspectorDescriptor> registerInspector,
            Action<ESWorkbenchToolDescriptor> registerTool,
            Action<ESWorkbenchCommandDescriptor> registerCommand,
            Action<ESWorkbenchCollectionSource<ESWorkbenchIssueDescriptor>> registerIssueSource,
            Action<string> reportDiagnostic)
        {
            WorkbenchId = workbenchId;
            Window = window;
            this.registerPage = registerPage;
            this.registerAssetSlot = registerAssetSlot;
            this.registerEntry = registerEntry;
            this.registerViewport = registerViewport;
            this.registerObject = registerObject;
            this.registerObjectSource = registerObjectSource;
            this.registerHierarchy = registerHierarchy;
            this.registerHierarchySource = registerHierarchySource;
            this.registerAuthoringAdapter = registerAuthoringAdapter;
            this.registerInspector = registerInspector;
            this.registerTool = registerTool;
            this.registerCommand = registerCommand;
            this.registerIssueSource = registerIssueSource;
            this.reportDiagnostic = reportDiagnostic;
        }

        public string WorkbenchId { get; }
        public object Window { get; }

        public void RegisterPage(ESWorkbenchPageDefinition page)
        {
            if (page != null) registerPage?.Invoke(page);
        }

        public void RegisterAssetSlot(ESWorkbenchAssetRegistrationSlot slot)
        {
            if (!string.IsNullOrWhiteSpace(slot.slotId)) registerAssetSlot?.Invoke(slot);
        }

        public void RegisterEntry(ESWorkbenchContributionEntry entry)
        {
            if (entry != null) registerEntry?.Invoke(entry);
        }

        public void RegisterViewport(ESWorkbenchViewportDescriptor viewport)
        {
            if (viewport != null) registerViewport?.Invoke(viewport);
        }

        public void RegisterObject(ESWorkbenchObjectDescriptor item)
        {
            if (item != null) registerObject?.Invoke(item);
        }

        public void RegisterObjectSource(ESWorkbenchCollectionSource<ESWorkbenchObjectDescriptor> source)
        {
            if (source != null) registerObjectSource?.Invoke(source);
        }

        public void RegisterHierarchy(ESWorkbenchHierarchyDescriptor item)
        {
            if (item != null) registerHierarchy?.Invoke(item);
        }

        public void RegisterHierarchySource(ESWorkbenchCollectionSource<ESWorkbenchHierarchyDescriptor> source)
        {
            if (source != null) registerHierarchySource?.Invoke(source);
        }

        public void RegisterAuthoringAdapter(ESWorkbenchAuthoringAdapterDescriptor adapter)
        {
            if (adapter != null) registerAuthoringAdapter?.Invoke(adapter);
        }

        public void RegisterInspector(ESWorkbenchInspectorDescriptor inspector)
        {
            if (inspector != null) registerInspector?.Invoke(inspector);
        }

        public void RegisterTool(ESWorkbenchToolDescriptor tool)
        {
            if (tool != null) registerTool?.Invoke(tool);
        }

        public void RegisterCommand(ESWorkbenchCommandDescriptor command)
        {
            if (command != null) registerCommand?.Invoke(command);
        }

        public void RegisterIssueSource(ESWorkbenchCollectionSource<ESWorkbenchIssueDescriptor> source)
        {
            if (source != null) registerIssueSource?.Invoke(source);
        }

        public void ReportDiagnostic(string message)
        {
            if (!string.IsNullOrWhiteSpace(message)) reportDiagnostic?.Invoke(message);
        }
    }

    /// <summary>贡献在当前工作台实例中的可见目录项。</summary>
    public sealed class ESWorkbenchContributionEntry
    {
        public string ContributionId { get; }
        public string DisplayName { get; }
        public string Owner { get; }
        public ESWorkbenchContributionCategory Category { get; }

        public ESWorkbenchContributionEntry(
            string contributionId,
            string displayName,
            ESWorkbenchContributionCategory category,
            string owner)
        {
            ContributionId = contributionId ?? string.Empty;
            DisplayName = displayName ?? contributionId ?? string.Empty;
            Category = category;
            Owner = owner ?? string.Empty;
        }
    }

    public sealed class ESWorkbenchContributionSession : IDisposable
    {
        private readonly List<IDisposable> releases;

        internal ESWorkbenchContributionSession(
            string workbenchId,
            IReadOnlyList<ESWorkbenchContributionDescriptor> descriptors,
            List<IDisposable> releases,
            IReadOnlyDictionary<string, ESWorkbenchAssetRegistrationSlot> assetSlots,
            List<ESWorkbenchContributionEntry> entries,
            List<ESWorkbenchViewportDescriptor> viewports,
            List<ESWorkbenchObjectDescriptor> objects,
            List<ESWorkbenchCollectionSource<ESWorkbenchObjectDescriptor>> objectSources,
            List<ESWorkbenchHierarchyDescriptor> hierarchy,
            List<ESWorkbenchCollectionSource<ESWorkbenchHierarchyDescriptor>> hierarchySources,
            List<ESWorkbenchAuthoringAdapterDescriptor> authoringAdapters,
            List<ESWorkbenchInspectorDescriptor> inspectors,
            List<ESWorkbenchToolDescriptor> tools,
            List<ESWorkbenchCommandDescriptor> commands,
            List<ESWorkbenchCollectionSource<ESWorkbenchIssueDescriptor>> issueSources,
            List<string> diagnostics)
        {
            WorkbenchId = workbenchId;
            Descriptors = descriptors;
            this.releases = releases;
            AssetSlots = assetSlots;
            Entries = entries;
            Viewports = viewports;
            Objects = objects;
            ObjectSources = objectSources;
            Hierarchy = hierarchy;
            HierarchySources = hierarchySources;
            AuthoringAdapters = authoringAdapters;
            Inspectors = inspectors;
            Tools = tools;
            Commands = commands;
            IssueSources = issueSources;
            Diagnostics = diagnostics;
        }

        public string WorkbenchId { get; }
        public IReadOnlyList<ESWorkbenchContributionDescriptor> Descriptors { get; }
        public IReadOnlyList<ESWorkbenchContributionEntry> Entries { get; }
        public IReadOnlyDictionary<string, ESWorkbenchAssetRegistrationSlot> AssetSlots { get; }
        public IReadOnlyList<ESWorkbenchViewportDescriptor> Viewports { get; }
        public IReadOnlyList<ESWorkbenchObjectDescriptor> Objects { get; }
        public IReadOnlyList<ESWorkbenchCollectionSource<ESWorkbenchObjectDescriptor>> ObjectSources { get; }
        public IReadOnlyList<ESWorkbenchHierarchyDescriptor> Hierarchy { get; }
        public IReadOnlyList<ESWorkbenchCollectionSource<ESWorkbenchHierarchyDescriptor>> HierarchySources { get; }
        public IReadOnlyList<ESWorkbenchAuthoringAdapterDescriptor> AuthoringAdapters { get; }
        public IReadOnlyList<ESWorkbenchInspectorDescriptor> Inspectors { get; }
        public IReadOnlyList<ESWorkbenchToolDescriptor> Tools { get; }
        public IReadOnlyList<ESWorkbenchCommandDescriptor> Commands { get; }
        public IReadOnlyList<ESWorkbenchCollectionSource<ESWorkbenchIssueDescriptor>> IssueSources { get; }
        public IReadOnlyList<string> Diagnostics { get; }

        public void Dispose()
        {
            for (int i = releases.Count - 1; i >= 0; i--)
            {
                try { releases[i]?.Dispose(); }
                catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
            }
            releases.Clear();
        }
    }

    /// <summary>
    /// 工作台贡献目录。RegisterOrUpdate 只写入轻量描述；Open 才在窗口主线程实例化真实页面和工具。
    /// </summary>
    public static class ESWorkbenchContributionRegistry
    {
        private static readonly Dictionary<string, ESWorkbenchContributionDescriptor> descriptors =
            new Dictionary<string, ESWorkbenchContributionDescriptor>(StringComparer.Ordinal);

        public static bool RegisterOrUpdate(ESWorkbenchContributionDescriptor descriptor, out string message)
        {
            message = string.Empty;
            if (descriptor == null) { message = "贡献描述为空。"; return false; }
            if (!descriptors.TryGetValue(descriptor.StableKey, out ESWorkbenchContributionDescriptor existing))
            {
                descriptors.Add(descriptor.StableKey, descriptor);
                return true;
            }

            if (!string.Equals(existing.Owner, descriptor.Owner, StringComparison.Ordinal))
            {
                message = "贡献 ID 冲突：" + descriptor.StableKey + " 已由 " + existing.Owner + " 占用。";
                return false;
            }
            if (descriptor.Revision < existing.Revision)
            {
                message = "忽略旧版本贡献：" + descriptor.StableKey;
                return false;
            }
            if (descriptor.Revision == existing.Revision)
            {
                // 同一模块在 Disable Domain Reload 或窗口重开时可能重新提供新的委托实例；
                // 只要版本一致，按最新声明替换，保持注册幂等且不持有旧窗口闭包。
                descriptors[descriptor.StableKey] = descriptor;
                return true;
            }

            descriptors[descriptor.StableKey] = descriptor;
            return true;
        }

        public static IReadOnlyList<ESWorkbenchContributionDescriptor> GetDescriptors(string workbenchId)
        {
            return descriptors.Values
                .Where(value => string.Equals(value.WorkbenchId, workbenchId, StringComparison.Ordinal))
                .OrderByDescending(value => value.Priority)
                .ThenBy(value => value.ContributionId, StringComparer.Ordinal)
                .ToArray();
        }

        public static ESWorkbenchContributionSession Open(
            string workbenchId,
            object window,
            Action<ESWorkbenchPageDefinition> registerPage,
            Action<ESWorkbenchAssetRegistrationSlot> registerAssetSlot,
            Action<ESWorkbenchContributionEntry> registerEntry,
            Action<ESWorkbenchViewportDescriptor> registerViewport,
            Action<ESWorkbenchObjectDescriptor> registerObject,
            Action<ESWorkbenchCollectionSource<ESWorkbenchObjectDescriptor>> registerObjectSource,
            Action<ESWorkbenchHierarchyDescriptor> registerHierarchy,
            Action<ESWorkbenchCollectionSource<ESWorkbenchHierarchyDescriptor>> registerHierarchySource,
            Action<ESWorkbenchAuthoringAdapterDescriptor> registerAuthoringAdapter,
            Action<ESWorkbenchInspectorDescriptor> registerInspector,
            Action<ESWorkbenchToolDescriptor> registerTool,
            Action<ESWorkbenchCommandDescriptor> registerCommand,
            Action<string> reportDiagnostic,
            Action<ESWorkbenchCollectionSource<ESWorkbenchIssueDescriptor>> registerIssueSource = null)
        {
            IReadOnlyList<ESWorkbenchContributionDescriptor> ordered = GetDescriptors(workbenchId);
            var available = new HashSet<string>(ordered.Select(value => value.ContributionId), StringComparer.Ordinal);
            var injected = new HashSet<string>(StringComparer.Ordinal);
            var releases = new List<IDisposable>();
            var assetSlots = new Dictionary<string, ESWorkbenchAssetRegistrationSlot>(StringComparer.Ordinal);
            var entries = new List<ESWorkbenchContributionEntry>();
            var viewports = new List<ESWorkbenchViewportDescriptor>();
            var objects = new List<ESWorkbenchObjectDescriptor>();
            var objectSources = new List<ESWorkbenchCollectionSource<ESWorkbenchObjectDescriptor>>();
            var hierarchy = new List<ESWorkbenchHierarchyDescriptor>();
            var hierarchySources = new List<ESWorkbenchCollectionSource<ESWorkbenchHierarchyDescriptor>>();
            var authoringAdapters = new List<ESWorkbenchAuthoringAdapterDescriptor>();
            var inspectors = new List<ESWorkbenchInspectorDescriptor>();
            var tools = new List<ESWorkbenchToolDescriptor>();
            var commands = new List<ESWorkbenchCommandDescriptor>();
            var viewportIds = new HashSet<string>(StringComparer.Ordinal);
            var objectIds = new HashSet<string>(StringComparer.Ordinal);
            var objectSourceIds = new HashSet<string>(StringComparer.Ordinal);
            var hierarchyIds = new HashSet<string>(StringComparer.Ordinal);
            var hierarchySourceIds = new HashSet<string>(StringComparer.Ordinal);
            var authoringAdapterIds = new HashSet<string>(StringComparer.Ordinal);
            var inspectorIds = new HashSet<string>(StringComparer.Ordinal);
            var toolIds = new HashSet<string>(StringComparer.Ordinal);
            var commandIds = new HashSet<string>(StringComparer.Ordinal);
            var issueSourceIds = new HashSet<string>(StringComparer.Ordinal);
            var issueSources = new List<ESWorkbenchCollectionSource<ESWorkbenchIssueDescriptor>>();
            var diagnostics = new List<string>();
            var context = new ESWorkbenchContributionContext(
                workbenchId,
                window,
                registerPage,
                slot =>
                {
                    if (assetSlots.ContainsKey(slot.slotId))
                    {
                        string message = "资源注册槽位冲突：" + slot.slotId + "，已保留首次声明。";
                        diagnostics.Add(message);
                        reportDiagnostic?.Invoke(message);
                        return;
                    }
                    assetSlots.Add(slot.slotId, slot);
                    registerAssetSlot?.Invoke(slot);
                },
                entry => { entries.Add(entry); registerEntry?.Invoke(entry); },
                viewport => RegisterUnique(viewport?.ViewportId, viewport, viewportIds, viewports, registerViewport, diagnostics, reportDiagnostic, "视口"),
                item => RegisterUnique(item?.ObjectId, item, objectIds, objects, registerObject, diagnostics, reportDiagnostic, "对象"),
                source => RegisterUnique(source?.SourceId, source, objectSourceIds, objectSources, registerObjectSource, diagnostics, reportDiagnostic, "对象源"),
                item => RegisterUnique(item?.ItemId, item, hierarchyIds, hierarchy, registerHierarchy, diagnostics, reportDiagnostic, "层级项"),
                source => RegisterUnique(source?.SourceId, source, hierarchySourceIds, hierarchySources, registerHierarchySource, diagnostics, reportDiagnostic, "层级源"),
                adapter => RegisterUnique(adapter?.AdapterId, adapter, authoringAdapterIds, authoringAdapters, registerAuthoringAdapter, diagnostics, reportDiagnostic, "作者适配器"),
                inspector => RegisterUnique(inspector?.InspectorId, inspector, inspectorIds, inspectors, registerInspector, diagnostics, reportDiagnostic, "Inspector"),
                tool => RegisterUnique(tool?.ToolId, tool, toolIds, tools, registerTool, diagnostics, reportDiagnostic, "工具"),
                command => RegisterUnique(command?.CommandId, command, commandIds, commands, registerCommand, diagnostics, reportDiagnostic, "命令"),
                source => RegisterUnique(source?.SourceId, source, issueSourceIds, issueSources, registerIssueSource, diagnostics, reportDiagnostic, "问题源"),
                message => { diagnostics.Add(message); reportDiagnostic?.Invoke(message); });

            var pending = new List<ESWorkbenchContributionDescriptor>(ordered);
            while (pending.Count > 0)
            {
                bool progressed = false;
                for (int i = 0; i < pending.Count; i++)
                {
                    ESWorkbenchContributionDescriptor descriptor = pending[i];
                    string missing = descriptor.Dependencies.FirstOrDefault(value => !available.Contains(value));
                    if (!string.IsNullOrEmpty(missing))
                    {
                        string message = "贡献 " + descriptor.ContributionId + " 缺少依赖 " + missing + "，已跳过注入。";
                        diagnostics.Add(message);
                        reportDiagnostic?.Invoke(message);
                        pending.RemoveAt(i--);
                        progressed = true;
                        continue;
                    }
                    if (descriptor.IsEnabled != null && !descriptor.IsEnabled(context))
                    {
                        pending.RemoveAt(i--);
                        progressed = true;
                        continue;
                    }
                    string unresolved = descriptor.Dependencies.FirstOrDefault(value => !injected.Contains(value));
                    if (!string.IsNullOrEmpty(unresolved))
                        continue;

                    try
                    {
                        IDisposable release = descriptor.Inject(context);
                        if (release != null) releases.Add(release);
                        injected.Add(descriptor.ContributionId);
                        entries.Add(new ESWorkbenchContributionEntry(
                            descriptor.ContributionId,
                            descriptor.DisplayName,
                            descriptor.Category,
                            descriptor.Owner));
                    }
                    catch (Exception exception)
                    {
                        string message = "贡献 " + descriptor.ContributionId + " 注入失败：" + exception.Message;
                        diagnostics.Add(message);
                        reportDiagnostic?.Invoke(message);
                        UnityEngine.Debug.LogException(exception);
                    }
                    pending.RemoveAt(i--);
                    progressed = true;
                }
                if (!progressed)
                {
                    for (int i = 0; i < pending.Count; i++)
                    {
                        ESWorkbenchContributionDescriptor descriptor = pending[i];
                        string dependency = descriptor.Dependencies.FirstOrDefault(value => !injected.Contains(value));
                        string message = "贡献 " + descriptor.ContributionId + " 存在循环或失败依赖 " + dependency + "，已跳过注入。";
                        diagnostics.Add(message);
                        reportDiagnostic?.Invoke(message);
                    }
                    break;
                }
            }

            return new ESWorkbenchContributionSession(workbenchId, ordered, releases, assetSlots, entries,
                viewports, objects, objectSources, hierarchy, hierarchySources, authoringAdapters,
                inspectors, tools, commands, issueSources, diagnostics);
        }

        private static void RegisterUnique<T>(
            string id,
            T value,
            HashSet<string> ids,
            List<T> values,
            Action<T> callback,
            List<string> diagnostics,
            Action<string> reportDiagnostic,
            string kind) where T : class
        {
            if (value == null || string.IsNullOrWhiteSpace(id)) return;
            if (!ids.Add(id))
            {
                string message = kind + " ID 冲突：" + id + "，已保留首次声明。";
                diagnostics.Add(message);
                reportDiagnostic?.Invoke(message);
                return;
            }
            values.Add(value);
            callback?.Invoke(value);
        }

        public static void ClearOwner(string owner)
        {
            if (string.IsNullOrWhiteSpace(owner)) return;
            string[] keys = descriptors.Values
                .Where(value => string.Equals(value.Owner, owner, StringComparison.Ordinal))
                .Select(value => value.StableKey)
                .ToArray();
            for (int i = 0; i < keys.Length; i++) descriptors.Remove(keys[i]);
        }
    }
}
#endif
