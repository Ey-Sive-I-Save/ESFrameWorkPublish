using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ES.EditorInternal
{
    [CreateAssetMenu(fileName = "ESCompositeShaderPreset", menuName = "ES/Shader/Composite Preset")]
    public sealed class ESCompositeShaderPreset : ScriptableObject
    {
        private enum ValueKind
        {
            Float,
            Vector,
            Color,
            Texture
        }

        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private string propertyName;
            [SerializeField] private ValueKind valueKind;
            [SerializeField] private float floatValue;
            [SerializeField] private Vector4 vectorValue;
            [SerializeField] private Color colorValue = Color.white;
            [SerializeField] private Texture textureValue;
            [SerializeField] private Vector2 textureScale = Vector2.one;
            [SerializeField] private Vector2 textureOffset;

            internal string PropertyName => propertyName;

            internal static Entry Capture(Material material, int propertyIndex)
            {
                string name = material.shader.GetPropertyName(propertyIndex);
                ShaderPropertyType type = material.shader.GetPropertyType(propertyIndex);
                var entry = new Entry { propertyName = name };
                switch (type)
                {
                    case ShaderPropertyType.Color:
                        entry.valueKind = ValueKind.Color;
                        entry.colorValue = material.GetColor(name);
                        break;
                    case ShaderPropertyType.Vector:
                        entry.valueKind = ValueKind.Vector;
                        entry.vectorValue = material.GetVector(name);
                        break;
                    case ShaderPropertyType.Texture:
                        entry.valueKind = ValueKind.Texture;
                        entry.textureValue = material.GetTexture(name);
                        entry.textureScale = material.GetTextureScale(name);
                        entry.textureOffset = material.GetTextureOffset(name);
                        break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        entry.valueKind = ValueKind.Float;
                        entry.floatValue = material.GetFloat(name);
                        break;
                    default:
                        return null;
                }
                return entry;
            }

            internal bool Apply(Material material)
            {
                if (material == null || !material.HasProperty(propertyName))
                    return false;

                switch (valueKind)
                {
                    case ValueKind.Color:
                        material.SetColor(propertyName, colorValue);
                        break;
                    case ValueKind.Vector:
                        material.SetVector(propertyName, vectorValue);
                        break;
                    case ValueKind.Texture:
                        material.SetTexture(propertyName, textureValue);
                        material.SetTextureScale(propertyName, textureScale);
                        material.SetTextureOffset(propertyName, textureOffset);
                        break;
                    default:
                        material.SetFloat(propertyName, floatValue);
                        break;
                }
                return true;
            }
        }

        [SerializeField] private Shader shader;
        [SerializeField] private int materialSchemaVersion;
        [SerializeField] private string description;
        [SerializeField] private bool includeRenderQueue;
        [SerializeField] private int renderQueue = -1;
        [SerializeField] private List<Entry> entries = new List<Entry>();

        public Shader Shader => shader;
        public string ShaderName => shader == null ? string.Empty : shader.name;
        public int MaterialSchemaVersion => materialSchemaVersion;
        public string Description => description;
        public int EntryCount => entries == null ? 0 : entries.Count;

        public bool IsCompatible(Material material)
        {
            return material != null
                && material.shader == shader
                && materialSchemaVersion <= ESCompositeMaterialMigration.CurrentVersion;
        }

        public int CaptureFrom(Material material)
        {
            if (material == null || material.shader == null || !ESCompositeMaterialInstance.IsCompositeMaterial(material))
                return 0;

            shader = material.shader;
            materialSchemaVersion = ESCompositeMaterialMigration.GetStoredVersion(material);
            renderQueue = material.renderQueue;
            if (entries == null) entries = new List<Entry>();
            else entries.Clear();
            int propertyCount = shader.GetPropertyCount();
            for (int i = 0; i < propertyCount; i++)
            {
                ShaderPropertyFlags flags = shader.GetPropertyFlags(i);
                if ((flags & (ShaderPropertyFlags.HideInInspector | ShaderPropertyFlags.PerRendererData)) != 0)
                    continue;

                Entry entry = Entry.Capture(material, i);
                if (entry != null) entries.Add(entry);
            }
            return entries.Count;
        }

        public int ApplyTo(Material material)
        {
            if (!IsCompatible(material) || entries == null)
                return 0;

            int applied = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry != null && entry.Apply(material)) applied++;
            }
            if (includeRenderQueue) material.renderQueue = renderQueue;
            ESCompositeShaderGUI.SyncMaterialKeywords(material);
            EditorUtility.SetDirty(material);
            return applied;
        }

        public bool ContainsProperty(string propertyName)
        {
            if (entries == null || string.IsNullOrEmpty(propertyName)) return false;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && string.Equals(entries[i].PropertyName, propertyName, StringComparison.Ordinal))
                    return true;
            return false;
        }
    }

    [CustomEditor(typeof(ESCompositeShaderPreset))]
    public sealed class ESCompositeShaderPresetEditor : UnityEditor.Editor
    {
        private Material sourceMaterial;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("shader"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("materialSchemaVersion"), new GUIContent("材质 Schema"));
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("description"), new GUIContent("说明"));
            SerializedProperty includeQueue = serializedObject.FindProperty("includeRenderQueue");
            EditorGUILayout.PropertyField(includeQueue, new GUIContent("包含渲染队列"));
            if (includeQueue.boolValue)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("renderQueue"), new GUIContent("渲染队列"));
            EditorGUILayout.LabelField("捕获属性", ((ESCompositeShaderPreset)target).EntryCount.ToString());
            serializedObject.ApplyModifiedProperties();

            var preset = (ESCompositeShaderPreset)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("材质工作流", EditorStyles.boldLabel);
            sourceMaterial = (Material)EditorGUILayout.ObjectField("捕获来源", sourceMaterial, typeof(Material), false);
            using (new EditorGUI.DisabledScope(sourceMaterial == null))
            {
                if (GUILayout.Button("从材质重新捕获"))
                {
                    Undo.RecordObject(preset, "捕获 ES Composite Shader 预设");
                    int count = preset.CaptureFrom(sourceMaterial);
                    EditorUtility.SetDirty(preset);
                    ShowNotification("已捕获 " + count + " 个属性。");
                }
            }

            int compatibleCount = 0;
            for (int i = 0; i < Selection.objects.Length; i++)
                if (Selection.objects[i] is Material material && preset.IsCompatible(material)) compatibleCount++;
            using (new EditorGUI.DisabledScope(compatibleCount == 0))
            {
                if (GUILayout.Button("应用到选中的兼容材质（" + compatibleCount + "）"))
                {
                    var compatible = new List<Material>();
                    for (int i = 0; i < Selection.objects.Length; i++)
                        if (Selection.objects[i] is Material material && preset.IsCompatible(material)) compatible.Add(material);
                    Undo.RecordObjects(compatible.ToArray(), "应用 ES Composite Shader 预设");
                    for (int i = 0; i < compatible.Count; i++) preset.ApplyTo(compatible[i]);
                }
            }
        }

        private static void ShowNotification(string message)
        {
            if (EditorWindow.focusedWindow != null)
                EditorWindow.focusedWindow.ShowNotification(new GUIContent(message));
        }
    }
}
