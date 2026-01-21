using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ES.VMCP
{
    /// <summary>
    /// 通用GameObject操作类型（基础操作）
    /// 注意：高级或特化操作应创建独立的Command类，不应继续扩展此枚举
    /// </summary>
    public enum CommonGameObjectOperation
    {
        Create,             // 创建GameObject
        Destroy,            // 销毁GameObject
        SetActive,          // 设置激活状态
        Rename,             // 重命名
        SetTag,             // 设置Tag
        SetLayer,           // 设置Layer
        Duplicate,          // 复制GameObject
        FindByName,         // 按名称查找
        FindByTag,          // 按Tag查找
        FindInChildren,     // 在子对象中查找
        GetChildren,        // 获取所有子对象
        GetParent           // 获取父对象
    }

    /// <summary>
    /// 统一的GameObject操作命令
    /// </summary>
    [ESVMCPCommand("CommonGameObjectOperation", "统一的GameObject操作命令，支持创建、销毁、激活、重命名、Tag、Layer等")]
    public class GameObjectOperationCommand : ESVMCPCommandBase
    {
        [JsonProperty("operation")]
        public CommonGameObjectOperation Operation { get; set; }

        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("tag")]
        public string Tag { get; set; }

        [JsonProperty("layer")]
        public int? Layer { get; set; }

        [JsonProperty("active")]
        public bool Active { get; set; } = true;

        [JsonProperty("parent")]
        public string Parent { get; set; }

        [JsonConverter(typeof(Vector3Converter))]
        [JsonProperty("position")]
        public Vector3? Position { get; set; }

        [JsonProperty("primitiveType")]
        public PrimitiveType? PrimitiveType { get; set; }

        public override string Description
        {
            get
            {
                switch (Operation)
                {
                    case CommonGameObjectOperation.Create:
                        return $"创建GameObject: {Name ?? "New GameObject"}";
                    case CommonGameObjectOperation.Destroy:
                        return $"销毁GameObject: {Target}";
                    case CommonGameObjectOperation.SetActive:
                        return $"设置激活状态: {Target} -> {Active}";
                    case CommonGameObjectOperation.Rename:
                        return $"重命名: {Target} -> {Name}";
                    case CommonGameObjectOperation.SetTag:
                        return $"设置Tag: {Target} -> {Tag}";
                    case CommonGameObjectOperation.SetLayer:
                        return $"设置Layer: {Target} -> {Layer}";
                    case CommonGameObjectOperation.Duplicate:
                        return $"复制GameObject: {Target}";
                    case CommonGameObjectOperation.FindByName:
                        return $"查找GameObject: {Name}";
                    case CommonGameObjectOperation.FindByTag:
                        return $"查找Tag: {Tag}";
                    default:
                        return $"GameObject操作: {Operation}";
                }
            }
        }

        public override ESVMCPValidationResult Validate()
        {
            switch (Operation)
            {
                case CommonGameObjectOperation.Create:
                    // Create操作不需要target
                    break;
                case CommonGameObjectOperation.Rename:
                    if (string.IsNullOrEmpty(Name))
                        return ESVMCPValidationResult.Failure("重命名操作需要指定name参数");
                    goto default;
                case CommonGameObjectOperation.SetTag:
                    if (string.IsNullOrEmpty(Tag))
                        return ESVMCPValidationResult.Failure("设置Tag操作需要指定tag参数");
                    goto default;
                case CommonGameObjectOperation.SetLayer:
                    if (!Layer.HasValue)
                        return ESVMCPValidationResult.Failure("设置Layer操作需要指定layer参数");
                    goto default;
                case CommonGameObjectOperation.FindByName:
                    if (string.IsNullOrEmpty(Name))
                        return ESVMCPValidationResult.Failure("按名称查找需要指定name参数");
                    break;
                case CommonGameObjectOperation.FindByTag:
                    if (string.IsNullOrEmpty(Tag))
                        return ESVMCPValidationResult.Failure("按Tag查找需要指定tag参数");
                    break;
                default:
                    if (string.IsNullOrEmpty(Target))
                        return ESVMCPValidationResult.Failure("目标GameObject不能为空");
                    break;
            }

            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                ESVMCPCommandResult result;
                GameObject targetGo = null;

                switch (Operation)
                {
                    case CommonGameObjectOperation.Create:
                        result = ExecuteCreate(context, out targetGo);
                        break;
                    case CommonGameObjectOperation.Destroy:
                        result = ExecuteDestroy(context, out targetGo);
                        break;
                    case CommonGameObjectOperation.SetActive:
                        result = ExecuteSetActive(context, out targetGo);
                        break;
                    case CommonGameObjectOperation.Rename:
                        result = ExecuteRename(context, out targetGo);
                        break;
                    case CommonGameObjectOperation.SetTag:
                        result = ExecuteSetTag(context, out targetGo);
                        break;
                    case CommonGameObjectOperation.SetLayer:
                        result = ExecuteSetLayer(context, out targetGo);
                        break;
                    case CommonGameObjectOperation.Duplicate:
                        result = ExecuteDuplicate(context, out targetGo);
                        break;
                    case CommonGameObjectOperation.FindByName:
                        result = ExecuteFindByName(context, out targetGo);
                        break;
                    case CommonGameObjectOperation.FindByTag:
                        result = ExecuteFindByTag(context, out targetGo);
                        break;
                    case CommonGameObjectOperation.FindInChildren:
                        result = ExecuteFindInChildren(context, out targetGo);
                        break;
                    case CommonGameObjectOperation.GetChildren:
                        result = ExecuteGetChildren(context, out targetGo);
                        break;
                    case CommonGameObjectOperation.GetParent:
                        result = ExecuteGetParent(context, out targetGo);
                        break;
                    default:
                        result = ESVMCPCommandResult.Failed($"不支持的操作类型: {Operation}");
                        break;
                }

                // 自动保存记忆
                if (result.Success && targetGo != null)
                {
                    PostExecute(result, context, targetGo);
                }

                return result;
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"GameObject操作失败: {e.Message}", e);
            }
        }

        private ESVMCPCommandResult ExecuteCreate(ESVMCPExecutionContext context, out GameObject targetGo)
        {
            string objName = Name ?? "New GameObject";

            if (PrimitiveType.HasValue)
            {
                targetGo = GameObject.CreatePrimitive(PrimitiveType.Value);
                targetGo.name = objName;
            }
            else
            {
                targetGo = new GameObject(objName);
            }

            // 设置位置
            if (Position.HasValue)
            {
                targetGo.transform.position = Position.Value;
            }

            // 设置父对象
            if (!string.IsNullOrEmpty(Parent))
            {
                GameObject parentGo = TargetResolver.Resolve(Parent, context);
                if (parentGo != null)
                {
                    targetGo.transform.SetParent(parentGo.transform);
                }
            }

            return ESVMCPCommandResult.Succeed($"成功创建GameObject: {targetGo.name}", new Dictionary<string, object>
            {
                ["name"] = targetGo.name,
                ["instanceID"] = targetGo.GetInstanceID()
            });
        }

        private ESVMCPCommandResult ExecuteDestroy(ESVMCPExecutionContext context, out GameObject targetGo)
        {
            targetGo = TargetResolver.Resolve(Target, context);
            if (targetGo == null)
            {
                return ESVMCPCommandResult.Failed($"未找到GameObject: {Target}");
            }

            string name = targetGo.name;
            Object.DestroyImmediate(targetGo);
            targetGo = null;

            return ESVMCPCommandResult.Succeed($"成功销毁GameObject: {name}");
        }

        private ESVMCPCommandResult ExecuteSetActive(ESVMCPExecutionContext context, out GameObject targetGo)
        {
            targetGo = TargetResolver.Resolve(Target, context);
            if (targetGo == null)
            {
                return ESVMCPCommandResult.Failed($"未找到GameObject: {Target}");
            }

            targetGo.SetActive(Active);
            return ESVMCPCommandResult.Succeed($"成功设置激活状态: {targetGo.name} -> {Active}");
        }

        private ESVMCPCommandResult ExecuteRename(ESVMCPExecutionContext context, out GameObject targetGo)
        {
            targetGo = TargetResolver.Resolve(Target, context);
            if (targetGo == null)
            {
                return ESVMCPCommandResult.Failed($"未找到GameObject: {Target}");
            }

            string oldName = targetGo.name;
            targetGo.name = Name;
            return ESVMCPCommandResult.Succeed($"成功重命名: {oldName} -> {Name}");
        }

        private ESVMCPCommandResult ExecuteSetTag(ESVMCPExecutionContext context, out GameObject targetGo)
        {
            targetGo = TargetResolver.Resolve(Target, context);
            if (targetGo == null)
            {
                return ESVMCPCommandResult.Failed($"未找到GameObject: {Target}");
            }

            targetGo.tag = Tag;
            return ESVMCPCommandResult.Succeed($"成功设置Tag: {targetGo.name} -> {Tag}");
        }

        private ESVMCPCommandResult ExecuteSetLayer(ESVMCPExecutionContext context, out GameObject targetGo)
        {
            targetGo = TargetResolver.Resolve(Target, context);
            if (targetGo == null)
            {
                return ESVMCPCommandResult.Failed($"未找到GameObject: {Target}");
            }

            targetGo.layer = Layer.Value;
            return ESVMCPCommandResult.Succeed($"成功设置Layer: {targetGo.name} -> {Layer.Value}");
        }

        private ESVMCPCommandResult ExecuteDuplicate(ESVMCPExecutionContext context, out GameObject targetGo)
        {
            GameObject original = TargetResolver.Resolve(Target, context);
            if (original == null)
            {
                targetGo = null;
                return ESVMCPCommandResult.Failed($"未找到GameObject: {Target}");
            }

            targetGo = Object.Instantiate(original);
            targetGo.name = Name ?? (original.name + " (Clone)");

            return ESVMCPCommandResult.Succeed($"成功复制GameObject: {original.name} -> {targetGo.name}");
        }

        private ESVMCPCommandResult ExecuteFindByName(ESVMCPExecutionContext context, out GameObject targetGo)
        {
            targetGo = GameObject.Find(Name);
            if (targetGo == null)
            {
                return ESVMCPCommandResult.Failed($"未找到名为 {Name} 的GameObject");
            }

            return ESVMCPCommandResult.Succeed($"找到GameObject: {targetGo.name}");
        }

        private ESVMCPCommandResult ExecuteFindByTag(ESVMCPExecutionContext context, out GameObject targetGo)
        {
            targetGo = GameObject.FindGameObjectWithTag(Tag);
            if (targetGo == null)
            {
                return ESVMCPCommandResult.Failed($"未找到Tag为 {Tag} 的GameObject");
            }

            return ESVMCPCommandResult.Succeed($"找到GameObject: {targetGo.name} (Tag: {Tag})");
        }

        private ESVMCPCommandResult ExecuteFindInChildren(ESVMCPExecutionContext context, out GameObject targetGo)
        {
            GameObject parent = TargetResolver.Resolve(Target, context);
            if (parent == null)
            {
                targetGo = null;
                return ESVMCPCommandResult.Failed($"未找到父对象: {Target}");
            }

            Transform child = parent.transform.Find(Name);
            if (child == null)
            {
                targetGo = null;
                return ESVMCPCommandResult.Failed($"在 {parent.name} 的子对象中未找到 {Name}");
            }

            targetGo = child.gameObject;
            return ESVMCPCommandResult.Succeed($"找到子对象: {targetGo.name}");
        }

        private ESVMCPCommandResult ExecuteGetChildren(ESVMCPExecutionContext context, out GameObject targetGo)
        {
            targetGo = TargetResolver.Resolve(Target, context);
            if (targetGo == null)
            {
                return ESVMCPCommandResult.Failed($"未找到GameObject: {Target}");
            }

            var children = new List<object>();
            foreach (Transform child in targetGo.transform)
            {
                children.Add(new { name = child.name, instanceID = child.gameObject.GetInstanceID() });
            }

            return ESVMCPCommandResult.Succeed($"获取 {targetGo.name} 的 {children.Count} 个子对象", new Dictionary<string, object>
            {
                ["children"] = children
            });
        }

        private ESVMCPCommandResult ExecuteGetParent(ESVMCPExecutionContext context, out GameObject targetGo)
        {
            targetGo = TargetResolver.Resolve(Target, context);
            if (targetGo == null)
            {
                return ESVMCPCommandResult.Failed($"未找到GameObject: {Target}");
            }

            Transform parent = targetGo.transform.parent;
            if (parent == null)
            {
                return ESVMCPCommandResult.Succeed($"{targetGo.name} 没有父对象（是根对象）");
            }

            GameObject parentGo = parent.gameObject;
            return ESVMCPCommandResult.Succeed($"父对象: {parentGo.name}", new Dictionary<string, object>
            {
                ["name"] = parentGo.name,
                ["instanceID"] = parentGo.GetInstanceID()
            });
        }
    }
}
