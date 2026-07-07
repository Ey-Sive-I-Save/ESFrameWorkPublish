using UnityEngine;
using UnityEngine.Rendering;
using Newtonsoft.Json;
using System;

namespace ES.VMCP
{
    /// <summary>
    /// 通用环境操作类型
    /// </summary>
    public enum CommonEnvironmentOperation
    {
        SetAmbientLight,      // 设置环境光
        SetAmbientMode,       // 设置环境光模式
        SetSkybox,            // 设置天空盒
        SetFog,               // 设置雾效
        SetFogColor,          // 设置雾颜色
        SetFogDensity,        // 设置雾密度
        SetFogMode,           // 设置雾模式
        SetReflectionIntensity, // 设置反射强度
        GetEnvironmentInfo    // 获取环境信息
    }

    /// <summary>
    /// 环境光模式
    /// </summary>
    public enum ESVMCPAmbientMode
    {
        Skybox = 0,      // 天空盒
        Trilight = 1,    // 三色光
        Flat = 3,        // 单色
        Custom = 4       // 自定义
    }

    /// <summary>
    /// 雾效模式
    /// </summary>
    public enum ESVMCPFogMode
    {
        Linear = 1,        // 线性
        Exponential = 2,   // 指数
        ExponentialSquared = 3  // 指数平方
    }

    /// <summary>
    /// 统一的环境操作命令
    /// </summary>
    [ESVMCPCommand("CommonEnvironmentOperation", "统一的环境操作命令，支持环境光、天空盒、雾效等")]
    public class EnvironmentOperationCommand : ESVMCPCommandBase
    {
        [JsonProperty("operation")]
        public CommonEnvironmentOperation Operation { get; set; }

        [JsonProperty("color")]
        [JsonConverter(typeof(ColorConverter))]
        public Color? Color { get; set; }

        [JsonProperty("skyColor")]
        [JsonConverter(typeof(ColorConverter))]
        public Color? SkyColor { get; set; }

        [JsonProperty("equatorColor")]
        [JsonConverter(typeof(ColorConverter))]
        public Color? EquatorColor { get; set; }

        [JsonProperty("groundColor")]
        [JsonConverter(typeof(ColorConverter))]
        public Color? GroundColor { get; set; }

        [JsonProperty("ambientMode")]
        public ESVMCPAmbientMode? AmbientMode { get; set; }

        [JsonProperty("intensity")]
        public float? Intensity { get; set; }

        [JsonProperty("skyboxMaterial")]
        public string SkyboxMaterial { get; set; }

        [JsonProperty("fogEnabled")]
        public bool? FogEnabled { get; set; }

        [JsonProperty("fogMode")]
        public ESVMCPFogMode? FogMode { get; set; }

        [JsonProperty("fogDensity")]
        public float? FogDensity { get; set; }

        [JsonProperty("fogStartDistance")]
        public float? FogStartDistance { get; set; }

        [JsonProperty("fogEndDistance")]
        public float? FogEndDistance { get; set; }

        public override string Description
        {
            get
            {
                switch (Operation)
                {
                    case CommonEnvironmentOperation.SetAmbientLight:
                        return $"设置环境光: {Color?.ToString() ?? "未设置"}";
                    case CommonEnvironmentOperation.SetAmbientMode:
                        return $"设置环境光模式: {AmbientMode}";
                    case CommonEnvironmentOperation.SetSkybox:
                        return $"设置天空盒: {SkyboxMaterial}";
                    case CommonEnvironmentOperation.SetFog:
                        return $"设置雾效: {FogEnabled}";
                    case CommonEnvironmentOperation.SetFogColor:
                        return $"设置雾颜色: {Color?.ToString() ?? "未设置"}";
                    case CommonEnvironmentOperation.SetFogDensity:
                        return $"设置雾密度: {FogDensity}";
                    case CommonEnvironmentOperation.SetFogMode:
                        return $"设置雾模式: {FogMode}";
                    case CommonEnvironmentOperation.SetReflectionIntensity:
                        return $"设置反射强度: {Intensity}";
                    case CommonEnvironmentOperation.GetEnvironmentInfo:
                        return "获取环境信息";
                    default:
                        return $"环境操作: {Operation}";
                }
            }
        }

        public override bool IsDangerous => false;

        public override ESVMCPValidationResult Validate()
        {
            switch (Operation)
            {
                case CommonEnvironmentOperation.SetAmbientLight:
                    if (Color == null)
                        return ESVMCPValidationResult.Failure("color参数不能为空");
                    break;

                case CommonEnvironmentOperation.SetAmbientMode:
                    if (!AmbientMode.HasValue)
                        return ESVMCPValidationResult.Failure("ambientMode参数不能为空");
                    break;

                case CommonEnvironmentOperation.SetSkybox:
                    if (string.IsNullOrEmpty(SkyboxMaterial))
                        return ESVMCPValidationResult.Failure("skyboxMaterial参数不能为空");
                    break;

                case CommonEnvironmentOperation.SetFog:
                    if (!FogEnabled.HasValue)
                        return ESVMCPValidationResult.Failure("fogEnabled参数不能为空");
                    break;

                case CommonEnvironmentOperation.SetFogColor:
                    if (Color == null)
                        return ESVMCPValidationResult.Failure("color参数不能为空");
                    break;

                case CommonEnvironmentOperation.SetFogDensity:
                    if (!FogDensity.HasValue)
                        return ESVMCPValidationResult.Failure("fogDensity参数不能为空");
                    if (FogDensity.Value < 0)
                        return ESVMCPValidationResult.Failure("fogDensity不能为负数");
                    break;

                case CommonEnvironmentOperation.SetFogMode:
                    if (!FogMode.HasValue)
                        return ESVMCPValidationResult.Failure("fogMode参数不能为空");
                    break;

                case CommonEnvironmentOperation.SetReflectionIntensity:
                    if (!Intensity.HasValue)
                        return ESVMCPValidationResult.Failure("intensity参数不能为空");
                    if (Intensity.Value < 0)
                        return ESVMCPValidationResult.Failure("intensity不能为负数");
                    break;
            }

            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                switch (Operation)
                {
                    case CommonEnvironmentOperation.SetAmbientLight:
                        if (!Color.HasValue)
                            return ESVMCPCommandResult.Failed("颜色参数无效");
                        RenderSettings.ambientLight = Color.Value;
                        Debug.Log($"[ESVMCP] 设置环境光颜色: {Color.Value}");
                        return ESVMCPCommandResult.Succeed("已设置环境光颜色");

                    case CommonEnvironmentOperation.SetAmbientMode:
                        if (!AmbientMode.HasValue)
                            return ESVMCPCommandResult.Failed("环境光模式参数无效");
                        RenderSettings.ambientMode = (UnityEngine.Rendering.AmbientMode)AmbientMode.Value;
                        // 根据模式设置相应的颜色
                        if (AmbientMode.Value == ESVMCPAmbientMode.Trilight)
                        {
                            if (SkyColor.HasValue) RenderSettings.ambientSkyColor = SkyColor.Value;
                            if (EquatorColor.HasValue) RenderSettings.ambientEquatorColor = EquatorColor.Value;
                            if (GroundColor.HasValue) RenderSettings.ambientGroundColor = GroundColor.Value;
                        }
                        else if (AmbientMode.Value == ESVMCPAmbientMode.Flat && Color.HasValue)
                        {
                            RenderSettings.ambientLight = Color.Value;
                        }
                        Debug.Log($"[ESVMCP] 设置环境光模式: {AmbientMode.Value}");
                        return ESVMCPCommandResult.Succeed($"已设置环境光模式为 {AmbientMode.Value}");

                    case CommonEnvironmentOperation.SetSkybox:
                        Material skyboxMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(SkyboxMaterial);
                        if (skyboxMat == null)
                        {
                            return ESVMCPCommandResult.Failed($"找不到天空盒材质: {SkyboxMaterial}");
                        }
                        RenderSettings.skybox = skyboxMat;
                        Debug.Log($"[ESVMCP] 设置天空盒: {SkyboxMaterial}");
                        return ESVMCPCommandResult.Succeed($"已设置天空盒材质");

                    case CommonEnvironmentOperation.SetFog:
                        if (!FogEnabled.HasValue)
                            return ESVMCPCommandResult.Failed("雾效启用参数无效");
                        RenderSettings.fog = FogEnabled.Value;
                        Debug.Log($"[ESVMCP] 设置雾效: {FogEnabled.Value}");
                        return ESVMCPCommandResult.Succeed($"雾效已{(FogEnabled.Value ? "启用" : "禁用")}");

                    case CommonEnvironmentOperation.SetFogColor:
                        if (!Color.HasValue)
                            return ESVMCPCommandResult.Failed("雾颜色参数无效");
                        RenderSettings.fogColor = Color.Value;
                        Debug.Log($"[ESVMCP] 设置雾颜色: {Color.Value}");
                        return ESVMCPCommandResult.Succeed("已设置雾颜色");

                    case CommonEnvironmentOperation.SetFogDensity:
                        if (!FogDensity.HasValue)
                            return ESVMCPCommandResult.Failed("雾密度参数无效");
                        RenderSettings.fogDensity = FogDensity.Value;
                        Debug.Log($"[ESVMCP] 设置雾密度: {FogDensity.Value}");
                        return ESVMCPCommandResult.Succeed($"已设置雾密度为 {FogDensity.Value}");

                    case CommonEnvironmentOperation.SetFogMode:
                        if (!FogMode.HasValue)
                            return ESVMCPCommandResult.Failed("雾模式参数无效");
                        RenderSettings.fogMode = (UnityEngine.FogMode)FogMode.Value;
                        // 如果提供了距离参数，一并设置
                        if (FogStartDistance.HasValue)
                        {
                            RenderSettings.fogStartDistance = FogStartDistance.Value;
                        }
                        if (FogEndDistance.HasValue)
                        {
                            RenderSettings.fogEndDistance = FogEndDistance.Value;
                        }
                        Debug.Log($"[ESVMCP] 设置雾模式: {FogMode.Value}");
                        return ESVMCPCommandResult.Succeed($"已设置雾模式为 {FogMode.Value}");

                    case CommonEnvironmentOperation.SetReflectionIntensity:
                        if (!Intensity.HasValue)
                            return ESVMCPCommandResult.Failed("反射强度参数无效");
                        RenderSettings.reflectionIntensity = Intensity.Value;
                        Debug.Log($"[ESVMCP] 设置反射强度: {Intensity.Value}");
                        return ESVMCPCommandResult.Succeed($"已设置反射强度为 {Intensity.Value}");

                    case CommonEnvironmentOperation.GetEnvironmentInfo:
                        var info = $"Environment Settings:\n" +
                                  $"- Ambient Mode: {RenderSettings.ambientMode}\n" +
                                  $"- Ambient Light: ({RenderSettings.ambientLight.r:F2}, {RenderSettings.ambientLight.g:F2}, {RenderSettings.ambientLight.b:F2})\n" +
                                  $"- Fog Enabled: {RenderSettings.fog}\n" +
                                  $"- Fog Mode: {RenderSettings.fogMode}\n" +
                                  $"- Fog Color: ({RenderSettings.fogColor.r:F2}, {RenderSettings.fogColor.g:F2}, {RenderSettings.fogColor.b:F2})\n" +
                                  $"- Fog Density: {RenderSettings.fogDensity}\n" +
                                  $"- Reflection Intensity: {RenderSettings.reflectionIntensity}";
                        Debug.Log($"[ESVMCP] {info}");
                        return ESVMCPCommandResult.Succeed(info);

                    default:
                        return ESVMCPCommandResult.Failed($"未知的环境操作: {Operation}");
                }
            }
            catch (Exception ex)
            {
                return ESVMCPCommandResult.Failed($"执行环境操作失败: {ex.Message}");
            }
        }
    }
}
