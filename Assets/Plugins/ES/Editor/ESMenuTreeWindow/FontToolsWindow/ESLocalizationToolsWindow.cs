using System;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    /// <summary>
    /// 本地化工具。只编辑 ESLocalizationCatalog，不复制生成、发布或运行时解析管线。
    /// </summary>
    public sealed class ESLocalizationToolsWindow : ESMenuTreeWindow<ESLocalizationToolsWindow>
    {
        private const string CatalogPath = "Assets/ESNormalAssets/Localization/ESLocalizationCatalog.asset";
        private readonly List<ESLocalizationCatalogEntry> visibleEntries = new List<ESLocalizationCatalogEntry>();
        private static readonly EnumCollect.Envir_LanguageType[] SupportedLanguages =
            Enumerable.Range(0, ESLocaleIdentity.SupportedLanguageCount)
                .Select(ESLocaleIdentity.GetSupportedLanguageAt)
                .ToArray();
        private ESLocalizationCatalog catalog;
        private TextField searchField;
        private TextField newKeyField;
        private PopupField<string> localeFilter;
        private ListView entryList;
        private VisualElement detailHost;
        private SerializedObject detailSerializedCatalog;
        private VisualElement matrixHost;
        private Label summaryLabel;
        private VisualElement validationHost;
        private readonly List<string> validationIssues = new List<string>();
        private bool hasValidationResult;
        private Label previewLabel;
        private TextField previewKeyField;
        private TextField previewArgumentsField;
        private EnumField previewLanguageField;
        private ESAssetLibraryConsumer residentConsumer;
        private string sourceState = "源表：未验证";
        private int selectedIndex = -1;

        [MenuItem(MenuItemPathDefine.LOCALIZATION_WORKBENCH_WINDOW_PATH, false, 21)]
        [MenuItem(MenuItemPathDefine.QUICK_WINDOWS_PATH + "本地化工具", false, -944)]
        public static void TryOpenWindow()
        {
            ESWindowCommandRegistry.RecordOpened("localization_workbench");
            OpenWindow();
        }

        public override GUIContent ESWindow_GetWindowGUIContent() =>
            new GUIContent("ES 本地化工具", "管理 TextKey、Locale、缺失翻译与预览。");
        public override string ESWindow_PresentationShortTitle => "本地化";

        protected override string ESWindow_Subtitle => "TextKey、语言目录、翻译审查与运行时预览";
        protected override Vector2 ESWindow_MinSize => new Vector2(820f, 560f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(1180f, 760f);

        protected override void ESWindow_OnHostDisable()
        {
            ReleaseDetailSerializedCatalog();
            base.ESWindow_OnHostDisable();
        }

        private void ReleaseDetailSerializedCatalog()
        {
            try { detailSerializedCatalog?.Dispose(); }
            catch (Exception exception) { Debug.LogException(exception); }
            finally { detailSerializedCatalog = null; }
        }

        protected override void ESWindow_BuildMenuTree(ESMenuTreeBuilder builder)
        {
            builder.Add(ESMenuTreePageDefinition
                .ForPanel("localization.catalog", "目录 / 翻译审查", BuildCatalogPage)
                .WithUnityIcon("TextAsset Icon")
                .WithKeywords("本地化 Localization Locale TextKey 翻译 缺失 Luban")
                .WithLayout(ESMenuTreePageLayout.Standard, 1180f, 14f)
                .WithSelectionFeedback("已打开本地化目录", ESEditorFeedbackSoundKind.Open));
            builder.Add(ESMenuTreePageDefinition
                .ForPanel("localization.matrix", "矩阵 / 语言覆盖", BuildMatrixPage)
                .WithUnityIcon("Grid Icon")
                .WithKeywords("本地化 Localization Locale TextKey 矩阵 覆盖 缺失")
                .WithLayout(ESMenuTreePageLayout.Standard, 1180f, 14f)
                .WithSelectionFeedback("已打开本地化语言矩阵", ESEditorFeedbackSoundKind.Navigate));
        }

        private void BuildCatalogPage(ESMenuTreePageContext context, VisualElement root)
        {
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            var toolbar = new Toolbar();
            var catalogField = new ObjectField("本地化目录")
            {
                objectType = typeof(ESLocalizationCatalog),
                allowSceneObjects = false,
                value = catalog
            };
            catalogField.style.minWidth = 260f;
            catalogField.RegisterValueChangedCallback(evt =>
            {
                catalog = evt.newValue as ESLocalizationCatalog;
                selectedIndex = -1;
                validationIssues.Clear();
                hasValidationResult = false;
                RefreshSourceState();
                RebuildList();
                RebuildDetails();
                RebuildValidationPanel();
            });
            toolbar.Add(catalogField);
            toolbar.Add(new Button(() =>
            {
                try
                {
                    ESLocalizationCatalogEditor.BuildLubanTextCatalog();
                    catalog = AssetDatabase.LoadAssetAtPath<ESLocalizationCatalog>(CatalogPath);
                    catalogField.value = catalog;
                    RefreshSourceState();
                    RebuildList();
                    RebuildDetails();
                    context.Notify("已从 Luban 文本表更新本地化目录", ESMenuTreePageStatus.Ready, ESEditorFeedbackSoundKind.Success, false);
                }
                catch (Exception exception)
                {
                    context.Notify("本地化目录生成失败：" + exception.Message, ESMenuTreePageStatus.Error, ESEditorFeedbackSoundKind.Error);
                }
            }) { text = "从 Luban 生成" });
            toolbar.Add(new Button(() => ValidateCatalog(context)) { text = "验证" });
            toolbar.Add(new Button(() => SaveCatalog(context)) { text = "保存目录" });
            toolbar.Add(new Button(() => LocateCatalog(context)) { text = "定位" });
            toolbar.Add(new Button(() =>
            {
                if (catalog != null) ESResourceCollectionWorkflowWindow.OpenForAssetRegistration(catalog);
            }) { text = "登记资源" });
            var consumerField = new ObjectField("启动 Consumer")
            {
                objectType = typeof(ESAssetLibraryConsumer),
                allowSceneObjects = false,
                value = residentConsumer
            };
            consumerField.style.minWidth = 220f;
            consumerField.RegisterValueChangedCallback(evt => residentConsumer = evt.newValue as ESAssetLibraryConsumer);
            toolbar.Add(consumerField);
            toolbar.Add(new Button(() => RegisterResidentAsset(context)) { text = "登记启动常驻" });
            root.Add(toolbar);

            summaryLabel = new Label { style = { marginTop = 6f, marginBottom = 6f } };
            root.Add(summaryLabel);
            validationHost = new VisualElement();
            validationHost.style.marginBottom = 6f;
            root.Add(validationHost);
            var filterRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            searchField = new TextField("搜索 Key") { style = { flexGrow = 1f } };
            searchField.RegisterValueChangedCallback(_ => RebuildList());
            filterRow.Add(searchField);
            var languageOptions = new List<string> { "全部" };
            languageOptions.AddRange(SupportedLanguages.Select(ESLocaleIdentity.GetDisplayName));
            localeFilter = new PopupField<string>("语言", languageOptions, 0);
            localeFilter.style.width = 190f;
            localeFilter.RegisterValueChangedCallback(_ => RebuildList());
            filterRow.Add(localeFilter);
            root.Add(filterRow);

            var authorRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4f } };
            newKeyField = new TextField("新建 TextKey") { style = { flexGrow = 1f } };
            newKeyField.tooltip = "输入稳定 TextKey；创建后会一次生成十种语言的空翻译条目。";
            authorRow.Add(newKeyField);
            authorRow.Add(new Button(() => AddTextKey(context)) { text = "新增十语言 Key" });
            authorRow.Add(new Button(() => AddMissingLanguageEntries(context)) { text = "补齐缺失语言条目" });
            root.Add(authorRow);

            var split = new TwoPaneSplitView(0, 520f, TwoPaneSplitViewOrientation.Horizontal);
            entryList = new ListView(visibleEntries, 22f, MakeEntryItem, BindEntryItem)
            {
                selectionType = SelectionType.Single,
                showBorder = true,
                showAlternatingRowBackgrounds = AlternatingRowBackground.All
            };
            entryList.selectionChanged += OnEntrySelected;
            split.Add(entryList);
            detailHost = new ScrollView(ScrollViewMode.Vertical);
            detailHost.style.paddingLeft = 10f;
            detailHost.style.paddingRight = 10f;
            split.Add(detailHost);
            root.Add(split);

            var previewBox = new VisualElement { style = { marginTop = 8f, paddingTop = 6f, borderTopWidth = 1f, borderTopColor = new Color(0.35f, 0.35f, 0.35f, 0.8f) } };
            previewKeyField = new TextField("预览 Key");
            previewKeyField.RegisterValueChangedCallback(_ => RefreshPreview());
            previewBox.Add(previewKeyField);
            previewArgumentsField = new TextField("预览参数")
            {
                tooltip = "使用 name=value;count=3 格式。模板支持 {name}、{count|plural|one=# item;other=# items}、{kind|select|a=甲;other=其他}，字面花括号写 {{ 或 }}。"
            };
            previewArgumentsField.RegisterValueChangedCallback(_ => RefreshPreview());
            previewBox.Add(previewArgumentsField);
            previewLanguageField = new EnumField("预览语言", EnumCollect.Envir_LanguageType.ChineseSimplified);
            previewLanguageField.RegisterValueChangedCallback(_ => RefreshPreview());
            previewBox.Add(previewLanguageField);
            previewLabel = new Label("选择目录后输入 TextKey 预览") { style = { whiteSpace = WhiteSpace.Normal, minHeight = 32f } };
            previewBox.Add(previewLabel);
            root.Add(previewBox);

            catalog = AssetDatabase.LoadAssetAtPath<ESLocalizationCatalog>(CatalogPath) ?? catalog;
            catalogField.value = catalog;
            RefreshSourceState();
            RebuildList();
            RebuildDetails();
            RebuildValidationPanel();
        }

        private void BuildMatrixPage(ESMenuTreePageContext context, VisualElement root)
        {
            catalog = catalog ?? AssetDatabase.LoadAssetAtPath<ESLocalizationCatalog>(CatalogPath);
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;
            var toolbar = new Toolbar();
            var catalogField = new ObjectField("本地化目录")
            {
                objectType = typeof(ESLocalizationCatalog),
                allowSceneObjects = false,
                value = catalog
            };
            catalogField.style.minWidth = 260f;
            catalogField.RegisterValueChangedCallback(evt =>
            {
                catalog = evt.newValue as ESLocalizationCatalog;
                validationIssues.Clear();
                hasValidationResult = false;
                BuildMatrixRows(matrixHost);
                RebuildValidationPanel();
            });
            toolbar.Add(catalogField);
            toolbar.Add(new Button(() => BuildMatrixRows(matrixHost)) { text = "刷新矩阵" });
            toolbar.Add(new Button(() => AddMissingLanguageEntries(context)) { text = "补齐缺失语言条目" });
            toolbar.Add(new Button(() => ValidateCatalog(context)) { text = "验证" });
            root.Add(toolbar);
            validationHost = new VisualElement { style = { marginTop = 6f, marginBottom = 2f } };
            root.Add(validationHost);
            matrixHost = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            matrixHost.style.marginTop = 8f;
            root.Add(matrixHost);
            RebuildValidationPanel();
            BuildMatrixRows(matrixHost);
        }

        private void BuildMatrixRows(VisualElement host)
        {
            if (host == null) return;
            host.Clear();
            if (catalog == null)
            {
                host.Add(new HelpBox("请绑定 ESLocalizationCatalog。", HelpBoxMessageType.Info));
                return;
            }
            var keys = new HashSet<ESTextKey>();
            foreach (ESLocalizationCatalogEntry entry in catalog.entries ?? new List<ESLocalizationCatalogEntry>())
            {
                ESTextKey key = entry == null ? default : new ESTextKey(entry.textKey);
                if (key.IsValid) keys.Add(key);
            }
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4f } };
            AddMatrixCell(header, "TextKey", 240f, true);
            foreach (EnumCollect.Envir_LanguageType language in SupportedLanguages)
                AddMatrixCell(header, GetLanguageName(language), 190f, true);
            host.Add(header);
            foreach (ESTextKey key in keys.OrderBy(item => item.Value, StringComparer.Ordinal))
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2f } };
                AddMatrixCell(row, key.Value, 240f, false);
                foreach (EnumCollect.Envir_LanguageType language in SupportedLanguages)
                {
                    string value = catalog.TryResolve(key, language, out string resolved) ? resolved : "[缺失]";
                    AddMatrixCell(row, value, 190f, false, value == "[缺失]");
                }
                host.Add(row);
            }
        }

        private static void AddMatrixCell(VisualElement row, string text, float width, bool header, bool warning = false)
        {
            var label = new Label(text ?? string.Empty);
            label.style.width = width;
            label.style.minHeight = 24f;
            label.style.paddingLeft = 6f;
            label.style.paddingRight = 6f;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.borderBottomWidth = 1f;
            label.style.borderBottomColor = new Color(0.32f, 0.32f, 0.32f, 0.55f);
            if (header) label.style.unityFontStyleAndWeight = FontStyle.Bold;
            if (warning) label.style.color = new Color(0.82f, 0.48f, 0.2f, 1f);
            row.Add(label);
        }

        private VisualElement MakeEntryItem() => new Label { style = { unityTextAlign = TextAnchor.MiddleLeft } };

        private void BindEntryItem(VisualElement element, int index)
        {
            if (element is Label label && index >= 0 && index < visibleEntries.Count)
            {
                ESLocalizationCatalogEntry entry = visibleEntries[index];
                label.text = entry.textKey + "  ·  " + GetLanguageName(entry.language) + (string.IsNullOrEmpty(entry.value) ? "  [缺失]" : string.Empty);
            }
        }

        private void OnEntrySelected(IEnumerable<object> selected)
        {
            object value = selected?.FirstOrDefault();
            selectedIndex = value == null ? -1 : visibleEntries.IndexOf(value as ESLocalizationCatalogEntry);
            RebuildDetails();
            if (selectedIndex >= 0 && previewKeyField != null)
            {
                previewKeyField.value = visibleEntries[selectedIndex].textKey;
                RefreshPreview();
            }
        }

        private void RebuildList()
        {
            visibleEntries.Clear();
            if (catalog != null && catalog.entries != null)
            {
                string query = searchField?.value?.Trim() ?? string.Empty;
                string language = localeFilter?.value ?? "全部";
                visibleEntries.AddRange(catalog.entries.Where(entry => entry != null
                    && (string.IsNullOrEmpty(query) || entry.textKey.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    && (language == "全部" || GetLanguageName(entry.language) == language)));
            }
            if (entryList != null)
            {
                entryList.itemsSource = visibleEntries;
                entryList.Rebuild();
            }
            if (summaryLabel != null)
            {
                int missing = visibleEntries.Count(entry => entry == null || string.IsNullOrEmpty(entry.value));
                summaryLabel.text = catalog == null
                    ? "未绑定目录。"
                    : BuildCoverageSummary(missing);
            }
        }

        private string BuildCoverageSummary(int visibleMissing)
        {
            var keys = new HashSet<ESTextKey>();
            var cells = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESLocalizationCatalogEntry entry in catalog.entries ?? new List<ESLocalizationCatalogEntry>())
            {
                ESTextKey key = entry == null ? default : new ESTextKey(entry.textKey);
                if (!key.IsValid || !ESLocalizationRuntime.IsConcreteLanguage(entry.language)) continue;
                keys.Add(key);
                if (!string.IsNullOrEmpty(entry.value)) cells.Add(key + "|" + entry.language);
            }
            int expected = keys.Count * SupportedLanguages.Length;
            int missingCells = Math.Max(0, expected - cells.Count);
            string pipelineState = ESPresentationAssetPipelineStatus.Describe(catalog, residentConsumer);
            return $"Key：{keys.Count}    可用单元：{cells.Count}/{expected}    当前筛选缺失：{visibleMissing}    全局缺失：{missingCells}    {sourceState}\n发布链：{pipelineState}\n目录：{AssetDatabase.GetAssetPath(catalog)}";
        }

        private void RebuildDetails()
        {
            if (detailHost == null) return;
            ReleaseDetailSerializedCatalog();
            detailHost.Clear();
            if (catalog == null)
            {
                detailHost.Add(new HelpBox("请绑定 ESLocalizationCatalog，或点击“从 Luban 生成”。", HelpBoxMessageType.Info));
                return;
            }
            if (selectedIndex < 0 || selectedIndex >= visibleEntries.Count)
            {
                detailHost.Add(new HelpBox("从左侧选择一个 Key/Locale 条目。编辑会使用 Unity SerializedObject 和 Undo。", HelpBoxMessageType.Info));
                return;
            }
            ESLocalizationCatalogEntry selected = visibleEntries[selectedIndex];
            int actualIndex = catalog.entries.IndexOf(selected);
            detailSerializedCatalog = new SerializedObject(catalog);
            SerializedProperty entryProperty = detailSerializedCatalog.FindProperty("entries").GetArrayElementAtIndex(actualIndex);
            var propertyField = new PropertyField(entryProperty, "条目");
            propertyField.Bind(detailSerializedCatalog);
            propertyField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                catalog.InvalidateIndex();
                RebuildList();
                RefreshPreview();
            });
            detailHost.Add(propertyField);
            detailHost.Add(new HelpBox("协议身份：TextKey + Locale。显示文本可以修改，Key 不应作为业务值重复使用。", HelpBoxMessageType.None));
        }

        private void AddTextKey(ESMenuTreePageContext context)
        {
            if (catalog == null)
            {
                context.Notify("请先绑定或生成本地化目录。", ESMenuTreePageStatus.Warning);
                return;
            }
            string key = newKeyField?.value ?? string.Empty;
            if (!ESTextKey.IsCanonical(key))
            {
                context.Notify("TextKey 不能为空，也不能包含首尾空白。", ESMenuTreePageStatus.Warning);
                return;
            }
            if ((catalog.entries ?? new List<ESLocalizationCatalogEntry>())
                .Any(entry => entry != null && string.Equals(entry.textKey, key, StringComparison.Ordinal)))
            {
                context.Notify("TextKey 已存在：" + key, ESMenuTreePageStatus.Warning);
                return;
            }

            Undo.RecordObject(catalog, "新增十语言 TextKey");
            catalog.entries = catalog.entries ?? new List<ESLocalizationCatalogEntry>();
            foreach (EnumCollect.Envir_LanguageType language in SupportedLanguages)
            {
                catalog.entries.Add(new ESLocalizationCatalogEntry
                {
                    textKey = key,
                    language = language,
                    value = string.Empty
                });
            }
            catalog.InvalidateIndex();
            EditorUtility.SetDirty(catalog);
            newKeyField.value = string.Empty;
            RebuildList();
            BuildMatrixRows(matrixHost);
            context.Notify("已新增 TextKey，并建立十种语言条目；请补齐默认语言后再验证。",
                ESMenuTreePageStatus.Ready, ESEditorFeedbackSoundKind.Success);
        }

        private void AddMissingLanguageEntries(ESMenuTreePageContext context)
        {
            if (catalog == null)
            {
                context.Notify("请先绑定或生成本地化目录。", ESMenuTreePageStatus.Warning);
                return;
            }
            var keys = new HashSet<ESTextKey>();
            var existing = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESLocalizationCatalogEntry entry in catalog.entries ?? new List<ESLocalizationCatalogEntry>())
            {
                if (entry == null) continue;
                ESTextKey key = new ESTextKey(entry.textKey);
                if (!key.IsValid) continue;
                keys.Add(key);
                if (ESLocalizationRuntime.IsConcreteLanguage(entry.language))
                    existing.Add(key.Value + "|" + entry.language);
            }
            var missing = new List<ESLocalizationCatalogEntry>();
            foreach (ESTextKey key in keys.OrderBy(item => item.Value, StringComparer.Ordinal))
            {
                foreach (EnumCollect.Envir_LanguageType language in SupportedLanguages)
                {
                    if (existing.Contains(key.Value + "|" + language)) continue;
                    missing.Add(new ESLocalizationCatalogEntry
                    {
                        textKey = key.Value,
                        language = language,
                        value = string.Empty
                    });
                }
            }
            if (missing.Count == 0)
            {
                context.Notify("当前 TextKey 已全部具备十语言条目。", ESMenuTreePageStatus.Ready);
                return;
            }
            if (!EditorUtility.DisplayDialog("补齐本地化语言条目",
                "将新增 " + missing.Count + " 个空翻译条目。空翻译会继续被验证阻断，直到作者补齐内容。",
                "继续", "取消"))
                return;

            Undo.RecordObject(catalog, "补齐十语言本地化条目");
            catalog.entries = catalog.entries ?? new List<ESLocalizationCatalogEntry>();
            catalog.entries.AddRange(missing);
            catalog.InvalidateIndex();
            EditorUtility.SetDirty(catalog);
            RebuildList();
            BuildMatrixRows(matrixHost);
            context.Notify("已补齐 " + missing.Count + " 个语言条目；请完成翻译后重新验证。",
                ESMenuTreePageStatus.Ready, ESEditorFeedbackSoundKind.Success);
        }

        private void RefreshPreview()
        {
            if (previewLabel == null) return;
            if (catalog == null || string.IsNullOrWhiteSpace(previewKeyField?.value))
            {
                previewLabel.text = "选择目录后输入 TextKey 预览";
                return;
            }
            EnumCollect.Envir_LanguageType language = ESLocalizationRuntime.Resolve(
                (EnumCollect.Envir_LanguageType)previewLanguageField.value);
            if (!TryParsePreviewArguments(previewArgumentsField?.value, out Dictionary<string, string> arguments, out string argumentError))
            {
                previewLabel.text = argumentError;
                return;
            }
            if (!TryResolvePreviewTemplate(new ESTextKey(previewKeyField.value), language,
                out string template, out EnumCollect.Envir_LanguageType resolvedLanguage))
            {
                previewLabel.text = "当前语言及其完整回退链都没有该 TextKey。";
                return;
            }
            if (!ESLocalizationRuntime.TryFormatDetailed(template, arguments, resolvedLanguage, null,
                out string formatted, out ESLocalizationTemplateError templateError))
            {
                previewLabel.text = "预览失败：" + templateError;
                return;
            }
            previewLabel.text = resolvedLanguage == language
                ? formatted
                : "[回退到" + GetLanguageName(resolvedLanguage) + "] " + formatted;
        }

        private bool TryResolvePreviewTemplate(ESTextKey key,
            EnumCollect.Envir_LanguageType requestedLanguage,
            out string template,
            out EnumCollect.Envir_LanguageType resolvedLanguage)
        {
            template = null;
            resolvedLanguage = EnumCollect.Envir_LanguageType.NotClear;
            if (catalog.TryResolve(key, requestedLanguage, out template))
            {
                resolvedLanguage = requestedLanguage;
                return true;
            }
            for (int index = 0; ESLocalizationRuntime.TryGetFallbackLanguage(
                requestedLanguage, index, out EnumCollect.Envir_LanguageType fallback); index++)
            {
                if (!catalog.TryResolve(key, fallback, out template)) continue;
                resolvedLanguage = fallback;
                return true;
            }
            return false;
        }

        private static bool TryParsePreviewArguments(
            string raw,
            out Dictionary<string, string> arguments,
            out string error)
        {
            arguments = new Dictionary<string, string>(StringComparer.Ordinal);
            error = null;
            if (string.IsNullOrWhiteSpace(raw)) return true;
            foreach (string segment in raw.Split(';'))
            {
                string item = segment.Trim();
                if (item.Length == 0) continue;
                int separator = item.IndexOf('=');
                if (separator <= 0)
                {
                    error = "参数格式错误，应为 name=value;count=3。";
                    return false;
                }
                string name = item.Substring(0, separator).Trim();
                string value = item.Substring(separator + 1);
                if (!ESLocalizationRuntime.IsValidArgumentName(name))
                {
                    error = "参数名无效：" + name;
                    return false;
                }
                if (!arguments.TryAdd(name, value))
                {
                    error = "参数重复：" + name;
                    return false;
                }
            }
            return true;
        }

        private void ValidateCatalog(ESMenuTreePageContext context)
        {
            if (catalog == null) { context.Notify("尚未绑定本地化目录。", ESMenuTreePageStatus.Warning); return; }
            var errors = new List<string>(catalog.Validate());
            IReadOnlyList<string> sourceErrors = ESLocalizationCatalogEditor.ValidateSource(catalog);
            errors.AddRange(sourceErrors);
            validationIssues.Clear();
            validationIssues.AddRange(errors);
            hasValidationResult = true;
            sourceState = sourceErrors.Count == 0 ? "源表：一致" : "源表：需复核";
            RebuildList();
            RebuildValidationPanel();
            context.Notify(errors.Count == 0 ? "本地化目录验证通过。" : "发现 " + errors.Count + " 个问题。", errors.Count == 0 ? ESMenuTreePageStatus.Ready : ESMenuTreePageStatus.Error, ESEditorFeedbackSoundKind.Navigate);
        }

        private void RebuildValidationPanel()
        {
            if (validationHost == null) return;
            validationHost.Clear();
            if (catalog == null)
            {
                validationHost.Add(new HelpBox("状态：未绑定目录。下一步：选择目录或从 Luban 生成。", HelpBoxMessageType.Info));
                return;
            }
            if (!hasValidationResult)
            {
                validationHost.Add(new HelpBox("状态：尚未执行完整验证。下一步：点击“验证”检查目录、模板和源表签名。", HelpBoxMessageType.Info));
                return;
            }
            if (validationIssues.Count == 0)
            {
                validationHost.Add(new HelpBox("状态：当前未发现验证问题。发布前仍需完成资源登记、Consumer、Bake、Player 与字体覆盖验证。", HelpBoxMessageType.Info));
                return;
            }

            var message = new System.Text.StringBuilder();
            message.Append("状态：验证未通过。影响：目录不能作为已验收的运行时翻译源。\n恢复：按以下问题修正后重新点击“验证”。");
            int visibleCount = Math.Min(8, validationIssues.Count);
            for (int i = 0; i < visibleCount; i++)
                message.Append("\n").Append(i + 1).Append(". ").Append(validationIssues[i]);
            if (validationIssues.Count > visibleCount)
                message.Append("\n…其余 ").Append(validationIssues.Count - visibleCount).Append(" 项可复制查看。");
            validationHost.Add(new HelpBox(message.ToString(), HelpBoxMessageType.Error));
            validationHost.Add(new Button(() =>
            {
                GUIUtility.systemCopyBuffer = string.Join("\n", validationIssues);
            }) { text = "复制完整问题" });
        }

        private void RefreshSourceState()
        {
            if (catalog == null)
            {
                sourceState = "源表：未验证";
                return;
            }
            IReadOnlyList<string> errors = ESLocalizationCatalogEditor.ValidateSource(catalog);
            sourceState = errors.Count == 0 ? "源表：一致" : "源表：需复核";
        }

        private void LocateCatalog(ESMenuTreePageContext context)
        {
            if (catalog == null) return;
            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);
            context.Notify("已定位本地化目录。", ESMenuTreePageStatus.Ready, ESEditorFeedbackSoundKind.Navigate, false);
        }

        private void RegisterResidentAsset(ESMenuTreePageContext context)
        {
            if (catalog == null || residentConsumer == null)
            {
                context.Notify("请先绑定本地化目录和目标 Consumer。", ESMenuTreePageStatus.Warning);
                return;
            }
            if (!ESAssetConsumerReferenceAuthoring.TryAddResidentAsset(residentConsumer, catalog, out string error))
            {
                context.Notify("登记启动常驻失败：" + error, ESMenuTreePageStatus.Error, ESEditorFeedbackSoundKind.Error);
                return;
            }
            context.Notify("本地化目录已登记到 Consumer 启动常驻资产。", ESMenuTreePageStatus.Ready, ESEditorFeedbackSoundKind.Success);
        }

        private void SaveCatalog(ESMenuTreePageContext context)
        {
            if (catalog == null)
            {
                context.Notify("尚未绑定本地化目录。", ESMenuTreePageStatus.Warning);
                return;
            }
            AssetDatabase.SaveAssetIfDirty(catalog);
            context.Notify("本地化目录已保存。", ESMenuTreePageStatus.Ready, ESEditorFeedbackSoundKind.Success, false);
        }

        private static string GetLanguageName(EnumCollect.Envir_LanguageType language)
        {
            return ESLocaleIdentity.GetDisplayName(language);
        }
    }
}
