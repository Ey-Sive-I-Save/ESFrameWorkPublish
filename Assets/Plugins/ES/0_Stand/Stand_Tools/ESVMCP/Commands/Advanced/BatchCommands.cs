using UnityEngine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEditor;

namespace ES.VMCP
{
    /// <summary>
    /// 按标签批量操作命令
    /// </summary>
    [ESVMCPCommand("BatchOperationByTag", "按Tag批量操作对象")]
    public class BatchOperationByTagCommand : ESVMCPCommandBase
    {
        [JsonProperty("tag")]
        public string Tag { get; set; }

        [JsonProperty("operation")]
        public string Operation { get; set; }

        [JsonProperty("active")]
        public bool? Active { get; set; }

        [JsonProperty("layer")]
        public int? Layer { get; set; }

        [JsonProperty("materialName")]
        public string MaterialName { get; set; }

        public override string Description => $"按Tag批量操作: {Tag} -> {Operation}";

        public override bool IsDangerous => Operation?.ToLower() == "destroy";

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Tag))
            {
                return ESVMCPValidationResult.Failure("tag参数不能为空");
            }

            if (string.IsNullOrEmpty(Operation))
            {
                return ESVMCPValidationResult.Failure("operation参数不能为空");
            }

            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                GameObject[] objects = GameObject.FindGameObjectsWithTag(Tag);
                if (objects == null || objects.Length == 0)
                {
                    return ESVMCPCommandResult.Succeed($"没有找到Tag为 {Tag} 的对象");
                }

                int successCount = 0;
                List<string> errors = new List<string>();

                foreach (var obj in objects)
                {
                    try
                    {
                        switch (Operation.ToLower())
                        {
                            case "setactive":
                                if (Active.HasValue)
                                {
                                    obj.SetActive(Active.Value);
                                    successCount++;
                                }
                                break;

                            case "setlayer":
                                if (Layer.HasValue)
                                {
                                    obj.layer = Layer.Value;
                                    successCount++;
                                }
                                break;

                            case "applymaterial":
                                if (!string.IsNullOrEmpty(MaterialName))
                                {
                                    Material mat = FindMaterial(MaterialName, context);
                                    if (mat != null)
                                    {
                                        Renderer renderer = obj.GetComponent<Renderer>();
                                        if (renderer != null)
                                        {
                                            renderer.sharedMaterial = mat;
                                            successCount++;
                                        }
                                    }
                                }
                                break;

                            case "destroy":
                                UnityEngine.Object.DestroyImmediate(obj);
                                successCount++;
                                break;

                            default:
                                errors.Add($"未知操作: {Operation}");
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{obj.name}: {ex.Message}");
                    }
                }

                string resultMsg = $"批量操作完成: {successCount}/{objects.Length}个对象";
                if (errors.Count > 0)
                {
                    resultMsg += $"\n错误: {string.Join(", ", errors)}";
                }

                Debug.Log($"[ESVMCP] {resultMsg}");
                return ESVMCPCommandResult.Succeed(resultMsg);
            }
            catch (Exception ex)
            {
                return ESVMCPCommandResult.Failed($"批量操作失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 查找材质 - 改进的查找策略
        /// </summary>
        private Material FindMaterial(string materialName, ESVMCPExecutionContext context)
        {
            if (string.IsNullOrEmpty(materialName))
                return null;

            // 解析变量引用
            string resolvedName = context?.ResolveVariable(materialName) ?? materialName;

            // 1. 从AssetDatabase直接加载（完整路径）
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(resolvedName);
            if (mat != null)
            {
                return mat;
            }

            // 2. 提取材质名称（去掉路径和扩展名）
            string materialAssetName = System.IO.Path.GetFileNameWithoutExtension(resolvedName);

            // 3. 从场景记忆系统查找同名材质
            if (context?.SceneMemory != null)
            {
                // 遍历所有记忆项，查找Material类型的项
                foreach (var key in context.SceneMemory.GetAllKeys())
                {
                    var resolveResult = context.SceneMemory.Get(key);
                    if (resolveResult.Success && resolveResult.Value is Material sceneMat)
                    {
                        if (sceneMat.name == materialAssetName)
                        {
                            return sceneMat;
                        }
                    }
                }
            }

            // 4. 从持久记忆系统查找同名材质
            if (context?.PersistentMemory != null)
            {
                // 遍历所有记忆项，查找Material类型的项
                foreach (var key in context.PersistentMemory.GetAllKeys())
                {
                    var resolveResult = context.PersistentMemory.GetMemory(key);
                    if (resolveResult.Success && resolveResult.Value is Material persistentMat)
                    {
                        if (persistentMat.name == materialAssetName)
                        {
                            return persistentMat;
                        }
                    }
                }
            }

            // 5. 在Materials文件夹中查找
            var config = ESVMCPConfig.Instance;
            if (config != null)
            {
                string materialsPath = System.IO.Path.Combine(config.MaterialsPath, materialAssetName + ".mat");
                mat = AssetDatabase.LoadAssetAtPath<Material>(materialsPath);
                if (mat != null)
                {
                    return mat;
                }
            }

            // 6. 在整个项目中搜索同名材质
            string[] guids = AssetDatabase.FindAssets($"{materialAssetName} t:Material");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null)
                {
                    return mat;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// 复制并修改对象命令
    /// </summary>
    [ESVMCPCommand("DuplicateAndModify", "复制对象并修改参数")]
    public class DuplicateAndModifyCommand : ESVMCPCommandBase
    {
        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonConverter(typeof(Vector3Converter))]
        [JsonProperty("position")]
        public Vector3? Position { get; set; }

        [JsonConverter(typeof(Vector3Converter))]
        [JsonProperty("rotation")]
        public Vector3? Rotation { get; set; }

        [JsonConverter(typeof(Vector3Converter))]
        [JsonProperty("scale")]
        public Vector3? Scale { get; set; }

        [JsonProperty("parent")]
        public string Parent { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; } = 1;

        [JsonConverter(typeof(Vector3Converter))]
        [JsonProperty("offset")]
        public Vector3? Offset { get; set; }

        [JsonProperty("saveToMemory")]
        public new bool SaveToMemory { get; set; } = false;

        [JsonProperty("memoryKey")]
        public new string MemoryKey { get; set; }

        public override string Description => $"复制并修改: {Source} x {Count}";

        public override bool IsDangerous => false;

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Source))
            {
                return ESVMCPValidationResult.Failure("source参数不能为空");
            }

            if (Count < 1 || Count > 1000)
            {
                return ESVMCPValidationResult.Failure("count必须在1-1000之间");
            }

            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                GameObject sourceObj = ResolveTarget(Source, context);
                if (sourceObj == null)
                {
                    return ESVMCPCommandResult.Failed($"找不到源对象: {Source}");
                }

                GameObject parentObj = null;
                if (!string.IsNullOrEmpty(Parent))
                {
                    parentObj = ResolveTarget(Parent, context);
                }

                List<GameObject> createdObjects = new List<GameObject>();

                for (int i = 0; i < Count; i++)
                {
                    GameObject newObj = UnityEngine.Object.Instantiate(sourceObj);
                    
                    // 设置名称
                    if (!string.IsNullOrEmpty(Name))
                    {
                        newObj.name = Count > 1 ? $"{Name}_{i + 1}" : Name;
                    }
                    else
                    {
                        newObj.name = $"{sourceObj.name}_Copy_{i + 1}";
                    }

                    // 设置父对象
                    if (parentObj != null)
                    {
                        newObj.transform.SetParent(parentObj.transform, false);
                    }

                    // 设置位置（考虑偏移）
                    if (Position != null)
                    {
                        Vector3 basePos = Position.Value;
                        if (Offset != null && i > 0)
                        {
                            basePos += Offset.Value * i;
                        }
                        newObj.transform.position = basePos;
                    }

                    // 设置旋转
                    if (Rotation != null)
                    {
                        newObj.transform.eulerAngles = Rotation.Value;
                    }

                    // 设置缩放
                    if (Scale != null)
                    {
                        newObj.transform.localScale = Scale.Value;
                    }

                    createdObjects.Add(newObj);

                    // 保存到记忆系统
                    if (SaveToMemory && !string.IsNullOrEmpty(MemoryKey))
                    {
                        string key = Count > 1 ? $"{MemoryKey}_{i + 1}" : MemoryKey;
                        context.SceneMemory?.SaveGameObject(key, newObj);
                    }
                }

                string resultMsg = $"成功复制 {Count} 个对象";
                Debug.Log($"[ESVMCP] {resultMsg}");
                return ESVMCPCommandResult.Succeed(resultMsg, new Dictionary<string, object> { { "createdObjects", createdObjects } });
            }
            catch (Exception ex)
            {
                return ESVMCPCommandResult.Failed($"复制对象失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重写记忆保存方法 - 此命令已在Execute中手动处理记忆保存
        /// </summary>
        public override void TrySaveToMemory(ESVMCPCommandResult result, ESVMCPExecutionContext context)
        {
            // 此命令已在Execute方法中手动处理记忆保存，不需要额外操作
            // 避免基类方法重复保存
        }
    }

    /// <summary>
    /// 批量应用材质命令
    /// </summary>
    [ESVMCPCommand("ApplyMaterialToMultiple", "批量应用材质到多个对象")]
    public class ApplyMaterialToMultipleCommand : ESVMCPCommandBase
    {
        [JsonProperty("targets")]
        public string[] Targets { get; set; }

        [JsonProperty("materialName")]
        public string MaterialName { get; set; }

        [JsonProperty("materialIndex")]
        public int MaterialIndex { get; set; } = 0;

        public override string Description => $"批量应用材质: {MaterialName} -> {Targets?.Length ?? 0}个对象";

        public override bool IsDangerous => false;

        public override ESVMCPValidationResult Validate()
        {
            if (Targets == null || Targets.Length == 0)
            {
                return ESVMCPValidationResult.Failure("targets不能为空");
            }

            if (string.IsNullOrEmpty(MaterialName))
            {
                return ESVMCPValidationResult.Failure("materialName不能为空");
            }

            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                // 查找材质 - 使用改进的查找策略
                Material mat = FindMaterial(MaterialName, context);
                if (mat == null)
                {
                    return ESVMCPCommandResult.Failed($"找不到材质: {MaterialName}");
                }

                int successCount = 0;
                List<string> errors = new List<string>();

                foreach (var target in Targets)
                {
                    try
                    {
                        GameObject obj = ResolveTarget(target, context);
                        if (obj == null)
                        {
                            errors.Add($"找不到对象: {target}");
                            continue;
                        }

                        Renderer renderer = obj.GetComponent<Renderer>();
                        if (renderer == null)
                        {
                            errors.Add($"对象没有Renderer: {target}");
                            continue;
                        }

                        if (MaterialIndex >= 0 && MaterialIndex < renderer.sharedMaterials.Length)
                        {
                            Material[] materials = renderer.sharedMaterials;
                            materials[MaterialIndex] = mat;
                            renderer.sharedMaterials = materials;
                        }
                        else
                        {
                            renderer.sharedMaterial = mat;
                        }

                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{target}: {ex.Message}");
                    }
                }

                string resultMsg = $"批量应用材质完成: {successCount}/{Targets.Length}";
                if (errors.Count > 0)
                {
                    resultMsg += $"\n错误: {string.Join(", ", errors)}";
                }

                Debug.Log($"[ESVMCP] {resultMsg}");
                return ESVMCPCommandResult.Succeed(resultMsg);
            }
            catch (Exception ex)
            {
                return ESVMCPCommandResult.Failed($"批量应用材质失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 查找材质 - 改进的查找策略
        /// </summary>
        private Material FindMaterial(string materialName, ESVMCPExecutionContext context)
        {
            if (string.IsNullOrEmpty(materialName))
                return null;

            // 解析变量引用
            string resolvedName = context?.ResolveVariable(materialName) ?? materialName;

            // 1. 从AssetDatabase直接加载（完整路径）
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(resolvedName);
            if (mat != null)
            {
                return mat;
            }

            // 2. 提取材质名称（去掉路径和扩展名）
            string materialAssetName = System.IO.Path.GetFileNameWithoutExtension(resolvedName);

            // 3. 从场景记忆系统查找同名材质
            if (context?.SceneMemory != null)
            {
                // 遍历所有记忆项，查找Material类型的项
                foreach (var key in context.SceneMemory.GetAllKeys())
                {
                    var resolveResult = context.SceneMemory.Get(key);
                    if (resolveResult.Success && resolveResult.Value is Material sceneMat)
                    {
                        if (sceneMat.name == materialAssetName)
                        {
                            return sceneMat;
                        }
                    }
                }
            }

            // 4. 从持久记忆系统查找同名材质
            if (context?.PersistentMemory != null)
            {
                // 遍历所有记忆项，查找Material类型的项
                foreach (var key in context.PersistentMemory.GetAllKeys())
                {
                    var resolveResult = context.PersistentMemory.GetMemory(key);
                    if (resolveResult.Success && resolveResult.Value is Material persistentMat)
                    {
                        if (persistentMat.name == materialAssetName)
                        {
                            return persistentMat;
                        }
                    }
                }
            }

            // 5. 在Materials文件夹中查找
            var config = ESVMCPConfig.Instance;
            if (config != null)
            {
                string materialsPath = System.IO.Path.Combine(config.MaterialsPath, materialAssetName + ".mat");
                mat = AssetDatabase.LoadAssetAtPath<Material>(materialsPath);
                if (mat != null)
                {
                    return mat;
                }
            }

            // 6. 在整个项目中搜索同名材质
            string[] guids = AssetDatabase.FindAssets($"{materialAssetName} t:Material");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null)
                {
                    return mat;
                }
            }

            return null;
        }
    }
}
