using System;
using ES;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    public abstract class ESGameCoreConfigKeyDrawerBase : PropertyDrawer
    {
        private const float Line = 18f;
        private const float Gap = 4f;
        private const float Padding = 5f;

        private static readonly GUIContent EnumKeyLabel = new GUIContent("Enum Key");
        private static readonly GUIContent StringKeyLabel = new GUIContent("String Key");
        private static readonly GUIContent EnumPrimaryStatus = new GUIContent("Enum \u4e3b\u952e \u00b7 String \u5907\u7528");
        private static readonly GUIContent EnumOnlyStatus = new GUIContent("Enum \u4e3b\u952e");
        private static readonly GUIContent StringOnlyStatus = new GUIContent("String \u4e3b\u952e");
        private static readonly GUIContent EmptyStatus = new GUIContent("\u672a\u914d\u7f6e");
        private static GUIStyle statusStyle;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return Padding * 2f + Line * 6f + Gap * 5f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty enumKey = property.FindPropertyRelative("enumKey");
            SerializedProperty stringKey = property.FindPropertyRelative("stringKey");
            Type enumType = ResolveEnumType();
            bool hasEnum = enumKey != null && enumKey.intValue != 0;
            bool hasString = stringKey != null && !string.IsNullOrWhiteSpace(stringKey.stringValue);

            Color oldBackground = GUI.backgroundColor;
            GUI.backgroundColor = hasEnum || hasString
                ? new Color(0.34f, 0.68f, 0.48f, 1f)
                : new Color(0.82f, 0.48f, 0.28f, 1f);
            GUI.Box(position, GUIContent.none, EditorStyles.helpBox);
            GUI.backgroundColor = oldBackground;

            position.x += Padding;
            position.y += Padding;
            position.width -= Padding * 2f;
            position.height -= Padding * 2f;

            Rect row = NextLine(ref position);
            DrawSplit(row, 0.58f, out Rect left, out Rect right);
            EditorGUI.LabelField(left, label, EditorStyles.boldLabel);
            EditorGUI.LabelField(right, ResolveStatus(hasEnum, hasString), StatusStyle);

            row = NextLine(ref position);
            DrawSplit(row, 0.44f, out left, out right);
            EditorGUI.PropertyField(left, enumKey, EnumKeyLabel);
            EditorGUI.PropertyField(right, stringKey, StringKeyLabel);

            row = NextLine(ref position);
            DrawSplit(row, 0.5f, out left, out right);
            if (GUI.Button(left, "\u4ece\u5f53\u524d\u5b9a\u4e49\u586b\u5145 String", EditorStyles.miniButtonLeft))
            {
                stringKey.stringValue = ResolveSuggestedStringKey(property);
                property.serializedObject.ApplyModifiedProperties();
                MarkDirty(property);
            }
            if (GUI.Button(right, "\u590d\u5236 ConfigKey", EditorStyles.miniButtonRight))
                CopyKey(enumKey, stringKey, enumType);

            row = NextLine(ref position);
            DrawSplit(row, 0.5f, out left, out right);
            using (new EditorGUI.DisabledScope(enumType == null))
            {
                if (GUI.Button(left, "\u5b9a\u4f4d\u5f53\u524d\u679a\u4e3e", EditorStyles.miniButtonLeft))
                    OpenCurrentEnumMember(enumType, enumKey);
                if (GUI.Button(right, "\u679a\u4e3e\u6269\u5bb9\u4f4d\u7f6e", EditorStyles.miniButtonRight))
                    ESEnumScriptJump.OpenEnumAppendPosition(enumType);
            }

            row = NextLine(ref position);
            DrawSplit(row, 0.5f, out left, out right);
            using (new EditorGUI.DisabledScope(enumType == null))
            {
                if (GUI.Button(left, "\u590d\u5236\u811a\u672c\u5b9a\u4f4d", EditorStyles.miniButtonLeft))
                    CopyEnumScriptLocation(enumType, enumKey);
                if (GUI.Button(right, "\u590d\u5236\u679a\u4e3e\u6269\u5bb9\u8bf7\u6c42", EditorStyles.miniButtonRight))
                    CopyAiEnumRequest(enumType, enumKey, stringKey, ResolveSuggestedStringKey(property));
            }

            row = NextLine(ref position);
            DrawSplit(row, 0.5f, out left, out right);
            if (GUI.Button(left, "\u5b9a\u4f4d\u5b9a\u4e49\u8d44\u4ea7", EditorStyles.miniButtonLeft))
                LocateDefinitionAsset(property, enumType, openEditor: false);
            if (GUI.Button(right, "\u6253\u5f00\u5b9a\u4e49\u7f16\u8f91\u5668", EditorStyles.miniButtonRight))
                LocateDefinitionAsset(property, enumType, openEditor: true);

            EditorGUI.EndProperty();
        }

        protected abstract Type ResolveEnumType();

        private static GUIStyle StatusStyle
        {
            get
            {
                if (statusStyle == null)
                    statusStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleRight };
                return statusStyle;
            }
        }

        private static GUIContent ResolveStatus(bool hasEnum, bool hasString)
        {
            if (hasEnum) return hasString ? EnumPrimaryStatus : EnumOnlyStatus;
            return hasString ? StringOnlyStatus : EmptyStatus;
        }

        private static Rect NextLine(ref Rect position)
        {
            Rect rect = new Rect(position.x, position.y, position.width, Line);
            position.y += Line + Gap;
            return rect;
        }

        private static void DrawSplit(Rect rect, float leftRatio, out Rect left, out Rect right)
        {
            float width = Mathf.Floor((rect.width - Gap) * leftRatio);
            left = new Rect(rect.x, rect.y, width, rect.height);
            right = new Rect(left.xMax + Gap, rect.y, rect.width - width - Gap, rect.height);
        }

        private static string ResolveSuggestedStringKey(SerializedProperty property)
        {
            UnityEngine.Object target = property.serializedObject.targetObject;
            if (target is SoDataInfo info && !string.IsNullOrEmpty(info.KeyName))
                return info.KeyName;
            return target != null ? target.name : string.Empty;
        }

        private static void CopyKey(SerializedProperty enumKey, SerializedProperty stringKey, Type enumType)
        {
            string enumName = enumType != null && enumKey != null
                ? enumType.Name + "." + ResolveEnumMemberName(enumType, enumKey)
                : "UnknownEnum";
            EditorGUIUtility.systemCopyBuffer =
                "enumKey: " + enumName + Environment.NewLine +
                "stringKey: " + (stringKey != null ? stringKey.stringValue : string.Empty);
        }

        private static void OpenCurrentEnumMember(Type enumType, SerializedProperty enumKey)
        {
            string memberName = ResolveEnumMemberName(enumType, enumKey);
            if (string.IsNullOrEmpty(memberName))
                ESEnumScriptJump.OpenEnum(enumType);
            else
                ESEnumScriptJump.OpenEnumMember(enumType, memberName);
        }

        private static void CopyEnumScriptLocation(Type enumType, SerializedProperty enumKey)
        {
            string memberName = ResolveEnumMemberName(enumType, enumKey);
            ESEnumScriptJumpResult result;
            bool found = !string.IsNullOrEmpty(memberName)
                ? ESEnumScriptJump.TryFindEnumMember(enumType, memberName, out result)
                : ESEnumScriptJump.TryFindEnum(enumType, out result);
            if (!found)
            {
                Debug.LogWarning("[ESGameCore][Enum] \u672a\u627e\u5230\u679a\u4e3e\u811a\u672c\u5b9a\u4f4d\uff1a" + (enumType != null ? enumType.Name : "<null>"));
                return;
            }

            int line = result.HasMemberLine ? result.memberLine : result.enumLine;
            EditorGUIUtility.systemCopyBuffer = result.assetPath + ":" + line;
        }

        private static string ResolveEnumMemberName(Type enumType, SerializedProperty enumKey)
        {
            if (enumType == null || enumKey == null)
                return null;
            return Enum.GetName(enumType, enumKey.intValue);
        }

        private static void CopyAiEnumRequest(Type enumType, SerializedProperty enumKey, SerializedProperty stringKey, string fallbackStringKey)
        {
            string desiredStringKey = stringKey != null && !string.IsNullOrEmpty(stringKey.stringValue)
                ? stringKey.stringValue
                : fallbackStringKey;
            string current = ResolveEnumMemberName(enumType, enumKey) ?? "Unknown";
            ESEnumScriptJump.CopyAppendRequest(enumType, desiredStringKey, current);
        }

        private static void MarkDirty(SerializedProperty property)
        {
            UnityEngine.Object target = property.serializedObject.targetObject;
            if (target != null)
                EditorUtility.SetDirty(target);
        }

        private static void LocateDefinitionAsset(SerializedProperty property, Type enumType, bool openEditor)
        {
            ScriptableObject target = ESGameCoreDefinitionLocator.Find(property, enumType);
            if (target == null)
            {
                Debug.LogWarning("[ESGameCore][Locate] \u672a\u627e\u5230 ConfigKey \u5bf9\u5e94\u7684\u6839\u5b9a\u4e49\u8d44\u4ea7\u3002");
                return;
            }

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
            if (openEditor)
                ESGameCoreDefinitionEditorWindow.Open(target);
        }
    }

    internal static class ESGameCoreDefinitionLocator
    {
        public static ScriptableObject Find(SerializedProperty property, Type enumType)
        {
            if (property == null || enumType == null)
                return null;

            SerializedProperty enumKey = property.FindPropertyRelative("enumKey");
            SerializedProperty stringKey = property.FindPropertyRelative("stringKey");
            int enumValue = enumKey != null ? enumKey.intValue : 0;
            string stringValue = stringKey != null ? stringKey.stringValue : string.Empty;

            if (property.serializedObject.targetObject is ScriptableObject current
                && Matches(current, enumType, enumValue, stringValue))
                return current;

            Type assetType = ResolveAssetType(enumType);
            if (assetType == null || (enumValue == 0 && string.IsNullOrEmpty(stringValue)))
                return null;

            string[] guids = AssetDatabase.FindAssets("t:" + assetType.Name);
            ScriptableObject selected = null;
            string selectedPath = null;
            int matchCount = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ScriptableObject candidate = AssetDatabase.LoadAssetAtPath(path, assetType) as ScriptableObject;
                if (candidate == null || !Matches(candidate, enumType, enumValue, stringValue))
                    continue;

                matchCount++;
                if (selected == null || string.CompareOrdinal(path, selectedPath) < 0)
                {
                    selected = candidate;
                    selectedPath = path;
                }
            }

            if (matchCount > 1)
                Debug.LogWarning("[ESGameCore][Locate] ConfigKey \u5339\u914d\u5230 " + matchCount + " \u4e2a\u6839\u5b9a\u4e49\uff0c\u5df2\u5b9a\u4f4d\u5230\u8def\u5f84\u6392\u5e8f\u6700\u524d\u7684\u9879\u3002", selected);
            return selected;
        }

        private static Type ResolveAssetType(Type enumType)
        {
            if (enumType == typeof(ESBuffEnumKey)) return typeof(BuffDefinitionDataInfo);
            if (enumType == typeof(ESSkillEnumKey)) return typeof(SkillDefinitionDataInfo);
            if (enumType == typeof(ESMonsterEnumKey)) return typeof(MonsterDataInfo);
            if (enumType == typeof(ESNpcEnumKey)) return typeof(NpcDataInfo);
            if (enumType == typeof(ESShotEnumKey) || enumType == typeof(ESWeaponEnumKey)) return typeof(ItemDataInfo);
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
}
