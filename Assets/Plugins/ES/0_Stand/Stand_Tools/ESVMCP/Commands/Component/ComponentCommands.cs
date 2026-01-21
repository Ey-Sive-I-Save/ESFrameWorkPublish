using UnityEngine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ES.VMCP
{
    /// <summary>
    /// 通用Component操作类型(基础操作)
    /// 注意：高级或特化操作应创建独立的Command类，不应继续扩展此枚举
    /// </summary>
    public enum CommonComponentOperation
    {
        Add,        // 添加组件
        Remove,     // 移除组件
        Get,        // 获取组件信息
        SetEnabled, // 设置组件启用状态
        GetAll,     // 获取所有组件
        Copy        // 复制组件到其他对象
    }

    /// <summary>
    /// 统一的组件操作命令 - 合并所有组件相关操作
    /// </summary> 
    [ESVMCPCommand("CommonComponentOperation", "统一的组件操作命令，支持添加、移除、获取、设置等操作")]
    public class ComponentOperationCommand : ESVMCPCommandBase
    {
        [JsonProperty("operation")]
        public CommonComponentOperation Operation { get; set; }

        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("component")]
        public string Component { get; set; }

        [JsonProperty("enabled")]
        public bool? Enabled { get; set; }

        [JsonProperty("immediate")]
        public bool Immediate { get; set; } = false;

        [JsonProperty("source")]
        public string Source { get; set; } // 用于复制操作的源对象

        // 类型缓存 - 提高读取效率
        private static readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();
        private static readonly object _cacheLock = new object();

        public override string Description
        {
            get
            {
                switch (Operation)
                {
                    case CommonComponentOperation.Add:
                        return $"添加组件: {Target} <- {Component}";
                    case CommonComponentOperation.Remove:
                        return $"移除组件: {Target} <- {Component}";
                    case CommonComponentOperation.Get:
                        return $"获取组件: {Target} -> {Component}";
                    case CommonComponentOperation.SetEnabled:
                        return $"设置组件启用: {Target}.{Component} -> {Enabled}";
                    case CommonComponentOperation.GetAll:
                        return $"获取所有组件: {Target}";
                    case CommonComponentOperation.Copy:
                        return $"复制组件: {Source}.{Component} -> {Target}";
                    default:
                        return $"组件操作: {Operation}";
                }
            }
        }

        public override bool IsDangerous => Operation == CommonComponentOperation.Remove;

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Target))
            {
                return ESVMCPValidationResult.Failure("目标GameObject不能为空");
            }

            switch (Operation)
            {
                case CommonComponentOperation.Add:
                case CommonComponentOperation.Remove:
                case CommonComponentOperation.Get:
                case CommonComponentOperation.SetEnabled:
                    if (string.IsNullOrEmpty(Component))
                    {
                        return ESVMCPValidationResult.Failure("组件类型不能为空");
                    }
                    break;

                case CommonComponentOperation.Copy:
                    if (string.IsNullOrEmpty(Component) || string.IsNullOrEmpty(Source))
                    {
                        return ESVMCPValidationResult.Failure("源对象和组件类型都不能为空");
                    }
                    break;
            }

            if (Operation == CommonComponentOperation.SetEnabled && !Enabled.HasValue)
            {
                return ESVMCPValidationResult.Failure("设置启用状态时必须指定enabled参数");
            }

            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                // 使用新的目标解析器（支持多种定位方式）
                GameObject go = TargetResolver.Resolve(Target, context);
                if (go == null)
                {
                    return ESVMCPCommandResult.Failed($"未找到目标GameObject: {Target}");
                }

                // 智能类型适配（自动处理Component→GameObject转换）
                go = TypeAdapter.ToGameObject(go);
                if (go == null)
                {
                    return ESVMCPCommandResult.Failed($"无法将目标转换为GameObject: {Target}");
                }

                ESVMCPCommandResult result;

                switch (Operation)
                {
                    case CommonComponentOperation.Add:
                        result = ExecuteAddComponent(go);
                        break;
                    case CommonComponentOperation.Remove:
                        result = ExecuteRemoveComponent(go);
                        break;
                    case CommonComponentOperation.Get:
                        result = ExecuteGetComponent(go);
                        break;
                    case CommonComponentOperation.SetEnabled:
                        result = ExecuteSetComponentEnabled(go);
                        break;
                    case CommonComponentOperation.GetAll:
                        result = ExecuteGetAllComponents(go);
                        break;
                    case CommonComponentOperation.Copy:
                        result = ExecuteCopyComponent(context, go);
                        break;
                    default:
                        result = ESVMCPCommandResult.Failed($"不支持的操作类型: {Operation}");
                        break;
                }

                // 自动保存记忆（如果启用saveToMemory）
                if (result.Success)
                {
                    PostExecute(result, context, go);
                }

                return result;
            }
            catch (Exception e)
            {
                return ESVMCPCommandResult.Failed($"组件操作失败: {e.Message}", e);
            }
        }

        // SaveOperationToMemory 已由基类PostExecute方法替代

        private ESVMCPCommandResult ExecuteAddComponent(GameObject go)
        {
            Type componentType = GetComponentTypeCached(Component);
            if (componentType == null)
            {
                return ESVMCPCommandResult.Failed($"未找到组件类型: {Component}");
            }

            UnityEngine.Component addedComponent = go.AddComponent(componentType);

            var output = new Dictionary<string, object>
            {
                { "operation", "Add" },
                { "componentType", componentType.Name },
                { "gameObject", go.name },
                { "componentInstanceId", addedComponent.GetInstanceID() }
            };

            return ESVMCPCommandResult.Succeed($"成功添加组件: {componentType.Name} -> {go.name}", output);
        }

        private ESVMCPCommandResult ExecuteRemoveComponent(GameObject go)
        {
            Type componentType = GetComponentTypeCached(Component);
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

            var output = new Dictionary<string, object>
            {
                { "operation", "Remove" },
                { "componentType", Component },
                { "gameObject", go.name },
                { "immediate", Immediate }
            };

            return ESVMCPCommandResult.Succeed($"成功移除组件: {Component} from {go.name}", output);
        }

        private ESVMCPCommandResult ExecuteGetComponent(GameObject go)
        {
            Type componentType = GetComponentTypeCached(Component);
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
                { "operation", "Get" },
                { "hasComponent", true },
                { "componentType", comp.GetType().Name },
                { "componentInstanceId", comp.GetInstanceID() },
                { "enabled", comp is Behaviour behaviour ? behaviour.enabled : (bool?)null },
                { "gameObject", go.name }
            };

            return ESVMCPCommandResult.Succeed($"找到组件: {comp.GetType().Name} on {go.name}", output);
        }

        private ESVMCPCommandResult ExecuteSetComponentEnabled(GameObject go)
        {
            Type componentType = GetComponentTypeCached(Component);
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
                bool previousState = behaviour.enabled;
                behaviour.enabled = Enabled.Value;

                var output = new Dictionary<string, object>
                {
                    { "operation", "SetEnabled" },
                    { "componentType", Component },
                    { "gameObject", go.name },
                    { "previousEnabled", previousState },
                    { "currentEnabled", Enabled.Value }
                };

                return ESVMCPCommandResult.Succeed($"成功设置组件启用状态: {Component} -> {Enabled.Value}", output);
            }
            else
            {
                return ESVMCPCommandResult.Failed($"组件 {Component} 不支持启用/禁用");
            }
        }

        private ESVMCPCommandResult ExecuteGetAllComponents(GameObject go)
        {
            var components = go.GetComponents<Component>();
            var componentInfo = new List<Dictionary<string, object>>();

            foreach (var comp in components)
            {
                if (comp != null)
                {
                    var info = new Dictionary<string, object>
                    {
                        { "type", comp.GetType().Name },
                        { "fullType", comp.GetType().FullName },
                        { "instanceId", comp.GetInstanceID() },
                        { "enabled", comp is Behaviour behaviour ? behaviour.enabled : (bool?)null }
                    };
                    componentInfo.Add(info);
                }
            }

            var output = new Dictionary<string, object>
            {
                { "operation", "GetAll" },
                { "gameObject", go.name },
                { "componentCount", componentInfo.Count },
                { "components", componentInfo }
            };

            return ESVMCPCommandResult.Succeed($"获取到 {componentInfo.Count} 个组件 from {go.name}", output);
        }

        private ESVMCPCommandResult ExecuteCopyComponent(ESVMCPExecutionContext context, GameObject targetGo)
        {
            // 使用新的目标解析器
            GameObject sourceGo = TargetResolver.Resolve(Source, context);
            if (sourceGo == null)
            {
                return ESVMCPCommandResult.Failed($"未找到源GameObject: {Source}");
            }

            // 智能类型适配
            sourceGo = TypeAdapter.ToGameObject(sourceGo);
            if (sourceGo == null)
            {
                return ESVMCPCommandResult.Failed($"无法将源转换为GameObject: {Source}");
            }

            Type componentType = GetComponentTypeCached(Component);
            if (componentType == null)
            {
                return ESVMCPCommandResult.Failed($"未找到组件类型: {Component}");
            }

            UnityEngine.Component sourceComp = sourceGo.GetComponent(componentType);
            if (sourceComp == null)
            {
                return ESVMCPCommandResult.Failed($"源对象 {sourceGo.name} 上没有组件 {Component}");
            }

            // 检查目标是否已有该组件
            UnityEngine.Component existingComp = targetGo.GetComponent(componentType);
            if (existingComp != null)
            {
                return ESVMCPCommandResult.Failed($"目标对象 {targetGo.name} 已有组件 {Component}");
            }

            // 复制组件
            UnityEngine.Component copiedComp = targetGo.AddComponent(componentType);

            // 复制可序列化属性（简单实现）
            if (copiedComp != null)
            {
                var output = new Dictionary<string, object>
                {
                    { "operation", "Copy" },
                    { "componentType", Component },
                    { "sourceGameObject", sourceGo.name },
                    { "targetGameObject", targetGo.name },
                    { "sourceComponentId", sourceComp.GetInstanceID() },
                    { "copiedComponentId", copiedComp.GetInstanceID() }
                };

                return ESVMCPCommandResult.Succeed($"成功复制组件: {Component} from {sourceGo.name} to {targetGo.name}", output);
            }
            else
            {
                return ESVMCPCommandResult.Failed($"复制组件失败");
            }
        }

        /// <summary>
        /// 缓存优化的类型获取方法
        /// </summary>
        private static Type GetComponentTypeCached(string typeName)
        {
            lock (_cacheLock)
            {
                if (_typeCache.TryGetValue(typeName, out Type cachedType))
                {
                    return cachedType;
                }

                Type type = GetComponentTypeUncached(typeName);
                _typeCache[typeName] = type;
                return type;
            }
        }

        /// <summary>
        /// 非缓存的类型获取方法
        /// </summary>
        private static Type GetComponentTypeUncached(string typeName)
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

                // 尝试UnityEngine命名空间下的类型
                type = assembly.GetType($"UnityEngine.{typeName}");
                if (type != null) return type;
            }

            return null;
        }
    }
}
