using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ES.VMCP
{
    /// <summary>
    /// 设置Transform完整属性命令
    /// </summary>
    [ESVMCPCommand("SetTransform", "设置GameObject的Transform属性")]
    public class SetTransformCommand : ESVMCPCommandBase
    {
        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonConverter(typeof(Vector3Converter))]
        [JsonProperty("position")]
        public Vector3? Position { get; set; }

        [JsonConverter(typeof(Vector3Converter))]
        [JsonProperty("rotation")]
        public Vector3? Rotation { get; set; }

        [JsonConverter(typeof(Vector3Converter))]
        [JsonProperty("scale")]
        public Vector3? Scale { get; set; }

        [JsonProperty("local")]
        public bool Local { get; set; } = true;

        public override string Description => $"设置Transform: {Target}";

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

                Transform transform = go.transform;

                if (Position.HasValue)
                {
                    if (Local)
                        transform.localPosition = Position.Value;
                    else
                        transform.position = Position.Value;
                }

                if (Rotation.HasValue)
                {
                    if (Local)
                        transform.localEulerAngles = Rotation.Value;
                    else
                        transform.eulerAngles = Rotation.Value;
                }

                if (Scale.HasValue)
                {
                    transform.localScale = Scale.Value;
                }

                return ESVMCPCommandResult.Succeed($"成功设置Transform: {go.name}");
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"设置Transform失败: {e.Message}", e);
            }
        }
    }

    /// <summary>
    /// 设置位置命令
    /// </summary>
    [ESVMCPCommand("SetPosition", "设置GameObject的位置")]
    public class SetPositionCommand : ESVMCPCommandBase
    {
        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonConverter(typeof(Vector3Converter))]
        [JsonProperty("position")]
        public Vector3 Position { get; set; }

        [JsonProperty("local")]
        public bool Local { get; set; } = true;

        public override string Description => $"设置位置: {Target} -> {Position}";

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

                if (Local)
                    go.transform.localPosition = Position;
                else
                    go.transform.position = Position;

                return ESVMCPCommandResult.Succeed($"成功设置位置: {go.name} -> {Position}");
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"设置位置失败: {e.Message}", e);
            }
        }
    }

    /// <summary>
    /// 设置旋转命令
    /// </summary>
    [ESVMCPCommand("SetRotation", "设置GameObject的旋转")]
    public class SetRotationCommand : ESVMCPCommandBase
    {
        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonConverter(typeof(Vector3Converter))]
        [JsonProperty("rotation")]
        public Vector3 Rotation { get; set; }

        [JsonProperty("local")]
        public bool Local { get; set; } = true;

        public override string Description => $"设置旋转: {Target} -> {Rotation}";

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

                if (Local)
                    go.transform.localEulerAngles = Rotation;
                else
                    go.transform.eulerAngles = Rotation;

                return ESVMCPCommandResult.Succeed($"成功设置旋转: {go.name} -> {Rotation}");
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"设置旋转失败: {e.Message}", e);
            }
        }
    }

    /// <summary>
    /// 设置缩放命令
    /// </summary>
    [ESVMCPCommand("SetScale", "设置GameObject的缩放")]
    public class SetScaleCommand : ESVMCPCommandBase
    {
        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonConverter(typeof(Vector3Converter))]
        [JsonProperty("scale")]
        public Vector3 Scale { get; set; }

        public override string Description => $"设置缩放: {Target} -> {Scale}";

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

                go.transform.localScale = Scale;

                return ESVMCPCommandResult.Succeed($"成功设置缩放: {go.name} -> {Scale}");
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"设置缩放失败: {e.Message}", e);
            }
        }
    }

    /// <summary>
    /// 设置父对象命令
    /// </summary>
    [ESVMCPCommand("SetParent", "设置GameObject的父对象")]
    public class SetParentCommand : ESVMCPCommandBase
    {
        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("parent")]
        public string Parent { get; set; }

        [JsonProperty("worldPositionStays")]
        public bool WorldPositionStays { get; set; } = true;

        public override string Description => $"设置父对象: {Target} -> {Parent}";

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
                    return ESVMCPCommandResult.Failed($"未找到目标GameObject: {Target}");
                }

                Transform parentTransform = null;
                if (!string.IsNullOrEmpty(Parent))
                {
                    GameObject parentGo = ResolveGameObject(Parent, context);
                    if (parentGo == null)
                    {
                        return ESVMCPCommandResult.Failed($"未找到父GameObject: {Parent}");
                    }
                    parentTransform = parentGo.transform;
                }

                go.transform.SetParent(parentTransform, WorldPositionStays);

                string parentName = parentTransform != null ? parentTransform.name : "null (根节点)";
                return ESVMCPCommandResult.Succeed($"成功设置父对象: {go.name} -> {parentName}");
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"设置父对象失败: {e.Message}", e);
            }
        }
    }

    /// <summary>
    /// 看向目标命令
    /// </summary>
    [ESVMCPCommand("LookAt", "让GameObject看向目标")]
    public class LookAtCommand : ESVMCPCommandBase
    {
        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonConverter(typeof(Vector3Converter))]
        [JsonProperty("worldUp")]
        public Vector3 WorldUp { get; set; } = Vector3.up;

        public override string Description => $"看向: {Source} -> {Target}";

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Source) || string.IsNullOrEmpty(Target))
            {
                return ESVMCPValidationResult.Failure("源和目标GameObject不能为空");
            }
            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                GameObject sourceGo = ResolveGameObject(Source, context);
                GameObject targetGo = ResolveGameObject(Target, context);

                if (sourceGo == null)
                {
                    return ESVMCPCommandResult.Failed($"未找到源GameObject: {Source}");
                }

                if (targetGo == null)
                {
                    return ESVMCPCommandResult.Failed($"未找到目标GameObject: {Target}");
                }

                sourceGo.transform.LookAt(targetGo.transform, WorldUp);

                return ESVMCPCommandResult.Succeed($"成功让 {sourceGo.name} 看向 {targetGo.name}");
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"LookAt失败: {e.Message}", e);
            }
        }
    }
}
