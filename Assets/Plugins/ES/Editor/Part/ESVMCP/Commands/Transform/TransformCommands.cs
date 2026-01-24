using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ES.VMCP
{
    /// <summary>
    /// 通用Transform操作类型（基础操作）
    /// 注意：高级或特化操作应创建独立的Command类，不应继续扩展此枚举
    /// </summary>
    public enum CommonTransformOperation
    {
        SetTransform,   // 设置完整Transform属性
        SetPosition,    // 设置位置
        SetRotation,    // 设置旋转
        SetScale,       // 设置缩放
        SetParent,      // 设置父对象
        LookAt          // 看向目标
    }

    /// <summary>
    /// 统一的Transform操作命令 - 合并所有Transform相关操作
    /// </summary>
    [ESVMCPCommand("CommonTransformOperation", "统一的Transform操作命令，支持位置、旋转、缩放、父对象、LookAt等操作")]
    public class TransformOperationCommand : ESVMCPCommandBase
    {
        [JsonProperty("operation")]
        public CommonTransformOperation Operation { get; set; }

        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; } // 用于LookAt操作的源对象

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

        [JsonProperty("worldPositionStays")]
        public bool WorldPositionStays { get; set; } = true;

        [JsonConverter(typeof(Vector3Converter))]
        [JsonProperty("worldUp")]
        public Vector3 WorldUp { get; set; } = Vector3.up;

        [JsonProperty("local")]
        public bool Local { get; set; } = true;

        public override string Description
        {
            get
            {
                switch (Operation)
                {
                    case CommonTransformOperation.SetTransform:
                        return $"设置Transform: {Target}";
                    case CommonTransformOperation.SetPosition:
                        return $"设置位置: {Target} -> {Position}";
                    case CommonTransformOperation.SetRotation:
                        return $"设置旋转: {Target} -> {Rotation}";
                    case CommonTransformOperation.SetScale:
                        return $"设置缩放: {Target} -> {Scale}";
                    case CommonTransformOperation.SetParent:
                        return $"设置父对象: {Target} -> {Parent}";
                    case CommonTransformOperation.LookAt:
                        return $"看向: {Source} -> {Target}";
                    default:
                        return $"Transform操作: {Operation}";
                }
            }
        }

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Target) && Operation != CommonTransformOperation.LookAt)
            {
                return ESVMCPValidationResult.Failure("目标GameObject不能为空");
            }

            if (Operation == CommonTransformOperation.LookAt && (string.IsNullOrEmpty(Source) || string.IsNullOrEmpty(Target)))
            {
                return ESVMCPValidationResult.Failure("LookAt操作需要指定源和目标GameObject");
            }

            switch (Operation)
            {
                case CommonTransformOperation.SetPosition:
                    if (!Position.HasValue)
                        return ESVMCPValidationResult.Failure("设置位置时必须指定position参数");
                    break;
                case CommonTransformOperation.SetRotation:
                    if (!Rotation.HasValue)
                        return ESVMCPValidationResult.Failure("设置旋转时必须指定rotation参数");
                    break;
                case CommonTransformOperation.SetScale:
                    if (!Scale.HasValue)
                        return ESVMCPValidationResult.Failure("设置缩放时必须指定scale参数");
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
                    case CommonTransformOperation.SetTransform:
                        result = ExecuteSetTransform(context, out targetGo);
                        break;
                    case CommonTransformOperation.SetPosition:
                        result = ExecuteSetPosition(context, out targetGo);
                        break;
                    case CommonTransformOperation.SetRotation:
                        result = ExecuteSetRotation(context, out targetGo);
                        break;
                    case CommonTransformOperation.SetScale:
                        result = ExecuteSetScale(context, out targetGo);
                        break;
                    case CommonTransformOperation.SetParent:
                        result = ExecuteSetParent(context, out targetGo);
                        break;
                    case CommonTransformOperation.LookAt:
                        result = ExecuteLookAt(context, out targetGo);
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
                return ESVMCPCommandResult.Failed($"Transform操作失败: {e.Message}", e);
            }
        }

        private ESVMCPCommandResult ExecuteSetTransform(ESVMCPExecutionContext context, out GameObject targetGo)
        {
            targetGo = TargetResolver.Resolve(Target, context);
            if (targetGo == null)
            {
                return ESVMCPCommandResult.Failed($"未找到GameObject: {Target}");
            }

            // 智能类型适配
            targetGo = TypeAdapter.ToGameObject(targetGo);
            if (targetGo == null)
            {
                return ESVMCPCommandResult.Failed($"无法将目标转换为GameObject: {Target}");
            }

            Transform transform = targetGo.transform;

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

            return ESVMCPCommandResult.Succeed($"成功设置Transform: {targetGo.name}");
        }

        private ESVMCPCommandResult ExecuteSetPosition(ESVMCPExecutionContext context, out GameObject targetGo)
        {
            targetGo = TargetResolver.Resolve(Target, context);
            if (targetGo == null)
            {
                return ESVMCPCommandResult.Failed($"未找到GameObject: {Target}");
            }

            targetGo = TypeAdapter.ToGameObject(targetGo);
            if (targetGo == null)
            {
                return ESVMCPCommandResult.Failed($"无法转换目标: {Target}");
            }

            if (Local)
                targetGo.transform.localPosition = Position.Value;
            else
                targetGo.transform.position = Position.Value;

            return ESVMCPCommandResult.Succeed($"成功设置位置: {targetGo.name} -> {Position.Value}");
        }

        private ESVMCPCommandResult ExecuteSetRotation(ESVMCPExecutionContext context, out GameObject targetGo)
        {
            targetGo = TargetResolver.Resolve(Target, context);
            if (targetGo == null)
            {
                return ESVMCPCommandResult.Failed($"未找到GameObject: {Target}");
            }

            targetGo = TypeAdapter.ToGameObject(targetGo);
            if (targetGo == null)
            {
                return ESVMCPCommandResult.Failed($"无法转换目标: {Target}");
            }

            if (Local)
                targetGo.transform.localEulerAngles = Rotation.Value;
            else
                targetGo.transform.eulerAngles = Rotation.Value;

            return ESVMCPCommandResult.Succeed($"成功设置旋转: {targetGo.name} -> {Rotation.Value}");
        }

        private ESVMCPCommandResult ExecuteSetScale(ESVMCPExecutionContext context, out GameObject targetGo)
        {
            targetGo = TargetResolver.Resolve(Target, context);
            if (targetGo == null)
            {
                return ESVMCPCommandResult.Failed($"未找到GameObject: {Target}");
            }

            targetGo = TypeAdapter.ToGameObject(targetGo);
            if (targetGo == null)
            {
                return ESVMCPCommandResult.Failed($"无法转换目标: {Target}");
            }

            targetGo.transform.localScale = Scale.Value;

            return ESVMCPCommandResult.Succeed($"成功设置缩放: {targetGo.name} -> {Scale.Value}");
        }

        private ESVMCPCommandResult ExecuteSetParent(ESVMCPExecutionContext context, out GameObject targetGo)
        {
            targetGo = TargetResolver.Resolve(Target, context);
            if (targetGo == null)
            {
                return ESVMCPCommandResult.Failed($"未找到目标GameObject: {Target}");
            }

            targetGo = TypeAdapter.ToGameObject(targetGo);
            if (targetGo == null)
            {
                return ESVMCPCommandResult.Failed($"无法转换目标: {Target}");
            }

            Transform parentTransform = null;
            if (!string.IsNullOrEmpty(Parent))
            {
                GameObject parentGo = TargetResolver.Resolve(Parent, context);
                if (parentGo == null)
                {
                    return ESVMCPCommandResult.Failed($"未找到父GameObject: {Parent}");
                }
                parentGo = TypeAdapter.ToGameObject(parentGo);
                parentTransform = parentGo.transform;
            }

            targetGo.transform.SetParent(parentTransform, WorldPositionStays);

            string parentName = parentTransform != null ? parentTransform.name : "null (根节点)";
            return ESVMCPCommandResult.Succeed($"成功设置父对象: {targetGo.name} -> {parentName}");
        }

        private ESVMCPCommandResult ExecuteLookAt(ESVMCPExecutionContext context, out GameObject targetGo)
        {
            GameObject sourceGo = TargetResolver.Resolve(Source, context);
            targetGo = TargetResolver.Resolve(Target, context);

            if (sourceGo == null)
            {
                targetGo = null;
                return ESVMCPCommandResult.Failed($"未找到源GameObject: {Source}");
            }

            if (targetGo == null)
            {
                return ESVMCPCommandResult.Failed($"未找到目标GameObject: {Target}");
            }

            // 智能类型适配
            sourceGo = TypeAdapter.ToGameObject(sourceGo);
            targetGo = TypeAdapter.ToGameObject(targetGo);

            if (sourceGo == null || targetGo == null)
            {
                return ESVMCPCommandResult.Failed("无法转换源或目标对象");
            }

            sourceGo.transform.LookAt(targetGo.transform, WorldUp);

            return ESVMCPCommandResult.Succeed($"成功让 {sourceGo.name} 看向 {targetGo.name}");
        }
    }
}
