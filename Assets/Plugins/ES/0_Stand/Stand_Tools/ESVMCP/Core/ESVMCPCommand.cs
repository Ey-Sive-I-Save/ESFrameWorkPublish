using UnityEngine;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ES.VMCP
{
    /// <summary>
    /// ESVMCP命令基类 - 使用多态设计
    /// 每个具体命令继承此类并定义自己的强类型属性
    /// </summary>
    [JsonConverter(typeof(ESVMCPCommandConverter))]
    public abstract class ESVMCPCommandBase
    {
        /// <summary>
        /// 命令类型（由type字段反序列化时自动设置）
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>
        /// 命令ID（用于结果引用和记忆系统）
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>
        /// 是否保存到记忆系统（默认true，记忆系统是核心）
        /// </summary>
        [JsonProperty("saveToMemory")]
        public bool SaveToMemory { get; set; } = true;

        /// <summary>
        /// 记忆键名（如果saveToMemory=true，使用此键保存结果）
        /// 如果未指定，默认使用Id或自动生成
        /// </summary>
        [JsonProperty("memoryKey")]
        public string MemoryKey { get; set; }

        /// <summary>
        /// 是否持久化记忆（资产类命令应设置为true）
        /// </summary>
        [JsonProperty("persistent")]
        public bool Persistent { get; set; } = false;

        /// <summary>
        /// 命令描述
        /// </summary>
        [JsonIgnore]
        public abstract string Description { get; }

        /// <summary>
        /// 是否是危险操作（删除、清空等）
        /// </summary>
        [JsonIgnore]
        public virtual bool IsDangerous => false;

        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="context">执行上下文（包含记忆系统、变量解析等）</param>
        /// <returns>执行结果</returns>
        public abstract ESVMCPCommandResult Execute(ESVMCPExecutionContext context);

        /// <summary>
        /// 命令执行后的记忆保存（多态设计）
        /// 每个命令可以重写此方法，实现自己独立的记忆保存逻辑
        /// </summary>
        /// <param name="result">命令执行结果</param>
        /// <param name="context">执行上下文</param>
        public virtual void TrySaveToMemory(ESVMCPCommandResult result, ESVMCPExecutionContext context)
        {
            // 默认实现：如果SaveToMemory为true，使用PostExecute保存
            if (SaveToMemory)
            {
                PostExecute(result, context);
            }
        }

        /// <summary>
        /// 执行后处理（自动保存记忆）- 增强版
        /// </summary>
        protected void PostExecute(ESVMCPCommandResult result, ESVMCPExecutionContext context, GameObject targetObject = null, UnityEngine.Object targetAsset = null)
        {
            if (!SaveToMemory || context?.SceneMemory == null)
                return;

            try
            {
                // 确定记忆键（自动生成）
                string key = !string.IsNullOrEmpty(MemoryKey) ? MemoryKey : 
                            !string.IsNullOrEmpty(Id) ? Id : 
                            $"{Type}_{DateTime.Now:HHmmss}";

                // 保存GameObject到增强记忆
                if (targetObject != null)
                {
                    context.SceneMemory.SaveGameObject(key, targetObject, Persistent);
                }
                // 保存资产到增强记忆（自动持久化）
                else if (targetAsset != null)
                {
                    context.SceneMemory.SaveAsset(key, targetAsset);
                }
                // 保存输出数据
                else if (result.OutputData != null && result.OutputData.Count > 0)
                {
                    foreach (var kvp in result.OutputData)
                    {
                        // 智能判断类型：如果是UnityEngine.Object，保存为Asset；否则保存为Primitive
                        if (kvp.Value is UnityEngine.Object unityObject)
                        {
                            context.SceneMemory.SaveAsset($"{key}_{kvp.Key}", unityObject);
                        }
                        else if (kvp.Value is GameObject go)
                        {
                            context.SceneMemory.SaveGameObject($"{key}_{kvp.Key}", go, Persistent);
                        }
                        else
                        {
                            context.SceneMemory.SavePrimitive($"{key}_{kvp.Key}", kvp.Value);
                        }
                    }
                }

                // 记录操作
                var operationRecord = new Dictionary<string, object>
                {
                    { "commandType", Type },
                    { "timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                    { "success", result.Success },
                    { "message", result.Message }
                };

                if (targetObject != null)
                {
                    operationRecord["targetName"] = targetObject.name;
                }
                else if (targetAsset != null)
                {
                    operationRecord["assetName"] = targetAsset.name;
                }

                context.SceneMemory.SavePrimitive(key + "_record", operationRecord);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ESVMCP] 保存记忆失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 验证命令参数（可选实现，默认通过）
        /// </summary>
        /// <returns>验证结果</returns>
        public virtual ESVMCPValidationResult Validate()
        {
            return ESVMCPValidationResult.Success();
        }

        /// <summary>
        /// 解析变量引用（支持{{var}}语法）
        /// </summary>
        protected string ResolveVariable(string value, ESVMCPExecutionContext context)
        {
            if (string.IsNullOrEmpty(value) || !value.Contains("{{"))
                return value;

            return context.ResolveVariable(value);
        }

        /// <summary>
        /// 解析GameObject引用（支持多种定位方式）
        /// 推荐使用 TargetResolver.Resolve 替代此方法
        /// </summary>
        [Obsolete("推荐使用 TargetResolver.Resolve(reference, context) 以获得更强大的目标定位功能")]
        protected GameObject ResolveGameObject(string reference, ESVMCPExecutionContext context)
        {
            return TargetResolver.Resolve(reference, context);
        }

        /// <summary>
        /// 高级目标解析（新推荐方式）
        /// </summary>
        protected GameObject ResolveTarget(string reference, ESVMCPExecutionContext context)
        {
            return TargetResolver.Resolve(reference, context);
        }

        /// <summary>
        /// 批量目标解析
        /// </summary>
        protected List<GameObject> ResolveTargets(string[] references, ESVMCPExecutionContext context)
        {
            return TargetResolver.ResolveMultiple(references, context);
        }
    }

    /// <summary>
    /// 命令执行结果
    /// </summary>
    public class ESVMCPCommandResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public Dictionary<string, object> OutputData { get; set; }
        public Exception Error { get; set; }

        public static ESVMCPCommandResult Succeed(string message = "", Dictionary<string, object> output = null)
        {
            return new ESVMCPCommandResult
            {
                Success = true,
                Message = message,
                OutputData = output ?? new Dictionary<string, object>()
            };
        }

        public static ESVMCPCommandResult Failed(string message, Exception error = null)
        {
            return new ESVMCPCommandResult
            {
                Success = false,
                Message = message,
                Error = error,
                OutputData = new Dictionary<string, object>()
            };
        }
    }

    /// <summary>
    /// 命令验证结果
    /// </summary>
    public class ESVMCPValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; }
        public List<string> Warnings { get; set; }

        public ESVMCPValidationResult()
        {
            IsValid = true;
            Errors = new List<string>();
            Warnings = new List<string>();
        }

        public void AddError(string error)
        {
            IsValid = false;
            Errors.Add(error);
        }

        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }

        public static ESVMCPValidationResult Success()
        {
            return new ESVMCPValidationResult { IsValid = true };
        }

        public static ESVMCPValidationResult Failure(params string[] errors)
        {
            var result = new ESVMCPValidationResult { IsValid = false };
            result.Errors.AddRange(errors);
            return result;
        }
    }

    /// <summary>
    /// 执行上下文
    /// </summary>
    public class ESVMCPExecutionContext
    {
        /// <summary>
        /// 命令ID映射表（用于引用其他命令的结果）
        /// </summary>
        public Dictionary<string, ESVMCPCommandResult> CommandResults { get; set; }

        /// <summary>
        /// 临时记忆
        /// </summary>
        public Dictionary<string, object> TempMemory { get; set; }

        /// <summary>
        /// 场景记忆引用（增强版）
        /// </summary>
        public ESVMCPMemoryEnhanced SceneMemory { get; set; }

        /// <summary>
        /// 持久记忆引用（增强版）
        /// </summary>
        public ESVMCPMemoryAssetEnhanced PersistentMemory { get; set; }

        /// <summary>
        /// 执行配置
        /// </summary>
        public ESVMCPConfig Config { get; set; }

        /// <summary>
        /// 当前执行的命令索引
        /// </summary>
        public int CurrentCommandIndex { get; set; }

        /// <summary>
        /// 总命令数
        /// </summary>
        public int TotalCommands { get; set; }

        public ESVMCPExecutionContext()
        {
            CommandResults = new Dictionary<string, ESVMCPCommandResult>();
            TempMemory = new Dictionary<string, object>();
        }

        /// <summary>
        /// 解析变量引用（支持 {{cmd_id.property}} 或 {{memory_key}} 语法）
        /// </summary>
        public string ResolveVariable(string value)
        {
            if (string.IsNullOrEmpty(value) || !value.Contains("{{"))
                return value;

            string result = value;
            
            // 查找所有 {{...}} 引用
            int startIndex = 0;
            while ((startIndex = result.IndexOf("{{", startIndex)) != -1)
            {
                int endIndex = result.IndexOf("}}", startIndex);
                if (endIndex == -1) break;

                string varRef = result.Substring(startIndex + 2, endIndex - startIndex - 2).Trim();
                object resolvedValue = ResolveVariableInternal(varRef);
                
                string replacement = resolvedValue?.ToString() ?? "";
                result = result.Remove(startIndex, endIndex - startIndex + 2);
                result = result.Insert(startIndex, replacement);
                
                startIndex += replacement.Length;
            }

            return result;
        }

        /// <summary>
        /// 内部变量解析逻辑
        /// </summary>
        private object ResolveVariableInternal(string varName)
        {
            // 检查命令结果引用 (cmd_id.property)
            if (varName.Contains("."))
            {
                string[] parts = varName.Split('.');
                if (parts.Length == 2 && CommandResults.TryGetValue(parts[0], out ESVMCPCommandResult cmdResult))
                {
                    if (cmdResult.OutputData != null && cmdResult.OutputData.TryGetValue(parts[1], out object propValue))
                    {
                        return propValue;
                    }
                }
            }

            // 检查临时记忆
            if (TempMemory.TryGetValue(varName, out object tempValue))
            {
                return tempValue;
            }

            // 检查场景记忆（增强版）
            if (SceneMemory != null && SceneMemory.Has(varName))
            {
                return SceneMemory.Get(varName);
            }

            // 检查持久记忆（增强版）
            if (PersistentMemory != null && PersistentMemory.Has(varName))
            {
                return PersistentMemory.GetMemory(varName);
            }

            Debug.LogWarning($"[ESVMCP] 无法解析变量引用: {varName}");
            return null;
        }
    }

    /// <summary>
    /// 命令特性标记（用于自动注册）
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ESVMCPCommandAttribute : Attribute
    {
        public string CommandType { get; set; }
        public string Description { get; set; }
        public bool IsDangerous { get; set; }

        public ESVMCPCommandAttribute(string commandType, string description = "", bool isDangerous = false)
        {
            CommandType = commandType;
            Description = description;
            IsDangerous = isDangerous;
        }
    }
}
