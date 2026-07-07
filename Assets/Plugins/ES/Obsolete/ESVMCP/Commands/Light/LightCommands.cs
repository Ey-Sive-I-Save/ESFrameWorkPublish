using UnityEngine;
using Newtonsoft.Json;
using System;

namespace ES.VMCP
{
    /// <summary>
    /// 通用Light操作类型
    /// </summary>
    public enum CommonLightOperation
    {
        SetIntensity,   // 设置强度
        SetColor,       // 设置颜色
        SetType,        // 设置类型(方向光/点光源/聚光灯)
        SetRange,       // 设置范围
        SetSpotAngle,   // 设置聚光灯角度
        SetShadowType,  // 设置阴影类型
        Enable,         // 启用光源
        Disable,        // 禁用光源
        GetProperties   // 获取光源属性
    }

    /// <summary>
    /// 光照类型
    /// </summary>
    public enum ESVMCPLightType
    {
        Directional = 1,  // 方向光
        Point = 2,        // 点光源
        Spot = 0          // 聚光灯
    }

    /// <summary>
    /// 阴影类型
    /// </summary>
    public enum ESVMCPShadowType
    {
        None = 0,       // 无阴影
        Hard = 1,       // 硬阴影
        Soft = 2        // 软阴影
    }

    /// <summary>
    /// 统一的光照操作命令
    /// </summary>
    [ESVMCPCommand("CommonLightOperation", "统一的光照操作命令，支持设置光源强度、颜色、类型等")]
    public class LightOperationCommand : ESVMCPCommandBase
    {
        [JsonProperty("operation")]
        public CommonLightOperation Operation { get; set; }

        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("intensity")]
        public float? Intensity { get; set; }

        [JsonConverter(typeof(ColorConverter))]
        [JsonProperty("color")]
        public Color? Color { get; set; }

        [JsonProperty("lightType")]
        public ESVMCPLightType? LightType { get; set; }

        [JsonProperty("range")]
        public float? Range { get; set; }

        [JsonProperty("spotAngle")]
        public float? SpotAngle { get; set; }

        [JsonProperty("shadowType")]
        public ESVMCPShadowType? ShadowType { get; set; }

        public override string Description
        {
            get
            {
                switch (Operation)
                {
                    case CommonLightOperation.SetIntensity:
                        return $"设置光源强度: {Target} -> {Intensity}";
                    case CommonLightOperation.SetColor:
                        return $"设置光源颜色: {Target} -> {Color}";
                    case CommonLightOperation.SetType:
                        return $"设置光源类型: {Target} -> {LightType}";
                    case CommonLightOperation.SetRange:
                        return $"设置光源范围: {Target} -> {Range}";
                    case CommonLightOperation.SetSpotAngle:
                        return $"设置聚光灯角度: {Target} -> {SpotAngle}";
                    case CommonLightOperation.SetShadowType:
                        return $"设置阴影类型: {Target} -> {ShadowType}";
                    case CommonLightOperation.Enable:
                        return $"启用光源: {Target}";
                    case CommonLightOperation.Disable:
                        return $"禁用光源: {Target}";
                    case CommonLightOperation.GetProperties:
                        return $"获取光源属性: {Target}";
                    default:
                        return $"光照操作: {Operation}";
                }
            }
        }

        public override bool IsDangerous => false;

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Target))
            {
                return ESVMCPValidationResult.Failure("目标GameObject不能为空");
            }

            switch (Operation)
            {
                case CommonLightOperation.SetIntensity:
                    if (!Intensity.HasValue)
                        return ESVMCPValidationResult.Failure("intensity参数不能为空");
                    if (Intensity.Value < 0)
                        return ESVMCPValidationResult.Failure("intensity不能为负数");
                    break;

                case CommonLightOperation.SetColor:
                    if (Color == null)
                        return ESVMCPValidationResult.Failure("color参数不能为空");
                    break;

                case CommonLightOperation.SetType:
                    if (!LightType.HasValue)
                        return ESVMCPValidationResult.Failure("lightType参数不能为空");
                    break;

                case CommonLightOperation.SetRange:
                    if (!Range.HasValue)
                        return ESVMCPValidationResult.Failure("range参数不能为空");
                    if (Range.Value <= 0)
                        return ESVMCPValidationResult.Failure("range必须大于0");
                    break;

                case CommonLightOperation.SetSpotAngle:
                    if (!SpotAngle.HasValue)
                        return ESVMCPValidationResult.Failure("spotAngle参数不能为空");
                    if (SpotAngle.Value <= 0 || SpotAngle.Value > 179)
                        return ESVMCPValidationResult.Failure("spotAngle必须在1-179之间");
                    break;

                case CommonLightOperation.SetShadowType:
                    if (!ShadowType.HasValue)
                        return ESVMCPValidationResult.Failure("shadowType参数不能为空");
                    break;
            }

            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                // 解析目标对象
                GameObject targetObj = ResolveTarget(Target, context);
                if (targetObj == null)
                {
                    return ESVMCPCommandResult.Failed($"找不到目标对象: {Target}");
                }

                // 获取或添加Light组件
                Light lightComponent = targetObj.GetComponent<Light>();
                if (lightComponent == null)
                {
                    if (Operation == CommonLightOperation.GetProperties)
                    {
                        return ESVMCPCommandResult.Failed($"对象 {Target} 没有Light组件");
                    }
                    lightComponent = targetObj.AddComponent<Light>();
                    Debug.Log($"[ESVMCP] 为对象 {Target} 添加了Light组件");
                }

                // 执行操作
                switch (Operation)
                {
                    case CommonLightOperation.SetIntensity:
                        lightComponent.intensity = Intensity.Value;
                        Debug.Log($"[ESVMCP] 设置光源强度: {Target} -> {Intensity.Value}");
                        return ESVMCPCommandResult.Succeed($"已设置光源强度为 {Intensity.Value}");

                    case CommonLightOperation.SetColor:
                        lightComponent.color = Color.Value;
                        Debug.Log($"[ESVMCP] 设置光源颜色: {Target} -> {Color}");
                        return ESVMCPCommandResult.Succeed($"已设置光源颜色");

                    case CommonLightOperation.SetType:
                        lightComponent.type = (LightType)LightType.Value;
                        Debug.Log($"[ESVMCP] 设置光源类型: {Target} -> {LightType.Value}");
                        return ESVMCPCommandResult.Succeed($"已设置光源类型为 {LightType.Value}");

                    case CommonLightOperation.SetRange:
                        lightComponent.range = Range.Value;
                        Debug.Log($"[ESVMCP] 设置光源范围: {Target} -> {Range.Value}");
                        return ESVMCPCommandResult.Succeed($"已设置光源范围为 {Range.Value}");

                    case CommonLightOperation.SetSpotAngle:
                        lightComponent.spotAngle = SpotAngle.Value;
                        Debug.Log($"[ESVMCP] 设置聚光灯角度: {Target} -> {SpotAngle.Value}");
                        return ESVMCPCommandResult.Succeed($"已设置聚光灯角度为 {SpotAngle.Value}");

                    case CommonLightOperation.SetShadowType:
                        lightComponent.shadows = (LightShadows)ShadowType.Value;
                        Debug.Log($"[ESVMCP] 设置阴影类型: {Target} -> {ShadowType.Value}");
                        return ESVMCPCommandResult.Succeed($"已设置阴影类型为 {ShadowType.Value}");

                    case CommonLightOperation.Enable:
                        lightComponent.enabled = true;
                        Debug.Log($"[ESVMCP] 启用光源: {Target}");
                        return ESVMCPCommandResult.Succeed("已启用光源");

                    case CommonLightOperation.Disable:
                        lightComponent.enabled = false;
                        Debug.Log($"[ESVMCP] 禁用光源: {Target}");
                        return ESVMCPCommandResult.Succeed("已禁用光源");

                    case CommonLightOperation.GetProperties:
                        var properties = $"Light Properties:\n" +
                                       $"- Type: {lightComponent.type}\n" +
                                       $"- Intensity: {lightComponent.intensity}\n" +
                                       $"- Color: ({lightComponent.color.r:F2}, {lightComponent.color.g:F2}, {lightComponent.color.b:F2})\n" +
                                       $"- Range: {lightComponent.range}\n" +
                                       $"- SpotAngle: {lightComponent.spotAngle}\n" +
                                       $"- Shadows: {lightComponent.shadows}\n" +
                                       $"- Enabled: {lightComponent.enabled}";
                        Debug.Log($"[ESVMCP] {properties}");
                        return ESVMCPCommandResult.Succeed(properties);

                    default:
                        return ESVMCPCommandResult.Failed($"未知的光照操作: {Operation}");
                }
            }
            catch (Exception ex)
            {
                return ESVMCPCommandResult.Failed($"执行光照操作失败: {ex.Message}", ex);
            }
        }
    }
}
