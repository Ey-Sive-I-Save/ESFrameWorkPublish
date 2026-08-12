using System;
using System.Collections.Generic;
using System.Linq;
using ES;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    public abstract class ESGameCoreConfigKeyDrawerBase : PropertyDrawer
    {
        private const float Line = 18f;
        private const float Gap = 2f;
        private const float PanelPadding = 6f;
        private static readonly Color PanelAccent = new Color(1f, 0.72f, 0.18f, 0.95f);

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            ESGameCoreDefinitionLocator.Candidate current = ESGameCoreDefinitionLocator.FindCandidate(property, ResolveEnumType());
            int lines = 5 + (ESGameCoreDefinitionLocator.IsStale(property, current) ? 1 : 0) + (property.isExpanded ? 3 : 0);
            return lines * Line + (lines - 1) * Gap + PanelPadding * 2f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            int previousIndent = EditorGUI.indentLevel;
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUI.indentLevel = 0;
            EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.27f, 76f, 108f);

            GUI.Box(position, GUIContent.none, EditorStyles.helpBox);
            EditorGUI.DrawRect(new Rect(position.x + 2f, position.y + 2f, Mathf.Max(0f, position.width - 4f), 2f), PanelAccent);
            position = new Rect(
                position.x + PanelPadding,
                position.y + PanelPadding,
                Mathf.Max(0f, position.width - PanelPadding * 2f),
                Mathf.Max(0f, position.height - PanelPadding * 2f));

            SerializedProperty enumKey = property.FindPropertyRelative("enumKey");
            SerializedProperty stringKey = property.FindPropertyRelative("stringKey");
            Type enumType = ResolveEnumType();
            ESConfigKeyUsage usage = ResolveUsage();
            ESGameCoreDefinitionLocator.Candidate current = ESGameCoreDefinitionLocator.FindCandidate(property, enumType);
            bool isStale = ESGameCoreDefinitionLocator.IsStale(property, current);

            Rect row = NextLine(ref position);
            DrawHeader(row, ResolveTitle(property, label, enumType),
                usage == ESConfigKeyUsage.Declaration ? "GameCore 定义键" : "GameCore 引用键");

            row = NextLine(ref position);
            const string enumLabel = "枚举 Key";
            Rect contentRect = EditorGUI.PrefixLabel(row, new GUIContent(enumLabel));
            DrawActionRow(contentRect, out Rect selectorRect, out Rect clearRect, out Rect locateRect);
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(selectorRect, enumKey, GUIContent.none);
            if (EditorGUI.EndChangeCheck())
                ApplyEnumSelection(property, enumType, usage, enumKey.intValue);

            bool configured = (enumKey != null && enumKey.intValue != 0) || (stringKey != null && !string.IsNullOrWhiteSpace(stringKey.stringValue));
            using (new EditorGUI.DisabledScope(!configured))
            {
                if (GUI.Button(clearRect, new GUIContent("×", "清空配置"), EditorStyles.miniButton))
                    Clear(property);
                if (GUI.Button(locateRect, new GUIContent("定位", "定位定义资产"), EditorStyles.miniButton))
                    LocateDefinitionAsset(property, enumType);
            }

            row = NextLine(ref position);
            Rect stringRect = EditorGUI.PrefixLabel(row, new GUIContent("字符串 Key", "可直接输入，也可从已有 GameCore 定义中选择。"));
            DrawStringSelectionRow(stringRect, out Rect stringInputRect, out Rect stringSelectRect);
            EditorGUI.BeginChangeCheck();
            string editedStringKey = EditorGUI.DelayedTextField(stringInputRect, stringKey.stringValue);
            if (EditorGUI.EndChangeCheck())
                TryApplyIdentity(property, enumType, usage, enumKey.intValue, editedStringKey ?? string.Empty);
            if (GUI.Button(stringSelectRect, StringSelectContent(stringSelectRect), EditorStyles.miniButton))
                ShowStringKeyMenu(stringSelectRect, property, enumType, usage);

            row = NextLine(ref position);
            Rect assetRect = EditorGUI.PrefixLabel(row, new GUIContent("资产(备选)", "拖入定义资产以反向同步 ConfigKey"));
            Type definitionType = ESGameCoreDefinitionLocator.ResolveAssetType(enumType) ?? typeof(ScriptableObject);
            EditorGUI.BeginChangeCheck();
            ScriptableObject selected = EditorGUI.ObjectField(assetRect, current?.asset, definitionType, false) as ScriptableObject;
            if (EditorGUI.EndChangeCheck())
            {
                if (selected == null)
                {
                    ClearDefinitionIdentity(property);
                    Apply(property);
                }
                else if (ESGameCoreDefinitionLocator.TryCreateCandidate(selected, enumType, out ESGameCoreDefinitionLocator.Candidate selectedCandidate))
                    ApplyCandidate(property, enumType, usage, selectedCandidate);
                else
                    Debug.LogWarning("[ESGameCore][ConfigKey] 选择的定义资产不包含对应类型的 ConfigKey：" + selected.name, selected);
            }

            if (isStale)
            {
                row = NextLine(ref position);
                Rect syncLabel = EditorGUI.PrefixLabel(row, new GUIContent("Source Key changed", "The bound definition now has a different Key; runtime still reads this snapshot."));
                float syncWidth = Mathf.Min(72f, syncLabel.width * 0.34f);
                EditorGUI.HelpBox(new Rect(syncLabel.x, syncLabel.y, Mathf.Max(20f, syncLabel.width - syncWidth - Gap), syncLabel.height),
                    ESConfigKeyMatch.Describe(enumKey.intValue, stringKey.stringValue) + " -> " + ESConfigKeyMatch.Describe(current.enumKey, current.stringKey),
                    MessageType.Warning);
                if (GUI.Button(new Rect(syncLabel.xMax - syncWidth, syncLabel.y, syncWidth, syncLabel.height), "Sync", EditorStyles.miniButton))
                    ApplyCandidate(property, enumType, usage, current);
            }

            property.isExpanded = EditorGUI.Foldout(NextLine(ref position), property.isExpanded,
                BuildSummary(current, enumKey, stringKey), true, EditorStyles.foldout);

            if (property.isExpanded)
            {
                DrawReadOnly(ref position, "定义资产 GUID", property.FindPropertyRelative("definitionGuid"));
                DrawReadOnly(ref position, "子资产 FileId", property.FindPropertyRelative("definitionLocalFileId"));
                DrawReadOnly(ref position, "定义资产类型", property.FindPropertyRelative("definitionTypeName"));
            }

            EditorGUIUtility.labelWidth = previousLabelWidth;
            EditorGUI.indentLevel = previousIndent;
            EditorGUI.EndProperty();
        }

        protected abstract Type ResolveEnumType();

        private ESConfigKeyUsage ResolveUsage()
        {
            ESConfigKeyUsageAttribute usage = fieldInfo != null
                ? Attribute.GetCustomAttribute(fieldInfo, typeof(ESConfigKeyUsageAttribute), true) as ESConfigKeyUsageAttribute
                : null;
            return usage != null ? usage.Usage : ESConfigKeyUsage.Reference;
        }

        private static void DrawHeader(Rect rect, string title, string subtitle)
        {
            Rect marker = new Rect(rect.x, rect.y + 2f, 3f, rect.height - 4f);
            EditorGUI.DrawRect(marker, PanelAccent);
            Rect titleRect = new Rect(marker.xMax + 5f, rect.y, Mathf.Max(0f, rect.width - marker.width - 5f), rect.height);
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = PanelAccent;
            EditorGUI.LabelField(titleRect, title + "  ·  " + subtitle, style);
        }

        private static string ResolveTitle(SerializedProperty property, GUIContent label, Type enumType)
        {
            if (HasVisibleLabel(label)) return label.text;
            if (enumType == typeof(ESBuffEnumKey)) return "Buff 身份 Key";
            if (enumType == typeof(ESSkillEnumKey)) return "技能身份 Key";
            if (enumType == typeof(ESMonsterEnumKey)) return "怪物身份 Key";
            if (enumType == typeof(ESNpcEnumKey)) return "NPC 身份 Key";
            if (enumType == typeof(ESWeaponEnumKey)) return "武器身份 Key";
            if (enumType == typeof(ESShotEnumKey)) return "投射物身份 Key";
            if (enumType == typeof(ESStoryEnumKey)) return "任务与剧情身份 Key";
            if (enumType == typeof(ESFlowTestEnumKey)) return "流程验收身份 Key";
            return property != null && !string.IsNullOrWhiteSpace(property.displayName)
                ? property.displayName
                : "GameCore 身份 Key";
        }

        private static void DrawStringSelectionRow(Rect rect, out Rect input, out Rect select)
        {
            float selectWidth = rect.width >= 150f ? 42f : 24f;
            select = new Rect(rect.xMax - selectWidth, rect.y, selectWidth, rect.height);
            input = new Rect(rect.x, rect.y, Mathf.Max(20f, select.x - Gap - rect.x), rect.height);
        }

        private static GUIContent StringSelectContent(Rect rect)
        {
            return new GUIContent(rect.width >= 40f ? "选择" : "▼", "从已有 GameCore 定义中选择字符串 Key");
        }

        private static string BuildSummary(
            ESGameCoreDefinitionLocator.Candidate current,
            SerializedProperty enumKey,
            SerializedProperty stringKey)
        {
            int enumValue = enumKey != null ? enumKey.intValue : 0;
            string textKey = stringKey != null ? stringKey.stringValue : string.Empty;
            if (current != null)
            {
                string mode = enumValue != 0 ? "枚举优先" : "字符串模式";
                string key = string.IsNullOrWhiteSpace(current.stringKey)
                    ? enumValue.ToString()
                    : current.stringKey;
                return "已解析 · " + mode + " · " + key + " · " + current.asset.name;
            }

            if (enumValue != 0)
                return "枚举模式 · 尚未唯一解析资产 · Enum=" + enumValue;
            if (!string.IsNullOrWhiteSpace(textKey))
                return "字符串模式 · 尚未唯一解析资产 · " + textKey;
            return "未配置 · 可使用枚举、字符串或拖入资产";
        }

        private static void ShowStringKeyMenu(
            Rect position,
            SerializedProperty property,
            Type enumType,
            ESConfigKeyUsage usage)
        {
            IReadOnlyList<ESGameCoreDefinitionLocator.Candidate> candidates = ESGameCoreDefinitionLocator.GetCandidates(enumType);
            var entries = new List<ESSearchDropdown.Entry>(candidates.Count);
            string currentGuid = property.FindPropertyRelative("definitionGuid")?.stringValue ?? string.Empty;
            long currentLocalFileId = property.FindPropertyRelative("definitionLocalFileId")?.longValue ?? 0;
            if (candidates.Count == 0)
            {
                entries.Add(ESSearchDropdown.Entry.Disabled("没有可选的 GameCore 定义"));
            }
            else
            {
                foreach (ESGameCoreDefinitionLocator.Candidate item in candidates)
                {
                    ESGameCoreDefinitionLocator.Candidate captured = item;
                    string path = AssetDatabase.GetAssetPath(item.asset);
                    string key = string.IsNullOrWhiteSpace(item.stringKey) ? "<无字符串 Key>" : item.stringKey;
                    string folder = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                    entries.Add(ESSearchDropdown.Entry.Item(
                        item.asset.name,
                        () => ApplyCandidate(property, enumType, usage, captured),
                        folder,
                        AssetPreview.GetMiniThumbnail(item.asset),
                        subtitle: item.asset.GetType().Name + " · " + key,
                        tooltip: path,
                        badge: item.localFileId != 0 ? "子资产" : "主资产",
                        selected: item.guid == currentGuid && item.localFileId == currentLocalFileId));
                }
            }
            ESSearchDropdown.Open(position, "选择 GameCore 字符串 Key", entries);
        }

        private static bool HasVisibleLabel(GUIContent label)
        {
            return label != null && !string.IsNullOrWhiteSpace(label.text);
        }

        private static void DrawActionRow(Rect rect, out Rect selector, out Rect clear, out Rect locate)
        {
            float clearWidth = 22f;
            float locateWidth = rect.width >= 180f ? 42f : 24f;
            locate = new Rect(rect.xMax - locateWidth, rect.y, locateWidth, rect.height);
            clear = new Rect(locate.x - Gap - clearWidth, rect.y, clearWidth, rect.height);
            selector = new Rect(rect.x, rect.y, Mathf.Max(20f, clear.x - Gap - rect.x), rect.height);
        }

        private static Rect NextLine(ref Rect position)
        {
            Rect rect = new Rect(position.x, position.y, position.width, Line);
            position.y += Line + Gap;
            return rect;
        }

        private static void DrawReadOnly(ref Rect position, string label, SerializedProperty value)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUI.PropertyField(NextLine(ref position), value, new GUIContent(label));
        }

        private static void ApplyEnumSelection(
            SerializedProperty property,
            Type enumType,
            ESConfigKeyUsage usage,
            int enumValue)
        {
            string stringKey = property.FindPropertyRelative("stringKey")?.stringValue ?? string.Empty;
            TryApplyIdentity(property, enumType, usage, enumValue, stringKey);
        }

        private static void TryBindOwningDefinition(
            SerializedProperty property,
            Type enumType,
            ESConfigKeyUsage usage)
        {
            property.serializedObject.Update();
            if (property.serializedObject.targetObject is ScriptableObject definition
                && ESGameCoreDefinitionLocator.TryCreateCandidate(definition, enumType, out ESGameCoreDefinitionLocator.Candidate candidate))
                ApplyCandidate(property, enumType, usage, candidate);
        }

        private static void ApplyCandidate(
            SerializedProperty property,
            Type enumType,
            ESConfigKeyUsage usage,
            ESGameCoreDefinitionLocator.Candidate candidate)
        {
            int resolvedEnumKey = candidate.enumKey;
            string resolvedStringKey = candidate.stringKey ?? string.Empty;
            if (usage == ESConfigKeyUsage.Declaration
                && !TryResolveDeclarationConflict(
                    property,
                    enumType,
                    candidate.enumKey,
                    candidate.stringKey,
                    out resolvedEnumKey,
                    out resolvedStringKey))
                return;

            property.serializedObject.Update();
            property.FindPropertyRelative("enumKey").intValue = resolvedEnumKey;
            property.FindPropertyRelative("stringKey").stringValue = resolvedStringKey ?? string.Empty;
            if (resolvedEnumKey == candidate.enumKey
                && string.Equals(resolvedStringKey, candidate.stringKey, StringComparison.Ordinal))
            {
                property.FindPropertyRelative("definitionGuid").stringValue = candidate.guid ?? string.Empty;
                property.FindPropertyRelative("definitionLocalFileId").longValue = candidate.localFileId;
                property.FindPropertyRelative("definitionTypeName").stringValue = candidate.assetTypeName ?? string.Empty;
            }
            else
            {
                ClearDefinitionIdentity(property);
            }
            Apply(property);
            if (usage == ESConfigKeyUsage.Declaration)
                ESGameCoreDefinitionLocator.ClearCache();
        }

        private static void TryApplyIdentity(
            SerializedProperty property,
            Type enumType,
            ESConfigKeyUsage usage,
            int enumKey,
            string stringKey)
        {
            if (usage == ESConfigKeyUsage.Declaration
                && !TryResolveDeclarationConflict(
                    property,
                    enumType,
                    enumKey,
                    stringKey,
                    out enumKey,
                    out stringKey))
                return;

            property.serializedObject.Update();
            property.FindPropertyRelative("enumKey").intValue = enumKey;
            property.FindPropertyRelative("stringKey").stringValue = stringKey ?? string.Empty;
            ClearDefinitionIdentity(property);
            Apply(property);
            if (usage == ESConfigKeyUsage.Declaration)
                ESGameCoreDefinitionLocator.ClearCache();
            TryBindOwningDefinition(property, enumType, usage);
        }

        private static bool TryResolveDeclarationConflict(
            SerializedProperty property,
            Type enumType,
            int requestedEnumKey,
            string requestedStringKey,
            out int resolvedEnumKey,
            out string resolvedStringKey)
        {
            resolvedEnumKey = requestedEnumKey;
            resolvedStringKey = requestedStringKey ?? string.Empty;
            if (!(property.serializedObject.targetObject is ScriptableObject owner))
                return true;

            ESGameCoreDefinitionLocator.Candidate conflict = ESGameCoreDefinitionLocator.FindConflict(
                enumType,
                owner,
                requestedEnumKey,
                requestedStringKey,
                refresh: true);
            if (conflict == null)
                return true;

            string conflictPath = AssetDatabase.GetAssetPath(conflict.asset);
            string requested = ESConfigKeyMatch.Describe(requestedEnumKey, requestedStringKey);
            string suggestion = ESGameCoreDefinitionLocator.CreateAvailableStringKey(
                enumType,
                requestedStringKey,
                owner);
            int choice = EditorUtility.DisplayDialogComplex(
                "GameCore Key 已被占用",
                "不能保存定义键 " + requested + "。\n\n"
                + "占用资产：" + conflict.asset.name + "\n"
                + "路径：" + conflictPath + "\n\n"
                + "可定位占用资产、取消本次修改，或明确改用建议 Key：\n"
                + suggestion,
                "定位占用资产",
                "取消修改",
                "使用建议 Key");

            if (choice == 0)
            {
                Selection.activeObject = conflict.asset;
                EditorGUIUtility.PingObject(conflict.asset);
                property.serializedObject.Update();
                return false;
            }

            if (choice != 2)
            {
                property.serializedObject.Update();
                return false;
            }

            if (requestedEnumKey != 0
                && ESGameCoreDefinitionLocator.HasEnumConflict(enumType, owner, requestedEnumKey))
                resolvedEnumKey = 0;
            resolvedStringKey = suggestion;
            return true;
        }

        private static void Clear(SerializedProperty property)
        {
            property.serializedObject.Update();
            property.FindPropertyRelative("enumKey").intValue = 0;
            property.FindPropertyRelative("stringKey").stringValue = string.Empty;
            ClearDefinitionIdentity(property);
            Apply(property);
        }

        private static void ClearDefinitionIdentity(SerializedProperty property)
        {
            property.FindPropertyRelative("definitionGuid").stringValue = string.Empty;
            property.FindPropertyRelative("definitionLocalFileId").longValue = 0;
            property.FindPropertyRelative("definitionTypeName").stringValue = string.Empty;
        }

        private static string ResolveEnumMemberName(Type enumType, SerializedProperty enumKey)
        {
            if (enumType == null || enumKey == null)
                return null;
            return Enum.GetName(enumType, enumKey.intValue);
        }

        private static void Apply(SerializedProperty property)
        {
            property.serializedObject.ApplyModifiedProperties();
            foreach (UnityEngine.Object target in property.serializedObject.targetObjects)
                if (target != null) EditorUtility.SetDirty(target);
        }

        private static void LocateDefinitionAsset(SerializedProperty property, Type enumType)
        {
            ScriptableObject target = ESGameCoreDefinitionLocator.Find(property, enumType);
            if (target == null)
            {
                Debug.LogWarning("[ESGameCore][Locate] \u672a\u627e\u5230 ConfigKey \u5bf9\u5e94\u7684\u6839\u5b9a\u4e49\u8d44\u4ea7\u3002");
                return;
            }

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);  
        }
    }

    internal static class ESGameCoreDefinitionLocator
    {
        internal sealed class Candidate
        {
            public ScriptableObject asset;
            public int enumKey;
            public string stringKey;
            public string guid;
            public long localFileId;
            public string assetTypeName;
        }

        private static readonly Dictionary<Type, List<Candidate>> CandidateCache = new Dictionary<Type, List<Candidate>>();

        static ESGameCoreDefinitionLocator()
        {
            EditorApplication.projectChanged += CandidateCache.Clear;
        }

        internal static void ClearCache()
        {
            CandidateCache.Clear();
        }

        public static IReadOnlyList<Candidate> GetCandidates(Type enumType, bool refresh = false)
        {
            if (enumType == null) return Array.Empty<Candidate>();
            if (!refresh && CandidateCache.TryGetValue(enumType, out List<Candidate> cached)) return cached;
            var result = new List<Candidate>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            Type assetType = ResolveAssetType(enumType);
            if (assetType != null)
            {
                ESEditorSO.EnsureTypesAssignableTo(assetType);
                foreach (KeyValuePair<Type, List<ESSO>> group in ESEditorSO.SOS.Groups)
                {
                    if (group.Key == null || !assetType.IsAssignableFrom(group.Key) || group.Value == null)
                        continue;
                    List<ESSO> assets = group.Value;
                    for (int i = 0; i < assets.Count; i++)
                    {
                        if (assets[i] is ScriptableObject asset
                            && TryCreateCandidate(asset, enumType, out Candidate candidate)
                            && identities.Add(candidate.guid + ":" + candidate.localFileId))
                            result.Add(candidate);
                    }
                }
            }
            result.Sort((left, right) =>
            {
                int pathCompare = string.CompareOrdinal(AssetDatabase.GetAssetPath(left.asset), AssetDatabase.GetAssetPath(right.asset));
                return pathCompare != 0 ? pathCompare : left.localFileId.CompareTo(right.localFileId);
            });
            CandidateCache[enumType] = result;
            return result;
        }

        public static Candidate FindCandidate(SerializedProperty property, Type enumType)
        {
            if (property == null || enumType == null) return null;
            int enumValue = property.FindPropertyRelative("enumKey")?.intValue ?? 0;
            string stringValue = property.FindPropertyRelative("stringKey")?.stringValue ?? string.Empty;
            string definitionGuid = property.FindPropertyRelative("definitionGuid")?.stringValue ?? string.Empty;
            long definitionLocalFileId = property.FindPropertyRelative("definitionLocalFileId")?.longValue ?? 0;
            string definitionTypeName = property.FindPropertyRelative("definitionTypeName")?.stringValue ?? string.Empty;
            if (!string.IsNullOrEmpty(definitionGuid))
            {
                ScriptableObject exact = ResolveExact(definitionGuid, definitionLocalFileId);
                if (TryCreateCandidate(exact, enumType, out Candidate exactCandidate))
                    return string.IsNullOrEmpty(definitionTypeName)
                        || string.Equals(definitionTypeName, exactCandidate.assetTypeName, StringComparison.Ordinal)
                        ? exactCandidate
                        : null;
                return null;
            }
            List<Candidate> keyMatches = GetCandidates(enumType).Where(candidate =>
                (enumValue != 0 && candidate.enumKey == enumValue)
                || (enumValue == 0 && !string.IsNullOrEmpty(stringValue)
                    && string.Equals(candidate.stringKey, stringValue, StringComparison.Ordinal))).ToList();
            return keyMatches.Count == 1 ? keyMatches[0] : null;
        }

        public static Candidate FindConflict(
            Type enumType,
            ScriptableObject owner,
            int enumKey,
            string stringKey,
            bool refresh = false)
        {
            IReadOnlyList<Candidate> candidates = GetCandidates(enumType, refresh);
            for (int i = 0; i < candidates.Count; i++)
            {
                Candidate candidate = candidates[i];
                if (candidate.asset == owner)
                    continue;

                bool enumConflict = enumKey != 0 && candidate.enumKey == enumKey;
                bool stringConflict = !string.IsNullOrEmpty(stringKey)
                    && string.Equals(candidate.stringKey, stringKey, StringComparison.Ordinal);
                if (enumConflict || stringConflict)
                    return candidate;
            }
            return null;
        }

        public static bool HasEnumConflict(Type enumType, ScriptableObject owner, int enumKey)
        {
            if (enumKey == 0)
                return false;
            IReadOnlyList<Candidate> candidates = GetCandidates(enumType);
            for (int i = 0; i < candidates.Count; i++)
                if (candidates[i].asset != owner && candidates[i].enumKey == enumKey)
                    return true;
            return false;
        }

        public static string CreateAvailableStringKey(
            Type enumType,
            string requestedStringKey,
            ScriptableObject owner)
        {
            string baseKey = string.IsNullOrEmpty(requestedStringKey) ? "new_key" : requestedStringKey;
            string candidate = baseKey + "_2";
            int suffix = 3;
            while (FindConflict(enumType, owner, 0, candidate) != null)
                candidate = baseKey + "_" + suffix++;
            return candidate;
        }

        public static bool IsStale(SerializedProperty property, Candidate candidate)
        {
            if (property == null || candidate == null)
                return false;
            return !ESConfigKeyMatch.Matches(
                property.FindPropertyRelative("enumKey").intValue,
                property.FindPropertyRelative("stringKey").stringValue,
                candidate.enumKey,
                candidate.stringKey);
        }

        public static bool TryCreateCandidate(ScriptableObject asset, Type enumType, out Candidate candidate)
        {
            candidate = null;
            if (asset == null || enumType == null) return false;
            IESConfigKey key = ResolveKey(asset, enumType);
            if (key == null
                || !ESConfigKeyMatch.IsConfigured(key.EnumKeyInt, key.StringKey)
                || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localFileId))
                return false;
            candidate = new Candidate
            {
                asset = asset,
                enumKey = key.EnumKeyInt,
                stringKey = key.StringKey ?? string.Empty,
                guid = guid,
                localFileId = localFileId,
                assetTypeName = asset.GetType().AssemblyQualifiedName
            };
            return true;
        }

        public static ScriptableObject Find(SerializedProperty property, Type enumType)
        {
            if (property == null || enumType == null)
                return null;

            SerializedProperty enumKey = property.FindPropertyRelative("enumKey");
            SerializedProperty stringKey = property.FindPropertyRelative("stringKey");
            int enumValue = enumKey != null ? enumKey.intValue : 0;
            string stringValue = stringKey != null ? stringKey.stringValue : string.Empty;

            string definitionGuid = property.FindPropertyRelative("definitionGuid")?.stringValue ?? string.Empty;
            if (!string.IsNullOrEmpty(definitionGuid))
            {
                long localFileId = property.FindPropertyRelative("definitionLocalFileId")?.longValue ?? 0;
                return ResolveExact(definitionGuid, localFileId);
            }

            Candidate selected = FindCandidate(property, enumType);
            if (selected != null) return selected.asset;
            if (property.serializedObject.targetObject is ScriptableObject current && Matches(current, enumType, enumValue, stringValue)) return current;
            return null;
        }

        private static ScriptableObject ResolveExact(string guid, long localFileId)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is ScriptableObject scriptableObject
                    && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string candidateGuid, out long candidateLocalFileId)
                    && candidateGuid == guid && candidateLocalFileId == localFileId)
                    return scriptableObject;
            return null;
        }

        public static Type ResolveAssetType(Type enumType)
        {
            if (enumType == typeof(ESBuffEnumKey)) return typeof(BuffDefinitionDataInfo);
            if (enumType == typeof(ESSkillEnumKey)) return typeof(SkillDefinitionDataInfo);
            if (enumType == typeof(ESMonsterEnumKey)) return typeof(MonsterDataInfo);
            if (enumType == typeof(ESNpcEnumKey)) return typeof(NpcDataInfo);
            if (enumType == typeof(ESShotEnumKey) || enumType == typeof(ESWeaponEnumKey)) return typeof(ItemDataInfo);
            if (enumType == typeof(ESStoryEnumKey)) return typeof(ESStoryDefinitionDataInfo);
            if (enumType == typeof(ESFlowTestEnumKey)) return typeof(ESAssetGameCoreFlowTestDataInfo);
            return null;
        }

        private static bool Matches(ScriptableObject asset, Type enumType, int enumValue, string stringValue)
        {
            IESConfigKey key = ResolveKey(asset, enumType);
            if (key == null)
                return false;
            if (enumValue != 0)
                return key.EnumKeyInt == enumValue;

            return !string.IsNullOrEmpty(stringValue) && string.Equals(key.StringKey, stringValue, StringComparison.Ordinal);
        }

        private static IESConfigKey ResolveKey(ScriptableObject asset, Type enumType)
        {
            if (asset is BuffDefinitionDataInfo buff && enumType == typeof(ESBuffEnumKey)) return buff.sharedData?.key;
            if (asset is SkillDefinitionDataInfo skill && enumType == typeof(ESSkillEnumKey)) return skill.skillKey;
            if (asset is MonsterDataInfo monster && enumType == typeof(ESMonsterEnumKey)) return monster.monsterKey;
            if (asset is NpcDataInfo npc && enumType == typeof(ESNpcEnumKey)) return npc.npcKey;
            if (asset is ESStoryDefinitionDataInfo story && enumType == typeof(ESStoryEnumKey)) return story.definitionId;
            if (asset is ESAssetGameCoreFlowTestDataInfo flowTest && enumType == typeof(ESFlowTestEnumKey)) return flowTest.testKey;
            if (asset is ItemDataInfo item && item.baseConfig != null)
            {
                if (enumType == typeof(ESShotEnumKey) && item.kindData is ItemShotDataBlock shot) return shot.key;
                if (enumType == typeof(ESWeaponEnumKey) && item.kindData is ItemWeaponDataBlock weapon) return weapon.key;
            }
            return null;
        }
    }

    internal static class ESItemGameCoreEditorWorkflow
    {
        internal struct Report
        {
            public int scanned;
            public int repaired;
            public int valid;
            public int invalid;
            public int shotCount;
            public int weaponCount;
            public int injectedShotCount;
            public int injectedWeaponCount;
            public List<string> errors;

            public bool HasErrors => errors != null && errors.Count != 0;

            public override string ToString()
            {
                string result = "[ES Item GameCore] 扫描=" + scanned
                    + "，修复=" + repaired
                    + "，有效=" + valid
                    + "，无效=" + invalid
                    + "，Shot=" + shotCount
                    + "，Weapon=" + weaponCount;
                if (injectedShotCount != 0 || injectedWeaponCount != 0)
                    result += "，已注入 Shot=" + injectedShotCount + "，Weapon=" + injectedWeaponCount;
                if (HasErrors)
                    result += "，错误=" + errors.Count;
                return result;
            }
        }

        [MenuItem("【ES】/项目配置/GameCore/整理并校验 Item 配置")]
        public static void MenuRepairAndValidate()
        {
            Report report = Run(repair: true, rebuildTables: false);
            Debug.Log(report.ToString());
            LogErrors(report);
        }

        [MenuItem("【ES】/项目配置/GameCore/重建并验证 Item GameCore 表")]
        public static void MenuRebuildTables()
        {
            Report report = Run(repair: true, rebuildTables: true);
            Debug.Log(report.ToString());
            LogErrors(report);
        }

        internal static Report Run(bool repair, bool rebuildTables)
        {
            Report report = new Report { errors = new List<string>() };
            List<ItemDataInfo> items = FindItems();
            report.scanned = items.Count;
            var keyOwners = new Dictionary<string, ItemDataInfo>(StringComparer.Ordinal);

            for (int i = 0; i < items.Count; i++)
            {
                ItemDataInfo item = items[i];
                if (repair)
                {
                    Undo.RecordObject(item, "整理 Item 配置");
                    if (item.EnsureActiveKindData())
                    {
                        report.repaired++;
                        EditorUtility.SetDirty(item);
                    }
                }

                ESItemDataValidationCode validation = item.ValidateConfiguration();
                if (validation != ESItemDataValidationCode.Valid)
                {
                    report.invalid++;
                    report.errors.Add(item.name + "：" + item.GetValidationMessage(validation));
                    continue;
                }

                report.valid++;
                if (!item.IsGameCoreRoot)
                    continue;

                if (item.baseConfig.kind == ItemKind.Shot) report.shotCount++;
                if (item.baseConfig.kind == ItemKind.Weapon) report.weaponCount++;
                if (!item.TryGetGameCoreKey(out IESConfigKey key))
                    continue;

                string signature = item.baseConfig.kind + "|" + (key.EnumKeyInt != 0
                    ? "E:" + key.EnumKeyInt
                    : "S:" + key.StringKey);
                if (keyOwners.TryGetValue(signature, out ItemDataInfo owner) && owner != item)
                    report.errors.Add("GameCore Key 重复：" + signature + "，资产为 " + Describe(owner) + " 与 " + Describe(item));
                else
                    keyOwners[signature] = item;
            }

            if (rebuildTables && !report.HasErrors)
                RebuildTables(items, ref report);
            if (repair && report.repaired != 0)
                AssetDatabase.SaveAssets();
            return report;
        }

        private static List<ItemDataInfo> FindItems()
        {
            var result = new List<ItemDataInfo>();
            var visitedPaths = new HashSet<string>(StringComparer.Ordinal);
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/ESNormalAssets/Data" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !visitedPaths.Add(path))
                    continue;

                foreach (UnityEngine.Object loaded in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (loaded is ItemDataInfo item && !result.Contains(item))
                        result.Add(item);
            }
            return result;
        }

        private static string Describe(ItemDataInfo item)
        {
            string path = AssetDatabase.GetAssetPath(item);
            long localFileId = 0;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item, out _, out localFileId);
            return item.name + " [" + path + "#" + localFileId + "]";
        }

        private static void RebuildTables(List<ItemDataInfo> items, ref Report report)
        {
            if (ESRuntimeDataGameCore.Shots.IsBuilding || ESRuntimeDataGameCore.Weapons.IsBuilding)
            {
                report.errors.Add("当前 Item GameCore 表正在构建，不能嵌套重建。");
                return;
            }

            bool failed = false;
            ESRuntimeDataGameCore.Shots.BeginBuild(true);
            ESRuntimeDataGameCore.Weapons.BeginBuild(true);
            try
            {
                for (int i = 0; i < items.Count; i++)
                {
                    ItemDataInfo item = items[i];
                    if (!item.IsGameCoreRoot)
                        continue;
                    try
                    {
                        item.InjectGameCoreTables();
                    }
                    catch (Exception exception)
                    {
                        failed = true;
                        report.errors.Add(item.name + " 注入失败：" + exception.Message);
                    }
                }
            }
            finally
            {
                ESRuntimeDataGameCore.Weapons.EndBuild();
                ESRuntimeDataGameCore.Shots.EndBuild();
            }

            if (failed)
            {
                ESRuntimeDataGameCore.Shots.BeginBuild(true);
                ESRuntimeDataGameCore.Weapons.BeginBuild(true);
                ESRuntimeDataGameCore.Weapons.EndBuild();
                ESRuntimeDataGameCore.Shots.EndBuild();
                return;
            }

            report.injectedShotCount = ESRuntimeDataGameCore.Shots.Count;
            report.injectedWeaponCount = ESRuntimeDataGameCore.Weapons.Count;
        }

        private static void LogErrors(Report report)
        {
            if (!report.HasErrors)
                return;
            for (int i = 0; i < report.errors.Count; i++)
                Debug.LogError("[ES Item GameCore] " + report.errors[i]);
        }
    }

    internal sealed class ESGameCoreDefinitionEditorWindow : ESSinglePageIMGUIWindow<ESGameCoreDefinitionEditorWindow>
    {
        [SerializeField] private ScriptableObject target;
        [NonSerialized] private UnityEditor.Editor targetEditor;
        [NonSerialized] private string targetPath;
        [NonSerialized] private string targetSubtitle;
        [NonSerialized] private string itemWorkflowResult;
        [NonSerialized] private MessageType itemWorkflowMessageType;
        private Vector2 scroll;

        public static void Open(ScriptableObject target)
        {
            if (target == null)
                return;
            bool alreadyOpen = HasOpenInstances<ESGameCoreDefinitionEditorWindow>();
            ESGameCoreDefinitionEditorWindow window = GetWindow<ESGameCoreDefinitionEditorWindow>(utility: false, title: "GameCore Definition", focus: true);
            window.minSize = new Vector2(560f, 480f);
            window.maxSize = new Vector2(1200f, 1100f);
            window.SetTarget(target);
            if (!alreadyOpen)
                window.PlaceNearMainWindow();
            window.Show();
            window.Focus();
        }

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent(
                target != null ? "GameCore: " + target.name : "GameCore Definition",
                "使用原生 SerializedObject Inspector 编辑 GameCore 定义资产");
        }

        protected override string ESWindow_Subtitle => "GameCore 定义与标准配置工作流";
        protected override Vector2 ESWindow_MinSize => new Vector2(560f, 480f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(820f, 720f);
        protected override string ESWindow_PageStableId => "gamecore.definition";
        protected override string ESWindow_PageTitle => "GameCore Definition";
        protected override string ESWindow_PageKeywords => "GameCore Definition Item 配置 SerializedObject Inspector";

        protected override void ESWindow_BuildPageActions(
            ICollection<ESMenuTreePageAction> actions)
        {
            actions.Add(new ESMenuTreePageAction(
                    "gamecore.locate",
                    "定位资产",
                    "在 Project 中定位当前 GameCore 定义。",
                    context =>
                    {
                        Selection.activeObject = target;
                        EditorGUIUtility.PingObject(target);
                        context.SetStatus("已定位 GameCore 定义资产");
                    })
                .When(() => target != null)
                .WithUnityIcon("Project")
                .WithPriority(100));
            actions.Add(new ESMenuTreePageAction(
                    "gamecore.open-inspector",
                    "标准 Inspector",
                    "使用 Unity 标准 Inspector 打开当前资产。",
                    _ => AssetDatabase.OpenAsset(target))
                .When(() => target != null)
                .WithUnityIcon("UnityEditor.InspectorWindow")
                .WithPriority(90));
        }

        private void SetTarget(ScriptableObject value)
        {
            if (target == value && targetEditor != null)
                return;
            DestroyTargetEditor();
            target = value;
            itemWorkflowResult = null;
            if (target != null)
            {
                targetEditor = UnityEditor.Editor.CreateEditor(target);
                targetPath = AssetDatabase.GetAssetPath(target);
                targetSubtitle = target.GetType().Name + "  \u00b7  " + targetPath;
                titleContent = new GUIContent("GameCore: " + target.name);
            }
            ESWindow_CurrentPageContext?.RefreshPageActions();
            Repaint();
        }

        private void PlaceNearMainWindow()
        {
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            float width = Mathf.Clamp(main.width * 0.56f, 680f, 900f);
            float height = Mathf.Clamp(main.height * 0.78f, 580f, 860f);
            position = new Rect(
                main.x + (main.width - width) * 0.5f,
                main.y + (main.height - height) * 0.5f,
                width,
                height);
        }

        protected override void ESWindow_OnHostEnable()
        {
            autoRepaintOnSceneChange = false;
            if (target != null)
                SetTarget(target);
        }

        protected override void ESWindow_DrawIMGUI(ESMenuTreePageContext context)
        {
            if (target == null)
            {
                EditorGUILayout.HelpBox("GameCore \u5b9a\u4e49\u8d44\u4ea7\u5df2\u4e22\u5931\u6216\u88ab\u5220\u9664\u3002", MessageType.Warning);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField(target, target.GetType(), false);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(target.name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(targetSubtitle ?? target.GetType().Name, EditorStyles.miniLabel);
            }

            if (targetEditor == null)
                targetEditor = UnityEditor.Editor.CreateEditor(target);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(4f);
            if (target is ItemDataInfo item)
                DrawItemWorkflow(item);
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                targetEditor?.OnInspectorGUI();
            EditorGUILayout.Space(8f);
            EditorGUILayout.EndScrollView();
        }

        private void DrawItemWorkflow(ItemDataInfo item)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Item 标准配置工作流", EditorStyles.boldLabel);
                ItemKind kind = item.baseConfig != null ? item.baseConfig.kind : ItemKind.None;
                string blockName = item.kindData != null ? item.kindData.GetType().Name : "<缺少类型块>";
                EditorGUILayout.LabelField("当前类型", kind.ToString());
                EditorGUILayout.LabelField("激活配置块", blockName);
                EditorGUILayout.LabelField("GameCore 路由", item.GetGameCoreRouteName());

                ESItemDataValidationCode validation = item.ValidateConfiguration();
                if (validation == ESItemDataValidationCode.Valid)
                    EditorGUILayout.HelpBox("当前 Item 配置有效，可以按上述路由注入。", MessageType.Info);
                else
                    EditorGUILayout.HelpBox(item.GetValidationMessage(validation), MessageType.Error);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("整理当前类型块"))
                {
                    Undo.RecordObject(item, "整理当前 Item 类型配置");
                    bool changed = item.EnsureActiveKindData();
                    if (changed)
                        EditorUtility.SetDirty(item);
                    AssetDatabase.SaveAssets();
                    ESItemDataValidationCode current = item.ValidateConfiguration();
                    itemWorkflowMessageType = current == ESItemDataValidationCode.Valid ? MessageType.Info : MessageType.Error;
                    itemWorkflowResult = changed ? "已整理当前 Item 配置。" : "当前 Item 无需整理。";
                    if (current != ESItemDataValidationCode.Valid)
                        itemWorkflowResult += "\n" + item.GetValidationMessage(current);
                }
                if (GUILayout.Button("校验全项目 Item"))
                {
                    ESItemGameCoreEditorWorkflow.Report report = ESItemGameCoreEditorWorkflow.Run(repair: false, rebuildTables: false);
                    Debug.Log(report.ToString());
                    if (report.HasErrors)
                        for (int i = 0; i < report.errors.Count; i++) Debug.LogError("[ES Item GameCore] " + report.errors[i]);
                    SetWorkflowResult(report);
                }
                if (item.IsGameCoreRoot && GUILayout.Button("重建并验证表"))
                {
                    ESItemGameCoreEditorWorkflow.Report report = ESItemGameCoreEditorWorkflow.Run(repair: false, rebuildTables: true);
                    Debug.Log(report.ToString());
                    if (report.HasErrors)
                        for (int i = 0; i < report.errors.Count; i++) Debug.LogError("[ES Item GameCore] " + report.errors[i]);
                    SetWorkflowResult(report);
                }
                EditorGUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(itemWorkflowResult))
                    EditorGUILayout.HelpBox(itemWorkflowResult, itemWorkflowMessageType);
            }
        }

        private void SetWorkflowResult(ESItemGameCoreEditorWorkflow.Report report)
        {
            itemWorkflowMessageType = report.HasErrors ? MessageType.Error : MessageType.Info;
            itemWorkflowResult = report.ToString();
            if (report.HasErrors)
                itemWorkflowResult += "\n" + string.Join("\n", report.errors);
        }

        protected override void ESWindow_OnHostDisable()
        {
            DestroyTargetEditor();
        }

        private void DestroyTargetEditor()
        {
            if (targetEditor == null)
                return;
            DestroyImmediate(targetEditor);
            targetEditor = null;
        }
    }

    [CustomPropertyDrawer(typeof(ESBuffConfigKey))]
    public sealed class ESBuffConfigKeyDrawer : ESGameCoreConfigKeyDrawerBase
    {
        protected override Type ResolveEnumType() => typeof(ESBuffEnumKey);
    }

    [CustomPropertyDrawer(typeof(ESShotConfigKey))]
    public sealed class ESShotConfigKeyDrawer : ESGameCoreConfigKeyDrawerBase
    {
        protected override Type ResolveEnumType() => typeof(ESShotEnumKey);
    }

    [CustomPropertyDrawer(typeof(ESWeaponConfigKey))]
    public sealed class ESWeaponConfigKeyDrawer : ESGameCoreConfigKeyDrawerBase
    {
        protected override Type ResolveEnumType() => typeof(ESWeaponEnumKey);
    }

    [CustomPropertyDrawer(typeof(ESMonsterConfigKey))]
    public sealed class ESMonsterConfigKeyDrawer : ESGameCoreConfigKeyDrawerBase
    {
        protected override Type ResolveEnumType() => typeof(ESMonsterEnumKey);
    }

    [CustomPropertyDrawer(typeof(ESNpcConfigKey))]
    public sealed class ESNpcConfigKeyDrawer : ESGameCoreConfigKeyDrawerBase
    {
        protected override Type ResolveEnumType() => typeof(ESNpcEnumKey);
    }

    [CustomPropertyDrawer(typeof(ESSkillConfigKey))]
    public sealed class ESSkillConfigKeyDrawer : ESGameCoreConfigKeyDrawerBase
    {
        protected override Type ResolveEnumType() => typeof(ESSkillEnumKey);
    }

    [CustomPropertyDrawer(typeof(ESFlowTestConfigKey))]
    public sealed class ESFlowTestConfigKeyDrawer : ESGameCoreConfigKeyDrawerBase
    {
        protected override Type ResolveEnumType() => typeof(ESFlowTestEnumKey);
    }

    [CustomPropertyDrawer(typeof(ESStoryConfigKey))]
    public sealed class ESStoryConfigKeyDrawer : ESGameCoreConfigKeyDrawerBase
    {
        protected override Type ResolveEnumType() => typeof(ESStoryEnumKey);
    }
}
