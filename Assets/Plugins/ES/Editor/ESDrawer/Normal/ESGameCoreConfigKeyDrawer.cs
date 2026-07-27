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
            int lines = property.isExpanded ? 10 : 5;
            return lines * Line + (lines - 1) * Gap + PanelPadding * 2f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

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
            ESGameCoreDefinitionLocator.Candidate current = ESGameCoreDefinitionLocator.FindCandidate(property, enumType);

            Rect row = NextLine(ref position);
            DrawHeader(row, ResolveTitle(property, label, enumType), "GameCore ConfigKey");

            row = NextLine(ref position);
            const string enumLabel = "枚举 Key";
            Rect contentRect = EditorGUI.PrefixLabel(row, new GUIContent(enumLabel));
            DrawActionRow(contentRect, out Rect selectorRect, out Rect clearRect, out Rect locateRect);
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(selectorRect, enumKey, GUIContent.none);
            if (EditorGUI.EndChangeCheck())
                ApplyEnumSelection(property, enumType, enumKey.intValue);

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
            {
                stringKey.stringValue = editedStringKey?.Trim() ?? string.Empty;
                ClearDefinitionIdentity(property);
                Apply(property);
                TryBindOwningDefinition(property, enumType);
            }
            if (GUI.Button(stringSelectRect, StringSelectContent(stringSelectRect), EditorStyles.miniButton))
                ShowStringKeyMenu(stringSelectRect, property, enumType);

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
                    ApplyCandidate(property, selectedCandidate);
                else
                    Debug.LogWarning("[ESGameCore][ConfigKey] 选择的定义资产不包含对应类型的 ConfigKey：" + selected.name, selected);
            }

            string keySummary = current != null ? current.effectiveStringKey : (stringKey != null ? stringKey.stringValue : string.Empty);
            property.isExpanded = EditorGUI.Foldout(NextLine(ref position), property.isExpanded,
                current != null ? "已同步 · " + current.asset.GetType().Name + " · " + keySummary
                    : (string.IsNullOrEmpty(keySummary) ? "高级信息" : "仅 Key · 尚未绑定定义身份"), true, EditorStyles.foldout);

            if (property.isExpanded)
            {
                DrawReadOnly(ref position, "Enum Key", enumKey);
                DrawReadOnly(ref position, "String Key", stringKey);
                DrawReadOnly(ref position, "Definition GUID", property.FindPropertyRelative("definitionGuid"));
                DrawReadOnly(ref position, "Local File Id", property.FindPropertyRelative("definitionLocalFileId"));
                DrawReadOnly(ref position, "Definition Type", property.FindPropertyRelative("definitionTypeName"));
            }

            EditorGUI.EndProperty();
        }

        protected abstract Type ResolveEnumType();

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
            if (property != null && !string.IsNullOrWhiteSpace(property.displayName)) return property.displayName;
            string name = enumType != null ? enumType.Name : "GameCore";
            return name.Replace("EnumKey", string.Empty).Replace("ES", string.Empty) + " Key";
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

        private static void ShowStringKeyMenu(Rect position, SerializedProperty property, Type enumType)
        {
            var menu = new GenericMenu();
            IReadOnlyList<ESGameCoreDefinitionLocator.Candidate> candidates = ESGameCoreDefinitionLocator.GetCandidates(enumType, true);
            if (candidates.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("没有可选的 GameCore 定义"));
            }
            else
            {
                foreach (ESGameCoreDefinitionLocator.Candidate item in candidates)
                {
                    ESGameCoreDefinitionLocator.Candidate captured = item;
                    string path = AssetDatabase.GetAssetPath(item.asset).Replace("/", " › ");
                    string key = string.IsNullOrWhiteSpace(item.effectiveStringKey) ? "<无字符串 Key>" : item.effectiveStringKey;
                    string menuPath = key + " · " + item.asset.name + " · " + item.asset.GetType().Name + " · " + path;
                    menu.AddItem(
                        new GUIContent(menuPath, AssetPreview.GetMiniThumbnail(item.asset)),
                        false,
                        () => ApplyCandidate(property, captured));
                }
            }
            menu.DropDown(position);
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

        private static void ApplyEnumSelection(SerializedProperty property, Type enumType, int enumValue)
        {
            property.FindPropertyRelative("enumKey").intValue = enumValue;
            ClearDefinitionIdentity(property);
            SerializedProperty stringKey = property.FindPropertyRelative("stringKey");
            if (enumValue != 0 && stringKey != null && string.IsNullOrWhiteSpace(stringKey.stringValue))
            {
                UnityEngine.Object target = property.serializedObject.targetObject;
                stringKey.stringValue = target is SoDataInfo info && !string.IsNullOrWhiteSpace(info.KeyName)
                    ? info.KeyName
                    : (target != null ? target.name : string.Empty);
            }
            Apply(property);
            TryBindOwningDefinition(property, enumType);
        }

        private static void TryBindOwningDefinition(SerializedProperty property, Type enumType)
        {
            property.serializedObject.Update();
            if (property.serializedObject.targetObject is ScriptableObject definition
                && ESGameCoreDefinitionLocator.TryCreateCandidate(definition, enumType, out ESGameCoreDefinitionLocator.Candidate candidate))
                ApplyCandidate(property, candidate);
        }

        private static void ApplyCandidate(SerializedProperty property, ESGameCoreDefinitionLocator.Candidate candidate)
        {
            property.serializedObject.Update();
            property.FindPropertyRelative("enumKey").intValue = candidate.enumKey;
            property.FindPropertyRelative("stringKey").stringValue = candidate.effectiveStringKey ?? string.Empty;
            property.FindPropertyRelative("definitionGuid").stringValue = candidate.guid ?? string.Empty;
            property.FindPropertyRelative("definitionLocalFileId").longValue = candidate.localFileId;
            property.FindPropertyRelative("definitionTypeName").stringValue = candidate.assetTypeName ?? string.Empty;
            Apply(property);
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
            public string effectiveStringKey;
            public string guid;
            public long localFileId;
            public string assetTypeName;
        }

        private static readonly Dictionary<Type, List<Candidate>> CandidateCache = new Dictionary<Type, List<Candidate>>();

        static ESGameCoreDefinitionLocator()
        {
            EditorApplication.projectChanged += CandidateCache.Clear;
        }

        public static IReadOnlyList<Candidate> GetCandidates(Type enumType, bool refresh = false)
        {
            if (enumType == null) return Array.Empty<Candidate>();
            if (!refresh && CandidateCache.TryGetValue(enumType, out List<Candidate> cached)) return cached;
            var result = new List<Candidate>();
            Type assetType = ResolveAssetType(enumType);
            if (assetType != null)
                foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    foreach (UnityEngine.Object loaded in AssetDatabase.LoadAllAssetsAtPath(path))
                        if (loaded is ScriptableObject asset && assetType.IsInstanceOfType(asset)
                            && TryCreateCandidate(asset, enumType, out Candidate candidate))
                            result.Add(candidate);
                }
            result = result.GroupBy(item => item.guid + ":" + item.localFileId, StringComparer.Ordinal).Select(group => group.First()).ToList();
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
                    && string.Equals(candidate.effectiveStringKey, stringValue, StringComparison.Ordinal))).ToList();
            return keyMatches.Count == 1 ? keyMatches[0] : null;
        }

        public static bool TryCreateCandidate(ScriptableObject asset, Type enumType, out Candidate candidate)
        {
            candidate = null;
            if (asset == null || enumType == null) return false;
            IESConfigKey key = ResolveKey(asset, enumType);
            if (key == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localFileId)) return false;
            string fallback = asset is SoDataInfo info ? info.KeyName : asset.name;
            candidate = new Candidate
            {
                asset = asset,
                enumKey = key.EnumKeyInt,
                effectiveStringKey = string.IsNullOrEmpty(key.StringKey) ? fallback : key.StringKey,
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

            string fallback = asset is SoDataInfo info ? info.KeyName : asset.name;
            string effectiveString = string.IsNullOrEmpty(key.StringKey) ? fallback : key.StringKey;
            return !string.IsNullOrEmpty(stringValue) && string.Equals(effectiveString, stringValue, StringComparison.Ordinal);
        }

        private static IESConfigKey ResolveKey(ScriptableObject asset, Type enumType)
        {
            if (asset is BuffDefinitionDataInfo buff && enumType == typeof(ESBuffEnumKey)) return buff.sharedData?.key;
            if (asset is SkillDefinitionDataInfo skill && enumType == typeof(ESSkillEnumKey)) return skill.skillKey;
            if (asset is MonsterDataInfo monster && enumType == typeof(ESMonsterEnumKey)) return monster.monsterKey;
            if (asset is NpcDataInfo npc && enumType == typeof(ESNpcEnumKey)) return npc.npcKey;
            if (asset is ESAssetGameCoreFlowTestDataInfo flowTest && enumType == typeof(ESFlowTestEnumKey)) return flowTest.testKey;
            if (asset is ItemDataInfo item && item.baseConfig != null)
            {
                if (enumType == typeof(ESShotEnumKey) && item.baseConfig.kind == ItemKind.Shot) return item.shotKey;
                if (enumType == typeof(ESWeaponEnumKey) && item.baseConfig.kind == ItemKind.Weapon) return item.weaponKey;
            }
            return null;
        }
    }

    internal sealed class ESGameCoreDefinitionEditorWindow : EditorWindow
    {
        [SerializeField] private ScriptableObject target;
        [NonSerialized] private UnityEditor.Editor targetEditor;
        [NonSerialized] private string targetPath;
        [NonSerialized] private string targetSubtitle;
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

        private void SetTarget(ScriptableObject value)
        {
            if (target == value && targetEditor != null)
                return;
            DestroyTargetEditor();
            target = value;
            if (target != null)
            {
                targetEditor = UnityEditor.Editor.CreateEditor(target);
                targetPath = AssetDatabase.GetAssetPath(target);
                targetSubtitle = target.GetType().Name + "  \u00b7  " + targetPath;
                titleContent = new GUIContent("GameCore: " + target.name);
            }
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

        private void OnEnable()
        {
            autoRepaintOnSceneChange = false;
            if (target != null)
                SetTarget(target);
        }

        private void OnGUI()
        {
            if (target == null)
            {
                EditorGUILayout.HelpBox("GameCore \u5b9a\u4e49\u8d44\u4ea7\u5df2\u4e22\u5931\u6216\u88ab\u5220\u9664\u3002", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(22f));
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField(target, target.GetType(), false);
            if (GUILayout.Button("\u5b9a\u4f4d", EditorStyles.toolbarButton, GUILayout.Width(48f)))
            {
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
            }
            if (GUILayout.Button("\u6807\u51c6 Inspector", EditorStyles.toolbarButton, GUILayout.Width(92f)))
                AssetDatabase.OpenAsset(target);
            EditorGUILayout.EndHorizontal();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(target.name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(targetSubtitle ?? target.GetType().Name, EditorStyles.miniLabel);
            }

            if (targetEditor == null)
                targetEditor = UnityEditor.Editor.CreateEditor(target);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                targetEditor?.OnInspectorGUI();
            EditorGUILayout.Space(8f);
            EditorGUILayout.EndScrollView();
        }

        private void OnDisable()
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
}
