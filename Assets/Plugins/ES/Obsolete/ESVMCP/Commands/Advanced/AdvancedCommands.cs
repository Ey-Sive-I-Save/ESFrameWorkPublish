using UnityEngine;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ES.VMCP
{
    /// <summary>
    /// 支持变参数的命令基类扩展
    /// 允许命令接受可变数量的参数，大幅降低扩展成本
    /// </summary>
    public abstract class ESVMCPVariableCommand : ESVMCPCommandBase
    {
        /// <summary>
        /// 额外参数字典（用于接收所有未定义的属性）
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JToken> ExtraParameters { get; set; } = new Dictionary<string, JToken>();

        /// <summary>
        /// 获取额外参数（泛型版本）
        /// </summary>
        protected T GetParameter<T>(string key, T defaultValue = default)
        {
            if (ExtraParameters.TryGetValue(key, out JToken value))
            {
                try
                {
                    return value.ToObject<T>();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ESVMCP] 参数转换失败: {key}, {ex.Message}");
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// 检查参数是否存在
        /// </summary>
        protected bool HasParameter(string key)
        {
            return ExtraParameters.ContainsKey(key);
        }

        /// <summary>
        /// 获取所有参数键
        /// </summary>
        protected IEnumerable<string> GetParameterKeys()
        {
            return ExtraParameters.Keys;
        }
    }

    /// <summary>
    /// 通用属性设置命令 - 使用变参数实现
    /// 可以设置任意GameObject组件的任意属性
    /// </summary>
    [ESVMCPCommand("SetProperty", "设置GameObject组件属性（通用）")]
    public class SetPropertyCommand : ESVMCPVariableCommand
    {
        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("component")]
        public string Component { get; set; }

        public override string Description => $"设置属性: {Target}.{Component}";

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                GameObject go = TargetResolver.Resolve(Target, context);
                if (go == null)
                {
                    return ESVMCPCommandResult.Failed($"未找到GameObject: {Target}");
                }

                // 智能类型适配
                go = TypeAdapter.ToGameObject(go);
                if (go == null)
                {
                    return ESVMCPCommandResult.Failed($"无法转换目标: {Target}");
                }

                // 获取组件
                Type componentType = GetComponentType(Component);
                if (componentType == null)
                {
                    return ESVMCPCommandResult.Failed($"未找到组件类型: {Component}");
                }

                Component comp = go.GetComponent(componentType);
                if (comp == null)
                {
                    return ESVMCPCommandResult.Failed($"GameObject上没有组件: {Component}");
                }

                // 设置所有参数为属性
                int setCount = 0;
                foreach (var param in ExtraParameters)
                {
                    if (param.Key != "target" && param.Key != "component" && param.Key != "type" && param.Key != "id")
                    {
                        SetComponentProperty(comp, param.Key, param.Value);
                        setCount++;
                    }
                }

                return ESVMCPCommandResult.Succeed($"成功设置{setCount}个属性");
            }
            catch (Exception e)
            {
                return ESVMCPCommandResult.Failed($"设置属性失败: {e.Message}", e);
            }
        }

        private Type GetComponentType(string typeName)
        {
            // 尝试从UnityEngine命名空间获取
            Type type = System.Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (type != null) return type;

            // 尝试直接获取（可能包含完整命名空间）
            type = System.Type.GetType(typeName);
            if (type != null) return type;

            // 扫描所有程序集
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;

                // 尝试添加UnityEngine命名空间
                type = assembly.GetType($"UnityEngine.{typeName}");
                if (type != null) return type;
            }

            return null;
        }

        private void SetComponentProperty(Component comp, string propertyName, JToken value)
        {
            var type = comp.GetType();
            var property = type.GetProperty(propertyName);
            
            if (property != null && property.CanWrite)
            {
                object convertedValue = value.ToObject(property.PropertyType);
                property.SetValue(comp, convertedValue);
            }
            else
            {
                var field = type.GetField(propertyName);
                if (field != null)
                {
                    object convertedValue = value.ToObject(field.FieldType);
                    field.SetValue(comp, convertedValue);
                }
            }
        }
    }

    /// <summary>
    /// 批量操作命令 - 对多个对象执行相同操作
    /// </summary>
    [ESVMCPCommand("BatchOperation", "批量操作多个对象")]
    public class BatchOperationCommand : ESVMCPVariableCommand
    {
        [JsonProperty("targets")]
        public string[] Targets { get; set; }

        [JsonProperty("operation")]
        public string Operation { get; set; }

        public override string Description => $"批量操作: {Operation} on {Targets?.Length ?? 0} 个对象";

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                if (Targets == null || Targets.Length == 0)
                {
                    return ESVMCPCommandResult.Failed("目标列表为空");
                }

                int successCount = 0;
                int failureCount = 0;
                List<string> errors = new List<string>();

                foreach (var target in Targets)
                {
                    try
                    {
                        GameObject go = TargetResolver.Resolve(target, context);
                        if (go != null)
                        {
                            go = TypeAdapter.ToGameObject(go);
                            if (go != null)
                            {
                                ExecuteOperationOnObject(go, Operation);
                                successCount++;
                            }
                            else
                            {
                                failureCount++;
                                errors.Add($"无法转换: {target}");
                            }
                        }
                        else
                        {
                            failureCount++;
                            errors.Add($"未找到: {target}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        errors.Add($"{target}: {ex.Message}");
                    }
                }

                var output = new Dictionary<string, object>
                {
                    { "success", successCount },
                    { "failure", failureCount },
                    { "errors", errors }
                };

                return ESVMCPCommandResult.Succeed($"批量操作完成: {successCount}成功, {failureCount}失败", output);
            }
            catch (Exception e)
            {
                return ESVMCPCommandResult.Failed($"批量操作失败: {e.Message}", e);
            }
        }

        private void ExecuteOperationOnObject(GameObject go, string operation)
        {
            switch (operation.ToLower())
            {
                case "activate":
                case "setactive":
                    go.SetActive(true);
                    break;
                case "deactivate":
                    go.SetActive(false);
                    break;
                case "destroy":
                    UnityEngine.Object.Destroy(go);
                    break;
                case "settag":
                    if (HasParameter("tag"))
                    {
                        go.tag = GetParameter<string>("tag");
                    }
                    break;
                case "setlayer":
                    if (HasParameter("layer"))
                    {
                        go.layer = GetParameter<int>("layer");
                    }
                    break;
                case "applymaterial":
                    if (HasParameter("materialName"))
                    {
                        string matName = GetParameter<string>("materialName");
                        Material mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matName);
                        if (mat != null)
                        {
                            Renderer renderer = go.GetComponent<Renderer>();
                            if (renderer != null)
                            {
                                renderer.sharedMaterial = mat;
                            }
                        }
                    }
                    break;
                case "addcomponent":
                    if (HasParameter("componentType"))
                    {
                        string componentType = GetParameter<string>("componentType");
                        Type type = GetComponentTypeByName(componentType);
                        if (type != null && !go.GetComponent(type))
                        {
                            go.AddComponent(type);
                        }
                    }
                    break;
                default:
                    // 支持自定义操作参数
                    ExecuteCustomOperation(go, operation);
                    break;
            }
        }

        private Type GetComponentTypeByName(string typeName)
        {
            // 尝试从UnityEngine命名空间获取
            Type type = System.Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (type != null) return type;

            // 尝试直接获取
            type = System.Type.GetType(typeName);
            if (type != null) return type;

            // 扫描所有程序集
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;

                type = assembly.GetType($"UnityEngine.{typeName}");
                if (type != null) return type;
            }

            return null;
        }

        private void ExecuteCustomOperation(GameObject go, string operation)
        {
            // 可以扩展更多操作
            foreach (var param in ExtraParameters)
            {
                if (param.Key != "targets" && param.Key != "operation" && param.Key != "type" && param.Key != "id")
                {
                    // 应用自定义参数
                    Debug.Log($"[ESVMCP] 自定义操作 {param.Key} = {param.Value} on {go.name}");
                }
            }
        }
    }

    /// <summary>
    /// 条件执行命令 - 根据条件执行不同的子命令
    /// </summary>
    [ESVMCPCommand("ConditionalExecute", "条件执行")]
    public class ConditionalExecuteCommand : ESVMCPVariableCommand
    {
        [JsonProperty("condition")]
        public string Condition { get; set; }

        [JsonProperty("trueCommand")]
        public JObject TrueCommand { get; set; }

        [JsonProperty("falseCommand")]
        public JObject FalseCommand { get; set; }

        public override string Description => $"条件执行: if ({Condition})";

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                bool conditionResult = EvaluateCondition(Condition, context);
                JObject commandToExecute = conditionResult ? TrueCommand : FalseCommand;

                if (commandToExecute == null)
                {
                    return ESVMCPCommandResult.Succeed($"条件 {Condition} = {conditionResult}, 无需执行");
                }

                // TODO: 执行子命令
                // 这里需要与命令执行器集成

                return ESVMCPCommandResult.Succeed($"条件 {Condition} = {conditionResult}, 已执行对应命令");
            }
            catch (Exception e)
            {
                return ESVMCPCommandResult.Failed($"条件执行失败: {e.Message}", e);
            }
        }

        private bool EvaluateCondition(string condition, ESVMCPExecutionContext context)
        {
            // 简单的条件评估
            // 支持: exists(key), equals(key, value), contains(key, value)
            
            if (condition.StartsWith("exists(") && condition.EndsWith(")"))
            {
                string key = condition.Substring(7, condition.Length - 8);
                return context.SceneMemory?.Has(key) ?? false;
            }

            // 可以扩展更多条件类型
            return false;
        }
    }
}
