using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ES.EditorInternal
{
    #region Migration Contracts

    public enum ESCompositeSSUTargetMode
    {
        Auto = 0,
        TwoD = 1,
        UI = 2,
        Lit = 3
    }

    public enum ESCompositeSSUBlendMode
    {
        Auto = 0,
        Alpha = 1,
        Additive = 2,
        Premultiplied = 3,
        Multiply = 4
    }

    public enum ESCompositeSSUMigrationSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class ESCompositeSSUMigrationIssue
    {
        public ESCompositeSSUMigrationSeverity Severity { get; }
        public string Message { get; }

        internal ESCompositeSSUMigrationIssue(ESCompositeSSUMigrationSeverity severity, string message)
        {
            Severity = severity;
            Message = message ?? string.Empty;
        }
    }

    public sealed class ESCompositeSSUMigrationReport
    {
        private readonly List<ESCompositeSSUMigrationIssue> issues = new List<ESCompositeSSUMigrationIssue>();

        public Material SourceMaterial { get; internal set; }
        public string SourceShaderName { get; internal set; }
        public string TargetShaderName { get; internal set; }
        public ESCompositeSSUBlendMode BlendMode { get; internal set; }
        public int DirectPropertyCount { get; internal set; }
        public int RemappedPropertyCount { get; internal set; }
        public int PartiallyCompatibleEffectCount { get; internal set; }
        public int ClampedPropertyCount { get; internal set; }
        public int UnsupportedEnabledEffectCount { get; internal set; }
        public IReadOnlyList<ESCompositeSSUMigrationIssue> Issues => issues;

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < issues.Count; i++)
                    if (issues[i].Severity == ESCompositeSSUMigrationSeverity.Error) return true;
                return false;
            }
        }

        public bool HasWarnings
        {
            get
            {
                for (int i = 0; i < issues.Count; i++)
                    if (issues[i].Severity == ESCompositeSSUMigrationSeverity.Warning) return true;
                return false;
            }
        }

        public bool CanMigrate(bool allowLossy)
        {
            return !HasErrors && (allowLossy || !HasWarnings);
        }

        internal void Add(ESCompositeSSUMigrationSeverity severity, string message)
        {
            issues.Add(new ESCompositeSSUMigrationIssue(severity, message));
        }
    }

    #endregion

    /// <summary>
    /// 将 SSU Sprite/GUI/3D Lit 材质迁移到对应 ES Composite Shader。源材质始终只读，调用方负责保存生成的副本。
    /// </summary>
    public static class ESCompositeSSUMaterialMigration
    {
        #region Source Recognition And Public API

        private const string SsuPrefix = "Sprite Shaders Ultimate/";
        private const string TwoDShaderName = "ES/2D/Composite URP";
        private const string UiShaderName = "ES/UI/Composite URP";
        private const string LitShaderName = "ES/3D/Lit Composite URP";

        private static readonly HashSet<string> InternalTargetProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "_ESMaterialVersion",
            "_SpriteUVRect",
            "_SpriteUVTransformX",
            "_SpriteUVTransformY",
            "_SpriteUVTransformValid",
            "_RendererColor",
            "_Flip",
            "_ESInteractiveWindRotation",
            "_ESInteractiveWindHeight",
            "_ESInteractiveSquish",
            "_ESWindPhaseOffset"
        };

        private static readonly HashSet<string> RemappedEnableProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "_EnableAddColor",
            "_EnableAlphaTint",
            "_EnableColorReplace",
            "_EnableFullDistortion",
            "_EnableGaussianBlur",
            "_EnablePingPongGlow",
            "_EnablePixelate",
            "_EnableRecolorRGB",
            "_EnableRecolorRGBYCP",
            "_EnableScreenTiling",
            "_EnableSharpen",
            "_EnableSplitToning",
            "_EnableStrongTint",
            "_EnableUVRotate",
            "_EnableUVScale",
            "_EnableUVScroll",
            "_EnableWorldTiling"
        };

        private static readonly HashSet<string> LitRemappedEnableProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "_EnableAddColor",
            "_EnableAlphaTint",
            "_EnableColorReplace",
            "_EnableFullDistortion",
            "_EnableGaussianBlur",
            "_EnableGlitch",
            "_EnableHalftone",
            "_EnableHologram",
            "_EnableInnerOutline",
            "_EnableOuterOutline",
            "_EnablePingPongGlow",
            "_EnablePixelate",
            "_EnablePixelOutline",
            "_EnableRecolorRGB",
            "_EnableRecolorRGBYCP",
            "_EnableSharpen",
            "_EnableScreenTiling",
            "_EnableSplitToning",
            "_EnableStrongTint",
            "_EnableUVRotate",
            "_EnableUVScale",
            "_EnableUVScroll",
            "_EnableWorldTiling"
        };

        private static readonly string[] SsuFadeEnableProperties =
        {
            "_EnableFullAlphaDissolve",
            "_EnableSourceAlphaDissolve",
            "_EnableSourceGlowDissolve",
            "_EnableDirectionalAlphaFade",
            "_EnableDirectionalGlowFade",
            "_EnableDirectionalDistortion"
        };

        private static readonly string[] SsuOrderSensitiveColorEffects =
        {
            "_EnableRecolorRGB",
            "_EnableRecolorRGBYCP",
            "_EnableColorReplace",
            "_EnableNegative",
            "_EnableContrast",
            "_EnableBrightness",
            "_EnableHue",
            "_EnableSplitToning",
            "_EnableBlackTint",
            "_EnableInkSpread",
            "_EnableShiftHue",
            "_EnableAddHue",
            "_EnableSineGlow",
            "_EnableSaturation",
            "_EnablePingPongGlow",
            "_EnableCamouflage",
            "_EnableMetal",
            "_EnableEnchanted",
            "_EnableShifting",
            "_EnableHalftone",
            "_EnableAddColor",
            "_EnableAlphaTint",
            "_EnableStrongTint"
        };

        public static bool TryResolveSourceShader(
            string sourceShaderName,
            out string targetShaderName,
            out ESCompositeSSUBlendMode blendMode)
        {
            targetShaderName = string.Empty;
            blendMode = ESCompositeSSUBlendMode.Auto;
            switch (sourceShaderName)
            {
                case SsuPrefix + "Standard SSU":
                case SsuPrefix + "2D Lit URP SSU":
                    targetShaderName = TwoDShaderName;
                    blendMode = ESCompositeSSUBlendMode.Alpha;
                    return true;
                case SsuPrefix + "Additive SSU":
                    targetShaderName = TwoDShaderName;
                    blendMode = ESCompositeSSUBlendMode.Additive;
                    return true;
                case SsuPrefix + "Multiplicative SSU":
                    targetShaderName = TwoDShaderName;
                    blendMode = ESCompositeSSUBlendMode.Multiply;
                    return true;
                case SsuPrefix + "GUI SSU":
                    targetShaderName = UiShaderName;
                    blendMode = ESCompositeSSUBlendMode.Alpha;
                    return true;
                case SsuPrefix + "Additive GUI SSU":
                    targetShaderName = UiShaderName;
                    blendMode = ESCompositeSSUBlendMode.Additive;
                    return true;
                case SsuPrefix + "3D Lit URP SSU":
                case SsuPrefix + "3D Lit Cutout URP SSU":
                case SsuPrefix + "3D Lit BuiltIn SSU":
                case SsuPrefix + "3D Lit Cutout BuiltIn SSU":
                    targetShaderName = LitShaderName;
                    blendMode = ESCompositeSSUBlendMode.Alpha;
                    return true;
                default:
                    return false;
            }
        }

        public static ESCompositeSSUMigrationReport Analyze(
            Material source,
            ESCompositeSSUTargetMode targetMode = ESCompositeSSUTargetMode.Auto,
            ESCompositeSSUBlendMode blendOverride = ESCompositeSSUBlendMode.Auto)
        {
            var report = new ESCompositeSSUMigrationReport
            {
                SourceMaterial = source,
                SourceShaderName = source != null && source.shader != null ? source.shader.name : string.Empty
            };
            if (source == null)
            {
                report.Add(ESCompositeSSUMigrationSeverity.Error, "来源材质为空；请选择 SSU 材质后重新刷新。");
                return report;
            }
            if (ESCompositeMaterialInstance.IsCompositeMaterial(source))
            {
                report.Add(ESCompositeSSUMigrationSeverity.Error, "该材质已经使用 ES Composite Shader，不需要执行 SSU 迁移。");
                return report;
            }

            MaterialSnapshot snapshot = MaterialSnapshot.Capture(source);
            bool recognized = TryResolveSourceShader(report.SourceShaderName, out string suggestedTarget, out ESCompositeSSUBlendMode suggestedBlend);
            if (!recognized && !IsLikelySsuSnapshot(snapshot))
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Error,
                    "当前材质没有可识别的 SSU Shader，也不包含 SSU 的序列化属性特征；手动目标不能用于迁移普通材质。");
                return report;
            }
            if (!recognized && targetMode == ESCompositeSSUTargetMode.Auto)
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Error,
                    "无法从当前 Shader 引用识别 SSU 目标；源 Shader 可能缺失，请显式选择 2D、UI 或 3D Lit 目标。");
                return report;
            }

            report.TargetShaderName = targetMode == ESCompositeSSUTargetMode.TwoD
                ? TwoDShaderName
                : targetMode == ESCompositeSSUTargetMode.UI
                    ? UiShaderName
                    : targetMode == ESCompositeSSUTargetMode.Lit
                        ? LitShaderName
                        : suggestedTarget;
            bool litTarget = report.TargetShaderName == LitShaderName;
            report.BlendMode = litTarget
                ? ESCompositeSSUBlendMode.Alpha
                : blendOverride != ESCompositeSSUBlendMode.Auto ? blendOverride : suggestedBlend;
            if (litTarget
                && blendOverride != ESCompositeSSUBlendMode.Auto
                && blendOverride != ESCompositeSSUBlendMode.Alpha)
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Warning,
                    "3D Lit 目标按来源 Shader 选择透明混合或透明裁剪；手动叠加、预乘和正片叠底设置不会应用。");
            }
            if (report.BlendMode == ESCompositeSSUBlendMode.Auto)
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Error,
                    "无法推断源材质混合模式；请显式选择透明、叠加、预乘或正片叠底。");
            }
            if (!recognized)
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Warning,
                    "使用手动目标迁移缺失或未知 Shader 的材质；只能依据序列化属性近似恢复，必须逐材质视觉复核。");
            }
            else if (!string.Equals(report.TargetShaderName, suggestedTarget, StringComparison.Ordinal))
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Warning,
                    "手动目标与 SSU Shader 的默认目标不同，目标 Shader 不支持的已启用效果会被报告但无法保留。");
            }
            if (report.SourceShaderName == SsuPrefix + "2D Lit URP SSU")
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Info,
                    "目标 ES 2D Shader 保留 Renderer2D 光照入口，但法线、Mask 与 Renderer2D Blend Style 仍需在场景中复核。");
            }
            if (litTarget)
            {
                bool builtInSource = report.SourceShaderName.IndexOf("BuiltIn", StringComparison.Ordinal) >= 0;
                if (builtInSource)
                {
                    report.Add(
                        ESCompositeSSUMigrationSeverity.Warning,
                        "来源是 Built-in SSU Lit；目标固定为 URP，光照、阴影、雾、Lightmap 与 Render Queue 必须在 URP 场景中复核。");
                }
                report.Add(
                    ESCompositeSSUMigrationSeverity.Info,
                    "3D Lit 将映射基础纹理、法线、金属度、光滑度、自发光、时间源与已支持的 SSU 表面效果；未支持参数保持为逐项 Warning。");
            }

            Shader targetShader = Shader.Find(report.TargetShaderName);
            if (targetShader == null)
            {
                report.Add(ESCompositeSSUMigrationSeverity.Error, "找不到目标 Shader：" + report.TargetShaderName + "。");
                return report;
            }

            report.DirectPropertyCount = CountDirectProperties(snapshot, targetShader);
            AnalyzeCollapsedMappings(snapshot, report);
            AnalyzePartiallyCompatibleEffects(snapshot, targetShader, report);
            AnalyzeRangeClamps(snapshot, targetShader, report);
            AnalyzeUnsupportedEffects(snapshot, targetShader, report);
            AnalyzeRuntimeComponentMappings(snapshot, report);
            report.Add(
                ESCompositeSSUMigrationSeverity.Info,
                "将复制 " + report.DirectPropertyCount + " 个同名属性；源材质保持不变，输出为新的 Material 资产。");
            return report;
        }

        public static Material CreateMigratedMaterial(
            Material source,
            ESCompositeSSUTargetMode targetMode,
            ESCompositeSSUBlendMode blendOverride,
            bool allowLossy,
            out ESCompositeSSUMigrationReport report)
        {
            report = Analyze(source, targetMode, blendOverride);
            if (!report.CanMigrate(allowLossy)) return null;

            Shader targetShader = Shader.Find(report.TargetShaderName);
            if (targetShader == null) return null;
            MaterialSnapshot snapshot = MaterialSnapshot.Capture(source);
            var target = new Material(targetShader)
            {
                name = source.name + " ES",
                enableInstancing = source.enableInstancing,
                doubleSidedGI = source.doubleSidedGI,
                globalIlluminationFlags = source.globalIlluminationFlags
            };
            report.DirectPropertyCount = ApplyDirectProperties(snapshot, target);
            report.RemappedPropertyCount = ApplyCollapsedMappings(snapshot, target, report);
            report.RemappedPropertyCount += ApplySSUExactContract(snapshot, target);
            ApplyBlendMode(target, report);
            if (report.TargetShaderName != LitShaderName && source.renderQueue >= 0)
                target.renderQueue = source.renderQueue;
            ESCompositeMaterialMigration.Migrate(target, false);
            ESCompositeShaderGUI.SyncMaterialKeywords(target);
            return target;
        }

        #endregion

        #region Direct Property Copy

        private static bool IsLikelySsuSnapshot(MaterialSnapshot snapshot)
        {
            return snapshot.HasFloat("_EnableCustomFade")
                && snapshot.HasFloat("_EnableCamouflage")
                && snapshot.HasFloat("_EnableDirectionalAlphaFade");
        }

        private static int CountDirectProperties(MaterialSnapshot snapshot, Shader targetShader)
        {
            int count = 0;
            int propertyCount = targetShader.GetPropertyCount();
            for (int i = 0; i < propertyCount; i++)
            {
                string name = targetShader.GetPropertyName(i);
                if (InternalTargetProperties.Contains(name)) continue;
                ShaderPropertyType type = targetShader.GetPropertyType(i);
                if (type == ShaderPropertyType.Texture ? snapshot.HasTexture(name)
                    : type == ShaderPropertyType.Color || type == ShaderPropertyType.Vector
                        ? snapshot.HasVector(name)
                        : snapshot.HasFloat(name))
                    count++;
            }
            return count;
        }

        private static int ApplyDirectProperties(MaterialSnapshot snapshot, Material target)
        {
            int applied = 0;
            Shader shader = target.shader;
            int propertyCount = shader.GetPropertyCount();
            for (int i = 0; i < propertyCount; i++)
            {
                string name = shader.GetPropertyName(i);
                if (InternalTargetProperties.Contains(name)) continue;
                switch (shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Texture:
                        if (snapshot.TryGetTexture(name, out TextureSnapshot texture))
                        {
                            target.SetTexture(name, texture.Texture);
                            target.SetTextureScale(name, texture.Scale);
                            target.SetTextureOffset(name, texture.Offset);
                            applied++;
                        }
                        break;
                    case ShaderPropertyType.Color:
                        if (snapshot.TryGetVector(name, out Vector4 color))
                        {
                            target.SetColor(name, color);
                            applied++;
                        }
                        break;
                    case ShaderPropertyType.Vector:
                        if (snapshot.TryGetVector(name, out Vector4 vector))
                        {
                            target.SetVector(name, vector);
                            applied++;
                        }
                        break;
                    case ShaderPropertyType.Float:
                        if (snapshot.TryGetFloat(name, out float value))
                        {
                            target.SetFloat(name, value);
                            applied++;
                        }
                        break;
                    case ShaderPropertyType.Range:
                        if (snapshot.TryGetFloat(name, out float rangedValue))
                        {
                            Vector2 limits = shader.GetPropertyRangeLimits(i);
                            target.SetFloat(name, Mathf.Clamp(rangedValue, limits.x, limits.y));
                            applied++;
                        }
                        break;
                }
            }
            return applied;
        }

        #endregion

        #region Migration Diagnostics

        private static void AnalyzeCollapsedMappings(MaterialSnapshot snapshot, ESCompositeSSUMigrationReport report)
        {
            if (snapshot.IsEnabled("_ToggleUnscaledTime") && snapshot.IsEnabled("_ToggleCustomTime"))
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Warning,
                    "来源同时启用了 Unscaled Time 与 Custom Time；按 SSU 执行顺序采用 Unscaled Time。"
                );
            }

            int activeOrderSensitiveColorEffects = 0;
            for (int i = 0; i < SsuOrderSensitiveColorEffects.Length; i++)
                if (snapshot.IsEnabled(SsuOrderSensitiveColorEffects[i])) activeOrderSensitiveColorEffects++;
            if (activeOrderSensitiveColorEffects > 1)
                report.Add(
                    ESCompositeSSUMigrationSeverity.Info,
                    "来源叠加了 " + activeOrderSensitiveColorEffects
                    + " 个颜色或滤镜效果；参数会保留，但执行顺序遵循 ES Composite 管线，"
                    + "不承诺与 SSU Shader Graph 的全局节点顺序逐项一致，必须视觉复核。"
                );

            if (report.TargetShaderName == LitShaderName)
            {
                AnalyzeLitMappings(snapshot, report);
                return;
            }

            int activeFadeCount = 0;
            for (int i = 0; i < SsuFadeEnableProperties.Length; i++)
                if (snapshot.IsEnabled(SsuFadeEnableProperties[i])) activeFadeCount++;
            if (activeFadeCount > 0)
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Info,
                    "将保留 " + activeFadeCount
                    + " 个可叠加 SSU Fade，并按 SSU 的固定顺序执行；材质参数可直接迁移，外部动画轨道仍需单独迁移。"
                );
            }

            bool worldTiling = snapshot.IsEnabled("_EnableWorldTiling");
            bool screenTiling = snapshot.IsEnabled("_EnableScreenTiling");
            if (worldTiling && screenTiling)
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Error,
                    "源材质同时启用 World Tiling 与 Screen Tiling，ES TilingMode 只能选择一种空间。"
                );
            }
            else if (worldTiling || screenTiling)
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Warning,
                    "SSU 与 ES 的平铺单位不同，将按主纹理尺寸换算 World PPU；Screen 像素尺寸仍需视觉复核。"
                );
            }

            if (snapshot.IsEnabled("_EnableUVScale") && snapshot.IsEnabled("_EnableUVRotate"))
            {
                Vector2 scalePivot = snapshot.GetVector2("_UVScalePivot", new Vector2(0.5f, 0.5f));
                Vector2 rotatePivot = snapshot.GetVector2("_UVRotatePivot", new Vector2(0.5f, 0.5f));
                if ((scalePivot - rotatePivot).sqrMagnitude > 0.000001f)
                    report.Add(
                        ESCompositeSSUMigrationSeverity.Warning,
                        "UV Scale 与 UV Rotate 使用不同 Pivot；ES UVTransform 只有一个 Pivot，将优先采用 Rotate Pivot。"
                    );
            }
            int activeUvOperationCount = (snapshot.IsEnabled("_EnableUVScale") ? 1 : 0)
                + (snapshot.IsEnabled("_EnableUVRotate") ? 1 : 0)
                + (snapshot.IsEnabled("_EnableUVScroll") ? 1 : 0);
            if (activeUvOperationCount > 1)
                report.Add(
                    ESCompositeSSUMigrationSeverity.Warning,
                    "源材质组合了多个 SSU UV 操作；ES 的 UVTransform 与 Flow 执行顺序不同，只能保持参数近似。"
                );
            if (snapshot.IsEnabled("_EnableFullDistortion"))
                report.Add(
                    ESCompositeSSUMigrationSeverity.Info,
                    "Full Distortion 将按同名 Fade、Distortion、Noise Scale 与共享噪声纹理迁移，并保留两次独立噪声驱动 XY。"
                );
            if (snapshot.IsEnabled("_EnableGaussianBlur"))
                report.Add(
                    ESCompositeSSUMigrationSeverity.Info,
                    "Gaussian Blur 将映射到 ES Gaussian3x3，并按 SSU 0.005 采样尺度换算半径。"
                );
        }

        private static void AnalyzeUnsupportedEffects(
            MaterialSnapshot snapshot,
            Shader targetShader,
            ESCompositeSSUMigrationReport report)
        {
            HashSet<string> remappedEnableProperties = report.TargetShaderName == LitShaderName
                ? LitRemappedEnableProperties
                : RemappedEnableProperties;
            foreach (KeyValuePair<string, float> pair in snapshot.Floats)
            {
                if (!pair.Key.StartsWith("_Enable", StringComparison.Ordinal) || pair.Value <= 0.5f)
                    continue;
                if (targetShader.FindPropertyIndex(pair.Key) >= 0 || remappedEnableProperties.Contains(pair.Key))
                    continue;
                report.UnsupportedEnabledEffectCount++;
                report.Add(
                    ESCompositeSSUMigrationSeverity.Warning,
                    pair.Key + " 在目标 Shader 中没有对应入口；生成材质不会包含该已启用效果。"
                );
            }
        }

        private static void AnalyzePartiallyCompatibleEffects(
            MaterialSnapshot snapshot,
            Shader targetShader,
            ESCompositeSSUMigrationReport report)
        {
            HashSet<string> remappedEnableProperties = report.TargetShaderName == LitShaderName
                ? LitRemappedEnableProperties
                : RemappedEnableProperties;
            foreach (KeyValuePair<string, float> pair in snapshot.Floats)
            {
                if (!pair.Key.StartsWith("_Enable", StringComparison.Ordinal) || pair.Value <= 0.5f)
                    continue;
                if (remappedEnableProperties.Contains(pair.Key)
                    || targetShader.FindPropertyIndex(pair.Key) < 0)
                    continue;

                string effectPrefix = "_" + pair.Key.Substring("_Enable".Length);
                List<string> missing = snapshot.FindPropertiesMissingFromTarget(effectPrefix, targetShader);
                if (missing.Count == 0) continue;

                report.PartiallyCompatibleEffectCount++;
                int shown = Mathf.Min(4, missing.Count);
                string example = string.Join(", ", missing.GetRange(0, shown));
                if (shown < missing.Count) example += " 等";
                report.Add(
                    ESCompositeSSUMigrationSeverity.Warning,
                    pair.Key + " 的开关可迁移，但有 " + missing.Count
                    + " 个 SSU 参数在目标中没有同名属性（" + example
                    + "）；这些参数将使用 ES 默认值，必须视觉复核。");
            }
        }

        private static void AnalyzeRuntimeComponentMappings(
            MaterialSnapshot snapshot,
            ESCompositeSSUMigrationReport report)
        {
            bool hasInteractiveWind = snapshot.TryGetFloat("_WindRotation", out float windRotation);
            bool hasWindParallax = snapshot.TryGetFloat("_WindXPosition", out float windXPosition);
            if (!hasInteractiveWind && !hasWindParallax)
                return;

            bool hasRuntimeValue = (hasInteractiveWind && Mathf.Abs(windRotation) > 0.0001f)
                || (hasWindParallax && Mathf.Abs(windXPosition) > 0.0001f);
            report.Add(
                hasRuntimeValue
                    ? ESCompositeSSUMigrationSeverity.Warning
                    : ESCompositeSSUMigrationSeverity.Info,
                "SSU 的 _WindRotation/_WindXPosition 是场景组件写入的运行时状态，材质迁移不会自动迁移组件；"
                + "需要交互弯曲时挂载 ESCompositeInteractiveWind2D，需要世界位置错相时挂载 ESCompositeWindParallax。"
            );
        }

        private static void AnalyzeRangeClamps(
            MaterialSnapshot snapshot,
            Shader targetShader,
            ESCompositeSSUMigrationReport report)
        {
            int propertyCount = targetShader.GetPropertyCount();
            var clamped = new List<string>();
            for (int i = 0; i < propertyCount; i++)
            {
                if (targetShader.GetPropertyType(i) != ShaderPropertyType.Range) continue;
                string name = targetShader.GetPropertyName(i);
                if (!snapshot.TryGetFloat(name, out float value)
                    || !snapshot.IsRelevantProperty(name))
                    continue;

                Vector2 limits = targetShader.GetPropertyRangeLimits(i);
                if (value < limits.x || value > limits.y) clamped.Add(name);
            }
            if (clamped.Count == 0) return;

            clamped.Sort(StringComparer.Ordinal);
            report.ClampedPropertyCount = clamped.Count;
            int shown = Mathf.Min(4, clamped.Count);
            string example = string.Join(", ", clamped.GetRange(0, shown));
            if (shown < clamped.Count) example += " 等";
            report.Add(
                ESCompositeSSUMigrationSeverity.Warning,
                "有 " + clamped.Count + " 个启用中的同名参数超出 ES Range（" + example
                + "）；生成时会夹取到目标范围边界。");
        }

        #endregion

        #region Collapsed Property Mappings

        private static int ApplyCollapsedMappings(
            MaterialSnapshot snapshot,
            Material target,
            ESCompositeSSUMigrationReport report)
        {
            int applied = ApplyTimeMapping(snapshot, target);
            applied += ApplyHalftoneMapping(snapshot, target);
            applied += ApplySharedStylizedMappings(snapshot, target);
            applied += ApplyRestoredControlMappings(snapshot, target);
            applied += ApplySharedColorMappings(snapshot, target);
            applied += ApplySharedFilterMappings(snapshot, target);
            if (report.TargetShaderName == LitShaderName)
                return applied + ApplyLitMappings(snapshot, target);

            if (snapshot.IsEnabled("_EnableGaussianBlur"))
            {
                target.SetFloat("_EnableBlur", 1f);
                target.SetFloat("_BlurMode", 1f);
                target.SetFloat("_BlurIntensity", Mathf.Clamp01(snapshot.GetFloat("_GaussianBlurFade", 1f)));
                target.SetFloat("_BlurRadius", Mathf.Clamp(snapshot.GetFloat("_GaussianBlurOffset", 0.5f) * 0.005f, 0f, 0.02f));
                applied += 4;
            }

            bool uvScale = snapshot.IsEnabled("_EnableUVScale");
            bool uvRotate = snapshot.IsEnabled("_EnableUVRotate");
            if (uvScale || uvRotate)
            {
                target.SetFloat("_EnableUVTransform", 1f);
                target.SetVector("_UVScale", uvScale
                    ? snapshot.GetVector("_UVScaleScale", new Vector4(1f, 1f, 0f, 0f))
                    : new Vector4(1f, 1f, 0f, 0f));
                Vector2 pivot = uvRotate
                    ? snapshot.GetVector2("_UVRotatePivot", new Vector2(0.5f, 0.5f))
                    : snapshot.GetVector2("_UVScalePivot", new Vector2(0.5f, 0.5f));
                target.SetVector("_UVPivot", new Vector4(pivot.x, pivot.y, 0f, 0f));
                target.SetFloat("_UVRotationSpeed", uvRotate
                    ? Mathf.Clamp(snapshot.GetFloat("_UVRotateSpeed", 0f) * 180f, -7200f, 7200f)
                    : 0f);
                applied += 4;
            }
            if (snapshot.IsEnabled("_EnableUVScroll"))
            {
                target.SetFloat("_EnableFlow", 1f);
                target.SetVector("_FlowSpeed", snapshot.GetVector("_UVScrollSpeed", Vector4.zero));
                target.SetFloat("_FlowStrength", 1f);
                applied += 3;
            }

            if (snapshot.IsEnabled("_EnableWorldTiling"))
            {
                target.SetFloat("_TilingMode", 1f);
                Vector2 textureSize = snapshot.GetTextureSize("_MainTex", new Vector2(100f, 100f));
                float sourcePixelsPerUnit = snapshot.GetFloat("_WorldTilingPixelsPerUnit", 100f);
                Vector4 sourceScale = snapshot.GetVector("_WorldTilingScale", new Vector4(1f, 1f, 0f, 0f));
                Vector4 sourceOffset = snapshot.GetVector("_WorldTilingOffset", Vector4.zero);
                target.SetVector(
                    "_WorldTilingScale",
                    new Vector4(
                        sourceScale.x,
                        sourceScale.y * textureSize.x / textureSize.y,
                        sourceScale.z,
                        sourceScale.w));
                target.SetVector(
                    "_WorldTilingOffset",
                    new Vector4(
                        sourceOffset.x * sourcePixelsPerUnit / textureSize.x,
                        sourceOffset.y * sourcePixelsPerUnit / textureSize.y,
                        sourceOffset.z,
                        sourceOffset.w));
                target.SetFloat(
                    "_WorldTilingPixelsPerUnit",
                    Mathf.Clamp(sourcePixelsPerUnit / textureSize.x, 0.01f, 64f));
                applied += 4;
            }
            else if (snapshot.IsEnabled("_EnableScreenTiling"))
            {
                target.SetFloat("_TilingMode", 2f);
                target.SetFloat(
                    "_ScreenTilingPixelsPerUnit",
                    Mathf.Clamp(snapshot.GetFloat("_ScreenTilingPixelsPerUnit", 100f), 1f, 2048f));
                applied += 2;
            }

            report.RemappedPropertyCount = applied;
            return applied;
        }

        private static int ApplyHalftoneMapping(MaterialSnapshot snapshot, Material target)
        {
            if (!snapshot.IsEnabled("_EnableHalftone") || !target.HasProperty("_HalftoneScale")) return 0;

            target.SetFloat("_HalftoneScale", Mathf.Clamp(snapshot.GetFloat("_HalftoneTiling", 4f), 4f, 512f));
            target.SetVector("_HalftonePosition", snapshot.GetVector("_HalftonePosition", Vector4.zero));
            target.SetFloat("_HalftoneFade", snapshot.GetFloat("_HalftoneFade", 1f));
            target.SetFloat("_HalftoneFadeWidth", Mathf.Max(
                Mathf.Abs(snapshot.GetFloat("_HalftoneFadeWidth", 1.5f)),
                0.01f));
            target.SetFloat("_HalftoneInvert", snapshot.GetFloat("_HalftoneInvert", 0f) > 0.5f ? 1f : 0f);
            target.SetFloat("_HalftoneStrength", 0f);
            target.SetFloat("_HalftoneAlphaPattern", 1f);
            return 7;
        }

        private static void AnalyzeLitMappings(
            MaterialSnapshot snapshot,
            ESCompositeSSUMigrationReport report)
        {
            int activeFadeCount = 0;
            for (int i = 0; i < SsuFadeEnableProperties.Length; i++)
                if (snapshot.IsEnabled(SsuFadeEnableProperties[i])) activeFadeCount++;
            if (activeFadeCount > 0)
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Info,
                    "将保留 " + activeFadeCount
                    + " 个可叠加 SSU Fade，并在 Lit 各渲染 Pass 中按 SSU 固定顺序执行；外部动画轨道仍需单独迁移。");
            }
            if (snapshot.IsEnabled("_EnableGaussianBlur"))
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Info,
                    "Gaussian Blur 将映射到 ES Lit 的固定 3x3 模糊，并按 SSU 采样尺度换算半径。");
            }
            if (snapshot.IsEnabled("_EnableSharpen"))
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Info,
                    "Sharpen 的采样偏移将从 SSU texel 倍数换算为 ES 归一化半径，强度与淡入值会夹取到目标范围。");
            }
            if (snapshot.IsEnabled("_EnablePixelate"))
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Warning,
                    "Pixelate 将按来源主纹理尺寸、Pixel Density、Pixels Per Unit 与 Fade 近似换算；非方形纹理和透视缩放下必须视觉复核。");
            }
            if (snapshot.IsEnabled("_EnableHalftone"))
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Info,
                    "Halftone 将迁移 Tiling、Position、Fade、Fade Width 与 Invert，并启用 SSU 透明点阵；ES 自有 RGB 半色调强度会关闭。");
            }
            if (snapshot.IsEnabled("_EnableFullDistortion"))
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Info,
                    "Full Distortion 将按同名参数直接迁移，并保留两次独立噪声采样分别驱动 XY。");
            }
            if (snapshot.IsEnabled("_EnableHologram"))
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Info,
                    "Hologram 的 Fade、Contrast、世界高度扫描、Distortion、Tint、Line Speed 与 Line Gap 会进入 SSU 精确合同；透视相机与多相机画面仍需视觉复核。");
            }
            if (snapshot.IsEnabled("_EnableGlitch"))
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Info,
                    "Glitch 的 Fade、Mask、Noise、Hue、Brightness、Distortion 与独立速度会进入 SSU 精确合同。");
            }
            if (snapshot.IsEnabled("_EnableInnerOutline")
                || snapshot.IsEnabled("_EnableOuterOutline")
                || snapshot.IsEnabled("_EnablePixelOutline"))
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Warning,
                    "Outline 的 Fade、Distortion、独立 TintTexture 与 OutlineOnly 会进入 SSU 精确合同；Inner/Outer 使用八方向邻域，Pixel 使用四方向像素邻域。外描边仍受当前网格覆盖范围限制，透明边缘未预留空间时会被几何边界裁掉。");
            }
            if (snapshot.IsEnabled("_EnableUVScroll"))
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Warning,
                    "UV Scroll 将近似为 ES Lit Flow；两者与其他 UV 效果的执行顺序可能不同。");
            }
            if (snapshot.IsEnabled("_EnableWorldTiling") || snapshot.IsEnabled("_EnableScreenTiling"))
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Warning,
                    "3D Lit 的 World Tiling 使用世界 XZ 投影，垂直面与 SSU Sprite 平面语义不同；Screen Tiling 依赖当前相机和投影，必须在目标场景复核。");
            }
            if (snapshot.IsEnabled("_EnableSineMove") || snapshot.IsEnabled("_EnableWind")
                || snapshot.IsEnabled("_EnableSquish") || snapshot.IsEnabled("_EnableWiggle")
                || snapshot.IsEnabled("_EnableVibrate") || snapshot.IsEnabled("_EnableSineScale"))
            {
                report.Add(
                    ESCompositeSSUMigrationSeverity.Warning,
                    "顶点运动会同步 Shadow、Depth 和 Scene Pass，但 3D Lit 的法线仍按原始网格计算；大幅 Wiggle/Squish 需要在目标网格上做视觉复核。");
            }
            if (HasEnabledSSUStatusEffect(snapshot))
                report.Add(
                    ESCompositeSSUMigrationSeverity.Info,
                    "Frozen、Burn、Rainbow、Shine 与 Poison 将保留同名参数，并按 SSU 固定顺序执行。");
        }

        private static int ApplyLitMappings(MaterialSnapshot snapshot, Material target)
        {
            int applied = 0;
            applied += CopyTexture(snapshot, target, "_MainTex", "_BaseMap", true);
            applied += CopyFloat(snapshot, target, "_NormalIntensity", "_NormalScale", 0f, 2f);
            applied += CopyFloat(snapshot, target, "_MetallicMapToggle", "_UseMetallicMap", 0f, 1f);
            applied += CopyFloat(snapshot, target, "_EmissionToggle", "_UseEmission", 0f, 1f);
            applied += CopyVector(snapshot, target, "_EmissionTint", "_EmissionColor");
            target.SetFloat("_SmoothnessMapChannel", 0f);
            target.SetFloat("_EmissionUseAlpha", 1f);
            applied += 2;
            if (snapshot.HasTexture("_MetallicMap"))
            {
                // SSU samples the metallic texture with the main UV; ES exposes an additional map transform.
                target.SetTextureScale("_MetallicMap", Vector2.one);
                target.SetTextureOffset("_MetallicMap", Vector2.zero);
            }

            if (snapshot.TryGetTexture("_NormalMap", out TextureSnapshot normalMap))
            {
                target.SetFloat("_UseNormalMap", normalMap.Texture != null ? 1f : 0f);
                applied++;
            }

            bool uvScale = snapshot.IsEnabled("_EnableUVScale");
            bool uvRotate = snapshot.IsEnabled("_EnableUVRotate");
            if (uvScale || uvRotate)
            {
                target.SetFloat("_EnableUVTransform", 1f);
                target.SetVector("_UVScale", uvScale
                    ? snapshot.GetVector("_UVScaleScale", new Vector4(1f, 1f, 0f, 0f))
                    : new Vector4(1f, 1f, 0f, 0f));
                Vector2 pivot = uvRotate
                    ? snapshot.GetVector2("_UVRotatePivot", new Vector2(0.5f, 0.5f))
                    : snapshot.GetVector2("_UVScalePivot", new Vector2(0.5f, 0.5f));
                target.SetVector("_UVPivot", new Vector4(pivot.x, pivot.y, 0f, 0f));
                target.SetFloat("_UVRotationSpeed", uvRotate
                    ? Mathf.Clamp(snapshot.GetFloat("_UVRotateSpeed", 0f) * 180f, -7200f, 7200f)
                    : 0f);
                applied += 4;
            }
            if (snapshot.IsEnabled("_EnableWorldTiling"))
            {
                target.SetFloat("_TilingMode", 1f);
                Vector2 textureSize = snapshot.GetTextureSize("_MainTex", new Vector2(100f, 100f));
                float sourcePixelsPerUnit = snapshot.GetFloat("_WorldTilingPixelsPerUnit", 100f);
                Vector4 sourceScale = snapshot.GetVector("_WorldTilingScale", new Vector4(1f, 1f, 0f, 0f));
                Vector4 sourceOffset = snapshot.GetVector("_WorldTilingOffset", Vector4.zero);
                target.SetVector("_WorldTilingScale", new Vector4(
                    sourceScale.x,
                    sourceScale.y * textureSize.x / Mathf.Max(textureSize.y, 1f),
                    sourceScale.z,
                    sourceScale.w));
                target.SetVector("_WorldTilingOffset", new Vector4(
                    sourceOffset.x * sourcePixelsPerUnit / Mathf.Max(textureSize.x, 1f),
                    sourceOffset.y * sourcePixelsPerUnit / Mathf.Max(textureSize.y, 1f),
                    sourceOffset.z,
                    sourceOffset.w));
                target.SetFloat("_WorldTilingPixelsPerUnit", Mathf.Clamp(
                    sourcePixelsPerUnit / Mathf.Max(textureSize.x, 1f), 0.01f, 64f));
                applied += 4;
            }
            else if (snapshot.IsEnabled("_EnableScreenTiling"))
            {
                target.SetFloat("_TilingMode", 2f);
                target.SetFloat("_ScreenTilingPixelsPerUnit", Mathf.Clamp(
                    snapshot.GetFloat("_ScreenTilingPixelsPerUnit", 100f), 1f, 2048f));
                applied += 2;
            }

            if (snapshot.IsEnabled("_EnableGaussianBlur"))
            {
                target.SetFloat("_EnableBlur", 1f);
                target.SetFloat("_BlurIntensity", Mathf.Clamp01(snapshot.GetFloat("_GaussianBlurFade", 1f)));
                target.SetFloat(
                    "_BlurRadius",
                    Mathf.Clamp(snapshot.GetFloat("_GaussianBlurOffset", 0.5f) * 0.005f, 0f, 0.02f));
                applied += 3;
            }
            if (snapshot.IsEnabled("_EnableGlitch"))
            {
                Vector2 distortion = snapshot.GetVector2("_GlitchDistortion", new Vector2(0.03f, 0f));
                target.SetFloat(
                    "_GlitchIntensity",
                    Mathf.Clamp(distortion.magnitude, 0f, 0.2f));

                Vector2 maskSpeed = snapshot.GetVector2("_GlitchMaskSpeed", Vector2.zero);
                Vector2 noiseSpeed = snapshot.GetVector2("_GlitchNoiseSpeed", Vector2.zero);
                Vector2 distortionSpeed = snapshot.GetVector2("_GlitchDistortionSpeed", Vector2.zero);
                float speed = Mathf.Max(
                    Mathf.Abs(maskSpeed.x), Mathf.Abs(maskSpeed.y),
                    Mathf.Abs(noiseSpeed.x), Mathf.Abs(noiseSpeed.y),
                    Mathf.Abs(distortionSpeed.x), Mathf.Abs(distortionSpeed.y));
                target.SetFloat("_GlitchSpeed", Mathf.Clamp(speed, 0.01f, 128f));
                applied += 2;
            }
            if (snapshot.IsEnabled("_EnableUVScroll"))
            {
                target.SetFloat("_EnableFlow", 1f);
                target.SetVector("_FlowSpeed", snapshot.GetVector("_UVScrollSpeed", Vector4.zero));
                target.SetFloat("_FlowStrength", 1f);
                applied += 3;
            }

            ES3DLitCompositeURPProperties.SetQuality(target, ESCompositeQualityTier.高质量);
            applied++;
            return applied;
        }

        private static int ApplyRestoredControlMappings(MaterialSnapshot snapshot, Material target)
        {
            int applied = 0;
            applied += CopyFloat(snapshot, target, "_ColorReplaceContrast", "_ReplaceContrast", 0.001f, 8f);
            applied += CopyFloat(snapshot, target, "_ColorReplaceFade", "_ReplaceFade", 0f, 1f);
            applied += CopyFloat(snapshot, target, "_SplitToningContrast", "_SplitToneContrast", 0.001f, 8f);
            applied += CopyFloat(snapshot, target, "_SplitToningShift", "_SplitToneShift", -1f, 1f);
            applied += CopyFloat(snapshot, target, "_PingPongGlowContrast", "_GlowContrast", 0.001f, 8f);
            applied += CopyFloat(snapshot, target, "_PingPongGlowFade", "_GlowFade", 0f, 1f);
            return applied;
        }

        private static int ApplySharedColorMappings(MaterialSnapshot snapshot, Material target)
        {
            int applied = 0;
            applied += CopyVector(snapshot, target, "_StrongTintTint", "_StrongTint");
            applied += CopyVector(snapshot, target, "_AddColorColor", "_AddColor");
            applied += CopyVector(snapshot, target, "_AlphaTintColor", "_AlphaTint");
            applied += CopyFloat(snapshot, target, "_AlphaTintMinAlpha", "_AlphaTintMin", 0f, 1f);

            applied += CopyVector(snapshot, target, "_ColorReplaceFromColor", "_ReplaceFrom");
            applied += CopyVector(snapshot, target, "_ColorReplaceToColor", "_ReplaceTo");
            applied += CopyFloat(snapshot, target, "_ColorReplaceRange", "_ReplaceRange", 0f, 1f);
            applied += CopyFloat(snapshot, target, "_ColorReplaceSmoothness", "_ReplaceSoftness", 0.001f, 1f);

            applied += ApplyLitRecolorMappings(snapshot, target);

            applied += CopyVector(snapshot, target, "_SplitToningShadowsColor", "_SplitToneShadows");
            applied += CopyVector(snapshot, target, "_SplitToningHighlightsColor", "_SplitToneHighlights");
            applied += CopyFloat(snapshot, target, "_SplitToningBalance", "_SplitToneBalance", -1f, 1f);
            applied += CopyFloat(snapshot, target, "_SplitToningFade", "_SplitToneStrength", 0f, 1f);

            applied += CopyVector(snapshot, target, "_PingPongGlowFrom", "_GlowFrom");
            applied += CopyVector(snapshot, target, "_PingPongGlowTo", "_GlowTo");
            applied += CopyFloat(snapshot, target, "_PingPongGlowFrequency", "_GlowFrequency");
            return applied;
        }

        private static int ApplySharedFilterMappings(MaterialSnapshot snapshot, Material target)
        {
            int applied = 0;
            if (snapshot.IsEnabled("_EnableSharpen"))
            {
                target.SetFloat("_SharpenAmount", Mathf.Clamp(snapshot.GetFloat("_SharpenFactor", 1f), 0f, 4f));
                target.SetFloat("_SharpenRadius", Mathf.Clamp(snapshot.GetFloat("_SharpenOffset", 1f) / 512f, 0f, 0.02f));
                target.SetFloat("_SharpenFade", Mathf.Clamp01(snapshot.GetFloat("_SharpenFade", 1f)));
                applied += 3;
            }
            if (snapshot.IsEnabled("_EnablePixelate"))
            {
                Vector2 textureSize = snapshot.GetTextureSize("_MainTex", new Vector2(100f, 100f));
                float fade = Mathf.Clamp01(snapshot.GetFloat("_PixelateFade", 1f));
                float pixelsPerUnit = Mathf.Max(snapshot.GetFloat("_PixelatePixelsPerUnit", 100f), 0.0001f);
                float density = Mathf.Max(snapshot.GetFloat("_PixelatePixelDensity", 16f), 0f);
                float cells = density * Mathf.Max(textureSize.x, 1f)
                    / pixelsPerUnit / Mathf.Max(fade, 0.0001f);
                target.SetFloat("_PixelateCells", Mathf.Clamp(cells, 2f, 512f));
                target.SetFloat("_PixelateStrength", fade);
                applied += 2;
            }
            return applied;
        }

        private static int ApplySSUExactContract(MaterialSnapshot snapshot, Material target)
        {
            if (!HasEnabledSSUExactEffect(snapshot) || !target.HasProperty("_SSUStatusContract")) return 0;
            target.SetFloat("_SSUStatusContract", 1f);
            return 1;
        }

        private static int ApplySharedStylizedMappings(MaterialSnapshot snapshot, Material target)
        {
            int applied = 0;
            if (snapshot.IsEnabled("_EnableHologram"))
            {
                applied += CopyVector(snapshot, target, "_HologramTint", "_HologramColor");
                applied += CopyFloat(snapshot, target, "_HologramLineSpeed", "_HologramSpeed");
                if (target.HasProperty("_HologramSpace"))
                {
                    target.SetFloat("_HologramSpace", 1f);
                    applied++;
                }
                if (target.HasProperty("_HologramDirection"))
                {
                    target.SetVector("_HologramDirection", Vector3.up);
                    applied++;
                }
                if (target.HasProperty("_HologramDistortionDirection"))
                {
                    target.SetVector("_HologramDistortionDirection", Vector2.right);
                    applied++;
                }
            }
            if (snapshot.IsEnabled("_EnableShine"))
            {
                if (target.HasProperty("_ShineSpace"))
                {
                    target.SetFloat("_ShineSpace", (float)ESCompositeProjectionSpace.局部UV);
                    applied++;
                }
                if (target.HasProperty("_ShineDirection"))
                {
                    target.SetVector("_ShineDirection", Vector4.zero);
                    applied++;
                }
            }
            if (target.HasProperty("_QualityTier") && HasEnabledSSUExactEffect(snapshot))
            {
                target.SetFloat("_QualityTier", 2f);
                applied++;
            }
            return applied;
        }

        private static bool HasEnabledSSUExactEffect(MaterialSnapshot snapshot)
        {
            return snapshot.IsEnabled("_EnableHologram")
                || snapshot.IsEnabled("_EnableGlitch")
                || snapshot.IsEnabled("_EnableInnerOutline")
                || snapshot.IsEnabled("_EnableOuterOutline")
                || snapshot.IsEnabled("_EnablePixelOutline")
                || HasEnabledSSUStatusEffect(snapshot);
        }

        private static bool HasEnabledSSUStatusEffect(MaterialSnapshot snapshot)
        {
            return snapshot.IsEnabled("_EnableFrozen")
                || snapshot.IsEnabled("_EnableBurn")
                || snapshot.IsEnabled("_EnableRainbow")
                || snapshot.IsEnabled("_EnableShine")
                || snapshot.IsEnabled("_EnablePoison");
        }

        private static int ApplyTimeMapping(MaterialSnapshot snapshot, Material target)
        {
            bool customTime = snapshot.IsEnabled("_ToggleCustomTime");
            bool unscaledTime = snapshot.IsEnabled("_ToggleUnscaledTime");
            bool quantizeToFPS = snapshot.IsEnabled("_ToggleTimeFPS");
            bool useFrequency = snapshot.IsEnabled("_ToggleTimeFrequency");

            target.SetFloat("_TimeMode", unscaledTime ? 1f : customTime ? 2f : 0f);
            target.SetFloat("_CustomTime", snapshot.GetFloat("_TimeValue", 0f));
            target.SetFloat(
                "_TimeScale",
                snapshot.IsEnabled("_ToggleTimeSpeed")
                    ? Mathf.Clamp(snapshot.GetFloat("_TimeSpeed", 1f), -4f, 4f)
                    : 1f);
            target.SetFloat("_EnableTimeFPS", quantizeToFPS ? 1f : 0f);
            target.SetFloat(
                "_TimeFPS",
                Mathf.Clamp(Mathf.Abs(snapshot.GetFloat("_TimeFPS", 5f)), 0.01f, 240f));
            target.SetFloat("_EnableTimeFrequency", useFrequency ? 1f : 0f);
            target.SetFloat("_TimeFrequency", snapshot.GetFloat("_TimeFrequency", 2f));
            target.SetFloat("_TimeRange", snapshot.GetFloat("_TimeRange", 0.5f));
            return 8;
        }

        private static int ApplyLitRecolorMappings(MaterialSnapshot snapshot, Material target)
        {
            int applied = 0;
            applied += CopyVector(snapshot, target, "_RecolorRGBRedTint", "_RecolorRed");
            applied += CopyVector(snapshot, target, "_RecolorRGBGreenTint", "_RecolorGreen");
            applied += CopyVector(snapshot, target, "_RecolorRGBBlueTint", "_RecolorBlue");
            applied += CopyFloat(snapshot, target, "_RecolorRGBFade", "_RecolorRGBStrength", 0f, 1f);
            applied += CopyFloat(snapshot, target, "_RecolorRGBTextureToggle", "_RecolorRGBMaskToggle", 0f, 1f);
            applied += CopyTexture(snapshot, target, "_RecolorRGBTexture", "_RecolorRGBMask", false);

            applied += CopyVector(snapshot, target, "_RecolorRGBYCPRedTint", "_RecolorRGBYCPRed");
            applied += CopyVector(snapshot, target, "_RecolorRGBYCPGreenTint", "_RecolorRGBYCPGreen");
            applied += CopyVector(snapshot, target, "_RecolorRGBYCPBlueTint", "_RecolorRGBYCPBlue");
            applied += CopyVector(snapshot, target, "_RecolorRGBYCPYellowTint", "_RecolorRGBYCPYellow");
            applied += CopyVector(snapshot, target, "_RecolorRGBYCPCyanTint", "_RecolorRGBYCPCyan");
            applied += CopyVector(snapshot, target, "_RecolorRGBYCPPurpleTint", "_RecolorRGBYCPPurple");
            applied += CopyFloat(snapshot, target, "_RecolorRGBYCPFade", "_RecolorRGBYCPStrength", 0f, 1f);
            applied += CopyFloat(snapshot, target, "_RecolorRGBYCPTextureToggle", "_RecolorRGBYCPMaskToggle", 0f, 1f);
            applied += CopyTexture(snapshot, target, "_RecolorRGBYCPTexture", "_RecolorRGBYCPMask", false);
            return applied;
        }

        private static int CopyFloat(
            MaterialSnapshot snapshot,
            Material target,
            string sourceName,
            string targetName,
            float minimum = float.NegativeInfinity,
            float maximum = float.PositiveInfinity)
        {
            if (!snapshot.TryGetFloat(sourceName, out float value)) return 0;
            target.SetFloat(targetName, Mathf.Clamp(value, minimum, maximum));
            return 1;
        }

        private static int CopyVector(
            MaterialSnapshot snapshot,
            Material target,
            string sourceName,
            string targetName)
        {
            if (!snapshot.TryGetVector(sourceName, out Vector4 value)) return 0;
            target.SetVector(targetName, value);
            return 1;
        }

        private static int CopyTexture(
            MaterialSnapshot snapshot,
            Material target,
            string sourceName,
            string targetName,
            bool copyScaleOffset)
        {
            if (!snapshot.TryGetTexture(sourceName, out TextureSnapshot value)) return 0;
            target.SetTexture(targetName, value.Texture);
            if (copyScaleOffset)
            {
                target.SetTextureScale(targetName, value.Scale);
                target.SetTextureOffset(targetName, value.Offset);
            }
            return 1;
        }

        private static void ApplyBlendMode(Material target, ESCompositeSSUMigrationReport report)
        {
            if (report.TargetShaderName == LitShaderName)
            {
                bool cutout = report.SourceShaderName.IndexOf("Cutout", StringComparison.Ordinal) >= 0;
                ES3DLitCompositeURPProperties.SetSurfaceMode(
                    target,
                    cutout ? ES3DLitSurfaceMode.透明裁剪 : ES3DLitSurfaceMode.透明混合);
                if (cutout)
                {
                    MaterialSnapshot snapshot = MaterialSnapshot.Capture(report.SourceMaterial);
                    float cutoff = snapshot.GetFloat(
                        "_AlphaClip",
                        snapshot.GetFloat("_ShadowClip", snapshot.GetFloat("_AlphaCutoff", 0.5f)));
                    target.SetFloat("_Cutoff", Mathf.Clamp01(cutoff));
                }
                return;
            }
            if (report.TargetShaderName == UiShaderName)
            {
                ESUICompositeURPProperties.SetBlendMode(
                    target,
                    (ESUICompositeBlendMode)Mathf.Clamp((int)report.BlendMode - 1, 0, 3));
                return;
            }
            ES2DCompositeURPProperties.SetBlendMode(
                target,
                (ES2DCompositeBlendMode)Mathf.Clamp((int)report.BlendMode - 1, 0, 3));
        }

        #endregion

        #region Serialized Material Snapshot

        private readonly struct TextureSnapshot
        {
            internal readonly Texture Texture;
            internal readonly Vector2 Scale;
            internal readonly Vector2 Offset;

            internal TextureSnapshot(Texture texture, Vector2 scale, Vector2 offset)
            {
                Texture = texture;
                Scale = scale;
                Offset = offset;
            }
        }

        private sealed class MaterialSnapshot
        {
            internal readonly Dictionary<string, float> Floats = new Dictionary<string, float>(StringComparer.Ordinal);
            private readonly Dictionary<string, Vector4> vectors = new Dictionary<string, Vector4>(StringComparer.Ordinal);
            private readonly Dictionary<string, TextureSnapshot> textures = new Dictionary<string, TextureSnapshot>(StringComparer.Ordinal);

            internal static MaterialSnapshot Capture(Material material)
            {
                var snapshot = new MaterialSnapshot();
                var serialized = new SerializedObject(material);
                serialized.UpdateIfRequiredOrScript();
                snapshot.CaptureFloats(serialized.FindProperty("m_SavedProperties.m_Floats"));
                snapshot.CaptureFloats(serialized.FindProperty("m_SavedProperties.m_Ints"));
                snapshot.CaptureVectors(serialized.FindProperty("m_SavedProperties.m_Colors"));
                snapshot.CaptureTextures(serialized.FindProperty("m_SavedProperties.m_TexEnvs"));
                snapshot.CaptureShaderDefaults(material);
                return snapshot;
            }

            internal bool HasFloat(string name) => Floats.ContainsKey(name);
            internal bool HasVector(string name) => vectors.ContainsKey(name);
            internal bool HasTexture(string name) => textures.ContainsKey(name);
            internal bool IsEnabled(string name) => GetFloat(name, 0f) > 0.5f;

            internal bool TryGetFloat(string name, out float value) => Floats.TryGetValue(name, out value);
            internal bool TryGetVector(string name, out Vector4 value) => vectors.TryGetValue(name, out value);
            internal bool TryGetTexture(string name, out TextureSnapshot value) => textures.TryGetValue(name, out value);

            internal float GetFloat(string name, float fallback)
            {
                return Floats.TryGetValue(name, out float value) ? value : fallback;
            }

            internal Vector4 GetVector(string name, Vector4 fallback)
            {
                return vectors.TryGetValue(name, out Vector4 value) ? value : fallback;
            }

            internal Vector2 GetVector2(string name, Vector2 fallback)
            {
                if (!vectors.TryGetValue(name, out Vector4 value)) return fallback;
                return new Vector2(value.x, value.y);
            }

            internal Vector2 GetTextureSize(string name, Vector2 fallback)
            {
                return textures.TryGetValue(name, out TextureSnapshot value) && value.Texture != null
                    ? new Vector2(Mathf.Max(1f, value.Texture.width), Mathf.Max(1f, value.Texture.height))
                    : fallback;
            }

            internal bool IsRelevantProperty(string propertyName)
            {
                bool belongsToEffect = false;
                bool belongsToEnabledEffect = false;
                foreach (KeyValuePair<string, float> pair in Floats)
                {
                    if (!pair.Key.StartsWith("_Enable", StringComparison.Ordinal)) continue;
                    string effectPrefix = "_" + pair.Key.Substring("_Enable".Length);
                    if (!propertyName.StartsWith(effectPrefix, StringComparison.Ordinal)) continue;
                    belongsToEffect = true;
                    if (pair.Value > 0.5f) belongsToEnabledEffect = true;
                }
                return !belongsToEffect || belongsToEnabledEffect;
            }

            internal List<string> FindPropertiesMissingFromTarget(string prefix, Shader targetShader)
            {
                var candidates = new HashSet<string>(StringComparer.Ordinal);
                foreach (string name in Floats.Keys)
                    if (name.StartsWith(prefix, StringComparison.Ordinal)) candidates.Add(name);
                foreach (string name in vectors.Keys)
                    if (name.StartsWith(prefix, StringComparison.Ordinal)) candidates.Add(name);
                foreach (string name in textures.Keys)
                    if (name.StartsWith(prefix, StringComparison.Ordinal)) candidates.Add(name);

                var missing = new List<string>();
                foreach (string name in candidates)
                    if (targetShader.FindPropertyIndex(name) < 0) missing.Add(name);
                missing.Sort(StringComparer.Ordinal);
                return missing;
            }

            private void CaptureFloats(SerializedProperty array)
            {
                if (array == null || !array.isArray) return;
                for (int i = 0; i < array.arraySize; i++)
                {
                    SerializedProperty entry = array.GetArrayElementAtIndex(i);
                    SerializedProperty key = entry.FindPropertyRelative("first");
                    SerializedProperty value = entry.FindPropertyRelative("second");
                    if (key != null && value != null && !string.IsNullOrEmpty(key.stringValue))
                        Floats[key.stringValue] = value.propertyType == SerializedPropertyType.Integer
                            ? value.intValue
                            : value.floatValue;
                }
            }

            private void CaptureVectors(SerializedProperty array)
            {
                if (array == null || !array.isArray) return;
                for (int i = 0; i < array.arraySize; i++)
                {
                    SerializedProperty entry = array.GetArrayElementAtIndex(i);
                    SerializedProperty key = entry.FindPropertyRelative("first");
                    SerializedProperty value = entry.FindPropertyRelative("second");
                    if (key != null && value != null && !string.IsNullOrEmpty(key.stringValue))
                        vectors[key.stringValue] = value.colorValue;
                }
            }

            private void CaptureTextures(SerializedProperty array)
            {
                if (array == null || !array.isArray) return;
                for (int i = 0; i < array.arraySize; i++)
                {
                    SerializedProperty entry = array.GetArrayElementAtIndex(i);
                    SerializedProperty key = entry.FindPropertyRelative("first");
                    SerializedProperty value = entry.FindPropertyRelative("second");
                    if (key == null || value == null || string.IsNullOrEmpty(key.stringValue)) continue;
                    SerializedProperty texture = value.FindPropertyRelative("m_Texture");
                    SerializedProperty scale = value.FindPropertyRelative("m_Scale");
                    SerializedProperty offset = value.FindPropertyRelative("m_Offset");
                    textures[key.stringValue] = new TextureSnapshot(
                        texture != null ? texture.objectReferenceValue as Texture : null,
                        scale != null ? scale.vector2Value : Vector2.one,
                        offset != null ? offset.vector2Value : Vector2.zero);
                }
            }

            private void CaptureShaderDefaults(Material material)
            {
                Shader shader = material.shader;
                if (shader == null || shader.name == "Hidden/InternalErrorShader") return;
                int count = shader.GetPropertyCount();
                for (int i = 0; i < count; i++)
                {
                    string name = shader.GetPropertyName(i);
                    switch (shader.GetPropertyType(i))
                    {
                        case ShaderPropertyType.Texture:
                            if (!textures.ContainsKey(name))
                                textures[name] = new TextureSnapshot(
                                    material.GetTexture(name),
                                    material.GetTextureScale(name),
                                    material.GetTextureOffset(name));
                            break;
                        case ShaderPropertyType.Color:
                            if (!vectors.ContainsKey(name)) vectors[name] = material.GetColor(name);
                            break;
                        case ShaderPropertyType.Vector:
                            if (!vectors.ContainsKey(name)) vectors[name] = material.GetVector(name);
                            break;
                        case ShaderPropertyType.Float:
                        case ShaderPropertyType.Range:
                            if (!Floats.ContainsKey(name)) Floats[name] = material.GetFloat(name);
                            break;
                    }
                }
            }
        }

        #endregion
    }
}
