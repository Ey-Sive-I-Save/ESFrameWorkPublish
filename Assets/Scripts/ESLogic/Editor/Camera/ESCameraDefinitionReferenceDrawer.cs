using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>Camera Definition 的唯一 Inspector 作者入口。Catalog 扫描仅在点击下拉时执行。</summary>
    [CustomPropertyDrawer(typeof(ESCameraDefinitionReference))]
    public sealed class ESCameraDefinitionReferenceDrawer : PropertyDrawer
    {
        private static readonly List<Entry> cachedEntries = new List<Entry>();
        private static readonly Dictionary<ESCameraDefinitionReference, Entry> cachedByReference = new Dictionary<ESCameraDefinitionReference, Entry>();
        private static readonly Dictionary<string, ESCameraDefinitionReference> cachedByString = new Dictionary<string, ESCameraDefinitionReference>(StringComparer.Ordinal);
        private static string cacheError;

        static ESCameraDefinitionReferenceDrawer()
        {
            // Catalog assets can be reimported or removed while an Inspector is
            // still visible. Drop the derived display cache so OnGUI cannot show
            // a definition that no longer exists until the next picker opening.
            EditorApplication.projectChanged -= ClearCache;
            EditorApplication.projectChanged += ClearCache;
        }

        private static void ClearCache()
        {
            cachedEntries.Clear();
            cachedByReference.Clear();
            cachedByString.Clear();
            cacheError = null;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            ESCameraDefinitionReference reference = Read(property);
            bool mixedValue = property.hasMultipleDifferentValues;
            bool known = cachedByReference.TryGetValue(reference, out Entry entry);
            string text = mixedValue
                ? "多个不同值"
                : !reference.IsConfigured
                ? "选择 Camera Definition"
                : known ? entry.displayName : "缺失或未刷新 · " + reference;
            Color color = GUI.color;
            if (!mixedValue && reference.IsConfigured && !known && cachedEntries.Count > 0)
                GUI.color = new Color(1f, 0.55f, 0.55f);

            Rect content = EditorGUI.PrefixLabel(position, label);
            bool previousMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = mixedValue;
            try
            {
                if (EditorGUI.DropdownButton(content, new GUIContent(text, cacheError), FocusType.Keyboard))
                    OpenPicker(content, property, mixedValue ? default : reference);
            }
            finally
            {
                EditorGUI.showMixedValue = previousMixedValue;
            }

            GUI.color = color;
            EditorGUI.EndProperty();
        }

        private static void OpenPicker(Rect anchor, SerializedProperty property, ESCameraDefinitionReference selected)
        {
            RefreshCache();
            SerializedObject serializedObject = property.serializedObject;
            string propertyPath = property.propertyPath;
            var entries = new List<ESSearchDropdown.Entry>(cachedEntries.Count + 1)
            {
                ESSearchDropdown.Entry.Item("清空", () => Write(serializedObject, propertyPath, default), "操作")
            };

            for (int i = 0; i < cachedEntries.Count; i++)
            {
                Entry item = cachedEntries[i];
                ESCameraDefinitionReference captured = item.reference;
                entries.Add(ESSearchDropdown.Entry.Item(
                    item.displayName,
                    () => Write(serializedObject, propertyPath, captured),
                    "Camera/ViewDefinition",
                    subtitle: captured.ToString(),
                    selected: captured == selected));
            }

            if (cachedEntries.Count == 0)
                entries.Add(ESSearchDropdown.Entry.Disabled("没有唯一有效的 Camera Definition", "状态", cacheError));

            ESSearchDropdown.Open(anchor, "选择 Camera Definition", entries, minimumWindowSize: new Vector2(500f, 360f));
        }

        private static void RefreshCache()
        {
            ClearCache();
            string[] guids = AssetDatabase.FindAssets("t:ESCameraViewDefinitionCatalog", new[] { "Assets" });
            if (guids.Length != 1)
            {
                cacheError = "Picker 要求唯一有效的 ESCameraViewDefinitionCatalog，实际数量=" + guids.Length;
                return;
            }

            ESCameraViewDefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<ESCameraViewDefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
            var definitions = new List<ESCameraViewDefinition>();
            string catalogError = null;
            if (catalog == null || !catalog.TryCopyDefinitionsForAuthoring(definitions, out catalogError))
            {
                cacheError = catalogError ?? "无法读取 ESCameraViewDefinitionCatalog。";
                return;
            }

            var cachedByEnum = new Dictionary<ESCameraDefinitionEnumKey, ESCameraDefinitionReference>();
            for (int i = 0; i < definitions.Count; i++)
            {
                ESCameraViewDefinition definition = definitions[i];
                if (definition == null || !definition.IsValid || !definition.Definition.IsConfigured)
                    continue;

                if (cachedByReference.ContainsKey(definition.Definition)
                    || (definition.Definition.stringKey != null
                        && cachedByString.TryGetValue(definition.Definition.stringKey, out ESCameraDefinitionReference existing)
                        && existing != definition.Definition)
                    || (definition.Definition.enumKey != ESCameraDefinitionEnumKey.None
                        && cachedByEnum.TryGetValue(definition.Definition.enumKey, out existing)
                        && existing != definition.Definition))
                {
                    cacheError = "存在重复 Camera Definition：" + definition.Definition;
                    cachedEntries.Clear();
                    cachedByReference.Clear();
                    cachedByString.Clear();
                    return;
                }

                var entry = new Entry { reference = definition.Definition, displayName = definition.name };
                cachedEntries.Add(entry);
                cachedByReference.Add(entry.reference, entry);
                if (!string.IsNullOrEmpty(entry.reference.stringKey))
                    cachedByString.Add(entry.reference.stringKey, entry.reference);
                if (entry.reference.enumKey != ESCameraDefinitionEnumKey.None)
                    cachedByEnum.Add(entry.reference.enumKey, entry.reference);
            }

            cachedEntries.Sort((left, right) => string.CompareOrdinal(left.displayName, right.displayName));
            if (cachedEntries.Count == 0 && string.IsNullOrEmpty(cacheError))
                cacheError = "请先创建有效的 ESCameraViewDefinitionCatalog 与 Definition 资产。";
        }

        internal static ESCameraDefinitionReference Read(SerializedProperty property)
        {
            SerializedProperty enumKey = property.FindPropertyRelative(nameof(ESCameraDefinitionReference.enumKey));
            SerializedProperty stringKey = property.FindPropertyRelative(nameof(ESCameraDefinitionReference.stringKey));
            return new ESCameraDefinitionReference(
                enumKey != null ? (ESCameraDefinitionEnumKey)enumKey.intValue : ESCameraDefinitionEnumKey.None,
                stringKey?.stringValue);
        }

        internal static void Write(SerializedObject serializedObject, string propertyPath, ESCameraDefinitionReference reference)
        {
            if (serializedObject == null || string.IsNullOrEmpty(propertyPath))
                return;

            try
            {
                UnityEngine.Object[] targets = serializedObject.targetObjects;
                if (targets == null || targets.Length == 0)
                    return;
                for (int i = 0; i < targets.Length; i++)
                    if (targets[i] == null)
                        return;

                serializedObject.UpdateIfRequiredOrScript();
                SerializedProperty property = serializedObject.FindProperty(propertyPath);
                SerializedProperty enumKey = property?.FindPropertyRelative(nameof(ESCameraDefinitionReference.enumKey));
                SerializedProperty stringKey = property?.FindPropertyRelative(nameof(ESCameraDefinitionReference.stringKey));
                if (enumKey == null || stringKey == null)
                    return;

                Undo.RecordObjects(targets, "写入相机定义引用");
                enumKey.intValue = (int)reference.enumKey;
                stringKey.stringValue = reference.stringKey ?? string.Empty;
                serializedObject.ApplyModifiedProperties();
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    "[ESCameraDefinitionReferenceDrawer] 写回引用失败，目标可能已失效。", exception));
            }
        }

        private struct Entry
        {
            public ESCameraDefinitionReference reference;
            public string displayName;
        }
    }
}
