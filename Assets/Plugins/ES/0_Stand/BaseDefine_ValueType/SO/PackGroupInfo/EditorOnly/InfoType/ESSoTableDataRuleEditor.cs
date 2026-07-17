#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Sirenix.OdinInspector;
using ES;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    [CustomEditor(typeof(ESSoTableDataRule))]
    public sealed class ESSoTableDataRuleEditor : Editor
    {
        private GUIStyle _heroStyle;
        private GUIStyle _heroTitleStyle;
        private GUIStyle _heroSubTitleStyle;
        private GUIStyle _cardStyle;
        private GUIStyle _foldoutTitleStyle;
        private GUIStyle _foldoutHintStyle;
        private GUIStyle _sectionTitleStyle;
        private GUIStyle _pillStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _metricValueStyle;
        private readonly ESDropZoneSolver _tablePathDropZone = new ESDropZoneSolver();
        private readonly ESDropZoneSolver _folderPathDropZone = new ESDropZoneSolver();
        private string _prefsKeyPrefix;
        private bool _showBasic = true;
        private bool _showBuildStage = true;
        private bool _showUseStage = true;
        private bool _showColumns = true;
        private bool _showAdvanced;
        private bool _showAdvancedExpert;
        private bool _showTypeCache;

        private void OnEnable()
        {
            _tablePathDropZone.InitSolver<UnityEngine.Object>(allowFolderExpand: false, rejectScripts: false, maxCount: 1);
            _folderPathDropZone.InitSolver<DefaultAsset>(allowFolderExpand: false, rejectScripts: true, maxCount: 1);
            _prefsKeyPrefix = BuildPrefsKeyPrefix(target);
            LoadFoldoutPrefs();
        }

        private static string BuildPrefsKeyPrefix(UnityEngine.Object editorTarget)
        {
            string assetPath = editorTarget != null ? AssetDatabase.GetAssetPath(editorTarget) : string.Empty;
            string guid = string.IsNullOrEmpty(assetPath) ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath);
            string identity = string.IsNullOrEmpty(guid) && editorTarget != null ? editorTarget.GetInstanceID().ToString(CultureInfo.InvariantCulture) : guid;
            return "ES.EditorInternal.ESSoTableDataRuleEditor." + identity + ".";
        }

        private void LoadFoldoutPrefs()
        {
            _showBasic = LoadFoldoutPref("basic", _showBasic);
            _showBuildStage = LoadFoldoutPref("build", _showBuildStage);
            _showUseStage = LoadFoldoutPref("use", _showUseStage);
            _showColumns = LoadFoldoutPref("columns", _showColumns);
            _showAdvanced = LoadFoldoutPref("advanced", _showAdvanced);
            _showAdvancedExpert = LoadFoldoutPref("advancedExpert", _showAdvancedExpert);
            _showTypeCache = LoadFoldoutPref("typeCache", _showTypeCache);
        }

        private bool LoadFoldoutPref(string key, bool defaultValue)
        {
            return EditorPrefs.GetBool(_prefsKeyPrefix + key, defaultValue);
        }

        private void SaveFoldoutPref(string key, bool value)
        {
            EditorPrefs.SetBool(_prefsKeyPrefix + key, value);
        }

        public override void OnInspectorGUI()
        {
            var rule = target as ESSoTableDataRule;
            if (rule == null || targets.Length != 1)
            {
                base.OnInspectorGUI();
                return;
            }

            EnsureStyles();
            DrawHeader(rule);
            DrawQuickActions(rule);
            DrawOverview(rule);
            DrawWarnings(rule);
            EditorGUILayout.Space(8);
            DrawChineseFields();
        }

        private void EnsureStyles()
        {
            if (_heroStyle != null)
                return;

            _heroStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(14, 14, 12, 12),
                margin = new RectOffset(0, 0, 4, 8)
            };

            _heroTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft
            };

            _heroSubTitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true
            };

            _cardStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 0, 4, 4)
            };

            _foldoutTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(6, 6, 0, 0)
            };

            _foldoutHintStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                wordWrap = true,
                padding = new RectOffset(9, 9, 5, 7)
            };

            _sectionTitleStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleLeft
            };

            _pillStyle = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 20,
                padding = new RectOffset(8, 8, 2, 2)
            };

            _mutedStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true
            };

            _metricValueStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleRight
            };
        }

        private void DrawHeader(ESSoTableDataRule rule)
        {
            using (new EditorGUILayout.VerticalScope(_heroStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        string title = string.IsNullOrWhiteSpace(rule.ruleKey) ? rule.name : rule.ruleKey;
                        EditorGUILayout.LabelField(title, _heroTitleStyle);
                        EditorGUILayout.LabelField(BuildHeroSubtitle(rule), _heroSubTitleStyle);
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.Label(rule.enabled ? "\u5df2\u542f\u7528" : "\u5df2\u505c\u7528", _pillStyle, GUILayout.Width(82));
                }
            }
        }

        private string BuildHeroSubtitle(ESSoTableDataRule rule)
        {
            string source = GetSourceSummary(rule);
            string table = string.IsNullOrWhiteSpace(rule.tableName)
                ? FirstNotEmpty(GetBatchFileName(rule), rule.ruleKey, "\u672a\u8bbe\u7f6e\u8868\u540d")
                : rule.tableName;

            return "SO 表格规则  |  " + source + "  |  " + table;
        }

        private void DrawQuickActions(ESSoTableDataRule rule)
        {
            using (new EditorGUILayout.VerticalScope(_cardStyle))
            {
                EditorGUILayout.LabelField("构建规则", _sectionTitleStyle);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    if (GUILayout.Button("从当前选择生成", EditorStyles.toolbarButton, GUILayout.Width(118)))
                        ExecuteAndExit(rule, rule.BindAndGenerateFromSelection);

                    using (new EditorGUI.DisabledScope(!HasAnySource(rule)))
                    {
                        if (GUILayout.Button("从绑定来源生成", EditorStyles.toolbarButton, GUILayout.Width(118)))
                            ExecuteAndExit(rule, () => BindPreferredSource(rule));
                    }

                    if (GUILayout.Button("重建字段映射", EditorStyles.toolbarButton, GUILayout.Width(108)))
                        ExecuteAndExit(rule, rule.RebuildColumnsFromInfoFields);

                    if (GUILayout.Button("预热反射缓存", EditorStyles.toolbarButton, GUILayout.Width(108)))
                        ExecuteAndExit(rule, rule.PrewarmReflectionCache);

                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.LabelField("表格辅助", _sectionTitleStyle);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    if (GUILayout.Button("从表格表头构建", EditorStyles.toolbarButton, GUILayout.Width(118)))
                        ExecuteAndExit(rule, rule.RebuildColumnsFromBuildTable);
                    if (GUILayout.Button("* 空表案例", EditorStyles.toolbarButton, GUILayout.Width(92)))
                        ExecuteAndExit(rule, rule.GenerateEmptyTableExample);
                    if (GUILayout.Button("* 超级批模板", EditorStyles.toolbarButton, GUILayout.Width(106)))
                        ExecuteAndExit(rule, rule.GenerateSuperBatchTemplate);
                    if (GUILayout.Button("* 行 Debug", EditorStyles.toolbarButton, GUILayout.Width(86)))
                        ExecuteAndExit(rule, rule.DebugConfiguredTableRow);

                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.LabelField("执行批次", _sectionTitleStyle);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    if (GUILayout.Button("新增批次", EditorStyles.toolbarButton, GUILayout.Width(90)))
                        ExecuteAndExit(rule, rule.AddUseBatch);

                    using (new EditorGUI.DisabledScope(!HasColumns(rule)))
                    {
                        if (GUILayout.Button("执行全部启用批次", EditorStyles.toolbarButton, GUILayout.Width(150)))
                            ExecuteAndExit(rule, rule.ExecuteAllEnabledBatches);
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }
        private static void ExecuteAndExit(ESSoTableDataRule rule, Action action)
        {
            if (action == null)
                return;

            action();
            EditorUtility.SetDirty(rule);
            GUIUtility.ExitGUI();
        }

        private void DrawOverview(ESSoTableDataRule rule)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMetricCard("\u5b57\u6bb5\u6620\u5c04", GetColumnCount(rule).ToString(), "\u5df2\u542f\u7528 " + GetEnabledColumnCount(rule));
                DrawMetricCard("\u7ed1\u5b9a\u6765\u6e90", GetSourceKind(rule), ShortenMiddle(GetSourcePath(rule), 36));
                DrawMetricCard("\u7c7b\u578b", GetInfoTypeName(rule), GetPackGroupSummary(rule));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMetricCard("\u8f93\u51fa", GetOutputMode(rule), ShortenMiddle(GetOutputPath(rule), 42));
                DrawMetricCard("\u547d\u540d", FirstNotEmpty(rule.tableName, GetBatchFileName(rule), "-"), FirstNotEmpty(rule.beanName, GetBatchSheetName(rule), "-"));
                DrawMetricCard("\u6279\u6b21", GetBatchCountText(rule), GetBatchPolicyText(rule));
            }
        }

        private void DrawMetricCard(string title, string value, string detail)
        {
            using (new EditorGUILayout.VerticalScope(_cardStyle, GUILayout.MinHeight(58)))
            {
                EditorGUILayout.LabelField(title, _sectionTitleStyle);
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(value) ? "-" : value, _metricValueStyle);
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(detail) ? "-" : detail, _mutedStyle);
            }
        }

        private void DrawWarnings(ESSoTableDataRule rule)
        {
            if (!rule.enabled)
                EditorGUILayout.HelpBox("\u5f53\u524d\u89c4\u5219\u5df2\u505c\u7528\u3002\u4ecd\u7136\u53ef\u4ee5\u4ece\u8fd9\u4e2a\u9762\u677f\u624b\u52a8\u6267\u884c\u5bfc\u5165\u5bfc\u51fa\u3002", MessageType.Info);

            if (!HasAnySource(rule))
                EditorGUILayout.HelpBox("\u8fd8\u6ca1\u6709\u6307\u5b9a SO\u3001\u6587\u4ef6\u5939\u6216\u811a\u672c\u3002\u53ef\u4ee5\u5148\u5728\u7ed1\u5b9a\u533a\u57df\u62d6\u5165\u6765\u6e90\uff0c\u6216\u5728 Project \u91cc\u9009\u4e2d\u5bf9\u8c61\u540e\u70b9\u201c\u4ece\u5f53\u524d\u9009\u62e9\u751f\u6210\u201d\u3002", MessageType.Warning);

            if (!HasColumns(rule))
                EditorGUILayout.HelpBox("\u8fd8\u6ca1\u6709\u5b57\u6bb5\u6620\u5c04\u3002\u5bfc\u51fa\u524d\u9700\u8981\u5148\u7ed1\u5b9a\u6765\u6e90\uff0c\u6216\u70b9\u51fb\u201c\u91cd\u5efa\u5b57\u6bb5\u6620\u5c04\u201d\u3002", MessageType.Warning);

            Type packType;
            Type groupType;
            Type infoType;
            if (!rule.TryGetTargetTypes(out packType, out groupType, out infoType))
                EditorGUILayout.HelpBox("Pack\u3001Group \u6216 Info \u7c7b\u578b\u8fd8\u6ca1\u6709\u5b8c\u6574\u89e3\u6790\u3002\u901a\u5e38\u4ece\u6709\u6548\u7684 SoData \u6765\u6e90\u91cd\u65b0\u751f\u6210\u5373\u53ef\u3002", MessageType.Info);
        }

        private void DrawChineseFields()
        {
            serializedObject.Update();

            DrawFoldout("basic", ref _showBasic, "\u57fa\u7840\u914d\u7f6e", "\u8fd9\u91cc\u53ea\u653e Rule \u81ea\u8eab\u7684\u542f\u7528\u3001Key \u548c\u8bf4\u660e\uff0c\u4e0d\u51b3\u5b9a\u5b57\u6bb5\u548c\u6279\u6b21\u3002", DrawBasicFields);
            DrawFoldout("build", ref _showBuildStage, "Rule \u6784\u5efa\u9636\u6bb5", "\u7528 SO\u3001MonoScript \u6216\u8868\u683c\u6837\u672c\u751f\u6210\u5b57\u6bb5\u6620\u5c04\uff1b\u53ea\u6784\u5efa\u89c4\u5219\uff0c\u4e0d\u6267\u884c\u6279\u91cf\u5bfc\u5165\u5bfc\u51fa\u3002", DrawBuildStageFields);
            DrawFoldout("use", ref _showUseStage, "Rule \u4f7f\u7528\u9636\u6bb5", "\u914d\u7f6e\u4e00\u4e2a\u6216\u591a\u4e2a\u6267\u884c\u6279\u6b21\uff1a\u5904\u7406\u54ea\u4e9b SO\u3001\u8f93\u51fa\u5230\u54ea\u4e2a\u8868\u683c\u3001\u6309\u4ec0\u4e48\u8303\u56f4\u5199\u56de\u3002", DrawUseStageFields);
            DrawFoldout("columns", ref _showColumns, "\u5b57\u6bb5\u6620\u5c04", "\u5b9a\u4e49 SO \u5b57\u6bb5\u4e0e\u8868\u683c\u5217\u7684\u5bf9\u5e94\u5173\u7cfb\uff1b\u9501\u5b9a\u3001\u6743\u5a01\u3001\u6392\u5e8f\u90fd\u5728\u8fd9\u91cc\u7ba1\u3002", DrawColumnFields);
            DrawFoldout("advanced", ref _showAdvanced, "\u9ad8\u7ea7\u914d\u7f6e", "\u65e5\u5e38\u5148\u7528\u5feb\u901f\u65b9\u6848\u3002\u53ea\u6709\u884c\u7ed1\u5b9a\u3001\u5b50\u5bf9\u8c61\u5c55\u5f00\u3001\u8868\u5934\u6a21\u677f\u9700\u8981\u8c03\u65f6\u624d\u8fdb\u4e13\u5bb6\u8be6\u60c5\u3002", DrawAdvancedFields);
            DrawFoldout("typeCache", ref _showTypeCache, "\u7c7b\u578b\u7f13\u5b58", "\u8fd9\u662f\u4ece\u6784\u5efa\u6765\u6e90\u89e3\u6790\u51fa\u7684\u7c7b\u578b\u7ed3\u679c\uff0c\u901a\u5e38\u53ea\u7528\u67e5\u770b\uff0c\u4e0d\u624b\u52a8\u6539\u3002", DrawTypeCacheFields);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawFoldout(string prefsKey, ref bool expanded, string title, string hint, Action drawContent)
        {
            using (new EditorGUILayout.VerticalScope(_cardStyle))
            {
                bool oldExpanded = expanded;
                Rect headerRect = GUILayoutUtility.GetRect(0, 32, GUILayout.ExpandWidth(true));
                Color headerColor = expanded
                    ? new Color(0.18f, 0.29f, 0.38f, 0.98f)
                    : new Color(0.13f, 0.16f, 0.20f, 0.98f);
                EditorGUI.DrawRect(headerRect, headerColor);
                EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, 5, headerRect.height), new Color(0.32f, 0.72f, 0.92f, 1f));

                Event current = Event.current;
                if (current != null && current.type == EventType.MouseDown && headerRect.Contains(current.mousePosition))
                {
                    expanded = !expanded;
                    current.Use();
                }

                Rect foldoutRect = new Rect(headerRect.x + 9, headerRect.y + 6, 18, 20);
                expanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, true);
                if (expanded != oldExpanded)
                    SaveFoldoutPref(prefsKey, expanded);

                Rect titleRect = new Rect(headerRect.x + 29, headerRect.y + 6, headerRect.width - 36, 20);
                GUI.Label(titleRect, title, _foldoutTitleStyle);

                if (!string.IsNullOrWhiteSpace(hint))
                {
                    Rect hintRect = GUILayoutUtility.GetRect(0, 38, GUILayout.ExpandWidth(true));
                    EditorGUI.DrawRect(hintRect, expanded ? new Color(0.10f, 0.13f, 0.16f, 0.92f) : new Color(0.09f, 0.10f, 0.12f, 0.82f));
                    GUI.Label(hintRect, hint, _foldoutHintStyle);
                }

                if (!expanded)
                    return;

                EditorGUILayout.Space(4);
                drawContent?.Invoke();
            }
        }

        private void DrawBasicFields()
        {
            DrawProperty("enabled", "\u542f\u7528");
            DrawProperty("ruleKey", "\u89c4\u5219 Key");
            DrawProperty("description", "\u89c4\u5219\u8bf4\u660e");
        }

        private void DrawBuildStageFields()
        {
            SerializedProperty buildStageProperty = serializedObject.FindProperty("buildStage");
            if (buildStageProperty == null)
                return;

            SerializedProperty source = buildStageProperty.FindPropertyRelative("sourceBinding");
            if (source != null)
            {
                EditorGUILayout.LabelField("\u6784\u5efa\u6765\u6e90\uff08\u7528\u6765\u751f\u6210\u89c4\u5219\uff09", _sectionTitleStyle);
                DrawChild(source, "soAsset", "\u5355\u4e2a SO \u6837\u672c");
                DrawChild(source, "monoScript", "\u811a\u672c\u7c7b\u578b");
            }

            DrawPathProperty(buildStageProperty.FindPropertyRelative("tableFilePath"), "\u8868\u683c\u6837\u672c\u8def\u5f84", true, "csv,xlsx");
            DrawChild(buildStageProperty, "allowTableHeaderOverride", "\u5141\u8bb8\u8868\u5934\u8986\u76d6\u5b57\u6bb5\u6620\u5c04");

            EditorGUILayout.HelpBox("\u6784\u5efa\u9636\u6bb5\u53ea\u8d1f\u8d23\u751f\u6210\u89c4\u5219\uff1a\u7528\u5355\u4e2a SO\u3001\u811a\u672c\u6216\u8868\u683c\u8868\u5934\u63a8\u5bfc\u5b57\u6bb5\u6620\u5c04\u3002\u4e0d\u5728\u8fd9\u91cc\u914d\u6279\u91cf\u5bfc\u5165\u5bfc\u51fa\u6570\u636e\u3002", MessageType.Info);
        }

        private void DrawUseStageFields()
        {
            SerializedProperty batches = serializedObject.FindProperty("useBatches");
            if (batches == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("\u6279\u6b21\u6570\u91cf\uff1a" + batches.arraySize, _sectionTitleStyle);
                if (GUILayout.Button("\u65b0\u589e\u6279\u6b21", EditorStyles.miniButton, GUILayout.Width(82)))
                {
                    serializedObject.ApplyModifiedProperties();
                    ((ESSoTableDataRule)target).AddUseBatch();
                    serializedObject.Update();
                }
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("\u542f\u7528\u5168\u90e8", EditorStyles.toolbarButton, GUILayout.Width(76)))
                    SetAllBatchesEnabled(batches, true);
                if (GUILayout.Button("\u505c\u7528\u5168\u90e8", EditorStyles.toolbarButton, GUILayout.Width(76)))
                    SetAllBatchesEnabled(batches, false);
                if (GUILayout.Button("\u8865\u9f50\u9ed8\u8ba4\u8def\u5f84", EditorStyles.toolbarButton, GUILayout.Width(108)))
                    ApplyDefaultPathToAllBatches(batches, (ESSoTableDataRule)target);
                GUILayout.FlexibleSpace();
            }

            for (int i = 0; i < batches.arraySize; i++)
            {
                SerializedProperty batch = batches.GetArrayElementAtIndex(i);
                SerializedProperty batchName = batch.FindPropertyRelative("batchName");
                string title = "\u6279\u6b21 " + (i + 1);
                if (batchName != null && !string.IsNullOrEmpty(batchName.stringValue))
                    title += "  " + batchName.stringValue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        batch.isExpanded = EditorGUILayout.Foldout(batch.isExpanded, title, true);
                        using (new EditorGUI.DisabledScope(!HasColumns((ESSoTableDataRule)target)))
                        {
                            if (GUILayout.Button("\u6267\u884c", EditorStyles.miniButton, GUILayout.Width(52)))
                            {
                                serializedObject.ApplyModifiedProperties();
                                ((ESSoTableDataRule)target).ExecuteUseBatch(((ESSoTableDataRule)target).useBatches[i]);
                                GUIUtility.ExitGUI();
                            }
                        }

                        using (new EditorGUI.DisabledScope(i == 0))
                        {
                            if (GUILayout.Button("\u4e0a\u79fb", EditorStyles.miniButton, GUILayout.Width(44)))
                            {
                                batches.MoveArrayElement(i, i - 1);
                                break;
                            }
                        }

                        using (new EditorGUI.DisabledScope(i >= batches.arraySize - 1))
                        {
                            if (GUILayout.Button("\u4e0b\u79fb", EditorStyles.miniButton, GUILayout.Width(44)))
                            {
                                batches.MoveArrayElement(i, i + 1);
                                break;
                            }
                        }

                        if (GUILayout.Button("\u590d\u5236", EditorStyles.miniButton, GUILayout.Width(44)))
                        {
                            batches.InsertArrayElementAtIndex(i);
                            SerializedProperty copy = batches.GetArrayElementAtIndex(i + 1);
                            SerializedProperty copyName = copy.FindPropertyRelative("batchName");
                            if (copyName != null)
                                copyName.stringValue = FirstNotEmpty(copyName.stringValue, "\u6279\u6b21") + " \u526f\u672c";
                            break;
                        }

                        if (GUILayout.Button("\u5220\u9664", EditorStyles.miniButton, GUILayout.Width(52)))
                        {
                            batches.DeleteArrayElementAtIndex(i);
                            break;
                        }
                    }

                    if (!batch.isExpanded)
                        continue;

                    DrawChild(batch, "enabled", "\u542f\u7528");
                    DrawChild(batch, "batchName", "\u6279\u6b21\u540d");
                    DrawChildEnum(batch, "direction", "\u6267\u884c\u65b9\u5411", new[] { "\u5bfc\u51fa", "\u5bfc\u5165", "\u5bfc\u5165\u5e76\u5bfc\u51fa" });
                    DrawChild(batch, "useSuperBatch", "超级批");
                    SerializedProperty useSuperBatch = batch.FindPropertyRelative("useSuperBatch");
                    if (useSuperBatch != null && useSuperBatch.boolValue)
                    {
                        DrawPathProperty(batch.FindPropertyRelative("superBatchTablePath"), "超级批关系表", true, "csv,xlsx");
                        DrawChild(batch, "superBatchSkipInvalidRows", "跳过无效关系行");
                        EditorGUILayout.HelpBox("超级批仍只使用当前规则的一类 SO 和当前字段映射。关系表每一行派生一个标准批次，可覆盖来源、表格、截取范围、生效字段。", MessageType.Info);
                    }

                    SerializedProperty source = batch.FindPropertyRelative("sourceBinding");
                    if (source != null)
                    {
                        EditorGUILayout.LabelField("\u6570\u636e\u6765\u6e90\uff08\u8fd9\u4e00\u6279\u8981\u5904\u7406\u54ea\u4e9b SO\uff09", _sectionTitleStyle);
                        DrawChild(source, "soAsset", "SO \u6587\u4ef6");
                        DrawChild(source, "soFolder", "SO \u6587\u4ef6\u5939");
                        DrawChild(source, "includeSubFolders", "\u5305\u542b\u5b50\u6587\u4ef6\u5939");
                        DrawChildEnum(source, "folderSyncMode", "\u6587\u4ef6\u5939\u540c\u6b65\u6a21\u5f0f", new[] { "\u53ea\u6bd4\u5bf9", "\u589e\u91cf\u540c\u6b65", "\u91cd\u5efa\u751f\u6210" });
                    }

                    EditorGUILayout.LabelField("\u8868\u683c\u8def\u5f84", _sectionTitleStyle);
                    DrawChildEnum(batch, "fileKind", "\u6587\u4ef6\u683c\u5f0f", "\u8fd9\u4e00\u6279\u5bfc\u5165\u6216\u5bfc\u51fa CSV\u3001XLSX\uff0c\u6216\u4e24\u8005\u540c\u65f6\u751f\u6210\u3002", new[] { "CSV", "XLSX", "CSV \u548c XLSX" });
                    DrawChildEnum(batch, "columnNameMode", "\u8868\u683c\u5217\u540d", "\u82f1\u6587\u5217\u540d\u4f7f\u7528\u6620\u5c04\u7684\u8868\u683c\u5217\u540d\uff1b\u4e2d\u6587\u663e\u793a\u540d\u4f7f\u7528 SO \u663e\u793a\u540d\u4f5c\u4e3a\u8868\u5934\u5339\u914d\u3002", new[] { "\u82f1\u6587\u5217\u540d", "\u4e2d\u6587\u663e\u793a\u540d" });
                    DrawChild(batch, "fileName", "\u6587\u4ef6\u540d", "\u8868\u683c\u6587\u4ef6\u540d\uff0c\u4e0d\u9700\u8981\u586b .csv \u6216 .xlsx \u6269\u5c55\u540d\u3002");
                    DrawChild(batch, "sheetName", "Sheet \u540d", "XLSX \u7684 Sheet \u540d\u3002CSV \u4e0d\u4f7f\u7528\u8fd9\u4e2a\u5b57\u6bb5\u3002");
                    DrawPathProperty(batch.FindPropertyRelative("outputRoot"), "\u8f93\u51fa\u6839\u76ee\u5f55", false, string.Empty);
                    DrawChild(batch, "csvRelativePath", "CSV \u76f8\u5bf9\u8def\u5f84", "\u76f8\u5bf9\u8f93\u51fa\u6839\u76ee\u5f55\u7684 CSV \u5b50\u76ee\u5f55\u3002");
                    DrawChild(batch, "xlsxRelativePath", "XLSX \u76f8\u5bf9\u8def\u5f84", "\u76f8\u5bf9\u8f93\u51fa\u6839\u76ee\u5f55\u7684 XLSX \u5b50\u76ee\u5f55\u3002");
                    if (GUILayout.Button("\u5e94\u7528\u9ed8\u8ba4\u8868\u683c\u8def\u5f84", EditorStyles.miniButton, GUILayout.Width(150)))
                        ApplyDefaultBatchPath(batch, (ESSoTableDataRule)target);
                    DrawBatchTableOpenButtons(batch, (ESSoTableDataRule)target);

                    EditorGUILayout.LabelField("\u6279\u6b21\u7b56\u7565", _sectionTitleStyle);
                    DrawChildEnum(batch, "importConflictPolicy", "\u5bfc\u5165\u51b2\u7a81", "\u8868\u683c\u5199\u56de SO \u65f6\u9047\u5230\u5df2\u6709\u6570\u636e\u6216\u51b2\u7a81\u9879\u7684\u5904\u7406\u65b9\u5f0f\u3002", new[] { "\u8df3\u8fc7", "\u8986\u76d6", "\u521b\u5efa\u526f\u672c", "\u62a5\u9519" });
                    DrawChildEnum(batch, "exportConflictPolicy", "\u5bfc\u51fa\u51b2\u7a81", "\u5bfc\u51fa\u8868\u683c\u65f6\u5982\u679c\u76ee\u6807\u6587\u4ef6\u5df2\u5b58\u5728\uff0c\u91c7\u7528\u54ea\u79cd\u5904\u7406\u65b9\u5f0f\u3002", new[] { "\u8df3\u8fc7", "\u8986\u76d6", "\u521b\u5efa\u526f\u672c", "\u62a5\u9519" });
                    DrawChildEnum(batch, "exportWriteMode", "\u5bfc\u51fa\u5199\u5165\u6a21\u5f0f", "\u5bfc\u51fa\u5230\u5df2\u6709\u8868\u683c\u65f6\u7684\u5199\u5165\u7b56\u7565\u3002\u9ed8\u8ba4\u6309 Key \u5408\u5e76\uff0c\u5c3d\u91cf\u4fdd\u7559\u65e7\u8868\u7684\u65ad\u8a00\u3001\u5907\u6ce8\u548c\u672a\u6620\u5c04\u5217\u3002", new[] { "\u6574\u8868\u91cd\u5efa", "\u6309 Key \u5408\u5e76", "\u4ec5\u66f4\u65b0\u5df2\u6709\u884c" });
                    DrawChildEnum(batch, "serialChildWriteMode", "\u5b50\u7ea7\u5bfc\u51fa\u6a21\u5f0f", "List \u884c\u5bfc\u51fa\u65f6\u7684\u5b50\u7ea7\u5199\u5165\u7b56\u7565\u3002\u6309\u5bbf\u4e3b\u91cd\u5efa\u53ea\u4f1a\u91cd\u5efa\u5f53\u524d\u5bbf\u4e3b\u7684\u5b50\u7ea7\uff0c\u4e0d\u91cd\u5efa\u6574\u8868\u3002", new[] { "\u6309\u5bbf\u4e3b\u91cd\u5efa\u5b50\u7ea7", "\u6309\u5b50\u7ea7 Key \u5408\u5e76", "\u4ec5\u66f4\u65b0\u5df2\u6709\u5b50\u7ea7" });
                    DrawChildEnum(batch, "serialChildImportSyncMode", "\u5b50\u7ea7\u5bfc\u5165\u540c\u6b65", "List \u884c\u5bfc\u5165\u65f6\uff0c\u8868\u683c\u7f3a\u5931\u7684\u65e7\u5b50\u7ea7\u5982\u4f55\u5904\u7406\u3002", new[] { "\u4fdd\u7559\u8868\u5916\u5b50\u7ea7", "重建触达宿主子级", "\u6309\u8868\u88c1\u526a\u5b50\u7ea7", "\u4ec5 delete \u6307\u4ee4\u5220\u9664" });
                    DrawChild(batch, "activeFields", "仅生效字段", "逗号/分号分隔。留空表示全部字段。可写表格列名、SO 字段路径或显示名。");
                    DrawChild(batch, "excludedFields", "排除字段", "逗号/分号分隔。优先级高于仅生效字段。");

                    EditorGUILayout.LabelField("\u5e94\u7528\u8303\u56f4\uff08\u5bfc\u5165\u5199\u56de\uff09", _sectionTitleStyle);
                    DrawChildEnum(batch, "applyRangeMode", "\u8303\u56f4", "\u5bfc\u5165\u5199\u56de\u65f6\u662f\u5e94\u7528\u6574\u5f20\u8868\uff0c\u8fd8\u662f\u53ea\u6309\u67d0\u5217\u7684\u8d77\u6b62\u503c\u5e94\u7528\u4e00\u6bb5\uff0c\u6216\u53ea\u5e94\u7528\u4e00\u4e2a Group/Info\u3002", new[] { "\u5168\u91cf\u5e94\u7528", "\u7247\u6bb5\u622a\u53d6", "\u5355\u4e2a Group/Info" });
                    SerializedProperty range = batch.FindPropertyRelative("applyRangeMode");
                    if (range != null && range.enumValueIndex == (int)ESTableBatchApplyRangeMode.Slice)
                    {
                        DrawChild(batch, "sliceColumnName", "\u622a\u53d6\u5217\u540d", "\u7528\u54ea\u4e00\u5217\u6765\u5224\u65ad\u7247\u6bb5\u8d77\u6b62\u3002\u7559\u7a7a\u65f6\u4f7f\u7528 Info Key \u5217\u3002");
                        DrawChild(batch, "sliceStartValue", "\u8d77\u70b9\u503c", "\u5728\u622a\u53d6\u5217\u91cc\u627e\u5230\u8fd9\u4e2a\u503c\u540e\u5f00\u59cb\u5199\u56de\u3002");
                        DrawChild(batch, "sliceEndValue", "\u7ec8\u70b9\u503c", "\u5728\u622a\u53d6\u5217\u91cc\u627e\u5230\u8fd9\u4e2a\u503c\u540e\u505c\u6b62\u5199\u56de\u3002");
                        DrawChild(batch, "includeSliceStart", "\u5305\u542b\u8d77\u70b9", "\u8d77\u70b9\u884c\u672c\u8eab\u662f\u5426\u4e5f\u8981\u5199\u56de\u3002");
                        DrawChild(batch, "includeSliceEnd", "\u5305\u542b\u7ec8\u70b9", "\u7ec8\u70b9\u884c\u672c\u8eab\u662f\u5426\u4e5f\u8981\u5199\u56de\u3002");
                    }
                    else if (range != null && range.enumValueIndex == (int)ESTableBatchApplyRangeMode.SingleGroupInfo)
                    {
                        DrawChild(batch, "targetGroupKey", "\u76ee\u6807 Group", "\u53ea\u5199\u56de\u8fd9\u4e2a Group \u4e0b\u7684\u6570\u636e\u3002\u7559\u7a7a\u5219\u4e0d\u9650\u5236 Group\u3002");
                        DrawChild(batch, "targetInfoKey", "\u76ee\u6807 Info", "\u53ea\u5199\u56de\u8fd9\u4e2a Info Key \u5bf9\u5e94\u7684\u6570\u636e\u3002\u7559\u7a7a\u5219\u4e0d\u9650\u5236 Info\u3002");
                    }
                }
            }

            EditorGUILayout.HelpBox("\u4f7f\u7528\u9636\u6bb5\u53ef\u4ee5\u914d\u591a\u4e2a\u6279\u6b21\uff1a\u540c\u4e00\u5957\u5b57\u6bb5\u89c4\u5219\uff0c\u5206\u522b\u5904\u7406\u4e0d\u540c SO \u6587\u4ef6\u6216\u6587\u4ef6\u5939\u3002", MessageType.Info);
        }

        private void DrawAdvancedFields()
        {
            EditorGUILayout.LabelField("\u5feb\u901f\u65b9\u6848", _sectionTitleStyle);
            using (new EditorGUILayout.HorizontalScope())
            {
                var rule = target as ESSoTableDataRule;
                if (GUILayout.Button("\u6807\u51c6 SO \u8868", EditorStyles.miniButton))
                    ExecuteAndExit(rule, rule.ApplyPresetStandardSoTable);
                if (GUILayout.Button("\u53ea\u5bfc\u51fa", EditorStyles.miniButton))
                    ExecuteAndExit(rule, rule.ApplyPresetExportOnly);
                if (GUILayout.Button("\u8868\u683c\u5199\u56de", EditorStyles.miniButton))
                    ExecuteAndExit(rule, rule.ApplyPresetImportBack);
                if (GUILayout.Button("\u666e\u901a SO \u7b80\u5316", EditorStyles.miniButton))
                    ExecuteAndExit(rule, rule.ApplyPresetSimpleScriptableObject);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("\u5e38\u7528\u89c4\u5219", _sectionTitleStyle);
            DrawEnumProperty("infoExpandMode", "Info \u5c55\u5f00\u65b9\u5f0f", "\u51b3\u5b9a\u4e00\u4e2a Info \u5bf9\u8c61\u7684\u5b57\u6bb5\u5982\u4f55\u751f\u6210\u8868\u683c\u5217\uff1a\u53ea\u7528\u663e\u5f0f\u6620\u5c04\u3001\u626b\u53ef\u5e8f\u5217\u5316\u5b57\u6bb5\u3001\u628a\u5b50\u5bf9\u8c61\u5c55\u5f00\u6210\u591a\u5217\uff0c\u6216\u628a\u590d\u6742\u5bf9\u8c61\u4fdd\u5b58\u4e3a Json\u3002", new[] { "\u53ea\u4f7f\u7528\u663e\u5f0f\u6620\u5c04", "\u5c55\u5f00\u53ef\u5e8f\u5217\u5316\u5b57\u6bb5", "\u5d4c\u5957\u5bf9\u8c61\u5c55\u5f00\u591a\u5217", "\u590d\u6742\u5bf9\u8c61\u4fdd\u5b58\u4e3a Json" });
            DrawEnumProperty("groupSliceMode", "Group \u622a\u53d6\u65b9\u5f0f", "\u51b3\u5b9a\u5bfc\u51fa\u8868\u683c\u65f6 Group \u5982\u4f55\u8868\u8fbe\uff1a\u5ffd\u7565\u3001\u5199\u5165\u4e00\u5217\u3001\u6bcf\u4e2a Group \u4e00\u4e2a Sheet\uff0c\u6216\u6bcf\u4e2a Group \u4e00\u4e2a\u6587\u4ef6\u3002", new[] { "\u5ffd\u7565 Group", "Group \u540d\u5199\u5165\u5217", "\u6bcf\u4e2a Group \u4e00\u4e2a Sheet", "\u6bcf\u4e2a Group \u4e00\u4e2a\u6587\u4ef6" });
            DrawProperty("infoKeyColumnName", "Info Key \u5217\u540d", "\u8868\u683c\u4e2d\u7528\u6765\u5339\u914d Info \u7684 Key \u5217\u540d\u3002\u5bfc\u5165\u5199\u56de\u65f6\u4f18\u5148\u7528\u5b83\u627e\u5230\u5bf9\u5e94 SO\u3002");
            DrawEnumProperty("nameMatchMode", "\u540d\u79f0\u5339\u914d", "\u5bfc\u5165\u65f6\u8868\u683c\u5217\u540d\u548c\u5b57\u6bb5\u540d\u7684\u5339\u914d\u7b56\u7565\u3002\u4e00\u822c\u7528\u5b57\u6bb5\u540d\u8f6c\u5217\u540d\u6216\u5b8c\u5168\u5339\u914d\u3002", new[] { "\u5b8c\u5168\u5339\u914d", "\u5ffd\u7565\u5927\u5c0f\u5199", "\u5b57\u6bb5\u540d\u8f6c\u5217\u540d", "\u81ea\u5b9a\u4e49" });
            DrawProperty("allowCreateInfoOnImport", "\u5bfc\u5165\u65f6\u5141\u8bb8\u521b\u5efa Info", "\u8868\u683c\u91cc\u6709\u65b0 Key\uff0c\u4f46 SO \u4e2d\u627e\u4e0d\u5230\u5bf9\u5e94 Info \u65f6\uff0c\u662f\u5426\u5141\u8bb8\u81ea\u52a8\u521b\u5efa\u3002");
            DrawProperty("refreshPackBeforeExport", "\u5bfc\u51fa\u524d\u540c\u6b65 Pack \u7f13\u5b58", "\u5bfc\u51fa\u524d\u5148\u8ba9 Pack \u5237\u65b0\u5185\u90e8\u7f13\u5b58\uff0c\u907f\u514d\u8868\u683c\u4f7f\u7528\u5230\u8fc7\u671f\u7684 Group/Info \u5217\u8868\u3002");

            EditorGUILayout.Space(6);
            bool oldExpert = _showAdvancedExpert;
            _showAdvancedExpert = EditorGUILayout.Foldout(_showAdvancedExpert, "\u4e13\u5bb6\u8be6\u60c5", true, EditorStyles.foldout);
            if (_showAdvancedExpert != oldExpert)
                SaveFoldoutPref("advancedExpert", _showAdvancedExpert);
            if (!_showAdvancedExpert)
            {
                EditorGUILayout.HelpBox("\u65e5\u5e38\u60c5\u51b5\u7528\u4e0a\u9762\u7684\u5feb\u901f\u65b9\u6848\u548c\u5e38\u7528\u89c4\u5219\u5c31\u591f\u4e86\u3002\u884c\u7ed1\u5b9a\u3001\u5b50\u5bf9\u8c61\u5c55\u5f00\u3001\u8868\u5934\u6a21\u677f\u5df2\u653e\u5230\u4e13\u5bb6\u8be6\u60c5\u91cc\u3002", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Pack / Group / Info", _sectionTitleStyle);
            DrawProperty("packColumnName", "Pack \u5217\u540d", "\u5bfc\u51fa\u8868\u683c\u65f6 Pack \u6807\u8bc6\u5217\u7684\u5217\u540d\u3002\u591a Pack \u5408\u5e76\u8868\u65f6\u624d\u5e38\u7528\u3002");
            DrawProperty("groupColumnName", "Group \u5217\u540d", "\u5bfc\u51fa\u8868\u683c\u65f6 Group \u6807\u8bc6\u5217\u7684\u5217\u540d\u3002\u5bfc\u5165\u5355\u4e2a Group/Info \u65f6\u4e5f\u4f1a\u7528\u5230\u3002");

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("\u884c\u7ed1\u5b9a", _sectionTitleStyle);
            DrawChildEnum("rowBinding", "targetMode", "\u884c\u76ee\u6807", "\u51b3\u5b9a\u8868\u683c\u7684\u4e00\u884c\u5bf9\u5e94\u4ec0\u4e48\uff1a\u6574\u4e2a Info/SO \u5bf9\u8c61\uff0c\u6216\u5bf9\u8c61\u5185\u90e8\u67d0\u4e2a List \u7684\u5143\u7d20\u3002", new[] { "\u4e00\u884c = \u4e00\u4e2a\u5bf9\u8c61", "\u4e00\u884c = \u5bf9\u8c61\u5185 List \u5143\u7d20" });
            DrawChild("rowBinding", "rowKeyColumnName", "\u884c Key \u5217\u540d", "\u5f53\u4e00\u884c\u5bf9\u5e94 List \u5143\u7d20\u65f6\uff0c\u7528\u8fd9\u5217\u627e\u5230\u5177\u4f53\u5143\u7d20\u3002");
            DrawChild("rowBinding", "listFieldPath", "List \u5b57\u6bb5\u8def\u5f84", "\u5bf9\u8c61\u5185\u7684 List \u5b57\u6bb5\u8def\u5f84\uff0c\u4f8b\u5982 rewards \u6216 config.items\u3002");
            DrawChild("rowBinding", "elementKeyFieldPath", "\u5143\u7d20 Key \u5b57\u6bb5\u8def\u5f84", "\u7528\u6765\u5339\u914d List \u5143\u7d20\u7684 Key \u5b57\u6bb5\u3002");
            DrawChild("rowBinding", "createMissingElement", "\u5bfc\u5165\u65f6\u521b\u5efa\u7f3a\u5931\u5143\u7d20", "\u8868\u683c\u91cc\u6709\u65b0\u5143\u7d20 Key\uff0cList \u91cc\u4e0d\u5b58\u5728\u65f6\uff0c\u662f\u5426\u81ea\u52a8\u521b\u5efa\u3002");
            DrawChild("rowBinding", "allowEmptyRowKey", "\u5141\u8bb8\u7a7a Key", "\u5141\u8bb8\u884c Key \u4e3a\u7a7a\u3002\u4e00\u822c\u4e0d\u5efa\u8bae\u6253\u5f00\uff0c\u9664\u975e\u884c\u987a\u5e8f\u5c31\u662f\u552f\u4e00\u5339\u914d\u4f9d\u636e\u3002");

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("\u5b50\u5bf9\u8c61\u5b57\u6bb5", _sectionTitleStyle);
            DrawChild("nestedFieldRule", "expandNestedFields", "\u5c55\u5f00\u5b50\u5bf9\u8c61\u5b57\u6bb5", "\u6784\u5efa\u6620\u5c04\u65f6\uff0c\u662f\u5426\u628a\u53ef\u5e8f\u5217\u5316\u7684\u5b50\u5bf9\u8c61\u5b57\u6bb5\u5c55\u5f00\u6210\u591a\u4e2a\u8868\u683c\u5217\u3002");
            DrawChild("nestedFieldRule", "maxDepth", "\u6700\u5927\u5c55\u5f00\u6df1\u5ea6", "\u5b50\u5bf9\u8c61\u6700\u591a\u5c55\u5f00\u51e0\u5c42\u3002\u8d8a\u5927\u5217\u8d8a\u591a\uff0c\u8868\u683c\u4e5f\u8d8a\u590d\u6742\u3002");
            DrawChild("nestedFieldRule", "columnSeparator", "\u5217\u540d\u5206\u9694\u7b26", "\u5b50\u5bf9\u8c61\u5c55\u5f00\u540e\u5217\u540d\u7684\u8fde\u63a5\u7b26\uff0c\u4f8b\u5982 config_value\u4e2d\u7684 _\u3002");

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("\u8868\u5934\u6a21\u677f", _sectionTitleStyle);
            DrawChild("header", "varMark", "\u53d8\u91cf\u884c\u6807\u8bb0", "SO表格 \u8868\u5934\u7b2c 1 \u884c\u7684\u6807\u8bb0\uff0c\u9ed8\u8ba4 ##var\u3002");
            DrawChild("header", "typeMark", "\u7c7b\u578b\u884c\u6807\u8bb0", "SO表格 \u8868\u5934\u7b2c 2 \u884c\u7684\u6807\u8bb0\uff0c\u9ed8\u8ba4 ##type\u3002");
            DrawChild("header", "groupMark", "\u5206\u7ec4\u884c\u6807\u8bb0", "SO表格 \u8868\u5934\u7b2c 3 \u884c\u7684\u6807\u8bb0\uff0c\u9ed8\u8ba4 ##group\u3002");
            DrawChild("header", "commentMark", "\u6ce8\u91ca\u884c\u6807\u8bb0", "SO表格 \u8868\u5934\u7b2c 4 \u884c\u7684\u6807\u8bb0\uff0c\u9ed8\u8ba4 ##\u3002");
            DrawChild("header", "defaultGroup", "\u9ed8\u8ba4\u5206\u7ec4", "\u5b57\u6bb5\u6ca1\u6709\u5355\u72ec\u6307\u5b9a group \u65f6\uff0c\u5199\u5165\u8868\u5934\u7684\u9ed8\u8ba4 group \u503c\u3002");

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("\u5bfc\u5165\u884c\u4e3a", _sectionTitleStyle);
            DrawProperty("allowCreateGroupOnImport", "\u5bfc\u5165\u65f6\u5141\u8bb8\u521b\u5efa Group", "\u8868\u683c\u91cc\u51fa\u73b0\u65b0 Group\uff0c\u4f46 Pack \u91cc\u627e\u4e0d\u5230\u5bf9\u5e94 Group \u65f6\uff0c\u662f\u5426\u5141\u8bb8\u81ea\u52a8\u521b\u5efa\u3002");
        }

        private void DrawTypeCacheFields()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                DrawChildEnum("typeBinding", "objectKind", "\u5bf9\u8c61\u4f53\u7cfb", new[] { "SoData Pack / Group / Info", "\u666e\u901a ScriptableObject" });
                DrawChild("typeBinding", "objectTypeName", "\u666e\u901a SO \u7c7b\u578b");
                DrawChild("typeBinding", "packTypeName", "Pack \u7c7b\u578b");
                DrawChild("typeBinding", "groupTypeName", "Group \u7c7b\u578b");
                DrawChild("typeBinding", "infoTypeName", "Info \u7c7b\u578b");
            }
        }

        private void DrawColumnFields()
        {
            SerializedProperty columns = serializedObject.FindProperty("columns");
            if (columns == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("\u5217\u6620\u5c04\u6570\u91cf\uff1a" + columns.arraySize, _sectionTitleStyle);
                if (GUILayout.Button("\u5168\u90e8\u8be6\u60c5", EditorStyles.miniButton, GUILayout.Width(82)))
                    SetColumnDetails(columns, true);
                if (GUILayout.Button("\u6536\u8d77\u8be6\u60c5", EditorStyles.miniButton, GUILayout.Width(82)))
                    SetColumnDetails(columns, false);
                if (GUILayout.Button("\u6e05\u9664\u672a\u9501\u5b9a", EditorStyles.miniButton, GUILayout.Width(96)))
                    ClearUnlockedColumns(columns);
                if (GUILayout.Button("\u5168\u90e8\u5ffd\u7565", EditorStyles.miniButton, GUILayout.Width(82)))
                    SetColumnAuthority(columns, ESTableColumnAuthority.Ignore);
                if (GUILayout.Button("\u8868\u683c\u6743\u5a01", EditorStyles.miniButton, GUILayout.Width(82)))
                    SetColumnAuthority(columns, ESTableColumnAuthority.TableAuthority);
                if (GUILayout.Button("\u65b0\u589e\u5217", EditorStyles.miniButton, GUILayout.Width(72)))
                    columns.InsertArrayElementAtIndex(columns.arraySize);
            }

            DrawColumnHeader();

            for (int i = 0; i < columns.arraySize; i++)
            {
                SerializedProperty item = columns.GetArrayElementAtIndex(i);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope(GUILayout.MinHeight(22)))
                    {
                        DrawColumnIndex(i);
                        DrawCompactChild(item, "enabled", GUIContent.none, 34);
                        DrawCompactChild(item, "locked", GUIContent.none, 34);
                        DrawCompactChild(item, "authority", GUIContent.none, 86);
                        DrawCompactChild(item, "soFieldPath", GUIContent.none, 190);
                        DrawCompactChild(item, "displayName", GUIContent.none, 150);
                        DrawCompactChild(item, "columnName", GUIContent.none, 150);
                        DrawCompactChild(item, "availability", GUIContent.none, 84);
                        DrawCompactChild(item, "showDetail", GUIContent.none, 48);

                        using (new EditorGUI.DisabledScope(i == 0))
                        {
                            if (GUILayout.Button("\u4e0a", EditorStyles.miniButton, GUILayout.Width(32)))
                            {
                                columns.MoveArrayElement(i, i - 1);
                                break;
                            }
                        }

                        using (new EditorGUI.DisabledScope(i >= columns.arraySize - 1))
                        {
                            if (GUILayout.Button("\u4e0b", EditorStyles.miniButton, GUILayout.Width(32)))
                            {
                                columns.MoveArrayElement(i, i + 1);
                                break;
                            }
                        }

                        SerializedProperty locked = item.FindPropertyRelative("locked");
                        using (new EditorGUI.DisabledScope(locked != null && locked.boolValue))
                        {
                            if (GUILayout.Button("\u5220", EditorStyles.miniButton, GUILayout.Width(34)))
                            {
                                columns.DeleteArrayElementAtIndex(i);
                                break;
                            }
                        }
                    }

                    SerializedProperty showDetail = item.FindPropertyRelative("showDetail");
                    if (showDetail == null || !showDetail.boolValue)
                        continue;

                    EditorGUILayout.Space(2);
                    DrawChildEnum(item, "valueWriteMode", "SO \u5199\u5165\u65b9\u5f0f", new[] { "\u666e\u901a\u503c", "Unity \u5bf9\u8c61 GUID", "Unity \u5bf9\u8c61\u8def\u5f84", "Json", "\u7c7b\u578b\u540d" });
                    DrawChild(item, "isInfoKey", "Info Key");
                    DrawChild(item, "isGroupKey", "Group Key");
                    DrawChild(item, "comment", "\u4e2d\u6587\u8bf4\u660e");
                    DrawChild(item, "tableType", "SO表格 \u7c7b\u578b");
                    DrawChildEnum(item, "direction", "\u65b9\u5411", new[] { "\u53cc\u5411", "\u4ec5 SO \u5230\u8868\u683c", "\u4ec5\u8868\u683c\u5230 SO", "\u5ffd\u7565" });
                    DrawChild(item, "allowPassThrough", "\u4fdd\u7559\u672a\u6620\u5c04\u5217");
                }
            }
        }

        private void DrawColumnHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("\u5217", EditorStyles.miniBoldLabel, GUILayout.Width(48));
                GUILayout.Label("\u7528", EditorStyles.miniBoldLabel, GUILayout.Width(34));
                GUILayout.Label("\u9501", EditorStyles.miniBoldLabel, GUILayout.Width(34));
                GUILayout.Label("\u6743\u5a01", EditorStyles.miniBoldLabel, GUILayout.Width(86));
                GUILayout.Label("SO \u5b57\u6bb5\u540d", EditorStyles.miniBoldLabel, GUILayout.Width(190));
                GUILayout.Label("SO \u663e\u793a\u540d", EditorStyles.miniBoldLabel, GUILayout.Width(150));
                GUILayout.Label("\u8868\u683c\u5217\u540d", EditorStyles.miniBoldLabel, GUILayout.Width(150));
                GUILayout.Label("\u53ef\u7528", EditorStyles.miniBoldLabel, GUILayout.Width(84));
                GUILayout.Label("\u8be6\u60c5", EditorStyles.miniBoldLabel, GUILayout.Width(48));
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawColumnIndex(int index)
        {
            GUILayout.Label((index + 1).ToString() + " / " + ToColumnLetters(index), EditorStyles.miniLabel, GUILayout.Width(48));
        }

        private static string ToColumnLetters(int index)
        {
            index = Mathf.Max(0, index);
            var builder = new StringBuilder();
            int value = index;
            do
            {
                int mod = value % 26;
                builder.Insert(0, (char)('A' + mod));
                value = value / 26 - 1;
            }
            while (value >= 0);

            return builder.ToString();
        }

        private static void SetColumnDetails(SerializedProperty columns, bool value)
        {
            if (columns == null)
                return;

            for (int i = 0; i < columns.arraySize; i++)
            {
                SerializedProperty item = columns.GetArrayElementAtIndex(i);
                SerializedProperty showDetail = item.FindPropertyRelative("showDetail");
                if (showDetail != null)
                    showDetail.boolValue = value;
            }
        }

        private static void SetColumnAuthority(SerializedProperty columns, ESTableColumnAuthority authority)
        {
            if (columns == null)
                return;

            for (int i = 0; i < columns.arraySize; i++)
            {
                SerializedProperty item = columns.GetArrayElementAtIndex(i);
                SerializedProperty authorityProperty = item.FindPropertyRelative("authority");
                if (authorityProperty != null)
                    authorityProperty.enumValueIndex = (int)authority;
            }
        }

        private static void ClearUnlockedColumns(SerializedProperty columns)
        {
            if (columns == null)
                return;

            for (int i = columns.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty item = columns.GetArrayElementAtIndex(i);
                SerializedProperty locked = item.FindPropertyRelative("locked");
                if (locked == null || !locked.boolValue)
                    columns.DeleteArrayElementAtIndex(i);
            }
        }

        private void DrawCompactChild(SerializedProperty parent, string childName, GUIContent label, float width)
        {
            SerializedProperty child = parent.FindPropertyRelative(childName);
            if (child == null)
            {
                GUILayout.Space(width);
                return;
            }

            EditorGUILayout.PropertyField(child, label, GUILayout.Width(width));
        }

        private void DrawPathProperty(SerializedProperty property, string label, bool filePath, string extensions)
        {
            if (property == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label), true);
                if (GUILayout.Button(filePath ? "\u9009\u62e9\u6587\u4ef6" : "\u9009\u62e9\u6587\u4ef6\u5939", EditorStyles.miniButton, GUILayout.Width(86)))
                {
                    string selected = filePath
                        ? EditorUtility.OpenFilePanel(label, GetPathPanelFolder(property.stringValue), extensions)
                        : EditorUtility.OpenFolderPanel(label, GetPathPanelFolder(property.stringValue), string.Empty);
                    if (!string.IsNullOrEmpty(selected))
                        property.stringValue = filePath ? selected : MakeProjectRelative(selected);
                }
            }

            Rect dropRect = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
            ESDropZoneSolver dropZone = filePath ? _tablePathDropZone : _folderPathDropZone;
            if (dropZone.Draw(dropRect, out UnityEngine.Object[] dropped))
            {
                string path = ResolveDroppedPath(dropped, filePath, extensions);
                if (!string.IsNullOrEmpty(path))
                    property.stringValue = filePath ? path : MakeProjectRelative(path);
            }

            string prompt = filePath ? "\u62d6\u5165 Project \u91cc\u7684 CSV / XLSX \u8868\u683c" : "\u62d6\u5165 Project \u91cc\u7684\u8868\u683c\u8f93\u51fa\u6587\u4ef6\u5939";
            string detail = string.IsNullOrEmpty(dropZone.LastRejectReason)
                ? dropZone.LastAcceptedCount > 0 ? "\u677e\u5f00\u540e\u63a5\u6536 " + dropZone.LastAcceptedCount + " \u4e2a\u5bf9\u8c61" : prompt
                : "\u62d2\u7edd\uff1a" + dropZone.LastRejectReason;
            GUI.Label(dropRect, detail, EditorStyles.centeredGreyMiniLabel);
        }

        private static string GetPathPanelFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Application.dataPath;

            string fullPath = Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            if (Directory.Exists(fullPath))
                return fullPath;

            string folder = Path.GetDirectoryName(fullPath);
            return string.IsNullOrEmpty(folder) ? Application.dataPath : folder;
        }

        private static string MakeProjectRelative(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/');
            string fullPath = Path.GetFullPath(path).Replace('\\', '/');
            if (fullPath.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                return fullPath.Substring(projectRoot.Length + 1);

            return fullPath;
        }

        private static string ResolveDroppedPath(UnityEngine.Object[] dropped, bool filePath, string extensions)
        {
            if (dropped == null)
                return string.Empty;

            for (int i = 0; i < dropped.Length; i++)
            {
                string assetPath = AssetDatabase.GetAssetPath(dropped[i]);
                if (string.IsNullOrEmpty(assetPath))
                    continue;

                string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
                if (!filePath && AssetDatabase.IsValidFolder(assetPath))
                    return assetPath;

                if (filePath && File.Exists(fullPath) && IsAllowedExtension(fullPath, extensions))
                    return fullPath;
            }

            return string.Empty;
        }

        private static bool IsAllowedExtension(string path, string extensions)
        {
            if (string.IsNullOrWhiteSpace(extensions))
                return true;

            string extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            string[] allowed = extensions.Split(',');
            for (int i = 0; i < allowed.Length; i++)
            {
                if (extension == allowed[i].Trim().TrimStart('.').ToLowerInvariant())
                    return true;
            }

            return false;
        }

        private static void ApplyDefaultBatchPath(SerializedProperty batch, ESSoTableDataRule rule)
        {
            if (batch == null || rule == null)
                return;

            SetStringChild(batch, "fileName", FirstNotEmpty(GetStringChild(batch, "fileName"), rule.ruleKey, rule.tableName, rule.name));
            SetStringChild(batch, "sheetName", FirstNotEmpty(GetStringChild(batch, "sheetName"), rule.ruleKey, rule.beanName, rule.name));
            SetStringChild(batch, "outputRoot", FirstNotEmpty(GetStringChild(batch, "outputRoot"), "SoTableConfig/Tables"));
            SetStringChild(batch, "csvRelativePath", FirstNotEmpty(GetStringChild(batch, "csvRelativePath"), "csv"));
            SetStringChild(batch, "xlsxRelativePath", FirstNotEmpty(GetStringChild(batch, "xlsxRelativePath"), "xlsx"));
        }

        private void DrawBatchTableOpenButtons(SerializedProperty batch, ESSoTableDataRule rule)
        {
            if (batch == null || rule == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUIUtility.labelWidth);
                if (GUILayout.Button("打开 CSV", EditorStyles.miniButtonLeft, GUILayout.Width(86)))
                    OpenBatchTablePath(batch, rule, ".csv");
                if (GUILayout.Button("打开 XLSX", EditorStyles.miniButtonMid, GUILayout.Width(86)))
                    OpenBatchTablePath(batch, rule, ".xlsx");
                if (GUILayout.Button("打开输出文件夹", EditorStyles.miniButtonRight, GUILayout.Width(112)))
                    OpenBatchOutputFolder(batch);
            }
        }

        private static void OpenBatchTablePath(SerializedProperty batch, ESSoTableDataRule rule, string extension)
        {
            string path = BuildBatchTableFullPath(batch, rule, extension);
            if (File.Exists(path))
            {
                EditorUtility.OpenWithDefaultApp(path);
                return;
            }

            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
                EditorUtility.OpenWithDefaultApp(folder);
            }

            Debug.LogWarning("表格文件不存在，已打开输出文件夹：" + path, rule);
        }

        private static void OpenBatchOutputFolder(SerializedProperty batch)
        {
            string root = FirstNotEmpty(GetStringChild(batch, "outputRoot"), "SoTableConfig/Tables");
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", root));
            Directory.CreateDirectory(folder);
            EditorUtility.OpenWithDefaultApp(folder);
        }

        private static string BuildBatchTableFullPath(SerializedProperty batch, ESSoTableDataRule rule, string extension)
        {
            string root = FirstNotEmpty(GetStringChild(batch, "outputRoot"), "SoTableConfig/Tables");
            string relativeFolder = extension == ".xlsx"
                ? FirstNotEmpty(GetStringChild(batch, "xlsxRelativePath"), "xlsx")
                : FirstNotEmpty(GetStringChild(batch, "csvRelativePath"), "csv");
            string file = FirstNotEmpty(GetStringChild(batch, "fileName"), rule != null ? rule.ruleKey : null, rule != null ? rule.tableName : null, rule != null ? rule.name : null, "NewTable");
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", root, relativeFolder, file + extension));
        }

        private static void ApplyDefaultPathToAllBatches(SerializedProperty batches, ESSoTableDataRule rule)
        {
            if (batches == null)
                return;

            for (int i = 0; i < batches.arraySize; i++)
                ApplyDefaultBatchPath(batches.GetArrayElementAtIndex(i), rule);
        }

        private static void SetAllBatchesEnabled(SerializedProperty batches, bool enabled)
        {
            if (batches == null)
                return;

            for (int i = 0; i < batches.arraySize; i++)
            {
                SerializedProperty batch = batches.GetArrayElementAtIndex(i);
                SerializedProperty enabledProperty = batch.FindPropertyRelative("enabled");
                if (enabledProperty != null)
                    enabledProperty.boolValue = enabled;
            }
        }

        private static string GetStringChild(SerializedProperty parent, string childName)
        {
            SerializedProperty child = parent.FindPropertyRelative(childName);
            return child != null ? child.stringValue : string.Empty;
        }

        private static void SetStringChild(SerializedProperty parent, string childName, string value)
        {
            SerializedProperty child = parent.FindPropertyRelative(childName);
            if (child != null)
                child.stringValue = value;
        }

        private void DrawProperty(string propertyName, string label)
        {
            DrawProperty(propertyName, label, string.Empty);
        }

        private void DrawProperty(string propertyName, string label, string tooltip)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
        }

        private void DrawEnumProperty(string propertyName, string label, string[] displayNames)
        {
            DrawEnumProperty(propertyName, label, string.Empty, displayNames);
        }

        private void DrawEnumProperty(string propertyName, string label, string tooltip, string[] displayNames)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            DrawEnumProperty(property, label, tooltip, displayNames);
        }

        private void DrawChild(string parentName, string childName, string label)
        {
            DrawChild(parentName, childName, label, string.Empty);
        }

        private void DrawChild(string parentName, string childName, string label, string tooltip)
        {
            SerializedProperty parent = serializedObject.FindProperty(parentName);
            if (parent == null)
                return;

            DrawChild(parent, childName, label, tooltip);
        }

        private void DrawChild(SerializedProperty parent, string childName, string label)
        {
            DrawChild(parent, childName, label, string.Empty);
        }

        private void DrawChild(SerializedProperty parent, string childName, string label, string tooltip)
        {
            SerializedProperty child = parent.FindPropertyRelative(childName);
            if (child != null)
                EditorGUILayout.PropertyField(child, new GUIContent(label, tooltip), true);
        }

        private void DrawChildEnum(string parentName, string childName, string label, string[] displayNames)
        {
            DrawChildEnum(parentName, childName, label, string.Empty, displayNames);
        }

        private void DrawChildEnum(string parentName, string childName, string label, string tooltip, string[] displayNames)
        {
            SerializedProperty parent = serializedObject.FindProperty(parentName);
            if (parent == null)
                return;

            DrawChildEnum(parent, childName, label, tooltip, displayNames);
        }

        private void DrawChildEnum(SerializedProperty parent, string childName, string label, string[] displayNames)
        {
            DrawChildEnum(parent, childName, label, string.Empty, displayNames);
        }

        private void DrawChildEnum(SerializedProperty parent, string childName, string label, string tooltip, string[] displayNames)
        {
            SerializedProperty child = parent.FindPropertyRelative(childName);
            DrawEnumProperty(child, label, tooltip, displayNames);
        }

        private void DrawEnumProperty(SerializedProperty property, string label, string[] displayNames)
        {
            DrawEnumProperty(property, label, string.Empty, displayNames);
        }

        private void DrawEnumProperty(SerializedProperty property, string label, string tooltip, string[] displayNames)
        {
            if (property == null)
                return;

            if (property.propertyType != SerializedPropertyType.Enum || displayNames == null || displayNames.Length == 0)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
                return;
            }

            int index = Mathf.Clamp(property.enumValueIndex, 0, displayNames.Length - 1);
            property.enumValueIndex = EditorGUILayout.Popup(new GUIContent(label, tooltip), index, displayNames);
        }

        private static void BindPreferredSource(ESSoTableDataRule rule)
        {
            ESSoTableRuleSourceBinding source = GetBuildSourceBinding(rule);
            if (source == null)
                return;

            if (source.soFolder != null)
            {
                rule.BindAndGenerateFromFolder();
                return;
            }

            if (source.soAsset != null)
            {
                rule.BindAndGenerateFromSoAsset();
                return;
            }

            if (source.monoScript != null)
                rule.BindAndGenerateFromMonoScript();
        }

        private static bool HasAnySource(ESSoTableDataRule rule)
        {
            ESSoTableRuleSourceBinding source = GetBuildSourceBinding(rule);
            return source != null &&
                   (source.soAsset != null || source.soFolder != null || source.monoScript != null);
        }

        private static bool HasColumns(ESSoTableDataRule rule)
        {
            return rule.columns != null && rule.columns.Count > 0;
        }

        private static int GetColumnCount(ESSoTableDataRule rule)
        {
            return rule.columns == null ? 0 : rule.columns.Count;
        }

        private static int GetEnabledColumnCount(ESSoTableDataRule rule)
        {
            if (rule.columns == null)
                return 0;

            int count = 0;
            for (int i = 0; i < rule.columns.Count; i++)
            {
                if (rule.columns[i] != null && rule.columns[i].enabled)
                    count++;
            }

            return count;
        }

        private static string GetSourceSummary(ESSoTableDataRule rule)
        {
            ESSoTableRuleSourceBinding source = GetBuildSourceBinding(rule);
            if (source == null)
                return "\u65e0\u6765\u6e90";

            if (source.soFolder != null)
                return "\u6587\u4ef6\u5939\uff1a" + source.soFolder.name;
            if (source.soAsset != null)
                return "SO\uff1a" + source.soAsset.name;
            if (source.monoScript != null)
                return "\u811a\u672c\uff1a" + source.monoScript.name;

            return GetBindSourceKindText(source.sourceKind);
        }

        private static string GetSourceKind(ESSoTableDataRule rule)
        {
            ESSoTableRuleSourceBinding source = GetBuildSourceBinding(rule);
            if (source == null)
                return "\u65e0";

            if (source.soFolder != null)
                return "\u6587\u4ef6\u5939";
            if (source.soAsset != null)
                return "SO \u6587\u4ef6";
            if (source.monoScript != null)
                return "\u811a\u672c";

            return GetBindSourceKindText(source.sourceKind);
        }

        private static ESSoTableRuleSourceBinding GetBuildSourceBinding(ESSoTableDataRule rule)
        {
            if (rule == null)
                return null;

            return rule.buildStage != null ? rule.buildStage.sourceBinding : null;
        }
        private static string GetSourcePath(ESSoTableDataRule rule)
        {
            ESSoTableRuleSourceBinding source = GetBuildSourceBinding(rule);
            if (source == null)
                return string.Empty;

            string path = string.Empty;
            if (source.soFolder != null)
                path = AssetDatabase.GetAssetPath(source.soFolder);
            else if (source.soAsset != null)
                path = AssetDatabase.GetAssetPath(source.soAsset);
            else if (source.monoScript != null)
                path = AssetDatabase.GetAssetPath(source.monoScript);

            return FirstNotEmpty(path, source.sourcePath, "-");
        }

        private static string GetInfoTypeName(ESSoTableDataRule rule)
        {
            if (rule.typeBinding == null)
                return "-";

            return ShortTypeName(FirstNotEmpty(rule.typeBinding.infoTypeName, rule.typeBinding.objectTypeName, "-"));
        }

        private static string GetPackGroupSummary(ESSoTableDataRule rule)
        {
            if (rule.typeBinding == null)
                return "-";

            return ShortTypeName(rule.typeBinding.packTypeName) + " / " + ShortTypeName(rule.typeBinding.groupTypeName);
        }

        private static string GetOutputMode(ESSoTableDataRule rule)
        {
            ESSoTableRuleUseBatch batch = GetPrimaryBatch(rule);
            ESTableFileKind fileKind = batch != null ? batch.fileKind : ESTableFileKind.CsvAndXlsx;
            return GetFileKindText(fileKind) + "  |  " + GetGroupSliceModeText(rule.groupSliceMode);
        }

        private static string GetOutputPath(ESSoTableDataRule rule)
        {
            ESSoTableRuleUseBatch batch = GetPrimaryBatch(rule);
            string root = batch != null ? FirstNotEmpty(batch.outputRoot, "SoTableConfig/Tables") : "SoTableConfig/Tables";
            string file = FirstNotEmpty(GetBatchFileName(rule), rule.tableName, rule.ruleKey, rule.name);
            return root + "/" + file;
        }

        private static ESSoTableRuleUseBatch GetPrimaryBatch(ESSoTableDataRule rule)
        {
            if (rule == null || rule.useBatches == null || rule.useBatches.Count == 0)
                return null;

            for (int i = 0; i < rule.useBatches.Count; i++)
            {
                if (rule.useBatches[i] != null && rule.useBatches[i].enabled)
                    return rule.useBatches[i];
            }

            return rule.useBatches[0];
        }

        private static string GetBatchFileName(ESSoTableDataRule rule)
        {
            ESSoTableRuleUseBatch batch = GetPrimaryBatch(rule);
            return batch != null ? batch.fileName : string.Empty;
        }

        private static string GetBatchSheetName(ESSoTableDataRule rule)
        {
            ESSoTableRuleUseBatch batch = GetPrimaryBatch(rule);
            return batch != null ? batch.sheetName : string.Empty;
        }

        private static string GetBatchCountText(ESSoTableDataRule rule)
        {
            int count = rule != null && rule.useBatches != null ? rule.useBatches.Count : 0;
            if (count == 0)
                return "\u672a\u914d\u7f6e";
            return count + " \u4e2a\u6279\u6b21";
        }

        private static string GetBatchPolicyText(ESSoTableDataRule rule)
        {
            ESSoTableRuleUseBatch batch = GetPrimaryBatch(rule);
            if (batch == null)
                return "\u4f7f\u7528\u9636\u6bb5\u5c1a\u672a\u914d\u7f6e";

            return "\u5bfc\u5165 " + GetConflictPolicyText(batch.importConflictPolicy) + " / \u5bfc\u51fa " + GetConflictPolicyText(batch.exportConflictPolicy);
        }

        private static string FirstNotEmpty(params string[] values)
        {
            if (values == null)
                return string.Empty;

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i];
            }

            return string.Empty;
        }

        private static string ShortTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return "-";

            int index = typeName.LastIndexOf('.');
            return index >= 0 && index + 1 < typeName.Length ? typeName.Substring(index + 1) : typeName;
        }

        private static string ShortenMiddle(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
                return string.IsNullOrWhiteSpace(value) ? "-" : value;

            int keep = Math.Max(4, (maxLength - 3) / 2);
            return value.Substring(0, keep) + "..." + value.Substring(value.Length - keep);
        }

        private static string GetFileKindText(ESTableFileKind value)
        {
            switch (value)
            {
                case ESTableFileKind.Csv:
                    return "CSV";
                case ESTableFileKind.Xlsx:
                    return "XLSX";
                case ESTableFileKind.CsvAndXlsx:
                    return "CSV \u548c XLSX";
                default:
                    return value.ToString();
            }
        }

        private static string GetGroupSliceModeText(ESTableGroupSliceMode value)
        {
            switch (value)
            {
                case ESTableGroupSliceMode.IgnoreGroup:
                    return "\u5ffd\u7565 Group";
                case ESTableGroupSliceMode.GroupNameColumn:
                    return "Group \u540d\u5199\u5165\u5217";
                case ESTableGroupSliceMode.OneGroupPerSheet:
                    return "\u6bcf\u4e2a Group \u4e00\u4e2a Sheet";
                case ESTableGroupSliceMode.OneGroupPerFile:
                    return "\u6bcf\u4e2a Group \u4e00\u4e2a\u6587\u4ef6";
                default:
                    return value.ToString();
            }
        }

        private static string GetConflictPolicyText(ESTableConflictPolicy value)
        {
            switch (value)
            {
                case ESTableConflictPolicy.Skip:
                    return "\u8df3\u8fc7";
                case ESTableConflictPolicy.Overwrite:
                    return "\u8986\u76d6";
                case ESTableConflictPolicy.CreateCopy:
                    return "\u521b\u5efa\u526f\u672c";
                case ESTableConflictPolicy.Error:
                    return "\u62a5\u9519";
                default:
                    return value.ToString();
            }
        }

        private static string GetBindSourceKindText(ESSoTableRuleBindSourceKind value)
        {
            switch (value)
            {
                case ESSoTableRuleBindSourceKind.None:
                    return "\u65e0";
                case ESSoTableRuleBindSourceKind.SoAsset:
                    return "SO \u6587\u4ef6";
                case ESSoTableRuleBindSourceKind.SoFolder:
                    return "SO \u6587\u4ef6\u5939";
                case ESSoTableRuleBindSourceKind.MonoScript:
                    return "\u811a\u672c";
                default:
                    return value.ToString();
            }
        }
    }
}
#endif
