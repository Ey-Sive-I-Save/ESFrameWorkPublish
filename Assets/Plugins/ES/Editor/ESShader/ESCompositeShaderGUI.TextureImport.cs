using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    public sealed partial class ESCompositeShaderGUI
    {
        [Flags]
        private enum TextureImportFix
        {
            None = 0,
            FullRect = 1 << 0,
            Repeat = 1 << 1,
            Bilinear = 1 << 2,
            Clamp = 1 << 3,
            NoMipmaps = 1 << 4,
            WrapConflict = 1 << 5
        }

        private sealed class TextureImportIssue
        {
            internal readonly Texture Texture;
            internal readonly TextureImporter Importer;
            internal readonly string AssetPath;
            internal readonly TextureImportFix Fixes;

            internal TextureImportIssue(
                Texture texture,
                TextureImporter importer,
                string assetPath,
                TextureImportFix fixes)
            {
                Texture = texture;
                Importer = importer;
                AssetPath = assetPath;
                Fixes = fixes;
            }
        }

        private static readonly GUIContent FixTextureImportButtonContent = new GUIContent(
            "修复导入设置",
            "只修改面板列出的字段，并重新导入对应纹理。");
        private static readonly GUIContent RefreshTextureImportDiagnosticsButtonContent = new GUIContent(
            "检查当前材质",
            "按需读取所选材质引用纹理的导入设置；不会后台扫描项目。");
        private static readonly GUIContent LocateTextureButtonContent = new GUIContent("定位");
        private static readonly Dictionary<Texture, TextureImportFix> TextureImportRequirements =
            new Dictionary<Texture, TextureImportFix>();
        private static readonly List<TextureImportIssue> TextureImportIssues = new List<TextureImportIssue>();
        private static readonly StringBuilder TextureImportSummary = new StringBuilder(256);
        private static int textureImportSnapshotSignature = int.MinValue;
        private static bool hasTextureImportSnapshot;

        private static void DrawTextureImportDiagnostics(
            MaterialEditor editor,
            MaterialProperty[] properties,
            string shaderName)
        {
            if (!IsCompositeShader(shaderName)) return;

            int signature = GetTextureImportDiagnosticSignature(editor, properties, shaderName);
            bool snapshotCurrent = hasTextureImportSnapshot && textureImportSnapshotSignature == signature;
            string panelKey = "ES.Composite.TextureImport.Panel." + shaderName;
            bool expanded = SessionState.GetBool(panelKey, false);
            EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle);
            bool nextExpanded = EditorGUILayout.Foldout(expanded, "纹理导入诊断", true);
            if (nextExpanded != expanded)
            {
                expanded = nextExpanded;
                SessionState.SetBool(panelKey, expanded);
            }

            if (!expanded)
            {
                GUILayout.Label("按需检查所选材质，不执行后台扫描", ESEditorPresentation.MetaStyle);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4f);
                return;
            }

            if (HasMaterialFeatureCombination(editor, "_EnableSmoothPixelArt", "_EnableBlur"))
                EditorGUILayout.HelpBox(
                    "平滑像素画与纹理模糊同时启用：前者重建清晰像素边缘，后者会再次软化边缘。请按最终视觉目标保留其中一种。",
                    MessageType.Warning);
            if (HasMaterialFeatureCombination(editor, "_EnableSmoothPixelArt", "_EnablePixelate"))
                EditorGUILayout.HelpBox(
                    "平滑像素画与像素化同时启用：像素化会重新量化已重建的 UV，通常会覆盖平滑像素画效果。",
                    MessageType.Warning);
            if (shaderName == "ES/UI/Composite URP"
                && HasMaterialFeatureCombination(editor, "_EnableUnderlay", "_EnableShadow"))
                EditorGUILayout.HelpBox(
                    "TMP Underlay 与精灵阴影同时启用：两者都会在主体后方生成偏移层，可能重复加深或出现双边。请明确保留一套投影语义。",
                    MessageType.Warning);
            if (shaderName == "ES/UI/Composite URP"
                && (HasMaterialFeatureCombination(editor, "_EnableTMPCompatibility", "_EnableCustomFade")
                    || HasMaterialFeatureCombination(editor, "_EnableSDF", "_EnableCustomFade")))
                EditorGUILayout.HelpBox(
                    "Custom Fade 会在 TMP/SDF 轮廓求值后重建最终透明度。请在目标字体图集上检查边缘柔和度，Fade 遮罩不应被当作距离场使用。",
                    MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(
                snapshotCurrent ? "导入设置已按当前材质检查。" : "当前没有可复用的导入设置检查结果。",
                ESEditorPresentation.SubtitleStyle);
            GUILayout.FlexibleSpace();
            if (DrawContentSizedButton(RefreshTextureImportDiagnosticsButtonContent, EditorStyles.miniButton))
            {
                CollectTextureImportIssues(editor, shaderName);
                textureImportSnapshotSignature = signature;
                hasTextureImportSnapshot = true;
                snapshotCurrent = true;
            }
            EditorGUILayout.EndHorizontal();

            if (!snapshotCurrent)
            {
                EditorGUILayout.HelpBox(
                    hasTextureImportSnapshot
                        ? "材质参数或选择已变化。请重新检查后再修复导入设置。"
                        : "导入器读取只在点击“检查当前材质”后执行，避免 Inspector 重绘时访问 AssetDatabase。",
                    MessageType.Info);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4f);
                return;
            }

            if (TextureImportIssues.Count == 0)
            {
                GUILayout.Label("当前可导入纹理未发现与已启用效果冲突的设置。", ESEditorPresentation.SubtitleStyle);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4f);
                return;
            }

            int fixCount = 0;
            for (int i = 0; i < TextureImportIssues.Count; i++)
            {
                TextureImportIssue issue = TextureImportIssues[i];
                fixCount += CountTextureImportFixes(issue.Fixes);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(issue.Texture, typeof(Texture), false);
                if (DrawContentSizedButton(LocateTextureButtonContent, EditorStyles.miniButton))
                {
                    Selection.activeObject = issue.Texture;
                    EditorGUIUtility.PingObject(issue.Texture);
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Label(DescribeTextureImportFixes(issue.Fixes), ESEditorPresentation.SubtitleStyle);
            }

            EditorGUILayout.HelpBox(
                "修复会保留未列出的导入设置，并触发纹理重新导入。请先确认这些纹理没有被其他材质按不同采样语义复用。",
                MessageType.Warning);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(fixCount == 0))
            {
                if (DrawContentSizedButton(FixTextureImportButtonContent)
                    && ConfirmTextureImportFix(TextureImportIssues.Count, fixCount))
                {
                    ApplyTextureImportFixes(TextureImportIssues);
                    InvalidateTextureImportSnapshot();
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        private static int GetTextureImportDiagnosticSignature(
            MaterialEditor editor,
            MaterialProperty[] properties,
            string shaderName)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(shaderName ?? string.Empty);
                hash = hash * 31 + GetMaterialPropertyValueSignature(properties);
                for (int i = 0; i < editor.targets.Length; i++)
                    hash = hash * 31 + (editor.targets[i] == null ? 0 : editor.targets[i].GetInstanceID());
                return hash;
            }
        }

        private static bool IsCompositeShader(string shaderName)
        {
            return shaderName == "ES/2D/Composite URP"
                || shaderName == "ES/3D/Lit Composite URP"
                || shaderName == "ES/3D/VFX Composite URP"
                || shaderName == "ES/UI/Composite URP";
        }

        private static void CollectTextureImportIssues(MaterialEditor editor, string shaderName)
        {
            TextureImportRequirements.Clear();
            TextureImportIssues.Clear();
            TextureImportSummary.Length = 0;
            try
            {
                for (int i = 0; i < editor.targets.Length; i++)
                {
                    Material material = editor.targets[i] as Material;
                    if (material == null || material.shader == null || material.shader.name != shaderName) continue;

                    TextureImportFix requirements = GetTextureImportRequirements(material, shaderName);
                    AddTextureRequirement(GetPrimaryTexture(material), requirements);

                    if (IsMaterialFeatureEnabled(material, "_EnablePalette") && material.HasProperty("_PaletteTex"))
                        AddTextureRequirement(
                            material.GetTexture("_PaletteTex"),
                            TextureImportFix.Clamp | TextureImportFix.NoMipmaps);
                    if (IsMaterialFeatureEnabled(material, "_EnableCustomFade")
                        && material.HasProperty("_CustomFadeFadeMask"))
                        AddTextureRequirement(
                            material.GetTexture("_CustomFadeFadeMask"),
                            TextureImportFix.Clamp | TextureImportFix.NoMipmaps);
                    if (IsMaterialFeatureEnabled(material, "_EnableMetal")
                        && IsMaterialFeatureEnabled(material, "_MetalMaskToggle")
                        && material.HasProperty("_MetalMask"))
                        AddTextureRequirement(
                            material.GetTexture("_MetalMask"),
                            TextureImportFix.Clamp | TextureImportFix.NoMipmaps);
                    if ((IsMaterialFeatureEnabled(material, "_EnableFlame")
                        || IsMaterialFeatureEnabled(material, "_EnableSmoke")
                        || IsMaterialFeatureEnabled(material, "_EnableInkSpread")
                        || IsMaterialFeatureEnabled(material, "_EnableCamouflage")
                        || IsMaterialFeatureEnabled(material, "_EnableMetal")
                        || IsMaterialFeatureEnabled(material, "_EnableEnchanted")
                        || IsMaterialFeatureEnabled(material, "_EnableCustomFade")
                        || IsMaterialFeatureEnabled(material, "_EnableFullGlowDissolve"))
                        && material.HasProperty("_UberNoiseTexture"))
                    {
                        AddTextureRequirement(
                            material.GetTexture("_UberNoiseTexture"),
                            TextureImportFix.Repeat | TextureImportFix.Bilinear);
                    }
                }

                foreach (KeyValuePair<Texture, TextureImportFix> pair in TextureImportRequirements)
                {
                    string assetPath = AssetDatabase.GetAssetPath(pair.Key);
                    if (string.IsNullOrEmpty(assetPath)) continue;
                    TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer == null) continue;

                    TextureImporterSettings settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    TextureImportFix fixes = GetRequiredTextureImportFixes(importer, settings, pair.Value);
                    if (fixes != TextureImportFix.None)
                        TextureImportIssues.Add(new TextureImportIssue(pair.Key, importer, assetPath, fixes));
                }
            }
            finally
            {
                TextureImportRequirements.Clear();
            }
        }

        private static Texture GetPrimaryTexture(Material material)
        {
            if (material.HasProperty("_MainTex")) return material.GetTexture("_MainTex");
            return material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
        }

        private static void AddTextureRequirement(Texture texture, TextureImportFix requirements)
        {
            if (texture == null || requirements == TextureImportFix.None) return;
            TextureImportRequirements.TryGetValue(texture, out TextureImportFix current);
            TextureImportFix combined = current | requirements;
            if ((current & TextureImportFix.WrapConflict) != 0
                || ((combined & TextureImportFix.Repeat) != 0 && (combined & TextureImportFix.Clamp) != 0))
            {
                combined &= ~(TextureImportFix.Repeat | TextureImportFix.Clamp);
                combined |= TextureImportFix.WrapConflict;
            }
            TextureImportRequirements[texture] = combined;
        }

        private static TextureImportFix GetTextureImportRequirements(Material material, string shaderName)
        {
            TextureImportFix requirements = TextureImportFix.None;
            bool usesNeighbourSamples = IsMaterialFeatureEnabled(material, "_EnableInnerOutline")
                || IsMaterialFeatureEnabled(material, "_EnableOuterOutline")
                || IsMaterialFeatureEnabled(material, "_EnablePixelOutline")
                || IsMaterialFeatureEnabled(material, "_EnableShadow")
                || IsMaterialFeatureEnabled(material, "_EnableBlur")
                || IsMaterialFeatureEnabled(material, "_EnableChromatic");

            if (shaderName == "ES/2D/Composite URP" && usesNeighbourSamples)
                requirements |= TextureImportFix.FullRect;
            if (IsMaterialFeatureEnabled(material, "_EnableFlow") || UsesRepeatedTextureCoordinates(material))
                requirements |= TextureImportFix.Repeat;
            if (IsMaterialFeatureEnabled(material, "_EnableBlur")
                || IsMaterialFeatureEnabled(material, "_EnableSmoothPixelArt"))
                requirements |= TextureImportFix.Bilinear;
            if (IsMaterialFeatureEnabled(material, "_EnablePixelate"))
                requirements |= TextureImportFix.NoMipmaps;
            return requirements;
        }

        private static bool HasMaterialFeatureCombination(
            MaterialEditor editor,
            string firstProperty,
            string secondProperty)
        {
            for (int i = 0; i < editor.targets.Length; i++)
            {
                Material material = editor.targets[i] as Material;
                if (material != null
                    && IsMaterialFeatureEnabled(material, firstProperty)
                    && IsMaterialFeatureEnabled(material, secondProperty))
                    return true;
            }
            return false;
        }

        private static bool IsMaterialFeatureEnabled(Material material, string propertyName)
        {
            return material.HasProperty(propertyName) && material.GetFloat(propertyName) > 0.5f;
        }

        private static bool UsesRepeatedTextureCoordinates(Material material)
        {
            if (!material.HasProperty("_MainTexScaleOffset")) return false;
            Vector4 transform = material.GetVector("_MainTexScaleOffset");
            return Mathf.Abs(transform.x) > 1.0001f
                || Mathf.Abs(transform.y) > 1.0001f
                || Mathf.Abs(transform.z) > 0.0001f
                || Mathf.Abs(transform.w) > 0.0001f;
        }

        private static TextureImportFix GetRequiredTextureImportFixes(
            TextureImporter importer,
            TextureImporterSettings settings,
            TextureImportFix requirements)
        {
            TextureImportFix fixes = TextureImportFix.None;
            if ((requirements & TextureImportFix.FullRect) != 0
                && importer.textureType == TextureImporterType.Sprite
                && settings.spriteMeshType != SpriteMeshType.FullRect)
                fixes |= TextureImportFix.FullRect;
            if ((requirements & TextureImportFix.Repeat) != 0 && settings.wrapMode != TextureWrapMode.Repeat)
                fixes |= TextureImportFix.Repeat;
            if ((requirements & TextureImportFix.Bilinear) != 0 && settings.filterMode == FilterMode.Point)
                fixes |= TextureImportFix.Bilinear;
            if ((requirements & TextureImportFix.Clamp) != 0 && settings.wrapMode != TextureWrapMode.Clamp)
                fixes |= TextureImportFix.Clamp;
            if ((requirements & TextureImportFix.NoMipmaps) != 0 && settings.mipmapEnabled)
                fixes |= TextureImportFix.NoMipmaps;
            if ((requirements & TextureImportFix.WrapConflict) != 0)
                fixes |= TextureImportFix.WrapConflict;
            return fixes;
        }

        private static string DescribeTextureImportFixes(TextureImportFix fixes)
        {
            TextureImportSummary.Length = 0;
            AppendTextureImportFix(fixes, TextureImportFix.FullRect, "Sprite Mesh 改为 Full Rect，避免轮廓/邻域采样被紧密网格裁掉");
            AppendTextureImportFix(fixes, TextureImportFix.Repeat, "Wrap 改为 Repeat，支持流动及超出 0-1 的 UV");
            AppendTextureImportFix(fixes, TextureImportFix.Bilinear, "Filter 改为 Bilinear，满足模糊或平滑像素画的连续采样要求");
            AppendTextureImportFix(fixes, TextureImportFix.Clamp, "Wrap 改为 Clamp，避免调色板首尾颜色串色");
            AppendTextureImportFix(fixes, TextureImportFix.NoMipmaps, "关闭 Mipmap，避免像素化或调色板在缩放时混入相邻色阶");
            AppendTextureImportFix(fixes, TextureImportFix.WrapConflict, "同一纹理同时需要 Repeat 与 Clamp；请拆分主纹理和调色板资源，无法安全自动修复");
            return TextureImportSummary.ToString();
        }

        private static void AppendTextureImportFix(TextureImportFix fixes, TextureImportFix target, string description)
        {
            if ((fixes & target) == 0) return;
            if (TextureImportSummary.Length > 0) TextureImportSummary.Append('\n');
            TextureImportSummary.Append("- ").Append(description);
        }

        private static int CountTextureImportFixes(TextureImportFix fixes)
        {
            int count = 0;
            if ((fixes & TextureImportFix.FullRect) != 0) count++;
            if ((fixes & TextureImportFix.Repeat) != 0) count++;
            if ((fixes & TextureImportFix.Bilinear) != 0) count++;
            if ((fixes & TextureImportFix.Clamp) != 0) count++;
            if ((fixes & TextureImportFix.NoMipmaps) != 0) count++;
            return count;
        }

        private static bool ConfirmTextureImportFix(int textureCount, int fixCount)
        {
            return ESDialog.ConfirmModal(
                "es.composite.texture-import.fix",
                "修复纹理导入设置",
                "将修改 " + textureCount + " 张纹理的 " + fixCount
                    + " 项导入设置并触发重新导入。\n\n未列出的设置保持不变。是否继续？",
                "修复并重新导入",
                "取消",
                tone: ESDialogTone.Warning,
                host: ESDialogHost.Editor,
                allowMainWorkspaceFallback: true);
        }

        private static void ApplyTextureImportFixes(List<TextureImportIssue> issues)
        {
            UnityEngine.Object[] importers = new UnityEngine.Object[issues.Count];
            for (int i = 0; i < issues.Count; i++) importers[i] = issues[i].Importer;
            Undo.RecordObjects(importers, "修复 ES Shader 纹理导入设置");

            var failures = new List<string>();
            for (int i = 0; i < issues.Count; i++)
            {
                TextureImportIssue issue = issues[i];
                try
                {
                    TextureImporter importer = AssetImporter.GetAtPath(issue.AssetPath) as TextureImporter;
                    if (importer == null)
                    {
                        failures.Add(issue.AssetPath + "：导入器已失效");
                        continue;
                    }

                    TextureImporterSettings settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    if ((issue.Fixes & TextureImportFix.FullRect) != 0)
                        settings.spriteMeshType = SpriteMeshType.FullRect;
                    if ((issue.Fixes & TextureImportFix.Repeat) != 0)
                        settings.wrapMode = TextureWrapMode.Repeat;
                    if ((issue.Fixes & TextureImportFix.Bilinear) != 0)
                        settings.filterMode = FilterMode.Bilinear;
                    if ((issue.Fixes & TextureImportFix.Clamp) != 0)
                        settings.wrapMode = TextureWrapMode.Clamp;
                    if ((issue.Fixes & TextureImportFix.NoMipmaps) != 0)
                        settings.mipmapEnabled = false;

                    importer.SetTextureSettings(settings);
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                }
                catch (Exception exception)
                {
                    failures.Add(issue.AssetPath + "：" + exception.Message);
                }
            }

            if (failures.Count > 0)
                ESDialog.InfoModal(
                    "es.composite.texture-import.partial-failure",
                    "部分纹理修复失败",
                    string.Join("\n", failures.ToArray()) + "\n\n请检查文件权限、导入器类型和 Console。",
                    "知道了",
                    tone: ESDialogTone.Danger,
                    host: ESDialogHost.Editor,
                    allowMainWorkspaceFallback: true);
        }

        private static void InvalidateTextureImportSnapshot()
        {
            TextureImportRequirements.Clear();
            TextureImportIssues.Clear();
            TextureImportSummary.Length = 0;
            textureImportSnapshotSignature = int.MinValue;
            hasTextureImportSnapshot = false;
        }
    }
}
