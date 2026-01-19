using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ES.VMCP
{
    /// <summary>
    /// 创建材质命令
    /// </summary>
    [ESVMCPCommand("CreateMaterial", "创建新材质")]
    public class CreateMaterialCommand : ESVMCPCommandBase
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("shader")]
        public string Shader { get; set; } = "Standard";

        [JsonConverter(typeof(ColorConverter))]
        [JsonProperty("color")]
        public Color? Color { get; set; }

        [JsonProperty("metallic")]
        public float? Metallic { get; set; }

        [JsonProperty("smoothness")]
        public float? Smoothness { get; set; }

        public override string Description => $"创建材质: {Name}";

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Name))
            {
                return ESVMCPValidationResult.Failure("材质名称不能为空");
            }
            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                // 查找Shader
                UnityEngine.Shader shader = UnityEngine.Shader.Find(Shader);
                if (shader == null)
                {
                    Debug.LogWarning($"[ESVMCP] 未找到Shader: {Shader}，使用Standard");
                    shader = UnityEngine.Shader.Find("Standard");
                }

                // 创建材质
                Material material = new Material(shader);
                material.name = Name;

                // 设置颜色
                if (Color.HasValue && material.HasProperty("_Color"))
                {
                    material.color = Color.Value;
                }

                // 设置金属度
                if (Metallic.HasValue && material.HasProperty("_Metallic"))
                {
                    material.SetFloat("_Metallic", Metallic.Value);
                }

                // 设置光滑度
                if (Smoothness.HasValue && material.HasProperty("_Glossiness"))
                {
                    material.SetFloat("_Glossiness", Smoothness.Value);
                }

                // 保存到场景记忆
                if (context.SceneMemory != null && !string.IsNullOrEmpty(Id))
                {
                    context.SceneMemory.SaveMemory(Id, material);
                }

                var output = new Dictionary<string, object>
                {
                    { "materialName", material.name },
                    { "shader", shader.name },
                    { "instanceId", material.GetInstanceID() }
                };

                return ESVMCPCommandResult.Succeed($"成功创建材质: {Name}", output);
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"创建材质失败: {e.Message}", e);
            }
        }
    }

    /// <summary>
    /// 分配材质命令
    /// </summary>
    [ESVMCPCommand("AssignMaterial", "为GameObject分配材质")]
    public class AssignMaterialCommand : ESVMCPCommandBase
    {
        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("material")]
        public string Material { get; set; }

        [JsonProperty("materialIndex")]
        public int MaterialIndex { get; set; } = 0;

        public override string Description => $"分配材质: {Target} <- {Material}";

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Target) || string.IsNullOrEmpty(Material))
            {
                return ESVMCPValidationResult.Failure("目标GameObject和材质不能为空");
            }
            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                GameObject go = ResolveGameObject(Target, context);
                if (go == null)
                {
                    return ESVMCPCommandResult.Failed($"未找到GameObject: {Target}");
                }

                // 获取Renderer
                Renderer renderer = go.GetComponent<Renderer>();
                if (renderer == null)
                {
                    return ESVMCPCommandResult.Failed($"GameObject {go.name} 没有Renderer组件");
                }

                // 解析材质引用
                Material material = ResolveMaterial(Material, context);
                if (material == null)
                {
                    return ESVMCPCommandResult.Failed($"未找到材质: {Material}");
                }

                // 分配材质
                if (MaterialIndex == 0 && renderer.materials.Length == 1)
                {
                    renderer.material = material;
                }
                else
                {
                    Material[] materials = renderer.materials;
                    if (MaterialIndex >= 0 && MaterialIndex < materials.Length)
                    {
                        materials[MaterialIndex] = material;
                        renderer.materials = materials;
                    }
                    else
                    {
                        return ESVMCPCommandResult.Failed($"材质索引 {MaterialIndex} 超出范围");
                    }
                }

                return ESVMCPCommandResult.Succeed($"成功分配材质: {material.name} -> {go.name}");
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"分配材质失败: {e.Message}", e);
            }
        }

        private Material ResolveMaterial(string materialRef, ESVMCPExecutionContext context)
        {
            // 从记忆中获取
            if (context.SceneMemory != null && context.SceneMemory.HasMemory(materialRef))
            {
                object memoryValue = context.SceneMemory.GetMemory(materialRef);
                if (memoryValue is Material mat)
                {
                    return mat;
                }
            }

            // 从Resources加载
            Material loadedMat = Resources.Load<Material>(materialRef);
            if (loadedMat != null)
            {
                return loadedMat;
            }

            return null;
        }
    }

    /// <summary>
    /// 设置材质属性命令
    /// </summary>
    [ESVMCPCommand("SetMaterialProperty", "设置材质的属性")]
    public class SetMaterialPropertyCommand : ESVMCPCommandBase
    {
        [JsonProperty("material")]
        public string Material { get; set; }

        [JsonProperty("property")]
        public string Property { get; set; }

        [JsonProperty("value")]
        public object Value { get; set; }

        [JsonProperty("propertyType")]
        public string PropertyType { get; set; } // "float", "color", "vector", "texture"

        public override string Description => $"设置材质属性: {Material}.{Property}";

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Material) || string.IsNullOrEmpty(Property))
            {
                return ESVMCPValidationResult.Failure("材质和属性名不能为空");
            }
            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                // 获取材质
                Material material = null;
                if (context.SceneMemory != null && context.SceneMemory.HasMemory(Material))
                {
                    object memoryValue = context.SceneMemory.GetMemory(Material);
                    material = memoryValue as Material;
                }

                if (material == null)
                {
                    return ESVMCPCommandResult.Failed($"未找到材质: {Material}");
                }

                // 根据类型设置属性
                switch (PropertyType?.ToLower())
                {
                    case "float":
                        if (Value is double d)
                            material.SetFloat(Property, (float)d);
                        else if (Value is float f)
                            material.SetFloat(Property, f);
                        break;

                    case "color":
                        if (Value is Newtonsoft.Json.Linq.JArray colorArray && colorArray.Count >= 3)
                        {
                            Color color = new Color(
                                colorArray[0].ToObject<float>(),
                                colorArray[1].ToObject<float>(),
                                colorArray[2].ToObject<float>(),
                                colorArray.Count >= 4 ? colorArray[3].ToObject<float>() : 1f
                            );
                            material.SetColor(Property, color);
                        }
                        break;

                    case "vector":
                        if (Value is Newtonsoft.Json.Linq.JArray vectorArray && vectorArray.Count >= 4)
                        {
                            Vector4 vector = new Vector4(
                                vectorArray[0].ToObject<float>(),
                                vectorArray[1].ToObject<float>(),
                                vectorArray[2].ToObject<float>(),
                                vectorArray[3].ToObject<float>()
                            );
                            material.SetVector(Property, vector);
                        }
                        break;

                    default:
                        return ESVMCPCommandResult.Failed($"不支持的属性类型: {PropertyType}");
                }

                return ESVMCPCommandResult.Succeed($"成功设置材质属性: {material.name}.{Property}");
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"设置材质属性失败: {e.Message}", e);
            }
        }
    }

    /// <summary>
    /// 创建基础几何体命令（带材质）
    /// </summary>
    [ESVMCPCommand("CreatePrimitive", "创建基础几何体")]
    public class CreatePrimitiveCommand : ESVMCPCommandBase
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("primitiveType")]
        public string PrimitiveType { get; set; } = "Cube"; // Cube, Sphere, Cylinder, Plane, Quad

        [JsonConverter(typeof(Vector3Converter))]
        [JsonProperty("position")]
        public Vector3 Position { get; set; } = Vector3.zero;

        [JsonConverter(typeof(Vector3Converter))]
        [JsonProperty("scale")]
        public Vector3 Scale { get; set; } = Vector3.one;

        [JsonConverter(typeof(ColorConverter))]
        [JsonProperty("color")]
        public Color? Color { get; set; }

        public override string Description => $"创建几何体: {Name} ({PrimitiveType})";

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Name))
            {
                return ESVMCPValidationResult.Failure("几何体名称不能为空");
            }
            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                // 解析几何体类型
                if (!System.Enum.TryParse<PrimitiveType>(PrimitiveType, true, out PrimitiveType primitiveType))
                {
                    return ESVMCPCommandResult.Failed($"无效的几何体类型: {PrimitiveType}");
                }

                // 创建几何体
                GameObject go = GameObject.CreatePrimitive(primitiveType);
                go.name = Name;
                go.transform.position = Position;
                go.transform.localScale = Scale;

                // 设置颜色
                if (Color.HasValue)
                {
                    Renderer renderer = go.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = Color.Value;
                    }
                }

                // 保存到记忆
                if (context.SceneMemory != null && !string.IsNullOrEmpty(Id))
                {
                    context.SceneMemory.SaveGameObjectReference(Id, go);
                }

                var output = new Dictionary<string, object>
                {
                    { "gameObjectId", go.GetInstanceID().ToString() },
                    { "name", go.name },
                    { "primitiveType", primitiveType.ToString() }
                };

                return ESVMCPCommandResult.Succeed($"成功创建几何体: {Name}", output);
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"创建几何体失败: {e.Message}", e);
            }
        }
    }
}
