using System;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    internal static partial class ESCompositeCodingHelper
    {
        #region Code Generation

        private static CodeSet BuildCode(MaterialProperty property, string displayName, Shader shader)
        {
            bool isUi = shader != null && shader.name == "ES/UI/Composite URP";
            PropertyHelp help = ResolveHelp(shader, property, displayName);
            if (isUi && property.name == "_MainTex")
                return BuildUITextureCode(help);

            string propertyId = GetPropertyId(shader, property.name);
            string declaration;
            string assignment;
            CodeSet keywordCode;
            if (TryBuildMaterialKeywordCode(shader, property.name, displayName, help, out keywordCode))
                return keywordCode;

            if (TryBuildEnumCode(shader, property.name, out declaration, out assignment))
            {
                string enumCall = (isUi ? "materialInstance" : "propertyBlock") + assignment;
                string enumFocused = BuildFocusedExample(isUi, false, declaration, enumCall);
                string enumFull = BuildFullExample(isUi, declaration, enumCall);
                string enumNote = isUi
                    ? "UI Graphic 没有 MaterialPropertyBlock；示例只克隆一次材质并缓存，避免逐帧访问或修改共享材质。"
                    : "该属性在 ES 中有强枚举语义；示例仍通过 MaterialPropertyBlock 写入，不污染共享 Material。";
                return new CodeSet(enumCall, enumFocused, enumFull, enumNote, isUi, help, isUi);
            }

            if (IsBooleanProperty(property.name))
            {
                declaration = "public bool 是否启用 = false;";
                assignment = ".SetFloat(" + propertyId + ", 是否启用 ? 1f : 0f);";
                string boolCall = (isUi ? "materialInstance" : "propertyBlock") + assignment;
                string boolFocused = BuildFocusedExample(isUi, false, declaration, boolCall);
                string boolFull = BuildFullExample(isUi, declaration, boolCall);
                string boolNote = isUi
                    ? "UI Graphic 没有 MaterialPropertyBlock；示例使用缓存的独立材质实例写入 0/1。"
                    : "Shader Toggle 在运行时仍是 0/1 浮点属性；示例用 bool 表达语义，再转换为浮点写入 MaterialPropertyBlock。";
                return new CodeSet(boolCall, boolFocused, boolFull, boolNote, isUi, help, isUi);
            }

            switch (property.type)
            {
                case MaterialProperty.PropType.Color:
                    declaration = "public Color 属性值 = Color.white;";
                    assignment = ".SetColor(" + propertyId + ", 属性值);";
                    break;
                case MaterialProperty.PropType.Vector:
                    declaration = "public Vector4 属性值 = Vector4.zero;";
                    assignment = ".SetVector(" + propertyId + ", 属性值);";
                    break;
                case MaterialProperty.PropType.Texture:
                    declaration = "public Texture 属性值;";
                    assignment = ".SetTexture(" + propertyId + ", 属性值);";
                    break;
                default:
                    declaration = "public float 属性值;";
                    assignment = ".SetFloat(" + propertyId + ", 属性值);";
                    break;
            }

            string call = (isUi ? "materialInstance" : "propertyBlock") + assignment;
            string focused = BuildFocusedExample(isUi, false, declaration, call);
            string full = BuildFullExample(isUi, declaration, call);

            string note = isUi
                ? "UI Graphic 没有 MaterialPropertyBlock；示例克隆并缓存独立材质，不修改共享材质，也不逐帧创建实例。"
                : "Renderer 示例使用 MaterialPropertyBlock，不实例化共享 Material，适合对象级运行时参数。";
            return new CodeSet(call, focused, full, note, isUi, help, isUi);
        }

        private static CodeSet BuildUITextureCode(PropertyHelp sourceHelp)
        {
            const string call = "rawImage.texture = 主纹理;\n        rawImage.SetMaterialDirty();";
            const string focused = "using UnityEngine;\nusing UnityEngine.UI;\n\n"
                + "public static class ESUI主纹理示例\n"
                + "{\n"
                + "    public static void 应用到RawImage(RawImage rawImage, Texture 主纹理)\n"
                + "    {\n"
                + "        if (rawImage == null) return;\n"
                + "        rawImage.texture = 主纹理;\n"
                + "        rawImage.SetMaterialDirty();\n"
                + "    }\n"
                + "}";
            const string full = "using UnityEngine;\nusing UnityEngine.UI;\n\n"
                + "[RequireComponent(typeof(RawImage))]\n"
                + "public sealed class ESUI主纹理接线示例 : MonoBehaviour\n"
                + "{\n"
                + "    public Texture 主纹理;\n\n"
                + "    private RawImage rawImage;\n\n"
                + "    private void Awake()\n"
                + "    {\n"
                + "        rawImage = GetComponent<RawImage>();\n"
                + "        Apply();\n"
                + "    }\n\n"
                + "    public void Apply()\n"
                + "    {\n"
                + "        if (rawImage == null) return;\n"
                + "        rawImage.texture = 主纹理;\n"
                + "        rawImage.SetMaterialDirty();\n"
                + "    }\n"
                + "}";
            var help = new PropertyHelp(
                sourceHelp.Title,
                "UI 主纹理由 CanvasRenderer 按对象提供；RawImage 写 texture，Image 则替换 sprite。",
                "UI 图像资源",
                "RawImage.texture / Image.sprite",
                "UI Graphic 数据",
                "不要直接写材质 _MainTex；它会被 CanvasRenderer 的 PerRendererData 覆盖。",
                sourceHelp.Summary);
            return new CodeSet(
                call,
                focused,
                full,
                "示例使用 RawImage。若目标是 Image，请把纹理制作成 Sprite 并赋给 Image.sprite。",
                true,
                help,
                false);
        }

        private static bool TryBuildMaterialKeywordCode(Shader shader, string propertyName, string displayName, PropertyHelp sourceHelp, out CodeSet code)
        {
            code = null;
            if (shader == null) return false;

            bool isUi = shader.name == "ES/UI/Composite URP";
            string declaration;
            string call;
            string type;
            bool renderState = false;
            if (propertyName == "_QualityTier" && shader.name == "ES/3D/Lit Composite URP")
            {
                declaration = "public ESCompositeQualityTier 效果质量 = ESCompositeQualityTier.标准;";
                call = "ES3DLitCompositeURPProperties.SetQuality(materialInstance, 效果质量);";
                type = "枚举 + 材质关键词";
            }
            else if (propertyName == "_ResourceProfile" && shader.name == "ES/3D/Lit Composite URP")
            {
                declaration = "public ES3DLitResourceProfile 资源配置 = ES3DLitResourceProfile.动态完整;";
                call = "ES3DLitCompositeURPProperties.SetResourceProfile(materialInstance, 资源配置);";
                type = "枚举 + 资源掩码关键词";
            }
            else if (propertyName == "_QualityTier" && shader.name == "ES/3D/VFX Composite URP")
            {
                declaration = "public ESCompositeQualityTier 效果质量 = ESCompositeQualityTier.标准;";
                call = "ES3DVFXCompositeURPProperties.SetQuality(materialInstance, 效果质量);";
                type = "枚举 + 材质关键词";
            }
            else if (propertyName == "_ReceiveShadows" && shader.name == "ES/3D/Lit Composite URP")
            {
                declaration = "public bool 接收阴影 = true;";
                call = "ES3DLitCompositeURPProperties.SetReceiveShadows(materialInstance, 接收阴影);";
                type = "开关 + 材质关键词";
            }
            else if (propertyName == "_UseUIAlphaClip" && shader.name == "ES/UI/Composite URP")
            {
                declaration = "public bool 使用UI透明裁剪 = false;";
                call = "ESUICompositeURPProperties.SetUIAlphaClip(materialInstance, 使用UI透明裁剪);";
                type = "开关 + UI 材质关键词";
            }
            else if (shader.name == "ES/3D/VFX Composite URP" && propertyName == "_BlendMode")
            {
                declaration = "public ES3DVFXBlendMode 混合模式 = ES3DVFXBlendMode.透明混合;";
                call = "ES3DVFXCompositeURPProperties.SetBlendMode(materialInstance, 混合模式);";
                type = "强枚举 + 材质混合状态";
                renderState = true;
            }
            else if (shader.name == "ES/3D/VFX Composite URP" && propertyName == "_ZWriteMode")
            {
                declaration = "public ES3DVFXDepthWriteMode 深度写入 = ES3DVFXDepthWriteMode.关闭;";
                call = "ES3DVFXCompositeURPProperties.SetDepthWrite(materialInstance, 深度写入);";
                type = "强枚举 + 材质深度状态";
                renderState = true;
            }
            else if (shader.name == "ES/3D/VFX Composite URP" && propertyName == "_ZTest")
            {
                declaration = "public ES3DVFXDepthTestMode 深度测试 = ES3DVFXDepthTestMode.小于等于;";
                call = "ES3DVFXCompositeURPProperties.SetDepthTest(materialInstance, 深度测试);";
                type = "强枚举 + 材质深度状态";
                renderState = true;
            }
            else if (shader.name == "ES/3D/VFX Composite URP" && propertyName == "_Cull")
            {
                declaration = "public ES3DVFXCullMode 剔除模式 = ES3DVFXCullMode.双面;";
                call = "ES3DVFXCompositeURPProperties.SetCullMode(materialInstance, 剔除模式);";
                type = "强枚举 + 材质光栅状态";
                renderState = true;
            }
            else if (shader.name == "ES/3D/VFX Composite URP" && propertyName == "_QueueOffset")
            {
                declaration = "[Range(-50, 50)] public int 渲染队列偏移;";
                call = "ES3DVFXCompositeURPProperties.SetQueueOffset(materialInstance, 渲染队列偏移);";
                type = "整数 + 材质渲染队列";
                renderState = true;
            }
            else
            {
                return false;
            }

            PropertyHelp help = new PropertyHelp(
                displayName,
                sourceHelp.Description,
                type,
                isUi ? "UI Graphic / Image 独立材质" : "Renderer 独立材质实例",
                isUi ? "缓存的 Material 实例 + 本地 Shader Keyword" : "Material 属性 + 本地 Shader Keyword",
                renderState
                    ? "该选项属于 GPU 渲染状态，必须写入独立 Material；MaterialPropertyBlock 不能覆盖 Pass 状态。"
                    : "该选项决定编译变体，必须写入独立 Material；MaterialPropertyBlock 不能切换关键词。",
                sourceHelp.Summary);
            string full = isUi ? BuildFullExample(true, declaration, call) : BuildMaterialFullExample(declaration, call);
            string focused = BuildFocusedExample(isUi, !isUi, declaration, call);
            code = new CodeSet(call, focused, full,
                isUi
                    ? "UI Graphic 没有 MaterialPropertyBlock；示例缓存独立材质并同步关键词，修改后调用 SetMaterialDirty。"
                    : renderState
                    ? "这是材质级渲染状态。示例只创建一次材质实例，并在销毁时恢复原材质；不要用 MaterialPropertyBlock 设置。"
                    : "这是关键词驱动属性。示例只创建一次材质实例，并在销毁时恢复原材质；不要在 Update 中反复实例化材质。",
                isUi, help, true);
            return true;
        }

        private static string BuildFullExample(bool isUi, string declaration, string call)
        {
            if (isUi)
            {
                return "using UnityEngine;\n"
                    + "using UnityEngine.UI;\n"
                    + "using ES;\n\n"
                    + "public sealed class ESUIShaderExample : MonoBehaviour\n"
                    + "{\n    " + declaration + "\n\n"
                    + "    private Graphic graphic;\n"
                    + "    private Material originalMaterial;\n"
                    + "    private Material materialInstance;\n\n"
                    + "    private void Awake()\n"
                    + "    {\n"
                    + "        graphic = GetComponent<Graphic>();\n"
                    + "        originalMaterial = graphic.material;\n"
                    + "        Material source = graphic.materialForRendering;\n"
                    + "        if (source == null) return;\n\n"
                    + "        materialInstance = new Material(source)\n"
                    + "        {\n"
                    + "            name = source.name + \" (ES UI Instance)\"\n"
                    + "        };\n"
                    + "        graphic.material = materialInstance;\n"
                    + "        Apply();\n"
                    + "    }\n\n"
                    + "    public void Apply()\n"
                    + "    {\n"
                    + "        if (materialInstance == null) return;\n"
                    + "        " + call + "\n"
                    + "        graphic.SetMaterialDirty();\n"
                    + "    }\n\n"
                    + "    private void OnDestroy()\n"
                    + "    {\n"
                    + "        if (graphic != null && graphic.material == materialInstance)\n"
                    + "            graphic.material = originalMaterial;\n"
                    + "        if (materialInstance != null)\n"
                    + "            Destroy(materialInstance);\n"
                    + "    }\n"
                    + "}";
            }

            return "using UnityEngine;\n"
                + "using ES;\n\n"
                + "public sealed class ESShaderExample : MonoBehaviour\n"
                + "{\n    " + declaration + "\n\n"
                + "    private Renderer targetRenderer;\n"
                + "    private MaterialPropertyBlock propertyBlock;\n\n"
                + "    private void Awake()\n"
                + "    {\n"
                + "        targetRenderer = GetComponent<Renderer>();\n"
                + "        propertyBlock = new MaterialPropertyBlock();\n"
                + "        Apply();\n"
                + "    }\n\n"
                + "    public void Apply()\n"
                + "    {\n"
                + "        if (targetRenderer == null) return;\n"
                + "        targetRenderer.GetPropertyBlock(propertyBlock);\n"
                + "        " + call + "\n"
                + "        targetRenderer.SetPropertyBlock(propertyBlock);\n"
                + "    }\n"
                + "}";
        }

        private static string BuildFocusedExample(bool isUi, bool usesMaterialInstance, string declaration, string call)
        {
            string targetType = isUi || usesMaterialInstance ? "Material" : "MaterialPropertyBlock";
            string targetName = isUi || usesMaterialInstance ? "materialInstance" : "propertyBlock";
            string methodName = isUi || usesMaterialInstance ? "应用到独立材质" : "写入参数块";
            string valueParameter = BuildValueParameter(declaration);
            return "using UnityEngine;\nusing ES;\n\n"
                + "public static class ESShader参数示例\n"
                + "{\n"
                + "    // " + targetName + " 由调用方传入；这里只负责当前属性，不获取 Renderer，也不回写参数块。\n"
                + "    public static void " + methodName + "(" + targetType + " " + targetName + ", " + valueParameter + ")\n"
                + "    {\n"
                + "        if (" + targetName + " == null) return;\n"
                + "        " + call + "\n"
                + "    }\n"
                + "}";
        }

        private static string BuildValueParameter(string declaration)
        {
            string value = declaration == null ? "float 属性值" : declaration.Trim();
            while (value.StartsWith("[", StringComparison.Ordinal))
            {
                int attributeEnd = value.IndexOf(']');
                if (attributeEnd < 0) break;
                value = value.Substring(attributeEnd + 1).TrimStart();
            }
            if (value.StartsWith("public ", StringComparison.Ordinal))
                value = value.Substring("public ".Length);
            int equals = value.IndexOf('=');
            if (equals >= 0) value = value.Substring(0, equals);
            return value.Trim().TrimEnd(';');
        }

        private static string BuildMaterialFullExample(string declaration, string call)
        {
            return "using UnityEngine;\n"
                + "using ES;\n\n"
                + "public sealed class ESShaderMaterialKeywordExample : MonoBehaviour\n"
                + "{\n    " + declaration + "\n\n"
                + "    private Renderer targetRenderer;\n"
                + "    private Material originalMaterial;\n"
                + "    private Material materialInstance;\n\n"
                + "    private void Awake()\n"
                + "    {\n"
                + "        targetRenderer = GetComponent<Renderer>();\n"
                + "        originalMaterial = targetRenderer.sharedMaterial;\n"
                + "        if (originalMaterial == null) return;\n\n"
                + "        materialInstance = new Material(originalMaterial)\n"
                + "        {\n"
                + "            name = originalMaterial.name + \" (ES Instance)\"\n"
                + "        };\n"
                + "        targetRenderer.sharedMaterial = materialInstance;\n"
                + "        Apply();\n"
                + "    }\n\n"
                + "    public void Apply()\n"
                + "    {\n"
                + "        if (materialInstance == null) return;\n"
                + "        " + call + "\n"
                + "    }\n\n"
                + "    private void OnDestroy()\n"
                + "    {\n"
                + "        if (targetRenderer != null && targetRenderer.sharedMaterial == materialInstance)\n"
                + "            targetRenderer.sharedMaterial = originalMaterial;\n"
                + "        if (materialInstance != null)\n"
                + "            Destroy(materialInstance);\n"
                + "    }\n"
                + "}";
        }

        private static bool TryBuildEnumCode(Shader shader, string propertyName, out string declaration, out string assignment)
        {
            declaration = null;
            assignment = null;
            if (shader == null)
                return false;

            switch (shader.name)
            {
                case "ES/2D/Composite URP":
                    if (propertyName == "_AnimationMode")
                    {
                        declaration = "public ES2DCompositeAnimationMode 动画模式 = ES2DCompositeAnimationMode.序列帧;";
                        assignment = ".SetFloat(ES2DCompositeURPProperties.AnimationMode, (float)动画模式);";
                        return true;
                    }
                    if (propertyName == "_FadeMode")
                    {
                        declaration = "public ES2DCompositeFadeMode 渐隐模式 = ES2DCompositeFadeMode.渐隐;";
                        assignment = ".SetFloat(ES2DCompositeURPProperties.FadeMode, (float)渐隐模式);";
                        return true;
                    }
                    if (propertyName == "_CoordinateMode")
                    {
                        declaration = "public ES2DCompositeCoordinateMode 坐标模式 = ES2DCompositeCoordinateMode.UV;";
                        assignment = ".SetFloat(ES2DCompositeURPProperties.CoordinateMode, (float)坐标模式);";
                        return true;
                    }
                    if (propertyName == "_TimeMode")
                    {
                        declaration = "public ES2DCompositeTimeMode 时间模式 = ES2DCompositeTimeMode.场景时间;";
                        assignment = ".SetFloat(ES2DCompositeURPProperties.TimeMode, (float)时间模式);";
                        return true;
                    }
                    break;
                case "ES/3D/Lit Composite URP":
                    if (propertyName == "_VertexAnimationMask")
                    {
                        declaration = "public ESCompositeVertexColorMask 顶点色动画遮罩 = ESCompositeVertexColorMask.无;";
                        assignment = ".SetFloat(ES3DLitCompositeURPProperties.VertexAnimationMask, (float)顶点色动画遮罩);";
                        return true;
                    }
                    if (propertyName == "_TimeMode")
                    {
                        declaration = "public ESCompositeTimeMode 时间来源 = ESCompositeTimeMode.场景时间;";
                        assignment = ".SetFloat(ES3DLitCompositeURPProperties.TimeMode, (float)时间来源);";
                        return true;
                    }
                    if (propertyName == "_DissolveMode")
                    {
                        declaration = "public ES3DCompositeDissolveMode 溶解模式 = ES3DCompositeDissolveMode.噪声溶解;";
                        assignment = ".SetFloat(ES3DLitCompositeURPProperties.DissolveMode, (float)溶解模式);";
                        return true;
                    }
                    break;
                case "ES/3D/VFX Composite URP":
                    if (propertyName == "_SequencePlayback")
                    {
                        declaration = "public ES3DVFXSequencePlaybackMode 播放方式 = ES3DVFXSequencePlaybackMode.时间播放;";
                        assignment = ".SetFloat(ES3DVFXCompositeURPProperties.SequencePlayback, (float)播放方式);";
                        return true;
                    }
                    if (propertyName == "_VertexAnimationMask")
                    {
                        declaration = "public ESCompositeVertexColorMask 顶点色动画遮罩 = ESCompositeVertexColorMask.无;";
                        assignment = ".SetFloat(ES3DVFXCompositeURPProperties.VertexAnimationMask, (float)顶点色动画遮罩);";
                        return true;
                    }
                    if (propertyName == "_TimeMode")
                    {
                        declaration = "public ESCompositeTimeMode 时间来源 = ESCompositeTimeMode.场景时间;";
                        assignment = ".SetFloat(ES3DVFXCompositeURPProperties.TimeMode, (float)时间来源);";
                        return true;
                    }
                    if (propertyName == "_DissolveMode")
                    {
                        declaration = "public ES3DVFXDissolveMode 溶解模式 = ES3DVFXDissolveMode.溶解加边缘光;";
                        assignment = ".SetFloat(ES3DVFXCompositeURPProperties.DissolveMode, (float)溶解模式);";
                        return true;
                    }
                    break;
                case "ES/UI/Composite URP":
                    if (propertyName == "_TimeMode")
                    {
                        declaration = "public ESCompositeTimeMode 时间来源 = ESCompositeTimeMode.场景时间;";
                        assignment = ".SetFloat(ESUICompositeURPProperties.TimeMode, (float)时间来源);";
                        return true;
                    }
                    break;
            }
            return false;
        }

        private static string GetPropertyId(Shader shader, string propertyName)
        {
            if (shader != null)
            {
                bool litAdvancedSurfaceProperty = shader.name == "ES/3D/Lit Composite URP"
                    && (propertyName == "_EnableInnerOutline" || propertyName.StartsWith("_InnerOutline", StringComparison.Ordinal)
                        || propertyName == "_EnableOuterOutline" || propertyName.StartsWith("_OuterOutline", StringComparison.Ordinal)
                        || propertyName == "_EnablePixelOutline" || propertyName.StartsWith("_PixelOutline", StringComparison.Ordinal)
                        || propertyName == "_EnableHologram" || propertyName.StartsWith("_Hologram", StringComparison.Ordinal)
                        || propertyName == "_EnableGlitch" || propertyName.StartsWith("_Glitch", StringComparison.Ordinal));
                if (litAdvancedSurfaceProperty)
                    return "ES3DLitCompositeURPProperties." + ToPascal(propertyName);

                bool sharedESNativeGuiProperty = propertyName == "_EnableAddColor"
                    || propertyName.StartsWith("_AddColor", StringComparison.Ordinal)
                    || propertyName == "_EnableStrongTint" || propertyName.StartsWith("_StrongTint", StringComparison.Ordinal)
                    || propertyName == "_EnableAlphaTint" || propertyName.StartsWith("_AlphaTint", StringComparison.Ordinal)
                    || propertyName == "_EnableColorReplace" || propertyName.StartsWith("_Replace", StringComparison.Ordinal)
                    || propertyName == "_EnableBrightness" || propertyName == "_Brightness"
                    || propertyName == "_EnableContrast" || propertyName == "_Contrast"
                    || propertyName == "_EnableSaturation" || propertyName == "_Saturation"
                    || propertyName == "_EnableHue" || propertyName == "_Hue"
                    || propertyName == "_EnableNegative" || propertyName == "_NegativeFade"
                    || propertyName == "_EnableRainbow" || propertyName.StartsWith("_Rainbow", StringComparison.Ordinal)
                    || propertyName == "_EnableRecolorRGB" || propertyName.StartsWith("_RecolorRGB", StringComparison.Ordinal)
                    || propertyName == "_EnableRecolorRGBYCP" || propertyName.StartsWith("_RecolorRGBYCP", StringComparison.Ordinal)
                    || propertyName == "_EnableSplitToning" || propertyName.StartsWith("_SplitTone", StringComparison.Ordinal)
                    || propertyName == "_EnableInnerOutline" || propertyName.StartsWith("_InnerOutline", StringComparison.Ordinal)
                    || propertyName == "_EnableOuterOutline" || propertyName.StartsWith("_OuterOutline", StringComparison.Ordinal)
                    || propertyName == "_EnablePixelOutline" || propertyName.StartsWith("_PixelOutline", StringComparison.Ordinal)
                    || propertyName == "_EnablePingPongGlow" || propertyName.StartsWith("_Glow", StringComparison.Ordinal)
                    || propertyName == "_EnableFrozen" || propertyName.StartsWith("_Frozen", StringComparison.Ordinal)
                    || propertyName == "_EnableBurn" || propertyName.StartsWith("_Burn", StringComparison.Ordinal)
                    || propertyName == "_EnablePoison" || propertyName.StartsWith("_Poison", StringComparison.Ordinal)
                    || propertyName == "_EnableBlackTint" || propertyName.StartsWith("_BlackTint", StringComparison.Ordinal)
                    || propertyName == "_EnableInkSpread" || propertyName.StartsWith("_InkSpread", StringComparison.Ordinal)
                    || propertyName == "_EnableShiftHue" || propertyName.StartsWith("_ShiftHue", StringComparison.Ordinal)
                    || propertyName == "_EnableAddHue" || propertyName.StartsWith("_AddHue", StringComparison.Ordinal)
                    || propertyName == "_EnableSineGlow" || propertyName.StartsWith("_SineGlow", StringComparison.Ordinal)
                    || propertyName == "_EnableCamouflage" || propertyName.StartsWith("_Camouflage", StringComparison.Ordinal)
                    || propertyName == "_EnableMetal"
                    || (propertyName.StartsWith("_Metal", StringComparison.Ordinal)
                        && !propertyName.StartsWith("_Metallic", StringComparison.Ordinal))
                    || propertyName == "_EnableEnchanted" || propertyName.StartsWith("_Enchanted", StringComparison.Ordinal)
                    || propertyName == "_EnableShifting" || propertyName.StartsWith("_Shifting", StringComparison.Ordinal)
                    || propertyName == "_UberNoiseTexture";
                if (sharedESNativeGuiProperty)
                    return "ESCompositeURPProperties." + ToPascal(propertyName);

                bool sharedTimeProperty = propertyName == "_EnableTimeFPS"
                    || propertyName == "_TimeFPS"
                    || propertyName == "_EnableTimeFrequency"
                    || propertyName == "_TimeFrequency"
                    || propertyName == "_TimeRange";
                if (sharedTimeProperty)
                {
                    if (shader.name == "ES/2D/Composite URP") return "ES2DCompositeURPProperties." + ToPascal(propertyName);
                    if (shader.name == "ES/3D/Lit Composite URP") return "ES3DLitCompositeURPProperties." + ToPascal(propertyName);
                    if (shader.name == "ES/3D/VFX Composite URP") return "ES3DVFXCompositeURPProperties." + ToPascal(propertyName);
                    if (shader.name == "ES/UI/Composite URP") return "ESUICompositeURPProperties." + ToPascal(propertyName);
                }

                bool motionProperty = propertyName == "_EnableSparkle"
                    || propertyName == "_SparkleColor"
                    || propertyName == "_SparkleScale"
                    || propertyName == "_SparkleSpeed"
                    || propertyName == "_SparkleDensity"
                    || propertyName == "_SparkleSharpness"
                    || propertyName == "_SparkleIntensity"
                    || propertyName == "_EnableFlow" || propertyName == "_FlowSpeed" || propertyName == "_FlowStrength"
                    || propertyName == "_EnableFlowMap" || propertyName.StartsWith("_FlowMap", StringComparison.Ordinal)
                    || propertyName == "_EnableVertexAnimation" || propertyName.StartsWith("_VertexAnimation", StringComparison.Ordinal)
                    || propertyName == "_EnableSoftParticles" || propertyName.StartsWith("_SoftParticle", StringComparison.Ordinal)
                    || propertyName == "_EnableSequence" || propertyName.StartsWith("_Sequence", StringComparison.Ordinal)
                    || propertyName == "_EnablePolarUV" || propertyName.StartsWith("_Polar", StringComparison.Ordinal)
                    || propertyName == "_EnableVertexStreams" || propertyName.StartsWith("_VertexStream", StringComparison.Ordinal)
                    || propertyName == "_EnableRadialMask" || propertyName.StartsWith("_RadialMask", StringComparison.Ordinal)
                    || propertyName == "_EnableFresnelMask" || propertyName.StartsWith("_Fresnel", StringComparison.Ordinal)
                    || propertyName == "_EnableDepthIntersection" || propertyName.StartsWith("_DepthIntersection", StringComparison.Ordinal)
                    || propertyName == "_EnableChromatic"
                    || propertyName == "_ChromaticOffset"
                    || propertyName == "_ChromaticIntensity"
                    || propertyName == "_ChromaticEdgeOnly"
                    || propertyName == "_ChromaticAngle"
                    || propertyName == "_EnableBlur"
                     || propertyName == "_BlurRadius"
                     || propertyName == "_BlurIntensity"
                     || propertyName == "_TilingMode"
                     || propertyName.StartsWith("_WorldTiling", StringComparison.Ordinal)
                     || propertyName.StartsWith("_ScreenTiling", StringComparison.Ordinal)
                     || propertyName == "_EnableSmoothPixelArt"
                     || propertyName == "_SmoothPixelStrength"
                     || propertyName == "_EnableCheckerboard"
                     || propertyName.StartsWith("_Checkerboard", StringComparison.Ordinal)
                     || propertyName == "_UberNoiseTexture"
                     || propertyName == "_EnableFlame"
                     || propertyName.StartsWith("_Flame", StringComparison.Ordinal)
                    || propertyName == "_EnableSmoke"
                    || propertyName.StartsWith("_Smoke", StringComparison.Ordinal)
                    || propertyName == "_EnableShadow" || propertyName.StartsWith("_Shadow", StringComparison.Ordinal)
                    || propertyName == "_EnableBlackTint" || propertyName.StartsWith("_BlackTint", StringComparison.Ordinal)
                    || propertyName == "_EnableInkSpread" || propertyName.StartsWith("_InkSpread", StringComparison.Ordinal)
                    || propertyName == "_EnableShiftHue" || propertyName.StartsWith("_ShiftHue", StringComparison.Ordinal)
                    || propertyName == "_EnableAddHue" || propertyName.StartsWith("_AddHue", StringComparison.Ordinal)
                    || propertyName == "_EnableSineGlow" || propertyName.StartsWith("_SineGlow", StringComparison.Ordinal)
                    || propertyName == "_EnableSqueeze" || propertyName.StartsWith("_Squeeze", StringComparison.Ordinal)
                    || propertyName == "_EnableSineRotate" || propertyName.StartsWith("_SineRotate", StringComparison.Ordinal)
                    || propertyName == "_EnableSineMove" || propertyName.StartsWith("_SineMove", StringComparison.Ordinal)
                    || propertyName == "_EnableSineScale" || propertyName.StartsWith("_SineScale", StringComparison.Ordinal)
                    || propertyName == "_EnableCustomFade" || propertyName.StartsWith("_CustomFade", StringComparison.Ordinal)
                    || propertyName == "_EnableFullGlowDissolve" || propertyName.StartsWith("_FullGlowDissolve", StringComparison.Ordinal)
                    || propertyName == "_EnableCamouflage" || propertyName.StartsWith("_Camouflage", StringComparison.Ordinal)
                    || propertyName == "_EnableMetal" || propertyName.StartsWith("_Metal", StringComparison.Ordinal)
                    || propertyName == "_EnableEnchanted" || propertyName.StartsWith("_Enchanted", StringComparison.Ordinal)
                    || propertyName == "_EnableShifting" || propertyName.StartsWith("_Shifting", StringComparison.Ordinal)
                    || propertyName == "_EnableShine"
                    || propertyName == "_ShineColor"
                    || propertyName == "_ShineSpeed"
                    || propertyName == "_ShineWidth"
                    || propertyName == "_ShineIntensity"
                    || propertyName == "_ShineAngle"
                    || propertyName == "_ShineDirection";
                if (motionProperty)
                {
                    if (shader.name == "ES/2D/Composite URP") return "ES2DCompositeURPProperties." + ToPascal(propertyName);
                    if (shader.name == "ES/3D/Lit Composite URP") return "ES3DLitCompositeURPProperties." + ToPascal(propertyName);
                    if (shader.name == "ES/3D/VFX Composite URP") return "ES3DVFXCompositeURPProperties." + ToPascal(propertyName);
                    if (shader.name == "ES/UI/Composite URP") return "ESUICompositeURPProperties." + ToPascal(propertyName);
                }
                if (propertyName == "_EnableTextureLayer1"
                    || propertyName == "_EnableTextureLayer2"
                    || propertyName.StartsWith("_TextureLayer1", StringComparison.Ordinal)
                    || propertyName.StartsWith("_TextureLayer2", StringComparison.Ordinal))
                    return "ESCompositeURPProperties." + ToPascal(propertyName);
                switch (shader.name)
                {
                    case "ES/2D/Composite URP":
                        if (propertyName == "_AnimationMode"
                            || propertyName == "_FadeMode"
                            || propertyName == "_FadeProgress"
                            || propertyName == "_CoordinateMode"
                            || propertyName == "_TimeMode"
                            || propertyName == "_CustomTime"
                            || propertyName == "_TimeScale"
                            || propertyName == "_MainTexScaleOffset"
                            || propertyName == "_SequenceFrame"
                            || propertyName == "_SequenceSpeed"
                            || propertyName == "_GlowIntensity"
                            || propertyName == "_ShineIntensity"
                            || propertyName == "_MainTex"
                            || propertyName == "_Color"
                            || propertyName == "_VertexColorStrength"
                            || propertyName == "_DistortionStrength"
                            || propertyName == "_EnableHologram"
                            || propertyName == "_EnableGlitch"
                            || propertyName == "_EnableBurn"
                            || propertyName == "_EnablePoison"
                            || propertyName == "_EnableFrozen")
                            return "ES2DCompositeURPProperties." + ToPascal(propertyName);
                        break;
                    case "ES/3D/Lit Composite URP":
                        if (propertyName == "_TimeMode"
                            || propertyName == "_CustomTime"
                            || propertyName == "_TimeScale"
                            || propertyName == "_MainTexScaleOffset"
                            || propertyName == "_QualityTier"
                            || propertyName == "_DissolveMode"
                            || propertyName == "_DissolveProgress"
                            || propertyName == "_RimIntensity"
                            || propertyName == "_ShineIntensity"
                            || propertyName == "_BaseColor"
                            || propertyName == "_UseMetallicMap"
                            || propertyName == "_MetallicMap"
                            || propertyName == "_Metallic"
                            || propertyName == "_Smoothness"
                            || propertyName == "_Occlusion"
                            || propertyName == "_UseNormalMap"
                            || propertyName == "_UseOcclusionMap"
                            || propertyName == "_UseEmission"
                            || propertyName == "_EnableRim"
                            || propertyName == "_EnableBurn"
                            || propertyName == "_AlphaClip"
                            || propertyName == "_ReceiveShadows")
                            return "ES3DLitCompositeURPProperties." + ToPascal(propertyName);
                        break;
                    case "ES/3D/VFX Composite URP":
                        if (propertyName == "_TimeMode"
                            || propertyName == "_CustomTime"
                            || propertyName == "_TimeScale"
                            || propertyName == "_MainTexScaleOffset"
                            || propertyName == "_QualityTier"
                            || propertyName == "_DissolveMode"
                            || propertyName == "_DissolveProgress"
                            || propertyName == "_EnableHologram"
                            || propertyName == "_EnableGlitch"
                            || propertyName == "_Color"
                            || propertyName == "_VertexColorStrength"
                            || propertyName == "_Distortion")
                            return "ES3DVFXCompositeURPProperties." + ToPascal(propertyName);
                        break;
                    case "ES/UI/Composite URP":
                        if (propertyName == "_TimeMode"
                            || propertyName == "_CustomTime"
                            || propertyName == "_TimeScale"
                            || propertyName == "_MainTexScaleOffset"
                            || propertyName == "_EnableHologram"
                            || propertyName == "_EnableGlitch"
                            || propertyName == "_GlitchSpeed"
                            || propertyName == "_Color"
                            || propertyName == "_VertexColorStrength"
                            || propertyName == "_AlphaClip"
                            || propertyName == "_EnableTMPCompatibility"
                            || propertyName == "_FaceColor"
                            || propertyName == "_FaceDilate"
                            || propertyName == "_OutlineColor"
                            || propertyName == "_OutlineWidth"
                            || propertyName == "_OutlineSoftness"
                            || propertyName == "_EnableUnderlay"
                            || propertyName.StartsWith("_Underlay", StringComparison.Ordinal)
                            || propertyName == "_WeightNormal"
                            || propertyName == "_WeightBold")
                            return "ESUICompositeURPProperties." + ToPascal(propertyName);
                        break;
                }
            }
            return "Shader.PropertyToID(\"" + propertyName + "\")";
        }

        private static string ToPascal(string propertyName)
        {
            string value = propertyName.TrimStart('_');
            switch (value)
            {
                case "EnableHologram": return "HologramEnabled";
                case "EnableGlitch": return "GlitchEnabled";
                case "EnableTimeFPS": return "TimeFPSEnabled";
                case "EnableTimeFrequency": return "TimeFrequencyEnabled";
                case "EnableBurn": return "BurnEnabled";
                case "EnablePoison": return "PoisonEnabled";
                case "EnableFrozen": return "FrozenEnabled";
                case "EnableRim": return "RimEnabled";
                case "EnableShine": return "ShineEnabled";
                case "EnableSparkle": return "SparkleEnabled";
                case "EnableFlow": return "FlowEnabled";
                case "EnableFlowMap": return "FlowMapEnabled";
                case "EnableVertexAnimation": return "VertexAnimationEnabled";
                case "EnableSoftParticles": return "SoftParticlesEnabled";
                case "EnableSequence": return "SequenceEnabled";
                case "EnablePolarUV": return "PolarUVEnabled";
                case "EnableVertexStreams": return "VertexStreamsEnabled";
                case "EnableRadialMask": return "RadialMaskEnabled";
                case "EnableFresnelMask": return "FresnelMaskEnabled";
                case "EnableDepthIntersection": return "DepthIntersectionEnabled";
                case "EnableChromatic": return "ChromaticEnabled";
                case "EnableBlur": return "BlurEnabled";
                case "EnableSmoothPixelArt": return "SmoothPixelArtEnabled";
                case "EnableCheckerboard": return "CheckerboardEnabled";
                case "EnableFlame": return "FlameEnabled";
                case "EnableSmoke": return "SmokeEnabled";
                case "EnableShadow": return "SpriteShadowEnabled";
                case "ShadowFade": return "SpriteShadowFade";
                case "ShadowOffset": return "SpriteShadowOffset";
                case "ShadowColor": return "SpriteShadowColor";
                case "EnableBlackTint": return "BlackTintEnabled";
                case "EnableInkSpread": return "InkSpreadEnabled";
                case "EnableShiftHue": return "ShiftHueEnabled";
                case "EnableAddHue": return "AddHueEnabled";
                case "AddHueMaskToggle": return "AddHueMaskEnabled";
                case "EnableSineGlow": return "SineGlowEnabled";
                case "SineGlowMaskToggle": return "SineGlowMaskEnabled";
                case "EnableSqueeze": return "SqueezeEnabled";
                case "EnableSineRotate": return "SineRotateEnabled";
                case "EnableSineMove": return "SineMoveEnabled";
                case "EnableSineScale": return "SineScaleEnabled";
                case "EnableCustomFade": return "CustomFadeEnabled";
                case "CustomFadeFadeMask": return "CustomFadeMask";
                case "EnableFullGlowDissolve": return "FullGlowDissolveEnabled";
                case "EnableCamouflage": return "CamouflageEnabled";
                case "CamouflageAnimationToggle": return "CamouflageAnimationEnabled";
                case "EnableMetal": return "MetalEnabled";
                case "MetalMaskToggle": return "MetalMaskEnabled";
                case "EnableEnchanted": return "EnchantedEnabled";
                case "EnchantedRainbowToggle": return "EnchantedRainbowEnabled";
                case "EnchantedLerpToggle": return "EnchantedLerpEnabled";
                case "EnableShifting": return "ShiftingEnabled";
                case "ShiftingRainbowToggle": return "ShiftingRainbowEnabled";
                case "EnableAddColor": return "AddColorEnabled";
                case "EnableStrongTint": return "StrongTintEnabled";
                case "EnableAlphaTint": return "AlphaTintEnabled";
                case "EnableColorReplace": return "ColorReplaceEnabled";
                case "EnableBrightness": return "BrightnessEnabled";
                case "EnableContrast": return "ContrastEnabled";
                case "EnableSaturation": return "SaturationEnabled";
                case "EnableHue": return "HueEnabled";
                case "EnableNegative": return "NegativeEnabled";
                case "EnableRainbow": return "RainbowEnabled";
                case "EnableRecolorRGB": return "RecolorRGBEnabled";
                case "RecolorRGBMaskToggle": return "RecolorRGBMaskEnabled";
                case "EnableRecolorRGBYCP": return "RecolorRGBYCPEnabled";
                case "RecolorRGBYCPMaskToggle": return "RecolorRGBYCPMaskEnabled";
                case "EnableSplitToning": return "SplitToningEnabled";
                case "EnableInnerOutline": return "InnerOutlineEnabled";
                case "InnerOutlineDistortionToggle": return "InnerOutlineDistortionEnabled";
                case "InnerOutlineTextureToggle": return "InnerOutlineTextureEnabled";
                case "InnerOutlineOutlineOnlyToggle": return "InnerOutlineOnly";
                case "EnableOuterOutline": return "OuterOutlineEnabled";
                case "OuterOutlineDistortionToggle": return "OuterOutlineDistortionEnabled";
                case "OuterOutlineTextureToggle": return "OuterOutlineTextureEnabled";
                case "OuterOutlineOutlineOnlyToggle": return "OuterOutlineOnly";
                case "EnablePixelOutline": return "PixelOutlineEnabled";
                case "PixelOutlineTextureToggle": return "PixelOutlineTextureEnabled";
                case "PixelOutlineOutlineOnlyToggle": return "PixelOutlineOnly";
                case "EnablePingPongGlow": return "PingPongGlowEnabled";
                case "UseNormalMap": return "NormalMapEnabled";
                case "UseMetallicMap": return "MetallicMapEnabled";
                case "EnableTextureLayer1": return "TextureLayer1Enabled";
                case "EnableTextureLayer2": return "TextureLayer2Enabled";
                case "TextureLayer1ScrollToggle": return "TextureLayer1ScrollEnabled";
                case "TextureLayer2ScrollToggle": return "TextureLayer2ScrollEnabled";
                case "TextureLayer1SheetToggle": return "TextureLayer1SheetEnabled";
                case "TextureLayer2SheetToggle": return "TextureLayer2SheetEnabled";
                case "TextureLayer1ContrastToggle": return "TextureLayer1ContrastEnabled";
                case "TextureLayer2ContrastToggle": return "TextureLayer2ContrastEnabled";
                case "EnableTMPCompatibility": return "TMPCompatibility";
                case "FaceColor": return "TMPFaceColor";
                case "FaceDilate": return "TMPFaceDilate";
                case "OutlineColor": return "TMPOutlineColor";
                case "OutlineWidth": return "TMPOutlineWidth";
                case "OutlineSoftness": return "TMPOutlineSoftness";
                case "EnableUnderlay": return "TMPUnderlayEnabled";
                case "UnderlayColor": return "TMPUnderlayColor";
                case "UnderlayOffsetX": return "TMPUnderlayOffsetX";
                case "UnderlayOffsetY": return "TMPUnderlayOffsetY";
                case "UnderlayDilate": return "TMPUnderlayDilate";
                case "UnderlaySoftness": return "TMPUnderlaySoftness";
                case "WeightNormal": return "TMPWeightNormal";
                case "WeightBold": return "TMPWeightBold";
                case "UseOcclusionMap": return "OcclusionMapEnabled";
                case "UseEmission": return "EmissionEnabled";
            }
            if (string.IsNullOrEmpty(value)) return "Property";
            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }

        private sealed class CodeSet
        {
            internal readonly string PropertyCall;
            internal readonly string FocusedExample;
            internal readonly string FullExample;
            internal readonly string Note;
            internal readonly bool IsUi;
            internal readonly bool UsesMaterialInstance;
            internal readonly string Description;
            internal readonly string TypeLabel;
            internal readonly string TargetLabel;
            internal readonly string WriteMode;
            internal readonly string RecommendedUsage;

            internal CodeSet(string propertyCall, string focusedExample, string fullExample, string note, bool isUi, PropertyHelp help, bool usesMaterialInstance = false)
            {
                PropertyCall = propertyCall;
                FocusedExample = focusedExample;
                FullExample = fullExample;
                Note = note;
                IsUi = isUi;
                UsesMaterialInstance = usesMaterialInstance;
                Description = help.Description;
                TypeLabel = help.TypeLabel;
                TargetLabel = help.TargetLabel;
                WriteMode = help.WriteMode;
                RecommendedUsage = help.RecommendedUsage;
            }
        }

        #endregion
    }
}
