using UnityEngine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace ES.VMCP
{
    /// <summary>
    /// 添加组件命令
    /// </summary>
    [ESVMCPCommand("AddComponent", "为GameObject添加组件")]
    public class AddComponentCommand : ESVMCPCommandBase
    {
        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("component")]
        public string Component { get; set; }

        public override string Description => $"添加组件: {Target} <- {Component}";

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Target) || string.IsNullOrEmpty(Component))
            {
                return ESVMCPValidationResult.Failure("目标GameObject和组件类型不能为空");
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

                // 解析组件类型
                Type componentType = GetComponentType(Component);
                if (componentType == null)
                {
                    return ESVMCPCommandResult.Failed($"未找到组件类型: {Component}");
                }

                // 添加组件
                UnityEngine.Component addedComponent = go.AddComponent(componentType);

                var output = new Dictionary<string, object>
                {
                    { "componentType", componentType.Name },
                    { "gameObject", go.name }
                };

                return ESVMCPCommandResult.Succeed($"成功添加组件: {componentType.Name} -> {go.name}", output);
            }
            catch (Exception e)
            {
                return ESVMCPCommandResult.Failed($"添加组件失败: {e.Message}", e);
            }
        }

        private Type GetComponentType(string typeName)
        {
            // 尝试从UnityEngine命名空间获取
            Type type = System.Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (type != null) return type;

            // 尝试完整类型名
            type = System.Type.GetType(typeName);
            if (type != null) return type;

            // 扫描所有程序集
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            return null;
        }
    }

    /// <summary>
    /// 移除组件命令
    /// </summary>
    [ESVMCPCommand("RemoveComponent", "从GameObject移除组件", isDangerous: true)]
    public class RemoveComponentCommand : ESVMCPCommandBase
    {
        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("component")]
        public string Component { get; set; }

        [JsonProperty("immediate")]
        public bool Immediate { get; set; } = false;

        public override string Description => $"移除组件: {Target} <- {Component}";
        public override bool IsDangerous => true;

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Target) || string.IsNullOrEmpty(Component))
            {
                return ESVMCPValidationResult.Failure("目标GameObject和组件类型不能为空");
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

                Type componentType = System.Type.GetType($"UnityEngine.{Component}, UnityEngine") ?? System.Type.GetType(Component);
                if (componentType == null)
                {
                    return ESVMCPCommandResult.Failed($"未找到组件类型: {Component}");
                }

                UnityEngine.Component comp = go.GetComponent(componentType);
                if (comp == null)
                {
                    return ESVMCPCommandResult.Failed($"GameObject {go.name} 上没有组件 {Component}");
                }

                if (Immediate)
                {
                    UnityEngine.Object.DestroyImmediate(comp);
                }
                else
                {
                    UnityEngine.Object.Destroy(comp);
                }

                return ESVMCPCommandResult.Succeed($"成功移除组件: {Component} from {go.name}");
            }
            catch (Exception e)
            {
                return ESVMCPCommandResult.Failed($"移除组件失败: {e.Message}", e);
            }
        }
    }

    /// <summary>
    /// 获取组件信息命令
    /// </summary>
    [ESVMCPCommand("GetComponent", "获取GameObject上的组件信息")]
    public class GetComponentCommand : ESVMCPCommandBase
    {
        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("component")]
        public string Component { get; set; }

        public override string Description => $"获取组件: {Target} -> {Component}";

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Target) || string.IsNullOrEmpty(Component))
            {
                return ESVMCPValidationResult.Failure("目标GameObject和组件类型不能为空");
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

                Type componentType = System.Type.GetType($"UnityEngine.{Component}, UnityEngine") ?? System.Type.GetType(Component);
                if (componentType == null)
                {
                    return ESVMCPCommandResult.Failed($"未找到组件类型: {Component}");
                }

                UnityEngine.Component comp = go.GetComponent(componentType);
                if (comp == null)
                {
                    return ESVMCPCommandResult.Failed($"GameObject {go.name} 上没有组件 {Component}");
                }

                var output = new Dictionary<string, object>
                {
                    { "hasComponent", true },
                    { "componentType", comp.GetType().Name },
                    { "enabled", comp is Behaviour behaviour && behaviour.enabled }
                };

                return ESVMCPCommandResult.Succeed($"找到组件: {comp.GetType().Name} on {go.name}", output);
            }
            catch (Exception e)
            {
                return ESVMCPCommandResult.Failed($"获取组件失败: {e.Message}", e);
            }
        }
    }

    /// <summary>
    /// 设置组件启用状态命令
    /// </summary>
    [ESVMCPCommand("SetComponentEnabled", "设置组件的启用状态")]
    public class SetComponentEnabledCommand : ESVMCPCommandBase
    {
        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("component")]
        public string Component { get; set; }

        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        public override string Description => $"设置组件启用: {Target}.{Component} -> {Enabled}";

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Target) || string.IsNullOrEmpty(Component))
            {
                return ESVMCPValidationResult.Failure("目标GameObject和组件类型不能为空");
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

                Type componentType = System.Type.GetType($"UnityEngine.{Component}, UnityEngine") ?? System.Type.GetType(Component);
                if (componentType == null)
                {
                    return ESVMCPCommandResult.Failed($"未找到组件类型: {Component}");
                }

                UnityEngine.Component comp = go.GetComponent(componentType);
                if (comp == null)
                {
                    return ESVMCPCommandResult.Failed($"GameObject {go.name} 上没有组件 {Component}");
                }

                if (comp is Behaviour behaviour)
                {
                    behaviour.enabled = Enabled;
                    return ESVMCPCommandResult.Succeed($"成功设置组件启用状态: {Component} -> {Enabled}");
                }
                else
                {
                    return ESVMCPCommandResult.Failed($"组件 {Component} 不支持启用/禁用");
                }
            }
            catch (Exception e)
            {
                return ESVMCPCommandResult.Failed($"设置组件启用状态失败: {e.Message}", e);
            }
        }
    }
}
