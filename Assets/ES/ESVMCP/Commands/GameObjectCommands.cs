using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ES.VMCP
{
    /// <summary>
    /// 创建GameObject命令
    /// </summary>
    [ESVMCPCommand("CreateGameObject", "创建新的GameObject")]
    public class CreateGameObjectCommand : ESVMCPCommandBase
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("parent")]
        public string Parent { get; set; }

        [JsonConverter(typeof(Vector3Converter))]
        [JsonProperty("position")]
        public Vector3 Position { get; set; } = Vector3.zero;

        [JsonConverter(typeof(Vector3Converter))]
        [JsonProperty("rotation")]
        public Vector3 Rotation { get; set; } = Vector3.zero;

        [JsonConverter(typeof(Vector3Converter))]
        [JsonProperty("scale")]
        public Vector3 Scale { get; set; } = Vector3.one;

        [JsonProperty("tag")]
        public string Tag { get; set; }

        [JsonProperty("layer")]
        public int Layer { get; set; } = 0;

        [JsonProperty("addMeshRenderer")]
        public bool AddMeshRenderer { get; set; } = false;

        public override string Description => $"创建GameObject: {Name}";

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Name))
            {
                return ESVMCPValidationResult.Failure("GameObject名称不能为空");
            }
            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                // 创建GameObject
                GameObject go = new GameObject(Name);

                // 设置父对象
                if (!string.IsNullOrEmpty(Parent))
                {
                    GameObject parentGo = ResolveGameObject(Parent, context);
                    if (parentGo != null)
                    {
                        go.transform.SetParent(parentGo.transform);
                    }
                }

                // 设置Transform
                go.transform.localPosition = Position;
                go.transform.localEulerAngles = Rotation;
                go.transform.localScale = Scale;

                // 设置Tag
                if (!string.IsNullOrEmpty(Tag))
                {
                    go.tag = Tag;
                }

                // 设置Layer
                go.layer = Layer;

                // 添加MeshRenderer（用于可视化）
                if (AddMeshRenderer)
                {
                    go.AddComponent<MeshFilter>();
                    go.AddComponent<MeshRenderer>();
                }

                // 保存到场景记忆
                if (context.SceneMemory != null && !string.IsNullOrEmpty(Id))
                {
                    context.SceneMemory.SaveGameObjectReference(Id, go);
                }

                // 返回结果
                var output = new Dictionary<string, object>
                {
                    { "gameObjectId", go.GetInstanceID().ToString() },
                    { "name", go.name },
                    { "position", go.transform.position },
                    { "instanceId", go.GetInstanceID() }
                };

                return ESVMCPCommandResult.Succeed($"成功创建GameObject: {Name}", output);
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"创建GameObject失败: {e.Message}", e);
            }
        }
    }

    /// <summary>
    /// 销毁GameObject命令
    /// </summary>
    [ESVMCPCommand("DestroyGameObject", "销毁GameObject", isDangerous: true)]
    public class DestroyGameObjectCommand : ESVMCPCommandBase
    {
        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("immediate")]
        public bool Immediate { get; set; } = false;

        public override string Description => $"销毁GameObject: {Target}";
        public override bool IsDangerous => true;

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Target))
            {
                return ESVMCPValidationResult.Failure("目标GameObject不能为空");
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

                string objectName = go.name;

                if (Immediate)
                {
                    Object.DestroyImmediate(go);
                }
                else
                {
                    Object.Destroy(go);
                }

                // 从记忆中移除引用
                if (context.SceneMemory != null)
                {
                    context.SceneMemory.RemoveMemory(Target);
                }

                return ESVMCPCommandResult.Succeed($"成功销毁GameObject: {objectName}");
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"销毁GameObject失败: {e.Message}", e);
            }
        }
    }

    /// <summary>
    /// 查找GameObject命令
    /// </summary>
    [ESVMCPCommand("FindGameObject", "查找场景中的GameObject")]
    public class FindGameObjectCommand : ESVMCPCommandBase
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("tag")]
        public string Tag { get; set; }

        [JsonProperty("saveToMemory")]
        public bool SaveToMemory { get; set; } = true;

        public override string Description => $"查找GameObject: {Name ?? Tag}";

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Name) && string.IsNullOrEmpty(Tag))
            {
                return ESVMCPValidationResult.Failure("必须指定name或tag");
            }
            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                GameObject go = null;

                if (!string.IsNullOrEmpty(Name))
                {
                    go = GameObject.Find(Name);
                }
                else if (!string.IsNullOrEmpty(Tag))
                {
                    go = GameObject.FindGameObjectWithTag(Tag);
                }

                if (go == null)
                {
                    return ESVMCPCommandResult.Failed($"未找到GameObject: {Name ?? Tag}");
                }

                // 保存到记忆
                if (SaveToMemory && context.SceneMemory != null && !string.IsNullOrEmpty(Id))
                {
                    context.SceneMemory.SaveGameObjectReference(Id, go);
                }

                var output = new Dictionary<string, object>
                {
                    { "gameObjectId", go.GetInstanceID().ToString() },
                    { "name", go.name },
                    { "tag", go.tag },
                    { "position", go.transform.position },
                    { "instanceId", go.GetInstanceID() }
                };

                return ESVMCPCommandResult.Succeed($"找到GameObject: {go.name}", output);
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"查找GameObject失败: {e.Message}", e);
            }
        }
    }

    /// <summary>
    /// 设置GameObject激活状态命令
    /// </summary>
    [ESVMCPCommand("SetActiveGameObject", "设置GameObject的激活状态")]
    public class SetActiveGameObjectCommand : ESVMCPCommandBase
    {
        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("active")]
        public bool Active { get; set; }

        public override string Description => $"设置GameObject激活状态: {Target} -> {Active}";

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Target))
            {
                return ESVMCPValidationResult.Failure("目标GameObject不能为空");
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

                go.SetActive(Active);

                return ESVMCPCommandResult.Succeed($"成功设置GameObject激活状态: {go.name} -> {Active}");
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"设置激活状态失败: {e.Message}", e);
            }
        }
    }

    /// <summary>
    /// 克隆GameObject命令
    /// </summary>
    [ESVMCPCommand("CloneGameObject", "克隆GameObject")]
    public class CloneGameObjectCommand : ESVMCPCommandBase
    {
        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("parent")]
        public string Parent { get; set; }

        [JsonConverter(typeof(Vector3Converter))]
        [JsonProperty("position")]
        public Vector3? Position { get; set; }

        public override string Description => $"克隆GameObject: {Source} -> {Name}";

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Source))
            {
                return ESVMCPValidationResult.Failure("源GameObject不能为空");
            }
            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                GameObject sourceGo = ResolveGameObject(Source, context);
                if (sourceGo == null)
                {
                    return ESVMCPCommandResult.Failed($"未找到源GameObject: {Source}");
                }

                GameObject clonedGo = Object.Instantiate(sourceGo);

                // 设置名称
                if (!string.IsNullOrEmpty(Name))
                {
                    clonedGo.name = Name;
                }

                // 设置父对象
                if (!string.IsNullOrEmpty(Parent))
                {
                    GameObject parentGo = ResolveGameObject(Parent, context);
                    if (parentGo != null)
                    {
                        clonedGo.transform.SetParent(parentGo.transform);
                    }
                }

                // 设置位置
                if (Position.HasValue)
                {
                    clonedGo.transform.position = Position.Value;
                }

                // 保存到记忆
                if (context.SceneMemory != null && !string.IsNullOrEmpty(Id))
                {
                    context.SceneMemory.SaveGameObjectReference(Id, clonedGo);
                }

                var output = new Dictionary<string, object>
                {
                    { "gameObjectId", clonedGo.GetInstanceID().ToString() },
                    { "name", clonedGo.name },
                    { "instanceId", clonedGo.GetInstanceID() }
                };

                return ESVMCPCommandResult.Succeed($"成功克隆GameObject: {clonedGo.name}", output);
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"克隆GameObject失败: {e.Message}", e);
            }
        }
    }
}
