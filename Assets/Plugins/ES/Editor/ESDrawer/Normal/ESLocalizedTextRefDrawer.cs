using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ESLocalizedTextRef 的统一中文序列化投影。只使用当前 SerializedProperty，
    /// 不缓存目标对象，也不在绘制期间迁移或规范化权威数据。
    /// </summary>
    [CustomPropertyDrawer(typeof(ESLocalizedTextRef))]
    public sealed class ESLocalizedTextRefDrawer : PropertyDrawer
    {
        private const float Gap = 2f;
        private static readonly GUIContent TextKeyLabel = new GUIContent(
            "文本 Key", "稳定文本身份。不能使用显示文本充当 Key，也不能包含首尾空白。");
        private static readonly GUIContent LanguageLabel = new GUIContent(
            "指定语言", "“使用当前游戏语言”会在运行时跟随当前 Locale；选择具体语言则固定解析该语言及其回退链。");
        private static readonly GUIContent FallbackLabel = new GUIContent(
            "缺失回退文本", "Provider 或 TextKey 缺失时显示的明确回退文本；它不能替代正式翻译。");
        private static readonly string NonCanonicalMessage = "文本 Key 包含首尾空白；请显式修正，Drawer 不会静默改写稳定身份。";

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
                return line;

            SerializedProperty textKey = property.FindPropertyRelative("textKey");
            SerializedProperty language = property.FindPropertyRelative("language");
            SerializedProperty fallbackLiteral = property.FindPropertyRelative("fallbackLiteral");
            if (textKey == null || language == null || fallbackLiteral == null)
                return line * 2f + Gap;

            float height = line;
            height += Gap + EditorGUI.GetPropertyHeight(textKey, TextKeyLabel, true);
            height += Gap + EditorGUI.GetPropertyHeight(language, LanguageLabel, true);
            height += Gap + EditorGUI.GetPropertyHeight(fallbackLiteral, FallbackLabel, true);
            if (HasNonCanonicalKey(textKey))
                height += Gap + line * 2f;
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            try
            {
                Rect row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
                if (!property.isExpanded)
                    return;

                SerializedProperty textKey = property.FindPropertyRelative("textKey");
                SerializedProperty language = property.FindPropertyRelative("language");
                SerializedProperty fallbackLiteral = property.FindPropertyRelative("fallbackLiteral");
                if (textKey == null || language == null || fallbackLiteral == null)
                {
                    row.y += row.height + Gap;
                    EditorGUI.HelpBox(row, "本地化文本引用的序列化结构不完整。", MessageType.Error);
                    return;
                }

                using (new EditorGUI.IndentLevelScope())
                {
                    DrawProperty(ref row, textKey, TextKeyLabel);
                    DrawProperty(ref row, language, LanguageLabel);
                    DrawProperty(ref row, fallbackLiteral, FallbackLabel);
                    if (HasNonCanonicalKey(textKey))
                    {
                        row.y += row.height + Gap;
                        row.height = EditorGUIUtility.singleLineHeight * 2f;
                        EditorGUI.HelpBox(row, NonCanonicalMessage, MessageType.Warning);
                    }
                }
            }
            finally
            {
                EditorGUI.EndProperty();
            }
        }

        private static void DrawProperty(ref Rect row, SerializedProperty property, GUIContent label)
        {
            row.y += row.height + Gap;
            row.height = EditorGUI.GetPropertyHeight(property, label, true);
            EditorGUI.PropertyField(row, property, label, true);
        }

        private static bool HasNonCanonicalKey(SerializedProperty textKey)
        {
            if (textKey == null || textKey.hasMultipleDifferentValues)
                return false;
            string value = textKey.stringValue;
            return !string.IsNullOrWhiteSpace(value)
                && !string.Equals(value, value.Trim(), StringComparison.Ordinal);
        }
    }

    [CustomPropertyDrawer(typeof(ESLocalizationCatalogEntry))]
    public sealed class ESLocalizationCatalogEntryDrawer : PropertyDrawer
    {
        private const float Gap = 2f;
        private static readonly GUIContent TextKeyLabel = new GUIContent("文本 Key", "稳定文本身份，必须全目录唯一且不能包含首尾空白。");
        private static readonly GUIContent LanguageLabel = new GUIContent("语言", "目录条目必须使用具体语言，不能使用“当前游戏语言”。");
        private static readonly GUIContent ValueLabel = new GUIContent("翻译正文", "该 Locale 对应的正式显示文本，可包含受支持的 ES 模板表达式。");
        private const string MissingKeyMessage = "目录条目必须填写稳定 TextKey。";
        private const string InvalidLanguageMessage = "目录条目必须选择具体语言，不能使用“当前游戏语言”或未知值。";

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return line;
            SerializedProperty textKey = property.FindPropertyRelative("textKey");
            SerializedProperty language = property.FindPropertyRelative("language");
            SerializedProperty value = property.FindPropertyRelative("value");
            if (textKey == null || language == null || value == null) return line * 2f + Gap;
            float height = line
                + Gap + EditorGUI.GetPropertyHeight(textKey, TextKeyLabel, true)
                + Gap + EditorGUI.GetPropertyHeight(language, LanguageLabel, true)
                + Gap + EditorGUI.GetPropertyHeight(value, ValueLabel, true);
            if (IsMissingKey(textKey)) height += Gap + line;
            if (HasNonCanonicalKey(textKey)) height += Gap + line * 2f;
            if (IsInvalidLanguage(language)) height += Gap + line * 2f;
            if (IsMissingTranslation(value)) height += Gap + line;
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            try
            {
                Rect row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
                if (!property.isExpanded) return;
                SerializedProperty textKey = property.FindPropertyRelative("textKey");
                SerializedProperty language = property.FindPropertyRelative("language");
                SerializedProperty value = property.FindPropertyRelative("value");
                if (textKey == null || language == null || value == null)
                {
                    row.y += row.height + Gap;
                    EditorGUI.HelpBox(row, "本地化目录条目的序列化结构不完整。", MessageType.Error);
                    return;
                }
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawProperty(ref row, textKey, TextKeyLabel);
                    DrawProperty(ref row, language, LanguageLabel);
                    DrawProperty(ref row, value, ValueLabel);
                    if (IsMissingKey(textKey))
                    {
                        row.y += row.height + Gap;
                        row.height = EditorGUIUtility.singleLineHeight;
                        EditorGUI.HelpBox(row, MissingKeyMessage, MessageType.Error);
                    }
                    if (HasNonCanonicalKey(textKey))
                    {
                        row.y += row.height + Gap;
                        row.height = EditorGUIUtility.singleLineHeight * 2f;
                        EditorGUI.HelpBox(row, "文本 Key 包含首尾空白；请显式修正，不能让相同身份产生不同签名。", MessageType.Warning);
                    }
                    if (IsInvalidLanguage(language))
                    {
                        row.y += row.height + Gap;
                        row.height = EditorGUIUtility.singleLineHeight * 2f;
                        EditorGUI.HelpBox(row, InvalidLanguageMessage, MessageType.Error);
                    }
                    if (IsMissingTranslation(value))
                    {
                        row.y += row.height + Gap;
                        row.height = EditorGUIUtility.singleLineHeight;
                        EditorGUI.HelpBox(row, "当前语言的翻译正文为空。", MessageType.Warning);
                    }
                }
            }
            finally
            {
                EditorGUI.EndProperty();
            }
        }

        private static void DrawProperty(ref Rect row, SerializedProperty property, GUIContent label)
        {
            row.y += row.height + Gap;
            row.height = EditorGUI.GetPropertyHeight(property, label, true);
            EditorGUI.PropertyField(row, property, label, true);
        }

        private static bool HasNonCanonicalKey(SerializedProperty textKey)
        {
            if (textKey == null || textKey.hasMultipleDifferentValues) return false;
            string value = textKey.stringValue;
            return !string.IsNullOrWhiteSpace(value)
                && !string.Equals(value, value.Trim(), StringComparison.Ordinal);
        }

        private static bool IsMissingKey(SerializedProperty textKey)
        {
            return textKey != null && !textKey.hasMultipleDifferentValues
                && string.IsNullOrWhiteSpace(textKey.stringValue);
        }

        private static bool IsInvalidLanguage(SerializedProperty language)
        {
            if (language == null || language.hasMultipleDifferentValues)
                return false;
            var value = (EnumCollect.Envir_LanguageType)language.intValue;
            return !ESLocalizationRuntime.IsConcreteLanguage(value);
        }

        private static bool IsMissingTranslation(SerializedProperty value)
        {
            return value != null && !value.hasMultipleDifferentValues
                && string.IsNullOrWhiteSpace(value.stringValue);
        }
    }

    [CustomPropertyDrawer(typeof(ESRuntimeFontBinding))]
    public sealed class ESRuntimeFontBindingDrawer : PropertyDrawer
    {
        private const float Gap = 2f;
        private static readonly GUIContent LanguageLabel = new GUIContent(
            "语言", "字体绑定必须使用具体语言，不能使用“当前游戏语言”。");
        private static readonly GUIContent RoleLabel = new GUIContent(
            "字体角色", "同一语言下，正文、标题、数字、图标和自定义用途分别绑定字体。");
        private static readonly GUIContent FontLabel = new GUIContent(
            "TMP 字体资产", "运行时直接使用的 TMP_FontAsset；不会从项目目录动态扫描。");

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
                return line;
            SerializedProperty language = property.FindPropertyRelative("language");
            SerializedProperty role = property.FindPropertyRelative("role");
            SerializedProperty font = property.FindPropertyRelative("font");
            if (language == null || role == null || font == null)
                return line * 2f + Gap;

            float height = line
                + Gap + EditorGUI.GetPropertyHeight(language, LanguageLabel, true)
                + Gap + EditorGUI.GetPropertyHeight(role, RoleLabel, true)
                + Gap + EditorGUI.GetPropertyHeight(font, FontLabel, true);
            if (IsInvalidLanguage(language)) height += Gap + line * 2f;
            if (IsInvalidRole(role)) height += Gap + line * 2f;
            if (IsMissingFont(font)) height += Gap + line;
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            try
            {
                Rect row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
                if (!property.isExpanded)
                    return;

                SerializedProperty language = property.FindPropertyRelative("language");
                SerializedProperty role = property.FindPropertyRelative("role");
                SerializedProperty font = property.FindPropertyRelative("font");
                if (language == null || role == null || font == null)
                {
                    row.y += row.height + Gap;
                    EditorGUI.HelpBox(row, "运行时字体绑定的序列化结构不完整。", MessageType.Error);
                    return;
                }

                using (new EditorGUI.IndentLevelScope())
                {
                    DrawProperty(ref row, language, LanguageLabel);
                    DrawProperty(ref row, role, RoleLabel);
                    DrawProperty(ref row, font, FontLabel);
                    if (IsInvalidLanguage(language))
                    {
                        row.y += row.height + Gap;
                        row.height = EditorGUIUtility.singleLineHeight * 2f;
                        EditorGUI.HelpBox(row, "字体绑定必须选择具体语言，不能使用“当前游戏语言”或未知值。", MessageType.Error);
                    }
                    if (IsInvalidRole(role))
                    {
                        row.y += row.height + Gap;
                        row.height = EditorGUIUtility.singleLineHeight * 2f;
                        EditorGUI.HelpBox(row, "字体绑定包含当前版本不支持的字体角色。", MessageType.Error);
                    }
                    if (IsMissingFont(font))
                    {
                        row.y += row.height + Gap;
                        row.height = EditorGUIUtility.singleLineHeight;
                        EditorGUI.HelpBox(row, "字体绑定缺少 TMP 字体资产。", MessageType.Error);
                    }
                }
            }
            finally
            {
                EditorGUI.EndProperty();
            }
        }

        private static void DrawProperty(ref Rect row, SerializedProperty property, GUIContent label)
        {
            row.y += row.height + Gap;
            row.height = EditorGUI.GetPropertyHeight(property, label, true);
            EditorGUI.PropertyField(row, property, label, true);
        }

        private static bool IsInvalidLanguage(SerializedProperty language)
        {
            return language != null && !language.hasMultipleDifferentValues
                && !ESLocalizationRuntime.IsConcreteLanguage(
                    (EnumCollect.Envir_LanguageType)language.intValue);
        }

        private static bool IsInvalidRole(SerializedProperty role)
        {
            return role != null && !role.hasMultipleDifferentValues
                && !Enum.IsDefined(typeof(ESRuntimeFontRole), (ESRuntimeFontRole)role.intValue);
        }

        private static bool IsMissingFont(SerializedProperty font)
        {
            return font != null && !font.hasMultipleDifferentValues
                && font.objectReferenceValue == null;
        }
    }

    [CustomEditor(typeof(ESRuntimeFontCatalog)), CanEditMultipleObjects]
    public sealed class ESRuntimeFontCatalogInspector : UnityEditor.Editor
    {
        private const int MaxVisibleValidationIssues = 12;
        private static readonly GUIContent CatalogIdLabel = new GUIContent(
            "目录身份", "稳定字体目录身份，不能使用显示名称或路径代替。");
        private static readonly GUIContent FormatVersionLabel = new GUIContent(
            "格式版本", "必须与当前运行时支持的字体目录格式版本一致。");
        private static readonly GUIContent BindingsLabel = new GUIContent(
            "字体绑定", "以具体语言和字体角色为唯一绑定身份。");

        private readonly List<FontValidationResult> validationResults = new List<FontValidationResult>();
        private SerializedProperty catalogId;
        private SerializedProperty formatVersion;
        private SerializedProperty bindings;
        private bool hasValidationResult;
        private bool showValidationDetails;

        private sealed class FontValidationResult
        {
            public string Name;
            public string Path;
            public readonly List<string> Issues = new List<string>();
        }

        private void OnEnable()
        {
            catalogId = serializedObject.FindProperty("catalogId");
            formatVersion = serializedObject.FindProperty("formatVersion");
            bindings = serializedObject.FindProperty("bindings");
            Undo.undoRedoPerformed += HandleUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
            ClearValidationResult();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();
            DrawSummary();
            DrawActions();
            DrawValidationResult();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("目录设置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(catalogId, CatalogIdLabel);
            EditorGUILayout.PropertyField(formatVersion, FormatVersionLabel);
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("内容", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(bindings, BindingsLabel, true);

            if (serializedObject.ApplyModifiedProperties())
                ClearValidationResult();
        }

        private void DrawSummary()
        {
            int targetCount = targets?.Length ?? 0;
            string title = targetCount == 1 ? target.name : "已选择 " + targetCount + " 个运行时字体目录";
            string bindingSummary = bindings == null || bindings.hasMultipleDifferentValues
                ? "绑定数：多个值"
                : "绑定数：" + bindings.arraySize;
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(bindingSummary, EditorStyles.miniLabel);
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开字体资产工具"))
                    ESFontToolsWindow.TryOpenWindow();
                if (GUILayout.Button(targets != null && targets.Length > 1 ? "验证所选目录" : "验证当前目录"))
                    ValidateTargets();
            }
        }

        private void DrawValidationResult()
        {
            if (!hasValidationResult)
            {
                EditorGUILayout.HelpBox("验证状态：未验证。验证只在点击按钮时执行，不会在 Inspector 重绘时扫描字体资产。", MessageType.Info);
                return;
            }

            int issueCount = 0;
            for (int index = 0; index < validationResults.Count; index++)
                issueCount += validationResults[index].Issues.Count;
            if (issueCount == 0)
            {
                EditorGUILayout.HelpBox("验证通过：目录身份、格式版本、语言、字体角色和 TMP 字体绑定均符合当前规则。", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox("验证未通过：共发现 " + issueCount + " 个问题。目录仍可编辑，但不应进入发布链。", MessageType.Error);
            showValidationDetails = EditorGUILayout.Foldout(showValidationDetails, "查看验证详情", true);
            if (!showValidationDetails)
                return;

            int visibleIssueCount = 0;
            for (int resultIndex = 0;
                 resultIndex < validationResults.Count && visibleIssueCount < MaxVisibleValidationIssues;
                 resultIndex++)
            {
                FontValidationResult result = validationResults[resultIndex];
                if (result.Issues.Count == 0)
                    continue;
                EditorGUILayout.LabelField(result.Name, EditorStyles.boldLabel);
                if (!string.IsNullOrEmpty(result.Path))
                    EditorGUILayout.SelectableLabel(result.Path, EditorStyles.miniLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                for (int issueIndex = 0;
                     issueIndex < result.Issues.Count && visibleIssueCount < MaxVisibleValidationIssues;
                     issueIndex++, visibleIssueCount++)
                    EditorGUILayout.HelpBox(result.Issues[issueIndex], MessageType.Error);
            }
            if (issueCount > visibleIssueCount)
                EditorGUILayout.HelpBox(
                    "另有 " + (issueCount - visibleIssueCount) + " 个问题未在 Inspector 展开；可复制完整验证详情。",
                    MessageType.Info);
            if (GUILayout.Button("复制验证详情"))
                EditorGUIUtility.systemCopyBuffer = BuildValidationText();
        }

        private void ValidateTargets()
        {
            serializedObject.ApplyModifiedProperties();
            validationResults.Clear();
            foreach (UnityEngine.Object currentTarget in targets)
            {
                if (!(currentTarget is ESRuntimeFontCatalog catalog))
                    continue;
                var result = new FontValidationResult
                {
                    Name = catalog.name,
                    Path = AssetDatabase.GetAssetPath(catalog)
                };
                result.Issues.AddRange(catalog.Validate());
                validationResults.Add(result);
            }
            hasValidationResult = true;
            showValidationDetails = HasValidationIssues();
            Repaint();
        }

        private bool HasValidationIssues()
        {
            for (int index = 0; index < validationResults.Count; index++)
                if (validationResults[index].Issues.Count > 0)
                    return true;
            return false;
        }

        private string BuildValidationText()
        {
            var builder = new StringBuilder();
            for (int resultIndex = 0; resultIndex < validationResults.Count; resultIndex++)
            {
                FontValidationResult result = validationResults[resultIndex];
                if (builder.Length > 0)
                    builder.AppendLine();
                builder.AppendLine(result.Name);
                if (!string.IsNullOrEmpty(result.Path))
                    builder.AppendLine(result.Path);
                for (int issueIndex = 0; issueIndex < result.Issues.Count; issueIndex++)
                    builder.Append("- ").AppendLine(result.Issues[issueIndex]);
            }
            return builder.ToString();
        }

        private void HandleUndoRedo()
        {
            serializedObject.UpdateIfRequiredOrScript();
            ClearValidationResult();
            Repaint();
        }

        private void ClearValidationResult()
        {
            validationResults.Clear();
            hasValidationResult = false;
            showValidationDetails = false;
        }
    }

    [CustomEditor(typeof(ESLocalizationCatalog)), CanEditMultipleObjects]
    public sealed class ESLocalizationCatalogInspector : UnityEditor.Editor
    {
        private const int MaxVisibleValidationIssues = 12;
        private static readonly GUIContent CatalogIdLabel = new GUIContent(
            "目录身份", "稳定目录身份，不能使用显示名称或路径代替。");
        private static readonly GUIContent FormatVersionLabel = new GUIContent(
            "格式版本", "必须与当前运行时支持的目录格式版本一致。");
        private static readonly GUIContent DefaultLanguageLabel = new GUIContent(
            "默认语言", "请求语言及普通回退均失败时使用的最终语言，必须是具体语言。");
        private static readonly GUIContent SourceIdLabel = new GUIContent(
            "生成源标识", "Editor 生成源的项目内 Assets/ 路径；运行时不会读取该文件。");
        private static readonly GUIContent SourceHashLabel = new GUIContent(
            "源内容 SHA-256", "生成时记录的源文件摘要，用于显式验证目录是否发生漂移。");
        private static readonly GUIContent EntriesLabel = new GUIContent(
            "翻译条目", "每个条目由稳定 TextKey、具体语言和翻译正文共同组成。");

        private readonly List<ValidationResult> validationResults = new List<ValidationResult>();
        private SerializedProperty catalogId;
        private SerializedProperty formatVersion;
        private SerializedProperty defaultLanguage;
        private SerializedProperty sourceId;
        private SerializedProperty sourceHash;
        private SerializedProperty entries;
        private bool hasValidationResult;
        private bool showValidationDetails;

        private sealed class ValidationResult
        {
            public string Name;
            public string Path;
            public readonly List<string> Issues = new List<string>();
        }

        private void OnEnable()
        {
            catalogId = serializedObject.FindProperty("catalogId");
            formatVersion = serializedObject.FindProperty("formatVersion");
            defaultLanguage = serializedObject.FindProperty("defaultLanguage");
            sourceId = serializedObject.FindProperty("sourceId");
            sourceHash = serializedObject.FindProperty("sourceHash");
            entries = serializedObject.FindProperty("entries");
            Undo.undoRedoPerformed += HandleUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
            validationResults.Clear();
            hasValidationResult = false;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();

            DrawSummary();
            DrawActions();
            DrawValidationResult();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("目录设置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(catalogId, CatalogIdLabel);
            EditorGUILayout.PropertyField(formatVersion, FormatVersionLabel);
            EditorGUILayout.PropertyField(defaultLanguage, DefaultLanguageLabel);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("生成来源", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(sourceId, SourceIdLabel);
            EditorGUILayout.PropertyField(sourceHash, SourceHashLabel);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("内容", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(entries, EntriesLabel, true);

            if (serializedObject.ApplyModifiedProperties())
            {
                InvalidateCatalogIndexes();
                ClearValidationResult();
            }
        }

        private void DrawSummary()
        {
            int targetCount = targets?.Length ?? 0;
            string title = targetCount == 1 ? target.name : "已选择 " + targetCount + " 个本地化目录";
            string entrySummary = entries == null || entries.hasMultipleDifferentValues
                ? "条目数：多个值"
                : "条目数：" + entries.arraySize;

            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(entrySummary, EditorStyles.miniLabel);
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开本地化工具"))
                    ESLocalizationToolsWindow.TryOpenWindow();

                string validationLabel = targets != null && targets.Length > 1
                    ? "验证所选目录"
                    : "验证当前目录";
                if (GUILayout.Button(validationLabel))
                    ValidateTargets();
            }
        }

        private void DrawValidationResult()
        {
            if (!hasValidationResult)
            {
                EditorGUILayout.HelpBox("验证状态：未验证。验证只在点击按钮时执行，不会在 Inspector 重绘时扫描源文件。", MessageType.Info);
                return;
            }

            int issueCount = 0;
            for (int index = 0; index < validationResults.Count; index++)
                issueCount += validationResults[index].Issues.Count;

            if (issueCount == 0)
            {
                EditorGUILayout.HelpBox("验证通过：目录结构、默认语言、翻译条目和生成源 Hash 均符合当前规则。", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox("验证未通过：共发现 " + issueCount + " 个问题。目录仍可编辑，但不应进入发布链。", MessageType.Error);
            showValidationDetails = EditorGUILayout.Foldout(showValidationDetails, "查看验证详情", true);
            if (!showValidationDetails)
                return;

            int visibleIssueCount = 0;
            for (int resultIndex = 0;
                 resultIndex < validationResults.Count && visibleIssueCount < MaxVisibleValidationIssues;
                 resultIndex++)
            {
                ValidationResult result = validationResults[resultIndex];
                if (result.Issues.Count == 0)
                    continue;
                EditorGUILayout.LabelField(result.Name, EditorStyles.boldLabel);
                if (!string.IsNullOrEmpty(result.Path))
                    EditorGUILayout.SelectableLabel(result.Path, EditorStyles.miniLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                for (int issueIndex = 0;
                     issueIndex < result.Issues.Count && visibleIssueCount < MaxVisibleValidationIssues;
                     issueIndex++, visibleIssueCount++)
                    EditorGUILayout.HelpBox(result.Issues[issueIndex], MessageType.Error);
            }

            if (issueCount > visibleIssueCount)
                EditorGUILayout.HelpBox(
                    "另有 " + (issueCount - visibleIssueCount) + " 个问题未在 Inspector 展开；可复制完整验证详情。",
                    MessageType.Info);

            if (GUILayout.Button("复制验证详情"))
                EditorGUIUtility.systemCopyBuffer = BuildValidationText();
        }

        private void ValidateTargets()
        {
            if (serializedObject.ApplyModifiedProperties())
                InvalidateCatalogIndexes();

            validationResults.Clear();
            foreach (UnityEngine.Object currentTarget in targets)
            {
                if (!(currentTarget is ESLocalizationCatalog catalog))
                    continue;

                var result = new ValidationResult
                {
                    Name = catalog.name,
                    Path = AssetDatabase.GetAssetPath(catalog)
                };
                result.Issues.AddRange(catalog.Validate());
                result.Issues.AddRange(ESLocalizationCatalogEditor.ValidateSource(catalog));
                validationResults.Add(result);
            }

            hasValidationResult = true;
            showValidationDetails = HasValidationIssues();
            Repaint();
        }

        private bool HasValidationIssues()
        {
            for (int index = 0; index < validationResults.Count; index++)
                if (validationResults[index].Issues.Count > 0)
                    return true;
            return false;
        }

        private string BuildValidationText()
        {
            var builder = new StringBuilder();
            for (int resultIndex = 0; resultIndex < validationResults.Count; resultIndex++)
            {
                ValidationResult result = validationResults[resultIndex];
                if (builder.Length > 0)
                    builder.AppendLine();
                builder.AppendLine(result.Name);
                if (!string.IsNullOrEmpty(result.Path))
                    builder.AppendLine(result.Path);
                for (int issueIndex = 0; issueIndex < result.Issues.Count; issueIndex++)
                    builder.Append("- ").AppendLine(result.Issues[issueIndex]);
            }
            return builder.ToString();
        }

        private void InvalidateCatalogIndexes()
        {
            foreach (UnityEngine.Object currentTarget in targets)
                if (currentTarget is ESLocalizationCatalog catalog)
                    catalog.InvalidateIndex();
        }

        private void HandleUndoRedo()
        {
            serializedObject.UpdateIfRequiredOrScript();
            InvalidateCatalogIndexes();
            ClearValidationResult();
            Repaint();
        }

        private void ClearValidationResult()
        {
            validationResults.Clear();
            hasValidationResult = false;
            showValidationDetails = false;
        }
    }
}
