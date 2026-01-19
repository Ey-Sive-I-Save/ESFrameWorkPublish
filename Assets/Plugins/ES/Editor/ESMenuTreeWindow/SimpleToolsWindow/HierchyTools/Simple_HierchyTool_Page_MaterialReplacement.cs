using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEditor.Animations;


namespace ES
{

    #region 材质批量替换工具
    [Serializable]
    public class Page_MaterialReplacement : ESWindowPageBase
    {
        [Title("材质批量替换工具", "批量替换选中对象的材质", bold: true, titleAlignment: TitleAlignments.Centered)]

        [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
        public string readMe = "选择GameObject，\n设置源材质和目标材质，\n点击替换按钮批量修改";

        [LabelText("包含子对象"), Space(5)]
        public bool includeChildren = true;

        [Flags]
        public enum ComponentType
        {
            [LabelText("无")]
            None = 0,
            [LabelText("渲染器 (Renderer)")]
            Renderer = 1 << 0,
            [LabelText("粒子系统 (ParticleSystem)")]
            ParticleSystem = 1 << 1,
            [LabelText("脚本组件 (MonoBehaviour)")]
            MonoBehaviour = 1 << 2,
            [LabelText("所有支持类型")]
            All = Renderer | ParticleSystem | MonoBehaviour
        }

        [LabelText("处理组件类型"), EnumToggleButtons, Space(5)]
        public ComponentType componentTypes = ComponentType.Renderer;

        public enum ReplacementMode
        {
            [LabelText("替换指定材质")]
            ReplaceSpecific,
            [LabelText("替换所有材质")]
            ReplaceAll,
            [LabelText("按名称匹配")]
            MatchByName
        }

        [LabelText("替换模式"), Space(5)]
        public ReplacementMode replacementMode = ReplacementMode.ReplaceSpecific;

        [LabelText("源材质"), AssetsOnly, ShowIf("replacementMode", ReplacementMode.ReplaceSpecific), Space(5)]
        public Material sourceMaterial;

        [LabelText("目标材质"), AssetsOnly, Space(5)]
        public Material targetMaterial;

        [LabelText("匹配名称"), ShowIf("replacementMode", ReplacementMode.MatchByName), Space(5)]
        public string matchName = "";

        [LabelText("为空时设置目标材质"), Space(5)]
        public bool setDefaultWhenNull = false;

        #region 材质查询功能
        [FoldoutGroup("材质查询"), Title("材质使用情况查询", bold: true)]
        [FoldoutGroup("材质查询"), Button("🔍 查询材质使用情况", ButtonHeight = 40), GUIColor(0.3f, 0.6f, 0.9f)]
        public void QueryMaterialUsage()
        {
            usedMaterials.Clear();

            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("提示", "请先选择GameObject！", "确定");
                return;
            }

            var allObjects = new List<GameObject>();
            foreach (var obj in selectedObjects)
            {
                allObjects.Add(obj);
                if (includeChildren)
                {
                    allObjects.AddRange(obj.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject));
                }
            }

            foreach (var obj in allObjects)
            {
                // 查询Renderer材质
                if ((componentTypes & ComponentType.Renderer) != 0)
                {
                    var renderer = obj.GetComponent<Renderer>();
                    if (renderer != null && renderer.sharedMaterials != null)
                    {
                        foreach (var mat in renderer.sharedMaterials)
                        {
                            if (mat != null)
                            {
                                var newUsage = new MaterialUsage
                                {
                                    targetObject = obj,
                                    material = mat,
                                    componentType = "Renderer",
                                    fieldName = "sharedMaterials"
                                };
                                if (!usedMaterials.Contains(newUsage))
                                {
                                    usedMaterials.Add(newUsage);
                                }
                            }
                        }
                    }
                }

                // 查询ParticleSystem材质
                if ((componentTypes & ComponentType.ParticleSystem) != 0)
                {
                    var particleSystem = obj.GetComponent<ParticleSystem>();
                    if (particleSystem != null)
                    {
                        var particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
                        if (particleRenderer != null && particleRenderer.sharedMaterials != null)
                        {
                            foreach (var mat in particleRenderer.sharedMaterials)
                            {
                                if (mat != null)
                                {
                                    var newUsage = new MaterialUsage
                                    {
                                        targetObject = obj,
                                        material = mat,
                                        componentType = "ParticleSystemRenderer",
                                        fieldName = "sharedMaterials"
                                    };
                                    if (!usedMaterials.Contains(newUsage))
                                    {
                                        usedMaterials.Add(newUsage);
                                    }
                                }
                            }
                        }
                    }
                }

                // 查询MonoBehaviour材质
                if ((componentTypes & ComponentType.MonoBehaviour) != 0)
                {
                    var monoBehaviours = obj.GetComponents<MonoBehaviour>();
                    foreach (var mono in monoBehaviours)
                    {
                        if (mono != null)
                        {
                            var fields = mono.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            foreach (var field in fields)
                            {
                                if (field.FieldType == typeof(Material))
                                {
                                    var mat = field.GetValue(mono) as Material;
                                    if (mat != null)
                                    {
                                        var newUsage = new MaterialUsage
                                        {
                                            targetObject = obj,
                                            material = mat,
                                            componentType = "MonoBehaviour",
                                            fieldName = $"{mono.GetType().Name}.{field.Name}"
                                        };
                                        if (!usedMaterials.Contains(newUsage))
                                        {
                                            usedMaterials.Add(newUsage);
                                        }
                                    }
                                }
                                else if (field.FieldType == typeof(Material[]))
                                {
                                    var materials = field.GetValue(mono) as Material[];
                                    if (materials != null)
                                    {
                                        for (int i = 0; i < materials.Length; i++)
                                        {
                                            if (materials[i] != null)
                                            {
                                                var newUsage = new MaterialUsage
                                                {
                                                    targetObject = obj,
                                                    material = materials[i],
                                                    componentType = "MonoBehaviour",
                                                    fieldName = $"{mono.GetType().Name}.{field.Name}[{i}]"
                                                };
                                                if (!usedMaterials.Contains(newUsage))
                                                {
                                                    usedMaterials.Add(newUsage);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            EditorUtility.DisplayDialog("查询完成", $"找到 {usedMaterials.Count} 个材质使用情况", "确定");
        }

        [FoldoutGroup("材质查询"), ShowInInspector, LabelText("材质使用列表"), ListDrawerSettings(ShowPaging = true, NumberOfItemsPerPage = 10)]
        public List<MaterialUsage> usedMaterials = new List<MaterialUsage>();

        [Serializable]
        public class MaterialUsage : IEquatable<MaterialUsage>
        {
            [ReadOnly, LabelText("目标对象")]
            public GameObject targetObject;

            [ReadOnly, LabelText("材质")]
            public Material material;

            [ReadOnly, LabelText("组件类型")]
            public string componentType;

            [ReadOnly, LabelText("字段名称")]
            public string fieldName;

            [HorizontalGroup("Actions"), Button("跳转到对象", ButtonHeight = 25), GUIColor(0.4f, 0.8f, 0.4f)]
            public void FocusObject()
            {
                if (targetObject != null)
                {
                    Selection.activeGameObject = targetObject;
                    EditorGUIUtility.PingObject(targetObject);
                }
            }

            [HorizontalGroup("Actions"), Button("跳转到材质", ButtonHeight = 25), GUIColor(0.8f, 0.4f, 0.4f)]
            public void FocusMaterial()
            {
                if (material != null)
                {
                    Selection.activeObject = material;
                    EditorGUIUtility.PingObject(material);
                }
            }

            public bool Equals(MaterialUsage other)
            {
                if (other == null) return false;
                return targetObject == other.targetObject &&
                       material == other.material &&
                       componentType == other.componentType &&
                       fieldName == other.fieldName;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as MaterialUsage);
            }

            public override int GetHashCode()
            {
                return (targetObject, material, componentType, fieldName).GetHashCode();
            }
        }
        #endregion

        [Button("执行替换", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
        public void ReplaceMaterials()
        {
            if (targetMaterial == null)
            {
                EditorUtility.DisplayDialog("错误", "请先设置目标材质！", "确定");
                return;
            }

            if (componentTypes == ComponentType.None)
            {
                EditorUtility.DisplayDialog("错误", "请至少选择一种要处理的组件类型！", "确定");
                return;
            }

            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            var allObjects = new List<GameObject>();
            foreach (var obj in selectedObjects)
            {
                allObjects.Add(obj);
                if (includeChildren)
                {
                    allObjects.AddRange(obj.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject));
                }
            }

            int replacedCount = 0;
            foreach (var obj in allObjects)
            {
                bool objectChanged = false;

                // 处理Renderer组件
                if ((componentTypes & ComponentType.Renderer) != 0)
                {
                    var renderer = obj.GetComponent<Renderer>();
                    if (renderer != null && ReplaceMaterialsInRenderer(renderer))
                    {
                        objectChanged = true;
                    }
                }

                // 处理ParticleSystem组件
                if ((componentTypes & ComponentType.ParticleSystem) != 0)
                {
                    var particleSystem = obj.GetComponent<ParticleSystem>();
                    if (particleSystem != null && ReplaceMaterialsInParticleSystem(particleSystem))
                    {
                        objectChanged = true;
                    }
                }

                // 处理MonoBehaviour组件
                if ((componentTypes & ComponentType.MonoBehaviour) != 0)
                {
                    var monoBehaviours = obj.GetComponents<MonoBehaviour>();
                    foreach (var mono in monoBehaviours)
                    {
                        if (mono != null && ReplaceMaterialsInMonoBehaviour(mono))
                        {
                            objectChanged = true;
                        }
                    }
                }

                if (objectChanged)
                {
                    replacedCount++;
                }
            }

            EditorUtility.DisplayDialog("成功", $"成功替换 {replacedCount} 个对象的材质！", "确定");
        }

        private bool ReplaceMaterialsInRenderer(Renderer renderer)
        {

            Undo.RecordObject(renderer, "Replace Material in Renderer");
            var materials = renderer.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < materials.Length; i++)
            {
                if (ShouldReplaceMaterial(materials[i]))
                {
                                Debug.Log("尝试替换渲染器材质1: " + renderer.name);
                    materials[i] = targetMaterial;
                    changed = true;
                }
            }

            if (changed)
            {
                            Debug.Log("尝试替换渲染器材质2: " + renderer.name);
                     renderer.sharedMaterials = materials;
            }

            return changed;
        }

        private bool ReplaceMaterialsInParticleSystem(ParticleSystem particleSystem)
        {
            bool changed = false;

            // 更严谨地检查ParticleSystemRenderer
            var particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer != null && particleRenderer.enabled)
            {
                // 确认这是ParticleSystem的渲染器
                if (particleRenderer is ParticleSystemRenderer particleSystemRenderer)
                {
                    changed |= ReplaceMaterialsInRenderer(particleRenderer);
                }
            }

            // 检查ShapeModule中的材质（如果需要扩展）
            var shape = particleSystem.shape;
            if (shape.enabled && shape.shapeType == ParticleSystemShapeType.Mesh && shape.mesh != null)
            {
                // Mesh本身不直接有材质，但可以在这里处理相关的材质逻辑
            }

            // 检查TextureSheetAnimation中的材质（如果需要扩展）
            var textureSheet = particleSystem.textureSheetAnimation;
            if (textureSheet.enabled && textureSheet.mode == ParticleSystemAnimationMode.Sprites)
            {
                // Sprites材质处理可以在这里扩展
            }

            return changed;
        }

        private bool ReplaceMaterialsInMonoBehaviour(MonoBehaviour mono)
        {
            Undo.RecordObject(mono, "Replace Material in MonoBehaviour");
            bool changed = false;

            var fields = mono.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(Material))
                {
                    var currentMaterial = field.GetValue(mono) as Material;
                    if (ShouldReplaceMaterial(currentMaterial))
                    {
                        field.SetValue(mono, targetMaterial);
                        changed = true;
                    }
                }
                else if (field.FieldType == typeof(Material[]))
                {
                    var materials = field.GetValue(mono) as Material[];
                    if (materials != null)
                    {
                        bool arrayChanged = false;
                        for (int i = 0; i < materials.Length; i++)
                        {
                            if (ShouldReplaceMaterial(materials[i]))
                            {
                                materials[i] = targetMaterial;
                                arrayChanged = true;
                            }
                        }
                        if (arrayChanged)
                        {
                            field.SetValue(mono, materials);
                            changed = true;
                        }
                    }
                }
            }

            return changed;
        }

        private bool ShouldReplaceMaterial(Material material)
        {
            if (material == null)
            {
                return setDefaultWhenNull;
            }

            switch (replacementMode)
            {
                case ReplacementMode.ReplaceSpecific:
                    return material == sourceMaterial;
                case ReplacementMode.ReplaceAll:
                    return true;
                case ReplacementMode.MatchByName:
                    return material.name.Contains(matchName);
                default:
                    return false;
            }
        }
    }
    #endregion

}