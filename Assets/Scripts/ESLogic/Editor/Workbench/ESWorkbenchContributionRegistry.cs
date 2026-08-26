#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace ES
{
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
    public sealed class ESWorkbenchContributionDescriptor<TModule> where TModule : struct, Enum
    {
        public string WorkbenchId { get; }
        public string ContributionId { get; }
        public string DisplayName { get; }
        public string Tooltip { get; }
        public string Owner { get; }
        public int Priority { get; }
        public int Revision { get; }
        public TModule Module { get; }
        public ESWorkbenchContributionCategory Category { get; }
        public IReadOnlyList<string> Dependencies { get; }
        public Func<ESWorkbenchContributionContext, bool> IsEnabled { get; }
        public Func<ESWorkbenchContributionContext, IDisposable> Inject { get; }

        public ESWorkbenchContributionDescriptor(
            string workbenchId,
            string contributionId,
            string displayName,
            TModule module,
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
            Module = module;
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
        private readonly Action<ESWorkbenchDocumentDefinition> registerDocument;
        private readonly Action<ESWorkbenchAuthoringModeDefinition> registerAuthoringMode;
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
        private readonly Action<ESWorkbenchHostPresentationDescriptor> registerPresentation;
        private readonly Action<ESWorkbenchBottomPanelDescriptor> registerBottomPanel;
        private readonly Action<string> reportDiagnostic;
        private readonly Action<IDisposable> registerCleanup;

        internal ESWorkbenchContributionContext(
            string workbenchId,
            object window,
            Action<ESWorkbenchDocumentDefinition> registerDocument,
            Action<ESWorkbenchAuthoringModeDefinition> registerAuthoringMode,
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
            Action<ESWorkbenchHostPresentationDescriptor> registerPresentation,
            Action<ESWorkbenchBottomPanelDescriptor> registerBottomPanel,
            Action<string> reportDiagnostic,
            Action<IDisposable> registerCleanup)
        {
            WorkbenchId = workbenchId;
            Window = window;
            this.registerDocument = registerDocument;
            this.registerAuthoringMode = registerAuthoringMode;
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
            this.registerPresentation = registerPresentation;
            this.registerBottomPanel = registerBottomPanel;
            this.reportDiagnostic = reportDiagnostic;
            this.registerCleanup = registerCleanup;
        }

        public string WorkbenchId { get; }
        public object Window { get; }

        /// <summary>
        /// Registers cleanup for side effects created during Inject. The cleanup is
        /// committed with a successful contribution, or disposed immediately when
        /// Inject throws before returning its normal release handle.
        /// </summary>
        public void RegisterCleanup(IDisposable cleanup)
        {
            if (cleanup != null) registerCleanup?.Invoke(cleanup);
        }

        public void RegisterDocument(ESWorkbenchDocumentDefinition document)
        {
            if (document != null) registerDocument?.Invoke(document);
        }

        public void RegisterAuthoringMode(ESWorkbenchAuthoringModeDefinition mode)
        {
            if (mode != null) registerAuthoringMode?.Invoke(mode);
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

        public void RegisterPresentation(ESWorkbenchHostPresentationDescriptor presentation)
        {
            if (presentation != null) registerPresentation?.Invoke(presentation);
        }

        public void RegisterBottomPanel(ESWorkbenchBottomPanelDescriptor panel)
        {
            if (panel != null) registerBottomPanel?.Invoke(panel);
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

    /// <summary>单个贡献的临时注册缓冲。Inject 成功后才提交到窗口会话，避免异常留下半成品。</summary>
    internal sealed class ESWorkbenchContributionBuffer
    {
        internal readonly List<ESWorkbenchDocumentDefinition> Documents = new List<ESWorkbenchDocumentDefinition>();
        internal readonly List<ESWorkbenchAuthoringModeDefinition> AuthoringModes =
            new List<ESWorkbenchAuthoringModeDefinition>();
        internal readonly List<ESWorkbenchAssetRegistrationSlot> AssetSlots = new List<ESWorkbenchAssetRegistrationSlot>();
        internal readonly List<ESWorkbenchContributionEntry> Entries = new List<ESWorkbenchContributionEntry>();
        internal readonly List<ESWorkbenchViewportDescriptor> Viewports = new List<ESWorkbenchViewportDescriptor>();
        internal readonly List<ESWorkbenchObjectDescriptor> Objects = new List<ESWorkbenchObjectDescriptor>();
        internal readonly List<ESWorkbenchCollectionSource<ESWorkbenchObjectDescriptor>> ObjectSources =
            new List<ESWorkbenchCollectionSource<ESWorkbenchObjectDescriptor>>();
        internal readonly List<ESWorkbenchHierarchyDescriptor> Hierarchy = new List<ESWorkbenchHierarchyDescriptor>();
        internal readonly List<ESWorkbenchCollectionSource<ESWorkbenchHierarchyDescriptor>> HierarchySources =
            new List<ESWorkbenchCollectionSource<ESWorkbenchHierarchyDescriptor>>();
        internal readonly List<ESWorkbenchAuthoringAdapterDescriptor> AuthoringAdapters =
            new List<ESWorkbenchAuthoringAdapterDescriptor>();
        internal readonly List<ESWorkbenchInspectorDescriptor> Inspectors = new List<ESWorkbenchInspectorDescriptor>();
        internal readonly List<ESWorkbenchToolDescriptor> Tools = new List<ESWorkbenchToolDescriptor>();
        internal readonly List<ESWorkbenchCommandDescriptor> Commands = new List<ESWorkbenchCommandDescriptor>();
        internal readonly List<ESWorkbenchCollectionSource<ESWorkbenchIssueDescriptor>> IssueSources =
            new List<ESWorkbenchCollectionSource<ESWorkbenchIssueDescriptor>>();
        internal readonly List<ESWorkbenchHostPresentationDescriptor> Presentations =
            new List<ESWorkbenchHostPresentationDescriptor>();
        internal readonly List<ESWorkbenchBottomPanelDescriptor> BottomPanels =
            new List<ESWorkbenchBottomPanelDescriptor>();
        internal readonly List<IDisposable> Cleanups = new List<IDisposable>();
        internal readonly List<string> Diagnostics = new List<string>();
    }

    public sealed class ESWorkbenchContributionSession<TModule> : IDisposable where TModule : struct, Enum
    {
        private readonly List<IDisposable> releases;
        private readonly List<ESWorkbenchContributionDescriptor<TModule>> activeDescriptors;
        private bool disposed;

        internal ESWorkbenchContributionSession(
            string workbenchId,
            IReadOnlyList<ESWorkbenchContributionDescriptor<TModule>> descriptors,
            List<ESWorkbenchContributionDescriptor<TModule>> activeDescriptors,
            List<IDisposable> releases,
            List<ESWorkbenchDocumentDefinition> documents,
            List<ESWorkbenchAuthoringModeDefinition> authoringModes,
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
            List<ESWorkbenchHostPresentationDescriptor> presentations,
            List<ESWorkbenchBottomPanelDescriptor> bottomPanels,
            List<string> diagnostics)
        {
            WorkbenchId = workbenchId;
            Descriptors = descriptors;
            this.activeDescriptors = activeDescriptors;
            this.releases = releases;
            Documents = documents;
            AuthoringModes = authoringModes;
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
            Presentations = presentations;
            BottomPanels = bottomPanels;
            Diagnostics = diagnostics;
        }

        public string WorkbenchId { get; }
        public IReadOnlyList<ESWorkbenchContributionDescriptor<TModule>> Descriptors { get; }
        /// <summary>本次会话实际成功执行 Inject 的贡献；被禁用、依赖失败或注入异常的描述不会进入。</summary>
        public IReadOnlyList<ESWorkbenchContributionDescriptor<TModule>> ActiveDescriptors => activeDescriptors;
        public bool IsDisposed => disposed;
        public IReadOnlyList<ESWorkbenchContributionEntry> Entries { get; }
        public IReadOnlyList<ESWorkbenchDocumentDefinition> Documents { get; }
        public IReadOnlyList<ESWorkbenchAuthoringModeDefinition> AuthoringModes { get; }
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
        public IReadOnlyList<ESWorkbenchHostPresentationDescriptor> Presentations { get; }
        public IReadOnlyList<ESWorkbenchBottomPanelDescriptor> BottomPanels { get; }
        public IReadOnlyList<string> Diagnostics { get; }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            for (int i = releases.Count - 1; i >= 0; i--)
            {
                try { releases[i]?.Dispose(); }
                catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
            }
            releases.Clear();
            activeDescriptors.Clear();
            ClearProjection(Documents);
            ClearProjection(AuthoringModes);
            if (AssetSlots is IDictionary<string, ESWorkbenchAssetRegistrationSlot> slots) slots.Clear();
            ClearProjection(Entries);
            ClearProjection(Viewports);
            ClearProjection(Objects);
            ClearProjection(ObjectSources);
            ClearProjection(Hierarchy);
            ClearProjection(HierarchySources);
            ClearProjection(AuthoringAdapters);
            ClearProjection(Inspectors);
            ClearProjection(Tools);
            ClearProjection(Commands);
            ClearProjection(IssueSources);
            ClearProjection(Presentations);
            ClearProjection(BottomPanels);
        }

        private static void ClearProjection<T>(IReadOnlyList<T> values)
        {
            if (values is IList<T> mutable) mutable.Clear();
        }
    }

    /// <summary>
    /// 工作台贡献目录。RegisterOrUpdate 只写入轻量描述；Open 才在窗口主线程实例化真实页面和工具。
    /// </summary>
    public static class ESWorkbenchContributionRegistry<TModule> where TModule : struct, Enum
    {
        private static readonly Dictionary<string, ESWorkbenchContributionDescriptor<TModule>> descriptors =
            new Dictionary<string, ESWorkbenchContributionDescriptor<TModule>>(StringComparer.Ordinal);

        public static bool RegisterOrUpdate(ESWorkbenchContributionDescriptor<TModule> descriptor, out string message)
        {
            message = string.Empty;
            if (descriptor == null) { message = "贡献描述为空。"; return false; }
            if (!descriptors.TryGetValue(descriptor.StableKey, out ESWorkbenchContributionDescriptor<TModule> existing))
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

        public static IReadOnlyList<ESWorkbenchContributionDescriptor<TModule>> GetDescriptors(string workbenchId)
        {
            return descriptors.Values
                .Where(value => string.Equals(value.WorkbenchId, workbenchId, StringComparison.Ordinal))
                .OrderByDescending(value => value.Priority)
                .ThenBy(value => value.ContributionId, StringComparer.Ordinal)
                .ToArray();
        }

        public static ESWorkbenchContributionSession<TModule> Open(
            string workbenchId,
            IEnumerable<TModule> modules,
            object window,
            Action<ESWorkbenchDocumentDefinition> registerDocument,
            Action<ESWorkbenchAuthoringModeDefinition> registerAuthoringMode,
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
            Action<ESWorkbenchCollectionSource<ESWorkbenchIssueDescriptor>> registerIssueSource = null,
            Action<ESWorkbenchHostPresentationDescriptor> registerPresentation = null,
            Action<ESWorkbenchBottomPanelDescriptor> registerBottomPanel = null)
        {
            TModule[] finalModules = (modules ?? Enumerable.Empty<TModule>())
                .Distinct()
                .ToArray();
            var moduleOrder = new Dictionary<TModule, int>();
            for (int i = 0; i < finalModules.Length; i++) moduleOrder.Add(finalModules[i], i);
            ESWorkbenchContributionDescriptor<TModule>[] ordered = GetDescriptors(workbenchId)
                .Where(value => moduleOrder.ContainsKey(value.Module))
                .OrderBy(value => moduleOrder[value.Module])
                .ThenByDescending(value => value.Priority)
                .ThenBy(value => value.ContributionId, StringComparer.Ordinal)
                .ToArray();
            var available = new HashSet<string>(ordered.Select(value => value.ContributionId), StringComparer.Ordinal);
            var injected = new HashSet<string>(StringComparer.Ordinal);
            var activeDescriptors = new List<ESWorkbenchContributionDescriptor<TModule>>();
            var releases = new List<IDisposable>();
            var documents = new List<ESWorkbenchDocumentDefinition>();
            var authoringModes = new List<ESWorkbenchAuthoringModeDefinition>();
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
            var documentIds = new HashSet<string>(StringComparer.Ordinal);
            var authoringModeIds = new HashSet<string>(StringComparer.Ordinal);
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
            var presentations = new List<ESWorkbenchHostPresentationDescriptor>();
            var bottomPanelIds = new HashSet<string>(StringComparer.Ordinal);
            var bottomPanels = new List<ESWorkbenchBottomPanelDescriptor>();
            var diagnostics = new List<string>();
            ESWorkbenchContributionBuffer activeBuffer = null;
            var context = new ESWorkbenchContributionContext(
                workbenchId,
                window,
                document =>
                {
                    if (activeBuffer != null) activeBuffer.Documents.Add(document);
                    else RegisterUnique(document?.documentId, document, documentIds, documents, registerDocument,
                        diagnostics, reportDiagnostic, "文档");
                },
                mode =>
                {
                    if (activeBuffer != null) activeBuffer.AuthoringModes.Add(mode);
                    else RegisterUnique(mode?.ModeId, mode, authoringModeIds, authoringModes, registerAuthoringMode,
                        diagnostics, reportDiagnostic, "作者模式");
                },
                slot =>
                {
                    if (activeBuffer != null)
                    {
                        activeBuffer.AssetSlots.Add(slot);
                        return;
                    }
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
                entry =>
                {
                    if (activeBuffer != null) activeBuffer.Entries.Add(entry);
                    else { entries.Add(entry); registerEntry?.Invoke(entry); }
                },
                viewport =>
                {
                    if (activeBuffer != null) activeBuffer.Viewports.Add(viewport);
                    else RegisterUnique(viewport?.ViewportId, viewport, viewportIds, viewports, registerViewport,
                        diagnostics, reportDiagnostic, "视口");
                },
                item =>
                {
                    if (activeBuffer != null) activeBuffer.Objects.Add(item);
                    else RegisterUnique(item?.ObjectId, item, objectIds, objects, registerObject,
                        diagnostics, reportDiagnostic, "对象");
                },
                source =>
                {
                    if (activeBuffer != null) activeBuffer.ObjectSources.Add(source);
                    else RegisterUnique(source?.SourceId, source, objectSourceIds, objectSources, registerObjectSource,
                        diagnostics, reportDiagnostic, "对象源");
                },
                item =>
                {
                    if (activeBuffer != null) activeBuffer.Hierarchy.Add(item);
                    else RegisterUnique(item?.ItemId, item, hierarchyIds, hierarchy, registerHierarchy,
                        diagnostics, reportDiagnostic, "层级项");
                },
                source =>
                {
                    if (activeBuffer != null) activeBuffer.HierarchySources.Add(source);
                    else RegisterUnique(source?.SourceId, source, hierarchySourceIds, hierarchySources, registerHierarchySource,
                        diagnostics, reportDiagnostic, "层级源");
                },
                adapter =>
                {
                    if (activeBuffer != null) activeBuffer.AuthoringAdapters.Add(adapter);
                    else RegisterUnique(adapter?.AdapterId, adapter, authoringAdapterIds, authoringAdapters,
                        registerAuthoringAdapter, diagnostics, reportDiagnostic, "作者适配器");
                },
                inspector =>
                {
                    if (activeBuffer != null) activeBuffer.Inspectors.Add(inspector);
                    else RegisterUnique(inspector?.InspectorId, inspector, inspectorIds, inspectors, registerInspector,
                        diagnostics, reportDiagnostic, "Inspector");
                },
                tool =>
                {
                    if (activeBuffer != null) activeBuffer.Tools.Add(tool);
                    else RegisterUnique(tool?.ToolId, tool, toolIds, tools, registerTool,
                        diagnostics, reportDiagnostic, "工具");
                },
                command =>
                {
                    if (activeBuffer != null) activeBuffer.Commands.Add(command);
                    else RegisterUnique(command?.CommandId, command, commandIds, commands, registerCommand,
                        diagnostics, reportDiagnostic, "命令");
                },
                source =>
                {
                    if (activeBuffer != null) activeBuffer.IssueSources.Add(source);
                    else RegisterUnique(source?.SourceId, source, issueSourceIds, issueSources, registerIssueSource,
                        diagnostics, reportDiagnostic, "问题源");
                },
                value =>
                {
                    if (value == null) return;
                    if (activeBuffer != null)
                    {
                        activeBuffer.Presentations.Add(value);
                        return;
                    }
                    if (presentations.Count > 0)
                    {
                        string message = "展示合同冲突：" + value.PresentationId
                            + "，已保留 " + presentations[0].PresentationId + "。";
                        diagnostics.Add(message);
                        reportDiagnostic?.Invoke(message);
                        return;
                    }
                    presentations.Add(value);
                    registerPresentation?.Invoke(value);
                },
                panel =>
                {
                    if (activeBuffer != null) activeBuffer.BottomPanels.Add(panel);
                    else RegisterUnique(panel?.PanelId, panel, bottomPanelIds, bottomPanels, registerBottomPanel,
                        diagnostics, reportDiagnostic, "底部面板");
                },
                message =>
                {
                    if (activeBuffer != null) activeBuffer.Diagnostics.Add(message);
                    else { diagnostics.Add(message); reportDiagnostic?.Invoke(message); }
                },
                cleanup =>
                {
                    if (cleanup == null) return;
                    if (activeBuffer != null) activeBuffer.Cleanups.Add(cleanup);
                    else releases.Add(cleanup);
                });

            Action<ESWorkbenchContributionBuffer> commitBuffer = buffer =>
            {
                for (int i = 0; i < buffer.Documents.Count; i++) context.RegisterDocument(buffer.Documents[i]);
                for (int i = 0; i < buffer.AuthoringModes.Count; i++) context.RegisterAuthoringMode(buffer.AuthoringModes[i]);
                for (int i = 0; i < buffer.AssetSlots.Count; i++) context.RegisterAssetSlot(buffer.AssetSlots[i]);
                for (int i = 0; i < buffer.Entries.Count; i++) context.RegisterEntry(buffer.Entries[i]);
                for (int i = 0; i < buffer.Viewports.Count; i++) context.RegisterViewport(buffer.Viewports[i]);
                for (int i = 0; i < buffer.Objects.Count; i++) context.RegisterObject(buffer.Objects[i]);
                for (int i = 0; i < buffer.ObjectSources.Count; i++) context.RegisterObjectSource(buffer.ObjectSources[i]);
                for (int i = 0; i < buffer.Hierarchy.Count; i++) context.RegisterHierarchy(buffer.Hierarchy[i]);
                for (int i = 0; i < buffer.HierarchySources.Count; i++) context.RegisterHierarchySource(buffer.HierarchySources[i]);
                for (int i = 0; i < buffer.AuthoringAdapters.Count; i++) context.RegisterAuthoringAdapter(buffer.AuthoringAdapters[i]);
                for (int i = 0; i < buffer.Inspectors.Count; i++) context.RegisterInspector(buffer.Inspectors[i]);
                for (int i = 0; i < buffer.Tools.Count; i++) context.RegisterTool(buffer.Tools[i]);
                for (int i = 0; i < buffer.Commands.Count; i++) context.RegisterCommand(buffer.Commands[i]);
                for (int i = 0; i < buffer.IssueSources.Count; i++) context.RegisterIssueSource(buffer.IssueSources[i]);
                for (int i = 0; i < buffer.Presentations.Count; i++) context.RegisterPresentation(buffer.Presentations[i]);
                for (int i = 0; i < buffer.BottomPanels.Count; i++) context.RegisterBottomPanel(buffer.BottomPanels[i]);
                for (int i = 0; i < buffer.Diagnostics.Count; i++) context.ReportDiagnostic(buffer.Diagnostics[i]);
            };

            var pending = new List<ESWorkbenchContributionDescriptor<TModule>>(ordered);
            while (pending.Count > 0)
            {
                bool progressed = false;
                for (int i = 0; i < pending.Count; i++)
                {
                    ESWorkbenchContributionDescriptor<TModule> descriptor = pending[i];
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
                    var buffer = new ESWorkbenchContributionBuffer();
                    bool enabled = true;
                    try
                    {
                        activeBuffer = buffer;
                        enabled = descriptor.IsEnabled == null || descriptor.IsEnabled(context);
                    }
                    catch (Exception exception)
                    {
                        enabled = false;
                        string message = "贡献 " + descriptor.ContributionId
                            + " 可用性检查失败：" + exception.Message;
                        diagnostics.Add(message);
                        reportDiagnostic?.Invoke(message);
                        UnityEngine.Debug.LogException(exception);
                    }
                    finally
                    {
                        activeBuffer = null;
                    }
                    if (!enabled)
                    {
                        for (int diagnosticIndex = 0; diagnosticIndex < buffer.Diagnostics.Count; diagnosticIndex++)
                        {
                            string diagnostic = buffer.Diagnostics[diagnosticIndex];
                            diagnostics.Add(diagnostic);
                            reportDiagnostic?.Invoke(diagnostic);
                        }
                        pending.RemoveAt(i--);
                        progressed = true;
                        continue;
                    }
                    string unresolved = descriptor.Dependencies.FirstOrDefault(value => !injected.Contains(value));
                    if (!string.IsNullOrEmpty(unresolved))
                        continue;

                    try
                    {
                        activeBuffer = buffer;
                        IDisposable release = descriptor.Inject(context);
                        activeBuffer = null;
                        if (release != null) releases.Add(release);
                        for (int cleanupIndex = 0; cleanupIndex < buffer.Cleanups.Count; cleanupIndex++)
                            releases.Add(buffer.Cleanups[cleanupIndex]);
                        buffer.Cleanups.Clear();
                        commitBuffer(buffer);
                        injected.Add(descriptor.ContributionId);
                        activeDescriptors.Add(descriptor);
                        entries.Add(new ESWorkbenchContributionEntry(
                            descriptor.ContributionId,
                            descriptor.DisplayName,
                            descriptor.Category,
                            descriptor.Owner));
                    }
                    catch (Exception exception)
                    {
                        DisposeContributionCleanups(buffer.Cleanups);
                        string message = "贡献 " + descriptor.ContributionId + " 注入失败：" + exception.Message;
                        diagnostics.Add(message);
                        reportDiagnostic?.Invoke(message);
                        UnityEngine.Debug.LogException(exception);
                    }
                    finally
                    {
                        activeBuffer = null;
                    }
                    pending.RemoveAt(i--);
                    progressed = true;
                }
                if (!progressed)
                {
                    for (int i = 0; i < pending.Count; i++)
                    {
                        ESWorkbenchContributionDescriptor<TModule> descriptor = pending[i];
                        string dependency = descriptor.Dependencies.FirstOrDefault(value => !injected.Contains(value));
                        string message = "贡献 " + descriptor.ContributionId + " 存在循环或失败依赖 " + dependency + "，已跳过注入。";
                        diagnostics.Add(message);
                        reportDiagnostic?.Invoke(message);
                    }
                    break;
                }
            }

            return new ESWorkbenchContributionSession<TModule>(workbenchId, ordered, activeDescriptors, releases,
                documents, authoringModes, assetSlots, entries,
                viewports, objects, objectSources, hierarchy, hierarchySources, authoringAdapters,
                inspectors, tools, commands, issueSources, presentations, bottomPanels, diagnostics);
        }

        private static void DisposeContributionCleanups(List<IDisposable> cleanups)
        {
            for (int i = cleanups.Count - 1; i >= 0; i--)
            {
                try { cleanups[i]?.Dispose(); }
                catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
            }
            cleanups.Clear();
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
