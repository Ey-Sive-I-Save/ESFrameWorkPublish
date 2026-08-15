using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// ES Composite 材质 Inspector。
    /// 设计基线参考 SSU：按 Shader 属性声明顺序处理，使用状态机驱动分类、开关和隐藏，
    /// 同时保留 ES 的中文帮助、PropertyBlock 示例和 ESEditorPresentation 视觉体系。
    /// </summary>
    public sealed class ESCompositeShaderGUI : ShaderGUI
    {
        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "_MainTex", "主纹理" }, { "_BaseMap", "基础颜色纹理" }, { "_Color", "颜色" }, { "_BaseColor", "基础颜色" },
            { "_NormalMap", "法线纹理" }, { "_NormalScale", "法线强度" }, { "_Metallic", "金属度" }, { "_Smoothness", "光滑度" },
            { "_UseNormalMap", "启用法线纹理" }, { "_OcclusionMap", "环境遮挡纹理" }, { "_Occlusion", "环境遮挡强度" },
            { "_UseEmission", "启用自发光" }, { "_EmissionMap", "自发光纹理" },
            { "_EmissionColor", "自发光颜色" }, { "_NoiseTex", "噪声纹理" }, { "_NoiseScale", "噪声缩放" }, { "_NoiseSpeed", "噪声速度" },
            { "_Distortion", "扰动强度" }, { "_DistortionStrength", "扰动强度" }, { "_DissolveMode", "溶解模式" },
            { "_DissolveProgress", "溶解进度" }, { "_DissolveSoftness", "溶解柔和度" }, { "_DissolveWidth", "溶解边缘宽度" },
            { "_DissolveEdgeColor", "溶解边缘颜色" }, { "_DissolveColor", "溶解颜色" }, { "_EnableRim", "启用边缘光" },
            { "_RimColor", "边缘光颜色" }, { "_RimPower", "边缘光幂次" }, { "_RimIntensity", "边缘光强度" },
            { "_EnableShine", "启用扫光" }, { "_ShineColor", "扫光颜色" }, { "_ShineSpeed", "扫光速度" }, { "_ShineWidth", "扫光宽度" },
            { "_ShineAngle", "扫光角度" }, { "_ShineIntensity", "扫光强度" }, { "_EnableHologram", "启用全息" },
            { "_HologramColor", "全息颜色" }, { "_HologramFrequency", "全息线频率" }, { "_HologramLineFrequency", "全息线频率" },
            { "_HologramGap", "全息线间隔" }, { "_HologramLineGap", "全息线间隔" }, { "_HologramSpeed", "全息速度" },
            { "_HologramMinAlpha", "全息最低透明度" }, { "_EnableGlitch", "启用故障" }, { "_GlitchAmount", "故障强度" },
            { "_GlitchIntensity", "故障强度" }, { "_GlitchSpeed", "故障速度" }, { "_QualityTier", "效果质量档位" },
            { "_ReceiveShadows", "接收阴影" }, { "_AlphaClip", "启用透明裁剪" }, { "_Cutoff", "裁剪阈值" },
            { "_VertexColorStrength", "顶点色影响" }, { "_CoordinateMode", "坐标模式" }, { "_TimeMode", "时间来源" },
            { "_CustomTime", "自定义时间" }, { "_TimeScale", "时间倍率" }, { "_MainTexScaleOffset", "主纹理缩放/偏移" }, { "_AnimationMode", "动画模式" }, { "_SequenceColumns", "序列帧列数" },
            { "_SequenceRows", "序列帧行数" }, { "_SequenceFrame", "序列帧帧号" }, { "_SequenceSpeed", "序列帧速度" }
            , { "_FadeMode", "渐隐模式" }, { "_FadeProgress", "渐隐进度" }, { "_FadePosition", "渐隐位置" }, { "_FadeWidth", "渐隐宽度" },
            { "_FadeNoiseFactor", "渐隐噪声影响" }, { "_FadeMask", "渐隐遮罩" }, { "_EnableAddColor", "启用叠加颜色" }, { "_AddColor", "叠加颜色" },
            { "_AddColorFade", "叠加颜色强度" }, { "_EnableStrongTint", "启用强制染色" }, { "_StrongTint", "强制染色" }, { "_StrongTintFade", "强制染色强度" },
            { "_EnableAlphaTint", "启用透明染色" }, { "_AlphaTint", "透明染色" }, { "_AlphaTintMin", "透明染色下限" },
            { "_EnableColorReplace", "启用颜色替换" }, { "_ReplaceFrom", "替换源颜色" }, { "_ReplaceTo", "替换目标颜色" },
            { "_ReplaceRange", "替换范围" }, { "_ReplaceSoftness", "替换柔和度" }, { "_EnableBrightness", "启用亮度" }, { "_Brightness", "亮度" },
            { "_EnableContrast", "启用对比度" }, { "_Contrast", "对比度" }, { "_EnableSaturation", "启用饱和度" }, { "_Saturation", "饱和度" },
            { "_EnableHue", "启用色相偏移" }, { "_Hue", "色相偏移" }, { "_EnableNegative", "启用负片" }, { "_NegativeFade", "负片强度" },
            { "_EnableRainbow", "启用彩虹渐变" }, { "_RainbowSpeed", "彩虹速度" }, { "_RainbowDensity", "彩虹密度" }, { "_RainbowBrightness", "彩虹亮度" },
            { "_EnableInnerOutline", "启用内描边" }, { "_InnerOutlineColor", "内描边颜色" }, { "_InnerOutlineWidth", "内描边宽度" },
            { "_EnableOuterOutline", "启用外描边" }, { "_OuterOutlineColor", "外描边颜色" }, { "_OuterOutlineWidth", "外描边宽度" },
            { "_EnablePixelOutline", "启用像素描边" }, { "_PixelOutlineColor", "像素描边颜色" }, { "_PixelOutlineWidth", "像素描边宽度" },
            { "_EnablePingPongGlow", "启用往返发光" }, { "_GlowFrom", "发光起点颜色" }, { "_GlowTo", "发光终点颜色" },
            { "_GlowFrequency", "发光频率" }, { "_GlowIntensity", "发光强度" }, { "_EnableDistortion", "启用噪声扰动" },
            { "_EnableFrozen", "启用冰冻" }, { "_FrozenColor", "冰冻颜色" }, { "_FrozenHighlight", "冰冻高光" },
            { "_FrozenDensity", "冰冻雪花密度" }, { "_FrozenSpeed", "冰冻流动速度" }, { "_EnableBurn", "启用燃烧" },
            { "_BurnEdgeColor", "燃烧边缘颜色" }, { "_BurnInsideColor", "燃烧内部颜色" }, { "_BurnProgress", "燃烧进度" }, { "_BurnWidth", "燃烧边缘宽度" },
            { "_EnablePoison", "启用中毒" }, { "_PoisonColor", "中毒颜色" }, { "_PoisonDensity", "中毒密度" }, { "_PoisonSpeed", "中毒速度" },
            { "_UseOcclusionMap", "使用环境遮挡纹理" }, { "_StencilComp", "Stencil 比较方式" },
            { "_Stencil", "Stencil ID" }, { "_StencilOp", "Stencil 操作" }, { "_StencilReadMask", "Stencil 读取掩码" },
            { "_StencilWriteMask", "Stencil 写入掩码" }, { "_ColorMask", "颜色写入掩码" }, { "_UseUIAlphaClip", "启用 UI 透明裁剪" }
        };

        private static readonly Dictionary<Shader, Material> Defaults = new Dictionary<Shader, Material>();
        private static readonly string[] TwoDCategoryOrder =
        {
            "主设置", "坐标与动画", "渐隐与溶解", "颜色处理", "描边", "动态效果", "状态效果", "输出"
        };
        private static readonly string[] LitCategoryOrder =
        {
            "主材质", "时间与坐标", "光照", "渐隐与溶解", "表现效果", "输出与质量"
        };
        private static readonly string[] VfxCategoryOrder =
        {
            "主设置", "时间与坐标", "噪声与扰动", "溶解", "全息", "边缘光", "故障", "自发光", "输出与质量"
        };
        private static readonly string[] UiCategoryOrder =
        {
            "主设置", "时间与坐标", "动态效果", "遮罩与输出"
        };
        private static readonly Dictionary<string, int> EffectCosts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "_EnableDistortion", 2 }, { "_DissolveMode", 2 }, { "_EnableInnerOutline", 3 }, { "_EnableOuterOutline", 4 },
            { "_EnablePixelOutline", 5 }, { "_EnableShine", 2 }, { "_EnablePingPongGlow", 1 }, { "_EnableHologram", 2 },
            { "_EnableGlitch", 2 }, { "_EnableFrozen", 3 }, { "_EnableBurn", 3 }, { "_EnablePoison", 2 }, { "_EnableRim", 2 },
            { "_UseNormalMap", 1 }, { "_UseOcclusionMap", 1 }, { "_UseEmission", 1 }
        };

        private sealed class EffectRoute
        {
            internal readonly string Key;
            internal readonly string Title;
            internal readonly string Category;
            internal readonly string[] Aliases;

            internal EffectRoute(string key, string title, string category, params string[] aliases)
            {
                Key = key;
                Title = title;
                Category = category;
                Aliases = aliases ?? Array.Empty<string>();
            }
        }

        private sealed class RouteCacheEntry
        {
            internal readonly int PropertySignature;
            internal readonly EffectRoute[] Routes;
            internal readonly string[] Titles;

            internal RouteCacheEntry(int propertySignature, EffectRoute[] routes, string[] titles)
            {
                PropertySignature = propertySignature;
                Routes = routes;
                Titles = titles;
            }
        }

        private static readonly EffectRoute[] EffectRoutes =
        {
            new EffectRoute("base", "基础材质", "主材质", "基础", "主纹理", "颜色", "Base", "Main"),
            new EffectRoute("animation", "动画/坐标", "坐标与动画", "动画", "序列帧", "坐标", "Animation", "Sequence"),
            new EffectRoute("time", "时间/倍率", "时间与坐标", "时间", "倍率", "非缩放", "自定义", "Time", "Scale"),
            new EffectRoute("uv", "主纹理缩放", "时间与坐标", "主纹理", "UV", "缩放", "偏移", "Tiling", "Offset"),
            new EffectRoute("noise", "噪声/扰动", "噪声与扰动", "噪声", "扰动", "Noise", "Distortion"),
            new EffectRoute("dissolve", "溶解/渐隐", "渐隐与溶解", "溶解", "渐隐", "Dissolve", "Fade"),
            new EffectRoute("outline", "描边", "描边", "描边", "轮廓", "Outline"),
            new EffectRoute("shine", "扫光", "动态效果", "扫光", "高光带", "Shine"),
            new EffectRoute("rim", "边缘光", "表现效果", "边缘光", "轮廓光", "Rim"),
            new EffectRoute("hologram", "全息", "动态效果", "全息", "扫描线", "Hologram"),
            new EffectRoute("glitch", "故障", "动态效果", "故障", "抖动", "Glitch"),
            new EffectRoute("emission", "自发光", "自发光", "自发光", "发光", "Emission"),
            new EffectRoute("color", "颜色处理", "颜色处理", "颜色", "染色", "亮度", "对比度", "饱和度", "色相", "Color", "Tint"),
            new EffectRoute("state", "冰冻/燃烧/中毒", "状态效果", "冰冻", "燃烧", "中毒", "Frozen", "Burn", "Poison"),
            new EffectRoute("output", "裁剪/阴影", "输出与质量", "裁剪", "阴影", "质量", "Alpha", "Shadow", "Quality")
        };
        private static readonly Dictionary<string, RouteCacheEntry> RouteCache = new Dictionary<string, RouteCacheEntry>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Dictionary<string, string>> CategorySessionKeys = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> FeaturePurposeTitles = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> EffectCostLabels = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> EffectDescriptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "_UseNormalMap", "使用法线纹理改变光照法线；关闭时跳过法线采样。" },
            { "_UseOcclusionMap", "使用环境遮挡纹理压低间接光；关闭时使用统一遮挡强度。" },
            { "_UseEmission", "叠加自发光颜色和纹理；适合能量、霓虹和受击反馈。" },
            { "_EnableDistortion", "用噪声驱动 UV 扰动；会增加纹理采样和过绘成本。" },
            { "_EnableInnerOutline", "在原图形内部生成轮廓线；适合角色描边和选中反馈。" },
            { "_EnableOuterOutline", "向透明区域扩展轮廓线；会扩大实际过绘范围。" },
            { "_EnablePixelOutline", "使用像素宽度生成硬边轮廓；适合像素风和强调边缘。" },
            { "_EnableShine", "沿表面移动高光带；速度、宽度和颜色可独立调整。" },
            { "_EnablePingPongGlow", "在两个颜色之间往返发光；适合循环提示和呼吸效果。" },
            { "_EnableHologram", "叠加扫描线和最低透明度控制，形成全息显示效果。" },
            { "_EnableGlitch", "按时间和坐标产生故障抖动；高质量档位下成本更高。" },
            { "_EnableRim", "按视角边缘增加轮廓光；强度受光照和视角共同影响。" },
            { "_EnableFrozen", "叠加冰冻颜色与冰晶高光；需要噪声纹理参与。" },
            { "_EnableBurn", "按噪声推进燃烧边缘；通常与溶解或裁剪一起使用。" },
            { "_EnablePoison", "叠加周期性中毒染色；适合状态提示而非基础材质。" },
            { "_EnableAddColor", "在原始颜色上叠加一层可控颜色。" },
            { "_EnableStrongTint", "用指定颜色覆盖主要视觉色调。" },
            { "_EnableAlphaTint", "在保持主体颜色的同时调整透明度色调。" },
            { "_EnableColorReplace", "按颜色距离把指定颜色替换为目标颜色。" },
            { "_EnableBrightness", "调整输出亮度倍率。" },
            { "_EnableContrast", "调整颜色相对中性灰的对比度。" },
            { "_EnableSaturation", "调整颜色鲜艳程度。" },
            { "_EnableHue", "旋转颜色色相。" },
            { "_EnableNegative", "将颜色向负片效果偏移。" },
            { "_EnableRainbow", "按坐标和时间叠加彩虹渐变。" },
            { "_EnableAlphaClip", "按裁剪阈值丢弃低透明度像素。" },
            { "_AlphaClip", "按 Cutoff 阈值裁剪透明像素，会影响深度和渲染队列行为。" },
            { "_ReceiveShadows", "控制 Lit 材质是否接收主光源阴影；修改会同步 Shader Keyword。" }
        };
        private static readonly GUIContent SearchLabel = new GUIContent("查找效果");

        static ESCompositeShaderGUI()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseDefaults;
            EditorApplication.quitting += ReleaseDefaults;
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            if (materialEditor == null || properties == null) return;
            DrawStatus(materialEditor, properties);
            Material material = materialEditor.target as Material;
            string shaderName = material != null && material.shader != null ? material.shader.name : string.Empty;
            string effectFilter = DrawEffectNavigator(shaderName, properties);
            DrawPropertyStream(materialEditor, properties, shaderName, effectFilter);
            SyncKeywords(materialEditor, properties);
        }

        private static void DrawStatus(MaterialEditor editor, MaterialProperty[] properties)
        {
            int enabled = 0, effectCount = 0, mixedCount = 0, textureCount = 0, cost = 0;
            bool hasDistortion = false, hasOutline = false, hasDissolve = false;
            for (int i = 0; i < properties.Length; i++)
            {
                MaterialProperty p = properties[i];
                if (IsAlwaysHidden(p)) continue;
                if (p.type == MaterialProperty.PropType.Texture) textureCount++;
                if (IsStatusFeatureToggle(p.name))
                {
                    effectCount++;
                    if (p.hasMixedValue)
                    {
                        mixedCount++;
                        continue;
                    }
                    if (p.floatValue > 0.5f)
                    {
                        enabled++;
                        if (EffectCosts.TryGetValue(p.name, out int effectCost)) cost += effectCost;
                        if (p.name == "_EnableDistortion") hasDistortion = true;
                        if (p.name == "_EnableInnerOutline" || p.name == "_EnableOuterOutline" || p.name == "_EnablePixelOutline") hasOutline = true;
                    }
                }
                if (p.name == "_DissolveMode" && !p.hasMixedValue && p.floatValue > 0.5f) { hasDissolve = true; cost += 2; }
            }
            MaterialProperty quality = Find(properties, "_QualityTier");
            string tier = quality == null ? "标准" : (quality.hasMixedValue ? "混合" : QualityName(quality.floatValue));
            EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle);
            Material target = editor.target as Material;
            string shaderName = target != null && target.shader != null ? target.shader.name : "未知 Shader";
            GUILayout.Label("ES Composite · " + shaderName, ESEditorPresentation.HeaderStyle);
            string mixedText = mixedCount > 0 ? "  ·  混合 " + mixedCount : string.Empty;
            GUILayout.Label("启用 " + enabled + "/" + effectCount + mixedText + "  ·  质量 " + tier + "  ·  成本 " + cost + "  ·  纹理 " + textureCount, ESEditorPresentation.SubtitleStyle);
            if (textureCount > 6) EditorGUILayout.HelpBox("当前材质纹理入口较多，建议使用“基础”或“标准”质量档位控制变体和采样成本。", MessageType.Warning);
            if (hasDistortion && hasOutline)
                EditorGUILayout.HelpBox("扰动与描边同时启用，采样成本较高。", MessageType.Warning);
            if (hasDissolve && tier == "高质量" && cost >= 8)
                EditorGUILayout.HelpBox("高质量溶解叠加较多效果，移动端建议改为标准。", MessageType.Warning);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        private static string QualityName(float value)
        {
            switch (Mathf.Clamp(Mathf.RoundToInt(value), 0, 2))
            {
                case 0: return "基础";
                case 2: return "高质量";
                default: return "标准";
            }
        }

        private static string DrawEffectNavigator(string shaderName, MaterialProperty[] properties)
        {
            string searchKey = "ES.Composite.Navigator.Search." + shaderName;
            string routeKey = "ES.Composite.Navigator.Route." + shaderName;
            string search = SessionState.GetString(searchKey, string.Empty);
            string selectedRoute = SessionState.GetString(routeKey, string.Empty);

            EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("效果导航", ESEditorPresentation.HeaderStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("显示全部", EditorStyles.miniButton, GUILayout.Width(64f)))
            {
                search = string.Empty;
                selectedRoute = string.Empty;
                SessionState.SetString(searchKey, search);
                SessionState.SetString(routeKey, selectedRoute);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            string nextSearch = EditorGUILayout.TextField(SearchLabel, search, EditorStyles.toolbarSearchField);
            if (!string.Equals(nextSearch, search, StringComparison.Ordinal))
            {
                search = nextSearch;
                selectedRoute = string.Empty;
                SessionState.SetString(searchKey, search);
                SessionState.SetString(routeKey, selectedRoute);
            }
            EditorGUILayout.EndHorizontal();

            EffectRoute[] routes = RoutesForShader(shaderName, properties);
            if (routes.Length > 0)
            {
                string[] routeTitles = GetRouteTitles(shaderName, routes);
                int selectedIndex = -1;
                for (int i = 0; i < routes.Length; i++)
                {
                    if (string.Equals(selectedRoute, routes[i].Key, StringComparison.Ordinal)) selectedIndex = i;
                }
                int nextIndex = GUILayout.SelectionGrid(selectedIndex, routeTitles, 3, EditorStyles.toolbarButton);
                if (nextIndex >= 0 && nextIndex < routes.Length && nextIndex != selectedIndex)
                {
                    selectedRoute = routes[nextIndex].Key;
                    search = string.Empty;
                    SessionState.SetString(searchKey, search);
                    SessionState.SetString(routeKey, selectedRoute);
                }
            }

            EffectRoute selected = FindRoute(selectedRoute);
            if (selected != null)
            {
                GUILayout.Label(selected.Title + "  ·  " + selected.Category, ESEditorPresentation.SubtitleStyle);
            }
            else if (!string.IsNullOrWhiteSpace(search))
            {
                GUILayout.Label("正在匹配：" + search.Trim(), ESEditorPresentation.SubtitleStyle);
                bool found = false;
                for (int i = 0; i < properties.Length; i++)
                {
                    if (PropertyMatchesFilter(properties[i], search.Trim(), shaderName))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) EditorGUILayout.HelpBox("没有找到匹配的效果或属性名。可以试试：溶解、扫光、描边、全息、故障、颜色。", MessageType.Info);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
            return string.IsNullOrWhiteSpace(search) && selected == null ? string.Empty : (!string.IsNullOrWhiteSpace(search) ? search.Trim() : "@" + selected.Key);
        }

        private static EffectRoute[] RoutesForShader(string shaderName, MaterialProperty[] properties)
        {
            int signature = GetPropertySignature(properties);
            RouteCacheEntry entry;
            if (RouteCache.TryGetValue(shaderName, out entry) && entry.PropertySignature == signature)
                return entry.Routes;

            var result = new List<EffectRoute>();
            for (int i = 0; i < EffectRoutes.Length; i++)
            {
                EffectRoute route = EffectRoutes[i];
                for (int p = 0; p < properties.Length; p++)
                {
                    if (!IsAlwaysHidden(properties[p]) && PropertyMatches(properties[p], route, shaderName))
                    {
                        result.Add(route);
                        break;
                    }
                }
            }
            EffectRoute[] routes = result.ToArray();
            string[] titles = new string[routes.Length];
            for (int i = 0; i < routes.Length; i++) titles[i] = routes[i].Title;
            RouteCache[shaderName] = new RouteCacheEntry(signature, routes, titles);
            return routes;
        }

        private static string[] GetRouteTitles(string shaderName, EffectRoute[] routes)
        {
            RouteCacheEntry entry;
            if (RouteCache.TryGetValue(shaderName, out entry) && ReferenceEquals(entry.Routes, routes))
                return entry.Titles;

            string[] titles = new string[routes.Length];
            for (int i = 0; i < routes.Length; i++) titles[i] = routes[i].Title;
            return titles;
        }

        private static int GetPropertySignature(MaterialProperty[] properties)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < properties.Length; i++)
                {
                    MaterialProperty property = properties[i];
                    hash = hash * 31 + (property == null ? 0 : StringComparer.Ordinal.GetHashCode(property.name));
                    hash = hash * 31 + (property == null ? 0 : (int)property.flags);
                }
                return hash;
            }
        }

        private static EffectRoute FindRoute(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            for (int i = 0; i < EffectRoutes.Length; i++)
                if (EffectRoutes[i].Key == key) return EffectRoutes[i];
            return null;
        }

        private static bool PropertyMatches(MaterialProperty property, EffectRoute route, string shaderName)
        {
            if (property == null || route == null) return false;
            if (ResolveCategory(shaderName, property.name) == route.Category) return true;
            for (int i = 0; i < route.Aliases.Length; i++)
                if (property.name.IndexOf(route.Aliases[i], StringComparison.OrdinalIgnoreCase) >= 0
                    || GetDisplayName(property).IndexOf(route.Aliases[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static bool PropertyMatchesFilter(MaterialProperty property, string filter, string shaderName)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            if (filter.StartsWith("@", StringComparison.Ordinal))
            {
                EffectRoute route = FindRoute(filter.Substring(1));
                return PropertyMatches(property, route, shaderName);
            }
            return property.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                || GetDisplayName(property).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void DrawPropertyStream(MaterialEditor editor, MaterialProperty[] properties, string shaderName, string filter)
        {
            // 先确定稳定的分类顺序，再在分类内部保持 Shader 声明顺序。
            // 这样同一分类只会出现一次，不会因为属性交错而重复生成折叠页签。
            string[] categoryOrder = ResolveCategoryOrder(shaderName);
            for (int c = 0; c < categoryOrder.Length; c++)
            {
                string category = categoryOrder[c];
                if (!HasVisibleCategory(properties, category, shaderName, filter)) continue;
                if (!BeginCategoryCard(shaderName, category, !string.IsNullOrEmpty(filter))) continue;
                DrawCategoryProperties(editor, properties, category, shaderName, filter);
                EditorGUILayout.EndVertical();
            }
        }

        private static bool HasVisibleCategory(MaterialProperty[] properties, string category, string shaderName, string filter)
        {
            for (int i = 0; i < properties.Length; i++)
            {
                MaterialProperty property = properties[i];
                if (ResolveCategory(shaderName, property.name) == category && !IsAlwaysHidden(property) && PropertyPassesFilter(property, properties, filter, shaderName)) return true;
            }
            return false;
        }

        private static void DrawCategoryProperties(MaterialEditor editor, MaterialProperty[] properties, string category, string shaderName, string filter)
        {
            string activeGroup = null;
            bool groupOpen = false;
            for (int i = 0; i < properties.Length; i++)
            {
                MaterialProperty property = properties[i];
                if (ResolveCategory(shaderName, property.name) != category || IsAlwaysHidden(property)) continue;

                if (!PropertyPassesFilter(property, properties, filter, shaderName)) continue;

                if (IsEffectToggle(property.name))
                {
                    CloseEffectGroup(ref groupOpen, ref activeGroup);
                    bool expanded = DrawEffectCardHeader(editor, property, GetDisplayName(property), shaderName, !string.IsNullOrEmpty(filter));
                    if ((property.hasMixedValue || property.floatValue > 0.5f) && expanded)
                    {
                        activeGroup = property.name;
                        groupOpen = true;
                    }
                    else
                    {
                        EditorGUILayout.EndVertical();
                    }
                    continue;
                }

                if (property.name == "_AnimationMode")
                {
                    CloseEffectGroup(ref groupOpen, ref activeGroup);
                    EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle);
                    DrawProperty(editor, property, GetDisplayName(property));
                    activeGroup = property.name;
                    groupOpen = true;
                    continue;
                }

                if (groupOpen && !string.Equals(ResolveController(property.name), activeGroup, StringComparison.Ordinal))
                    CloseEffectGroup(ref groupOpen, ref activeGroup);
                if (string.IsNullOrEmpty(filter) && IsCollapsedEffectDependency(property, shaderName)) continue;
                if (!IsVisible(property, properties)) continue;
                DrawProperty(editor, property, GetDisplayName(property));
            }
            CloseEffectGroup(ref groupOpen, ref activeGroup);
        }

        private static bool DrawEffectCardHeader(MaterialEditor editor, MaterialProperty property, string displayName, string shaderName, bool forceExpanded)
        {
            bool mixed = property.hasMixedValue;
            bool enabled = !mixed && property.floatValue > 0.5f;
            string key = GetEffectSessionKey(shaderName, property.name);
            bool expanded = forceExpanded || mixed || SessionState.GetBool(key, true);
            string title = GetFeaturePurposeTitle(displayName);

            Color previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = mixed
                ? new Color(0.82f, 0.62f, 0.22f, 0.92f)
                : enabled
                    ? new Color(0.20f, 0.52f, 0.88f, 0.92f)
                    : new Color(0.35f, 0.38f, 0.44f, 0.72f);
            EditorGUILayout.BeginVertical("Helpbox");
            GUI.backgroundColor = previousBackground;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(title, ESEditorPresentation.HeaderStyle, GUILayout.ExpandWidth(true));
            GUILayout.Label(mixed ? "混合" : enabled ? "已启用" : "未启用", ESEditorPresentation.MetaStyle, GUILayout.Width(42f));
            ESCompositeCodingHelper.DrawCompactBooleanProperty(
                editor,
                property,
                displayName);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(!enabled && !mixed))
            {
                string arrow = expanded ? "▼" : "▶";
                if (GUILayout.Button(arrow, EditorStyles.miniButton, GUILayout.Width(22f), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                {
                    expanded = !expanded;
                    SessionState.SetBool(key, expanded);
                }
            }
            EditorGUILayout.EndHorizontal();

            if ((enabled || mixed) && expanded)
            {
                string description;
                if (!EffectDescriptions.TryGetValue(property.name, out description))
                    description = PropertyHint(property.name);
                if (!string.IsNullOrEmpty(description))
                    EditorGUILayout.LabelField(description, ESEditorPresentation.SubtitleStyle, GUILayout.ExpandWidth(true));

                if (EffectCosts.TryGetValue(property.name, out int effectCost) && effectCost > 0)
                    EditorGUILayout.LabelField(GetEffectCostLabel(property.name, effectCost), ESEditorPresentation.MetaStyle, GUILayout.ExpandWidth(true));
            }
            return expanded;
        }

        private static string GetFeaturePurposeTitle(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return "未命名功能";
            if (FeaturePurposeTitles.TryGetValue(displayName, out string title)) return title;
            title = displayName.StartsWith("启用", StringComparison.Ordinal) || displayName.StartsWith("使用", StringComparison.Ordinal)
                ? displayName.Substring(2).Trim()
                : displayName;
            FeaturePurposeTitles[displayName] = title;
            return title;
        }

        private static string GetEffectCostLabel(string propertyName, int effectCost)
        {
            if (!EffectCostLabels.TryGetValue(propertyName, out string label))
            {
                label = "成本等级 " + effectCost;
                EffectCostLabels[propertyName] = label;
            }
            return label;
        }

        private static bool IsEffectToggle(string name)
        {
            return IsToggle(name);
        }

        private static string GetEffectSessionKey(string shaderName, string propertyName)
        {
            return "ES.Composite.Effect." + shaderName + "." + propertyName;
        }

        private static bool IsCollapsedEffectDependency(MaterialProperty property, string shaderName)
        {
            if (property == null) return true;
            string controller = ResolveController(property.name);
            if (string.IsNullOrEmpty(controller) || !IsEffectToggle(controller)) return false;
            bool expanded = SessionState.GetBool(GetEffectSessionKey(shaderName, controller), true);
            return !expanded;
        }

        private static bool PropertyPassesFilter(MaterialProperty property, MaterialProperty[] all, string filter, string shaderName)
        {
            if (PropertyMatchesFilter(property, filter, shaderName)) return true;
            if (string.IsNullOrEmpty(filter) || !IsEnableProperty(property.name)) return false;

            for (int i = 0; i < all.Length; i++)
            {
                MaterialProperty dependent = all[i];
                if (string.Equals(ResolveController(dependent.name), property.name, StringComparison.Ordinal)
                    && PropertyMatchesFilter(dependent, filter, shaderName))
                    return true;
            }
            return false;
        }

        private static void CloseEffectGroup(ref bool groupOpen, ref string activeGroup)
        {
            if (!groupOpen) return;
            EditorGUILayout.EndVertical();
            groupOpen = false;
            activeGroup = null;
        }

        private static bool IsEnableProperty(string name)
        {
            return IsToggle(name) || name == "_AnimationMode";
        }

        private static string GetDisplayName(MaterialProperty property)
        {
            return Labels.TryGetValue(property.name, out string label) ? label : property.displayName;
        }

        private static bool BeginCategoryCard(string shaderName, string title, bool forceExpanded)
        {
            EditorGUILayout.Space(5f);
            string key = GetCategorySessionKey(shaderName, title);
            bool expanded = forceExpanded || SessionState.GetBool(key, true);

            Color previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.24f, 0.48f, 0.78f, 0.72f)
                : new Color(0.52f, 0.70f, 0.94f, 0.82f);
            EditorGUILayout.BeginVertical("Helpbox");
            GUI.backgroundColor = previousBackground;

            EditorGUILayout.BeginHorizontal();
            bool headerClicked = GUILayout.Button(title, ESEditorPresentation.HeaderStyle, GUILayout.Height(22f), GUILayout.ExpandWidth(true));
            bool arrowClicked = GUILayout.Button(expanded ? "▼" : "▶", EditorStyles.miniButton, GUILayout.Width(22f), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
            if (headerClicked || arrowClicked)
            {
                expanded = !expanded;
                SessionState.SetBool(key, expanded);
            }

            if (!expanded)
                EditorGUILayout.EndVertical();
            return expanded;
        }

        private static string GetCategorySessionKey(string shaderName, string title)
        {
            Dictionary<string, string> keys;
            if (!CategorySessionKeys.TryGetValue(shaderName, out keys))
            {
                keys = new Dictionary<string, string>(StringComparer.Ordinal);
                CategorySessionKeys[shaderName] = keys;
            }

            string key;
            if (!keys.TryGetValue(title, out key))
            {
                key = "ES.Composite.Category." + shaderName + "." + title;
                keys[title] = key;
            }
            return key;
        }

        private static void DrawProperty(MaterialEditor editor, MaterialProperty property, string displayName)
        {
            bool showReset = !IsToggle(property.name);
            string hint = PropertyHint(property.name);
            bool resetRequested = ESCompositeCodingHelper.DrawProperty(
                editor,
                property,
                displayName,
                showReset,
                !showReset || !IsDefault(property, editor),
                hint);
            if (resetRequested) Reset(property, editor);
        }

        private static bool IsVisible(MaterialProperty property, MaterialProperty[] all)
        {
            string controller = ResolveController(property.name);
            if (!string.IsNullOrEmpty(controller))
            {
                MaterialProperty toggle = Find(all, controller);
                if (toggle != null && !toggle.hasMixedValue && toggle.floatValue < 0.5f) return false;
            }
            if ((property.name.IndexOf("Dissolve", StringComparison.Ordinal) >= 0 && property.name != "_DissolveMode") || property.name == "_FadeProgress" || property.name == "_FadePosition" || property.name == "_FadeWidth" || property.name == "_FadeMask")
            {
                MaterialProperty mode = Find(all, "_DissolveMode") ?? Find(all, "_FadeMode");
                if (mode != null && !mode.hasMixedValue && mode.floatValue < 0.5f) return false;
            }
            if (property.name == "_SequenceColumns" || property.name == "_SequenceRows" || property.name == "_SequenceFrame" || property.name == "_SequenceSpeed")
            {
                MaterialProperty mode = Find(all, "_AnimationMode");
                if (mode != null && !mode.hasMixedValue && mode.floatValue < 0.5f) return false;
            }
            if (property.name == "_CustomTime")
            {
                MaterialProperty mode = Find(all, "_TimeMode");
                if (mode != null && !mode.hasMixedValue && Mathf.RoundToInt(mode.floatValue) != 2) return false;
            }
            return true;
        }

        private static string ResolveController(string name)
        {
            if (name.StartsWith("_Enable", StringComparison.Ordinal)) return null;
            if (name.StartsWith("_AddColor", StringComparison.Ordinal)) return "_EnableAddColor";
            if (name.StartsWith("_StrongTint", StringComparison.Ordinal)) return "_EnableStrongTint";
            if (name.StartsWith("_AlphaTint", StringComparison.Ordinal)) return "_EnableAlphaTint";
            if (name.StartsWith("_Replace", StringComparison.Ordinal)) return "_EnableColorReplace";
            if (name == "_Brightness") return "_EnableBrightness";
            if (name == "_Contrast") return "_EnableContrast";
            if (name == "_Saturation") return "_EnableSaturation";
            if (name == "_Hue") return "_EnableHue";
            if (name == "_NegativeFade") return "_EnableNegative";
            if (name.StartsWith("_Rainbow", StringComparison.Ordinal)) return "_EnableRainbow";
            if (name.StartsWith("_InnerOutline", StringComparison.Ordinal)) return "_EnableInnerOutline";
            if (name.StartsWith("_OuterOutline", StringComparison.Ordinal)) return "_EnableOuterOutline";
            if (name.StartsWith("_PixelOutline", StringComparison.Ordinal)) return "_EnablePixelOutline";
            if (name.StartsWith("_Glow", StringComparison.Ordinal)) return "_EnablePingPongGlow";
            if (name == "_NormalMap" || name == "_NormalScale") return "_UseNormalMap";
            if (name == "_EmissionMap" || name == "_EmissionColor") return "_UseEmission";
            if (name == "_NoiseTex" || name == "_NoiseScale" || name == "_NoiseSpeed" || name == "_DistortionStrength") return "_EnableDistortion";
            if (name == "_Cutoff") return "_AlphaClip";
            if (name == "_OcclusionMap" || name == "_Occlusion") return "_UseOcclusionMap";
            if (name.StartsWith("_Sequence", StringComparison.Ordinal)) return "_AnimationMode";
            if (name.StartsWith("_Frozen", StringComparison.Ordinal)) return "_EnableFrozen";
            if (name.StartsWith("_Burn", StringComparison.Ordinal)) return "_EnableBurn";
            if (name.StartsWith("_Poison", StringComparison.Ordinal)) return "_EnablePoison";
            if (name.StartsWith("_Hologram", StringComparison.Ordinal)) return "_EnableHologram";
            if (name.StartsWith("_Glitch", StringComparison.Ordinal)) return "_EnableGlitch";
            if (name.StartsWith("_Shine", StringComparison.Ordinal)) return "_EnableShine";
            if (name.StartsWith("_Rim", StringComparison.Ordinal)) return "_EnableRim";
            return null;
        }

        private static bool IsAlwaysHidden(MaterialProperty property)
        {
            if (property == null) return true;
            string name = property.name;
            return (property.flags & MaterialProperty.PropFlags.HideInInspector) != 0
                || name == "_texcoord"
                || name == "_AlphaTex"
                || name.StartsWith("unity_", StringComparison.Ordinal);
        }
        private static bool IsToggle(string name) { return name.StartsWith("_Enable", StringComparison.Ordinal) || name.StartsWith("_Use", StringComparison.Ordinal) || name == "_AlphaClip" || name == "_ReceiveShadows" || name.EndsWith("Toggle", StringComparison.Ordinal); }
        private static bool IsStatusFeatureToggle(string name) { return name.StartsWith("_Enable", StringComparison.Ordinal) || name.StartsWith("_Use", StringComparison.Ordinal) || name == "_AlphaClip"; }
        private static string[] ResolveCategoryOrder(string shaderName)
        {
            if (shaderName == "ES/2D/Composite URP") return TwoDCategoryOrder;
            if (shaderName == "ES/3D/VFX Composite URP") return VfxCategoryOrder;
            if (shaderName == "ES/UI/Composite URP") return UiCategoryOrder;
            return LitCategoryOrder;
        }

        private static string ResolveCategory(string shaderName, string name)
        {
            if (shaderName == "ES/2D/Composite URP")
            {
                if (name == "_MainTex" || name == "_Color" || name == "_VertexColorStrength") return "主设置";
                if (name == "_CoordinateMode" || name == "_TimeMode" || name == "_CustomTime" || name == "_TimeScale" || name == "_MainTexScaleOffset" || name == "_AnimationMode" || name.StartsWith("_Sequence", StringComparison.Ordinal)) return "坐标与动画";
                if (name.StartsWith("_Fade", StringComparison.Ordinal) || name.StartsWith("_Dissolve", StringComparison.Ordinal)) return "渐隐与溶解";
                if (name.IndexOf("Outline", StringComparison.Ordinal) >= 0) return "描边";
                if (name.StartsWith("_EnableFrozen", StringComparison.Ordinal) || name.StartsWith("_Frozen", StringComparison.Ordinal)
                    || name.StartsWith("_EnableBurn", StringComparison.Ordinal) || name.StartsWith("_Burn", StringComparison.Ordinal)
                    || name.StartsWith("_EnablePoison", StringComparison.Ordinal) || name.StartsWith("_Poison", StringComparison.Ordinal)) return "状态效果";
                if (name == "_AlphaClip" || name == "_Cutoff") return "输出";
                if (name.StartsWith("_EnableShine", StringComparison.Ordinal) || name.StartsWith("_Shine", StringComparison.Ordinal)
                    || name.StartsWith("_EnablePingPongGlow", StringComparison.Ordinal) || name.StartsWith("_Glow", StringComparison.Ordinal)
                    || name.StartsWith("_EnableDistortion", StringComparison.Ordinal) || name.StartsWith("_Noise", StringComparison.Ordinal)
                    || name.StartsWith("_Distortion", StringComparison.Ordinal) || name.StartsWith("_EnableHologram", StringComparison.Ordinal)
                    || name.StartsWith("_Hologram", StringComparison.Ordinal) || name.StartsWith("_EnableGlitch", StringComparison.Ordinal)
                    || name.StartsWith("_Glitch", StringComparison.Ordinal)) return "动态效果";
                return "颜色处理";
            }

            if (shaderName == "ES/3D/VFX Composite URP")
            {
                if (name == "_MainTex" || name == "_Color" || name == "_VertexColorStrength") return "主设置";
                if (name == "_TimeMode" || name == "_CustomTime" || name == "_TimeScale" || name == "_MainTexScaleOffset") return "时间与坐标";
                if (name.StartsWith("_Noise", StringComparison.Ordinal) || name == "_Distortion") return "噪声与扰动";
                if (name.StartsWith("_Dissolve", StringComparison.Ordinal)) return "溶解";
                if (name.StartsWith("_Hologram", StringComparison.Ordinal) || name == "_EnableHologram") return "全息";
                if (name.StartsWith("_Rim", StringComparison.Ordinal) || name == "_EnableRim") return "边缘光";
                if (name.StartsWith("_Glitch", StringComparison.Ordinal) || name == "_EnableGlitch") return "故障";
                if (name == "_EmissionColor") return "自发光";
                if (name == "_AlphaClip" || name == "_Cutoff" || name == "_QualityTier") return "输出与质量";
                return "主设置";
            }

            if (shaderName == "ES/UI/Composite URP")
            {
                if (name == "_MainTex" || name == "_Color" || name == "_VertexColorStrength") return "主设置";
                if (name == "_TimeMode" || name == "_CustomTime" || name == "_TimeScale" || name == "_MainTexScaleOffset") return "时间与坐标";
                if (name == "_AlphaClip" || name == "_Cutoff" || name.StartsWith("_Stencil", StringComparison.Ordinal) || name == "_ColorMask" || name == "_UseUIAlphaClip") return "遮罩与输出";
                return "动态效果";
            }

            if (name == "_BaseMap" || name == "_BaseColor" || name == "_UseNormalMap" || name == "_NormalMap" || name == "_NormalScale" || name == "_Metallic" || name == "_Smoothness") return "主材质";
            if (name == "_TimeMode" || name == "_CustomTime" || name == "_TimeScale" || name == "_MainTexScaleOffset") return "时间与坐标";
            if (name == "_Occlusion" || name == "_UseOcclusionMap" || name == "_OcclusionMap" || name == "_UseEmission" || name == "_EmissionColor" || name == "_EmissionMap" || name == "_ReceiveShadows") return "光照";
            if (name.StartsWith("_Dissolve", StringComparison.Ordinal) || name.StartsWith("_Noise", StringComparison.Ordinal)) return "渐隐与溶解";
            if (name == "_AlphaClip" || name == "_Cutoff" || name == "_QualityTier") return "输出与质量";
            return "表现效果";
        }
        private static string PropertyHint(string name)
        {
            if (name == "_QualityTier") return "基础/标准/高质量会同步控制 ES 关键词。";
            if (name == "_TimeMode") return "场景时间受 Time.timeScale 影响；非缩放时间由 ES 运行时驱动；自定义时间由调用方写入。";
            if (name == "_CustomTime") return "选择自定义时间后生效；建议通过 MaterialPropertyBlock 或 ESCompositeURPProperties.SetTime 写入。";
            if (name == "_TimeScale") return "统一乘在当前时间源上；各效果自身的速度参数仍独立生效。";
            if (name == "_MainTexScaleOffset") return "X/Y 为缩放，Z/W 为偏移；支持 MaterialPropertyBlock 对单个对象覆盖。";
            if (name == "_ReceiveShadows") return "关闭后不会写入接收阴影关键字。";
            if (name == "_UseNormalMap") return "关闭时跳过法线纹理采样；开启后才显示纹理和强度。";
            if (name == "_UseEmission") return "关闭时跳过自发光纹理采样；开启后才显示颜色和纹理。";
            if (name == "_NormalMap") return "纹理导入类型应为 Normal map。";
            if (name == "_NoiseTex") return "建议使用 Repeat 包裹和线性过滤。";
            if (name == "_AlphaClip") return "透明裁剪会改变渲染队列和深度行为。";
            return null;
        }

        private static MaterialProperty Find(MaterialProperty[] properties, string name)
        {
            for (int i = 0; i < properties.Length; i++) if (properties[i].name == name) return properties[i];
            return null;
        }

        private static void SyncKeywords(MaterialEditor editor, MaterialProperty[] properties)
        {
            MaterialProperty quality = Find(properties, "_QualityTier");
            if (quality != null)
            {
                for (int i = 0; i < editor.targets.Length; i++)
                {
                    Material material = editor.targets[i] as Material; if (material == null) continue;
                    int tier = material.HasProperty(quality.name)
                        ? Mathf.Clamp(Mathf.RoundToInt(material.GetFloat(quality.name)), 0, 2)
                        : 1;
                    bool changed = SetKeyword(material, "_ES_QUALITY_STANDARD", tier == 1);
                    changed |= SetKeyword(material, "_ES_QUALITY_HIGH", tier >= 2);
                    if (changed) EditorUtility.SetDirty(material);
                }
            }
            MaterialProperty shadows = Find(properties, "_ReceiveShadows");
            if (shadows != null)
            {
                for (int i = 0; i < editor.targets.Length; i++)
                {
                    Material material = editor.targets[i] as Material;
                    if (material == null) continue;
                    bool receiveShadows = !material.HasProperty(shadows.name) || material.GetFloat(shadows.name) > 0.5f;
                    if (SetKeyword(material, "_RECEIVE_SHADOWS_OFF", !receiveShadows))
                        EditorUtility.SetDirty(material);
                }
            }
        }

        private static bool SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled && !material.IsKeywordEnabled(keyword))
            {
                material.EnableKeyword(keyword);
                return true;
            }
            if (!enabled && material.IsKeywordEnabled(keyword))
            {
                material.DisableKeyword(keyword);
                return true;
            }
            return false;
        }

        private static Material GetDefault(MaterialEditor editor)
        {
            Material source = editor.target as Material; if (source == null || source.shader == null) return null;
            if (!Defaults.TryGetValue(source.shader, out Material value) || value == null)
            {
                value = new Material(source.shader) { hideFlags = HideFlags.HideAndDontSave };
                Defaults[source.shader] = value;
            }
            return value;
        }

        private static void ReleaseDefaults()
        {
            foreach (KeyValuePair<Shader, Material> pair in Defaults)
            {
                if (pair.Value != null) UnityEngine.Object.DestroyImmediate(pair.Value);
            }
            Defaults.Clear();
            RouteCache.Clear();
            CategorySessionKeys.Clear();
            FeaturePurposeTitles.Clear();
            EffectCostLabels.Clear();
        }

        private static bool IsDefault(MaterialProperty property, MaterialEditor editor)
        {
            if (property.hasMixedValue) return false;
            Material material = GetDefault(editor); if (material == null) return true;
            switch (property.type)
            {
                case MaterialProperty.PropType.Color: return property.colorValue == material.GetColor(property.name);
                case MaterialProperty.PropType.Vector: return property.vectorValue == material.GetVector(property.name);
                case MaterialProperty.PropType.Texture: return property.textureValue == material.GetTexture(property.name);
                default: return Mathf.Approximately(property.floatValue, material.GetFloat(property.name));
            }
        }

        private static void Reset(MaterialProperty property, MaterialEditor editor)
        {
            Material material = GetDefault(editor); if (material == null) return;
            Undo.RecordObjects(editor.targets, "重置 ES Composite 属性");
            switch (property.type)
            {
                case MaterialProperty.PropType.Color: property.colorValue = material.GetColor(property.name); break;
                case MaterialProperty.PropType.Vector: property.vectorValue = material.GetVector(property.name); break;
                case MaterialProperty.PropType.Texture: property.textureValue = material.GetTexture(property.name); break;
                default: property.floatValue = material.GetFloat(property.name); break;
            }
            for (int i = 0; i < editor.targets.Length; i++)
                if (editor.targets[i] != null) EditorUtility.SetDirty(editor.targets[i]);
        }
    }
}
