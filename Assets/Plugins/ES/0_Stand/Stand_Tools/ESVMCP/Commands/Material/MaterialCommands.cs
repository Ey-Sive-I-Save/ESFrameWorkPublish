using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace ES.VMCP
{
    /// <summary>
    /// 通用Material操作类型（基础操作）
    /// 注意：高级或特化操作应创建独立的Command类，不应继续扩展此枚举
    /// </summary>
    public enum CommonMaterialOperation
    {
        SetColor,           // 设置颜色
        SetFloat,           // 设置Float属性
        SetTexture,         // 设置纹理
        SetShader,          // 设置Shader
        GetColor,           // 获取颜色
        GetFloat,           // 获取Float属性
        EnableKeyword,      // 启用Keyword
        DisableKeyword,     // 禁用Keyword
        CreateMaterial,     // 创建Material
        ApplyToRenderer     // 应用到Renderer
    }

    /// <summary>
    /// 统一的Material操作命令
    /// </summary>
    [ESVMCPCommand("CommonMaterialOperation", "统一的Material操作命令，支持颜色、纹理、Shader、属性等")]
    public class MaterialOperationCommand : ESVMCPCommandBase
    {
        [JsonProperty("operation")]
        public CommonMaterialOperation Operation { get; set; }

        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("propertyName")]
        public string PropertyName { get; set; }

        [JsonConverter(typeof(ColorConverter))]
        [JsonProperty("color")]
        public Color? Color { get; set; }

        [JsonProperty("floatValue")]
        public float? FloatValue { get; set; }

        [JsonProperty("texturePath")]
        public string TexturePath { get; set; }

        [JsonProperty("shaderName")]
        public string ShaderName { get; set; }

        [JsonProperty("assetName")]
        public string AssetName { get; set; }

        [JsonProperty("keyword")]
        public string Keyword { get; set; }

        [JsonProperty("materialIndex")]
        public int MaterialIndex { get; set; } = 0;

        /// <summary>
        /// Material操作命令的持久化设置
        /// CreateMaterial操作创建资产，应保存到持久记忆
        /// </summary>
        [JsonProperty("persistent")]
        public new bool Persistent { get; set; }

        public override string Description
        {
            get
            {
                switch (Operation)
                {
                    case CommonMaterialOperation.SetColor:
                        return $"设置材质颜色: {Target}[{MaterialIndex}].{PropertyName}";
                    case CommonMaterialOperation.SetFloat:
                        return $"设置材质Float: {Target}[{MaterialIndex}].{PropertyName} = {FloatValue}";
                    case CommonMaterialOperation.SetTexture:
                        return $"设置材质纹理: {Target}[{MaterialIndex}].{PropertyName}";
                    case CommonMaterialOperation.SetShader:
                        return $"设置Shader: {Target}[{MaterialIndex}] -> {ShaderName}";
                    case CommonMaterialOperation.GetColor:
                        return $"获取材质颜色: {Target}[{MaterialIndex}].{PropertyName}";
                    case CommonMaterialOperation.GetFloat:
                        return $"获取材质Float: {Target}[{MaterialIndex}].{PropertyName}";
                    case CommonMaterialOperation.EnableKeyword:
                        return $"启用Keyword: {Target}[{MaterialIndex}] -> {Keyword}";
                    case CommonMaterialOperation.DisableKeyword:
                        return $"禁用Keyword: {Target}[{MaterialIndex}] -> {Keyword}";
                    case CommonMaterialOperation.CreateMaterial:
                        return $"创建Material: {AssetName ?? "New Material"} (Shader: {ShaderName ?? "URP Lit"})";
                    case CommonMaterialOperation.ApplyToRenderer:
                        return $"应用Material: {PropertyName} -> {Target}[{MaterialIndex}]";
                    default:
                        return $"Material操作: {Operation}";
                }
            }
        }

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Target) && Operation != CommonMaterialOperation.CreateMaterial)
            {
                return ESVMCPValidationResult.Failure("目标不能为空");
            }

            switch (Operation)
            {
                case CommonMaterialOperation.SetColor:
                case CommonMaterialOperation.GetColor:
                case CommonMaterialOperation.SetFloat:
                case CommonMaterialOperation.GetFloat:
                case CommonMaterialOperation.SetTexture:
                    if (string.IsNullOrEmpty(PropertyName))
                        return ESVMCPValidationResult.Failure($"{Operation}操作需要指定propertyName参数");
                    break;
                case CommonMaterialOperation.SetShader:
                    if (string.IsNullOrEmpty(ShaderName))
                        return ESVMCPValidationResult.Failure("设置Shader操作需要指定shaderName参数");
                    break;
                case CommonMaterialOperation.EnableKeyword:
                case CommonMaterialOperation.DisableKeyword:
                    if (string.IsNullOrEmpty(Keyword))
                        return ESVMCPValidationResult.Failure("Keyword操作需要指定keyword参数");
                    break;
                case CommonMaterialOperation.ApplyToRenderer:
                    if (string.IsNullOrEmpty(PropertyName))
                        return ESVMCPValidationResult.Failure("ApplyToRenderer操作需要指定propertyName参数作为Material目标");
                    break;
            }

            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                switch (Operation)
                {
                    case CommonMaterialOperation.SetColor:
                        return ExecuteSetColor(context);
                    case CommonMaterialOperation.SetFloat:
                        return ExecuteSetFloat(context);
                    case CommonMaterialOperation.SetTexture:
                        return ExecuteSetTexture(context);
                    case CommonMaterialOperation.SetShader:
                        return ExecuteSetShader(context);
                    case CommonMaterialOperation.GetColor:
                        return ExecuteGetColor(context);
                    case CommonMaterialOperation.GetFloat:
                        return ExecuteGetFloat(context);
                    case CommonMaterialOperation.EnableKeyword:
                        return ExecuteEnableKeyword(context);
                    case CommonMaterialOperation.DisableKeyword:
                        return ExecuteDisableKeyword(context);
                    case CommonMaterialOperation.CreateMaterial:
                        return ExecuteCreateMaterial(context);
                    case CommonMaterialOperation.ApplyToRenderer:
                        return ExecuteApplyToRenderer(context);
                    default:
                        return ESVMCPCommandResult.Failed($"不支持的操作类型: {Operation}");
                }
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"Material操作失败: {e.Message}", e);
            }
        }

        private Material GetMaterial(ESVMCPExecutionContext context)
        {
            if (string.IsNullOrEmpty(Target))
                return null;

            // 解析变量引用
            string resolvedTarget = context?.ResolveVariable(Target) ?? Target;
            TargetReference targetRef = TargetReference.Parse(resolvedTarget);

            // 如果是记忆键引用，直接从记忆系统获取
            if (targetRef.Type == TargetType.MemoryKey)
            {
                string memoryKey = targetRef.Value;

                // 场景寻找：从场景记忆系统获取Material
                if (context?.SceneMemory != null && context.SceneMemory.Has(memoryKey))
                {
                    var resolveResult = context.SceneMemory.Get(memoryKey);
                    if (resolveResult.Success && resolveResult.Value is Material)
                    {
                        Material sceneMaterial = (Material)resolveResult.Value;
                        return sceneMaterial;
                    }
                }

                // 持久寻找：从持久记忆系统获取Material
                if (context?.PersistentMemory != null && context.PersistentMemory.Has(memoryKey))
                {
                    var resolveResult = context.PersistentMemory.GetMemory(memoryKey);
                    if (resolveResult.Success && resolveResult.Value is Material)
                    {
                        Material persistentMaterial = (Material)resolveResult.Value;
                        return persistentMaterial;
                    }
                }

                Debug.LogWarning($"[ESVMCP] Material记忆键 '{memoryKey}' 未找到。");
                return null;
            }

            // 1. 本体寻找：从GameObject的Renderer获取Material
            GameObject go = TargetResolver.Resolve(resolvedTarget, context);
            if (go != null)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0)
                {
                    if (MaterialIndex >= 0 && MaterialIndex < renderer.sharedMaterials.Length)
                    {
                        return renderer.sharedMaterials[MaterialIndex];
                    }
                }
            }

            // 2. 场景寻找：从场景记忆系统获取Material
            if (context?.SceneMemory != null && context.SceneMemory.Has(resolvedTarget))
            {
                var resolveResult = context.SceneMemory.Get(resolvedTarget);
                if (resolveResult.Success && resolveResult.Value is Material)
                {
                    Material sceneMaterial = (Material)resolveResult.Value;
                    return sceneMaterial;
                }
            }

            // 3. 持久寻找：从持久记忆系统获取Material
            if (context?.PersistentMemory != null && context.PersistentMemory.Has(resolvedTarget))
            {
                var resolveResult = context.PersistentMemory.GetMemory(resolvedTarget);
                if (resolveResult.Success && resolveResult.Value is Material)
                {
                    Material persistentMaterial = (Material)resolveResult.Value;
                    return persistentMaterial;
                }
            }

            // 4. 资产名寻找：从AssetDatabase直接加载Material
            Material mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(resolvedTarget);
            if (mat != null)
            {
                return mat;
            }

            // 尝试在Materials文件夹中查找
            var config = ESVMCPConfig.Instance;
            if (config != null)
            {
                string materialsPath = System.IO.Path.Combine(config.MaterialsPath, resolvedTarget + ".mat");
                mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(materialsPath);
                if (mat != null)
                {
                    return mat;
                }
            }

            // 5. 智能寻找：在整个项目中搜索Material
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"{resolvedTarget} t:Material");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null)
                {
                    return mat;
                }
            }

            return null;
        }

        private ESVMCPCommandResult ExecuteSetColor(ESVMCPExecutionContext context)
        {
            if (!Color.HasValue)
                return ESVMCPCommandResult.Failed("必须指定color参数");

            Material mat = GetMaterial(context);
            if (mat == null)
                return ESVMCPCommandResult.Failed($"未找到Material: {Target}");

            mat.SetColor(PropertyName, Color.Value);
            return ESVMCPCommandResult.Succeed($"成功设置颜色: {PropertyName} = {Color.Value}");
        }

        private ESVMCPCommandResult ExecuteSetFloat(ESVMCPExecutionContext context)
        {
            if (!FloatValue.HasValue)
                return ESVMCPCommandResult.Failed("必须指定floatValue参数");

            Material mat = GetMaterial(context);
            if (mat == null)
                return ESVMCPCommandResult.Failed($"未找到Material: {Target}");

            mat.SetFloat(PropertyName, FloatValue.Value);
            return ESVMCPCommandResult.Succeed($"成功设置Float: {PropertyName} = {FloatValue.Value}");
        }

        private ESVMCPCommandResult ExecuteSetTexture(ESVMCPExecutionContext context)
        {
            Material mat = GetMaterial(context);
            if (mat == null)
                return ESVMCPCommandResult.Failed($"未找到Material: {Target}");

            if (string.IsNullOrEmpty(TexturePath))
            {
                mat.SetTexture(PropertyName, null);
                return ESVMCPCommandResult.Succeed($"清除纹理: {PropertyName}");
            }

            Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture == null)
                return ESVMCPCommandResult.Failed($"未找到纹理: {TexturePath}");

            mat.SetTexture(PropertyName, texture);
            return ESVMCPCommandResult.Succeed($"成功设置纹理: {PropertyName} = {TexturePath}");
        }

        private ESVMCPCommandResult ExecuteSetShader(ESVMCPExecutionContext context)
        {
            Material mat = GetMaterial(context);
            if (mat == null)
                return ESVMCPCommandResult.Failed($"未找到Material: {Target}");

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
                return ESVMCPCommandResult.Failed($"未找到Shader: {ShaderName}");

            mat.shader = shader;
            return ESVMCPCommandResult.Succeed($"成功设置Shader: {ShaderName}");
        }

        private ESVMCPCommandResult ExecuteGetColor(ESVMCPExecutionContext context)
        {
            Material mat = GetMaterial(context);
            if (mat == null)
                return ESVMCPCommandResult.Failed($"未找到Material: {Target}");

            if (!mat.HasProperty(PropertyName))
                return ESVMCPCommandResult.Failed($"Material没有属性: {PropertyName}");

            Color color = mat.GetColor(PropertyName);
            return ESVMCPCommandResult.Succeed($"颜色值: {PropertyName}", new Dictionary<string, object>
            {
                ["r"] = color.r,
                ["g"] = color.g,
                ["b"] = color.b,
                ["a"] = color.a
            });
        }

        private ESVMCPCommandResult ExecuteGetFloat(ESVMCPExecutionContext context)
        {
            Material mat = GetMaterial(context);
            if (mat == null)
                return ESVMCPCommandResult.Failed($"未找到Material: {Target}");

            if (!mat.HasProperty(PropertyName))
                return ESVMCPCommandResult.Failed($"Material没有属性: {PropertyName}");

            float value = mat.GetFloat(PropertyName);
            return ESVMCPCommandResult.Succeed($"Float值: {PropertyName} = {value}", new Dictionary<string, object>
            {
                ["value"] = value
            });
        }

        private ESVMCPCommandResult ExecuteEnableKeyword(ESVMCPExecutionContext context)
        {
            Material mat = GetMaterial(context);
            if (mat == null)
                return ESVMCPCommandResult.Failed($"未找到Material: {Target}");

            mat.EnableKeyword(Keyword);
            return ESVMCPCommandResult.Succeed($"已启用Keyword: {Keyword}");
        }

        private ESVMCPCommandResult ExecuteDisableKeyword(ESVMCPExecutionContext context)
        {
            Material mat = GetMaterial(context);
            if (mat == null)
                return ESVMCPCommandResult.Failed($"未找到Material: {Target}");

            mat.DisableKeyword(Keyword);
            return ESVMCPCommandResult.Succeed($"已禁用Keyword: {Keyword}");
        }

        private ESVMCPCommandResult ExecuteCreateMaterial(ESVMCPExecutionContext context)
        {
            // 优先使用URP Shader
            if (string.IsNullOrEmpty(ShaderName))
            {
                // 尝试URP Lit Shader
                Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
                if (urpLitShader != null)
                {
                    ShaderName = "Universal Render Pipeline/Lit";
                }
                else
                {
                    // 如果没有URP，尝试URP Simple Lit
                    Shader urpSimpleLitShader = Shader.Find("Universal Render Pipeline/Simple Lit");
                    if (urpSimpleLitShader != null)
                    {
                        ShaderName = "Universal Render Pipeline/Simple Lit";
                    }
                    else
                    {
                        // 如果都没有URP，尝试URP Unlit
                        Shader urpUnlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                        if (urpUnlitShader != null)
                        {
                            ShaderName = "Universal Render Pipeline/Unlit";
                        }
                        else
                        {
                            // 最后回退到Standard
                            ShaderName = "Standard";
                            Debug.LogWarning("[ESVMCP] 未找到URP Shader，使用Standard Shader");
                        }
                    }
                }
            }

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                // 如果指定的Shader找不到，尝试URP Lit作为备选
                if (ShaderName != "Universal Render Pipeline/Lit")
                {
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader != null)
                    {
                        Debug.LogWarning($"[ESVMCP] Shader '{ShaderName}' 未找到，使用 'Universal Render Pipeline/Lit' 作为备选");
                        ShaderName = "Universal Render Pipeline/Lit";
                    }
                }

                if (shader == null)
                {
                    return ESVMCPCommandResult.Failed($"未找到Shader: {ShaderName}，也无法找到URP备选Shader");
                }
            }

            Material mat = new Material(shader);
            mat.name = string.IsNullOrEmpty(AssetName) ? "New Material" : AssetName;

            // 保存到配置的路径
            var config = ESVMCPConfig.Instance;
            string savePath = config.MaterialsPath;

            // 确保文件夹存在
            string fullPath = config.GetFullPath(savePath);
            if (!System.IO.Directory.Exists(fullPath))
            {
                System.IO.Directory.CreateDirectory(fullPath);
                UnityEditor.AssetDatabase.Refresh();
            }

            // 生成唯一文件名
            string assetPath = System.IO.Path.Combine(savePath, mat.name + ".mat");
            assetPath = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(assetPath);

            // 创建资源
            UnityEditor.AssetDatabase.CreateAsset(mat, assetPath);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();

            var result = ESVMCPCommandResult.Succeed($"成功创建Material，Shader: {ShaderName}，路径: {assetPath}", new Dictionary<string, object>
            {
                ["material"] = mat,
                ["path"] = assetPath,
                ["name"] = mat.name,
                ["shader"] = ShaderName
            });

            // 如果启用了记忆保存，则保存Material到记忆系统（CreateMaterial默认持久化）
            if (SaveToMemory)
            {
                PostExecute(result, context, null, mat);
            }

            return result;
        }

        private ESVMCPCommandResult ExecuteApplyToRenderer(ESVMCPExecutionContext context)
        {
            GameObject go = TargetResolver.Resolve(Target, context);
            if (go == null)
                return ESVMCPCommandResult.Failed($"未找到GameObject: {Target}");

            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
                return ESVMCPCommandResult.Failed($"{Target} 没有Renderer组件");

            // 使用改进的GetMaterial方法获取Material
            // 注意：这里我们需要一个单独的Material目标参数
            // 暂时使用PropertyName作为Material目标（这不是最佳设计，但为了向后兼容）
            string materialTarget = PropertyName;
            if (string.IsNullOrEmpty(materialTarget))
                return ESVMCPCommandResult.Failed("需要指定Material目标（使用propertyName参数）");

            // 临时保存当前的Target和MaterialIndex
            string originalTarget = Target;
            int originalMaterialIndex = MaterialIndex;

            try
            {
                // 设置临时参数来获取Material
                Target = materialTarget;
                MaterialIndex = 0; // 应用到第一个材质槽

                Material mat = GetMaterial(context);
                if (mat == null)
                    return ESVMCPCommandResult.Failed($"未找到Material: {materialTarget}");

                // 应用Material
                if (MaterialIndex == 0)
                {
                    renderer.sharedMaterial = mat;
                }
                else if (MaterialIndex < renderer.sharedMaterials.Length)
                {
                    var materials = renderer.sharedMaterials.ToArray();
                    materials[MaterialIndex] = mat;
                    renderer.sharedMaterials = materials;
                }
                else
                {
                    return ESVMCPCommandResult.Failed($"MaterialIndex {MaterialIndex} 超出范围 (最大: {renderer.sharedMaterials.Length - 1})");
                }

                return ESVMCPCommandResult.Succeed($"成功应用Material '{mat.name}' 到 {go.name} (索引: {MaterialIndex})");
            }
            finally
            {
                // 恢复原始参数
                Target = originalTarget;
                MaterialIndex = originalMaterialIndex;
            }
        }
    }
}
