using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    public sealed partial class ESCompositeShaderGUI
    {
        #region Effect Navigation

        private static string DrawEffectNavigator(string shaderName, MaterialProperty[] properties)
        {
            string searchKey = "ES.Composite.Navigator.Search." + shaderName;
            string routeKey = "ES.Composite.Navigator.Route." + shaderName;
            string panelKey = "ES.Composite.Navigator.Panel." + shaderName;
            string search = SessionState.GetString(searchKey, string.Empty);
            string selectedRoute = SessionState.GetString(routeKey, string.Empty);
            bool hasActiveFilter = !string.IsNullOrWhiteSpace(search) || FindRoute(selectedRoute) != null;
            bool expanded = hasActiveFilter || SessionState.GetBool(panelKey, false);

            EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle);
            EditorGUILayout.BeginHorizontal();
            bool nextExpanded = EditorGUILayout.Foldout(expanded, "效果导航", true);
            bool filterCleared = false;
            GUILayout.FlexibleSpace();
            if (hasActiveFilter && DrawContentSizedButton(ClearFilterButtonContent, EditorStyles.miniButton))
            {
                search = string.Empty;
                selectedRoute = string.Empty;
                SessionState.SetString(searchKey, search);
                SessionState.SetString(routeKey, selectedRoute);
                hasActiveFilter = false;
                expanded = SessionState.GetBool(panelKey, false);
                filterCleared = true;
            }
            EditorGUILayout.EndHorizontal();
            if (!hasActiveFilter && !filterCleared && nextExpanded != expanded)
            {
                expanded = nextExpanded;
                SessionState.SetBool(panelKey, expanded);
            }
            if (!expanded)
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4f);
                return string.Empty;
            }

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
                float inspectorWidth = EditorGUIUtility.currentViewWidth;
                int columns = inspectorWidth < 330f ? 2 : inspectorWidth < 520f ? 3 : 4;
                int nextIndex = GUILayout.SelectionGrid(selectedIndex, routeTitles, columns, EditorStyles.toolbarButton);
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
                GUILayout.Label("正在查看：" + selected.Title, ESEditorPresentation.SubtitleStyle);
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
            if (route.Key == "animation" && shaderName != "ES/2D/Composite URP") return false;
            string routeController = ResolveRouteController(route.Key);
            if (!string.IsNullOrEmpty(routeController))
                return property.name == routeController
                    || string.Equals(ResolveController(property.name, shaderName), routeController, StringComparison.Ordinal);
            if (ResolveCategory(shaderName, property.name) == route.Category) return true;
            for (int i = 0; i < route.Aliases.Length; i++)
                if (property.name.IndexOf(route.Aliases[i], StringComparison.OrdinalIgnoreCase) >= 0
                    || GetDisplayName(property).IndexOf(route.Aliases[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static string ResolveRouteController(string routeKey)
        {
            switch (routeKey)
            {
                case "shine": return "_EnableShine";
                case "sparkle": return "_EnableSparkle";
                case "flow": return "_EnableFlow";
                case "flow-map": return "_EnableFlowMap";
                case "vertex-animation": return "_EnableVertexAnimation";
                case "sequence": return "_EnableSequence";
                case "polar-uv": return "_EnablePolarUV";
                case "vertex-streams": return "_EnableVertexStreams";
                case "soft-particles": return "_EnableSoftParticles";
                case "depth-intersection": return "_EnableDepthIntersection";
                case "radial-mask": return "_EnableRadialMask";
                case "fresnel-mask": return "_EnableFresnelMask";
                case "chromatic": return "_EnableChromatic";
                case "blur": return "_EnableBlur";
                case "rim": return "_EnableRim";
                case "hologram": return "_EnableHologram";
                case "glitch": return "_EnableGlitch";
                case "emission": return "_UseEmission";
                default: return null;
            }
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

        #endregion

        #region Property And Category Drawing

        private static void DrawPropertyStream(MaterialEditor editor, MaterialProperty[] properties, string shaderName, string filter, InspectorViewLevel viewLevel)
        {
            // 先确定稳定的分类顺序，再在分类内部保持 Shader 声明顺序。
            // 这样同一分类只会出现一次，不会因为属性交错而重复生成折叠页签。
            string[] categoryOrder = ResolveCategoryOrder(shaderName);
            for (int c = 0; c < categoryOrder.Length; c++)
            {
                string category = categoryOrder[c];
                if (!HasVisibleCategory(properties, category, shaderName, filter, viewLevel)) continue;
                if (!BeginCategoryCard(shaderName, category, !string.IsNullOrEmpty(filter))) continue;
                DrawCategoryProperties(editor, properties, category, shaderName, filter, viewLevel);
                EditorGUILayout.EndVertical();
            }
        }

        private static bool HasVisibleCategory(MaterialProperty[] properties, string category, string shaderName, string filter, InspectorViewLevel viewLevel)
        {
            for (int i = 0; i < properties.Length; i++)
            {
                MaterialProperty property = properties[i];
                if (ResolveCategory(shaderName, property.name) == category
                    && !IsAlwaysHidden(property)
                    && PropertyPassesFilter(property, properties, filter, shaderName)
                    && PropertyPassesViewLevel(property, filter, viewLevel)) return true;
            }
            return false;
        }

        private static void DrawCategoryProperties(MaterialEditor editor, MaterialProperty[] properties, string category, string shaderName, string filter, InspectorViewLevel viewLevel)
        {
            string activeGroup = null;
            bool groupOpen = false;
            for (int i = 0; i < properties.Length; i++)
            {
                MaterialProperty property = properties[i];
                if (ResolveCategory(shaderName, property.name) != category || IsAlwaysHidden(property)) continue;

                if (!PropertyPassesFilter(property, properties, filter, shaderName)) continue;
                if (!PropertyPassesViewLevel(property, filter, viewLevel)) continue;

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

                if (IsModeFeature(property.name))
                {
                    CloseEffectGroup(ref groupOpen, ref activeGroup);
                    BeginModeFeatureCard(property);
                    DrawProperty(editor, property, GetDisplayName(property));
                    activeGroup = property.name;
                    groupOpen = true;
                    continue;
                }

                if (groupOpen && !string.Equals(ResolveController(property.name, shaderName), activeGroup, StringComparison.Ordinal))
                    CloseEffectGroup(ref groupOpen, ref activeGroup);
                if (string.IsNullOrEmpty(filter) && IsCollapsedEffectDependency(property, shaderName)) continue;
                if (!IsVisible(property, properties, shaderName)) continue;
                DrawProperty(editor, property, GetDisplayName(property));
            }
            CloseEffectGroup(ref groupOpen, ref activeGroup);
        }

        private static bool IsModeFeature(string propertyName)
        {
            return propertyName == "_AnimationMode" || propertyName == "_FadeMode" || propertyName == "_DissolveMode";
        }

        private static void BeginModeFeatureCard(MaterialProperty property)
        {
            bool active = property.hasMixedValue || property.floatValue > 0.5f;
            Color accent = GetEffectAccent(property.name);
            Color previousBackground = GUI.backgroundColor;
            Rect cardRect;
            try
            {
                GUI.backgroundColor = Color.Lerp(Color.white, accent, active ? 0.24f : 0.08f);
                cardRect = EditorGUILayout.BeginVertical("Helpbox");
            }
            finally
            {
                GUI.backgroundColor = previousBackground;
            }
            DrawEffectCardBorder(cardRect, accent, active && !property.hasMixedValue, property.hasMixedValue);
        }

        private static bool DrawEffectCardHeader(MaterialEditor editor, MaterialProperty property, string displayName, string shaderName, bool forceExpanded)
        {
            bool mixed = property.hasMixedValue;
            bool enabled = !mixed && property.floatValue > 0.5f;
            string key = GetEffectSessionKey(shaderName, property.name);
            bool expanded = forceExpanded || mixed || SessionState.GetBool(key, true);
            string title = GetFeaturePurposeTitle(displayName);
            Color accent = GetEffectAccent(property.name);

            Color previousBackground = GUI.backgroundColor;
            try
            {
                Color frameAccent = mixed ? new Color(0.92f, 0.66f, 0.20f, 1f) : accent;
                GUI.backgroundColor = Color.Lerp(Color.white, frameAccent, enabled || mixed ? 0.34f : 0.12f);
                Rect groupRect = EditorGUILayout.BeginVertical("Helpbox");
                DrawEffectCardBorder(groupRect, accent, enabled, mixed);
            }
            finally
            {
                GUI.backgroundColor = previousBackground;
            }

            bool stackedHeader = EditorGUIUtility.currentViewWidth < 260f;
            Rect headerRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.Height(stackedHeader ? 50f : 30f),
                GUILayout.ExpandWidth(true));
            DrawEffectHeaderBackground(headerRect, accent, enabled, mixed);

            const float gap = 3f;
            float right = headerRect.xMax - 5f
                - ESEditorPresentation.GetInspectorRightGutter(headerRect.width);
            float controlY = headerRect.y + (stackedHeader ? 26f : 5f);
            Rect arrowRect = new Rect(right - 22f, controlY, 22f, 20f);
            right = arrowRect.x - gap;
            Rect codeRect = new Rect(right - 28f, controlY, 28f, 20f);
            right = codeRect.x - gap;
            Rect toggleRect = new Rect(right - 18f, controlY + 1f, 18f, 18f);
            right = toggleRect.x - gap;
            bool showStatus = !stackedHeader;
            Rect statusRect = showStatus
                ? new Rect(right - 48f, headerRect.y + 6f, 48f, 18f)
                : Rect.zero;
            if (showStatus)
                right = statusRect.x - gap;
            float titleX = headerRect.x + 11f;
            float titleRight = stackedHeader ? headerRect.xMax - 8f : right;
            Rect titleRect = new Rect(titleX, headerRect.y + 3f, Mathf.Max(0f, titleRight - titleX), 22f);

            bool headerClicked = GUI.Button(titleRect, title, ESEditorPresentation.HeaderStyle);
            if (showStatus)
                DrawEffectStatus(statusRect, accent, enabled, mixed);
            ESCompositeCodingHelper.DrawCompactBooleanProperty(
                editor,
                property,
                displayName,
                toggleRect,
                codeRect);
            bool arrowClicked = false;
            using (new EditorGUI.DisabledScope(!enabled && !mixed))
            {
                string arrow = expanded ? "▼" : "▶";
                arrowClicked = GUI.Button(arrowRect, arrow, EditorStyles.miniButton);
            }
            if ((headerClicked || arrowClicked) && (enabled || mixed))
            {
                expanded = !expanded;
                SessionState.SetBool(key, expanded);
            }

            if ((enabled || mixed) && expanded)
            {
                EditorGUILayout.Space(2f);
                string description = GetEffectDescription(shaderName, property.name);
                if (!string.IsNullOrEmpty(description))
                    EditorGUILayout.LabelField(description, ESEditorPresentation.SubtitleStyle, GUILayout.ExpandWidth(true));
            }
            return expanded;
        }

        private static string GetEffectDescription(string shaderName, string propertyName)
        {
            EffectDescriptions.TryGetValue(propertyName, out string description);
            if (string.IsNullOrEmpty(description))
                description = PropertyHint(propertyName, shaderName);

            int minimumQuality = GetMinimumQualityTier(shaderName, propertyName);
            if (minimumQuality <= 0)
                return description;

            string qualityText = "需要“" + QualityName(minimumQuality) + "”质量档。";
            return string.IsNullOrEmpty(description) ? qualityText : description + " " + qualityText;
        }

        private static Color GetEffectAccent(string propertyName)
        {
            if (EffectAccentOverrides.TryGetValue(propertyName, out Color accent))
                return accent;

            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < propertyName.Length; i++)
                    hash = (hash ^ propertyName[i]) * 16777619u;
                return EffectAccentPalette[(int)(hash % (uint)EffectAccentPalette.Length)];
            }
        }

        private static void DrawEffectHeaderBackground(Rect rect, Color accent, bool enabled, bool mixed)
        {
            Color stateAccent = mixed ? new Color(0.92f, 0.66f, 0.20f, 1f) : accent;
            Color neutral = EditorGUIUtility.isProSkin
                ? new Color(0.12f, 0.14f, 0.18f, 1f)
                : new Color(0.82f, 0.84f, 0.88f, 1f);
            float strength = mixed ? 0.48f : enabled ? 0.52f : 0.14f;
            EditorGUI.DrawRect(rect, Color.Lerp(neutral, stateAccent, strength));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 5f, rect.height), stateAccent);
            EditorGUI.DrawRect(new Rect(rect.x + 5f, rect.y, rect.width - 5f, 1f), Color.Lerp(stateAccent, Color.white, 0.18f));
        }

        private static void DrawEffectCardBorder(Rect rect, Color accent, bool enabled, bool mixed)
        {
            if (Event.current.type != EventType.Repaint || rect.width <= 0f || rect.height <= 0f)
                return;

            Color border = mixed ? new Color(0.92f, 0.66f, 0.20f, 1f) : accent;
            border.a = mixed || enabled ? 0.95f : 0.48f;

            const float thickness = 1f;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), border);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), border);
        }

        private static void DrawEffectStatus(Rect rect, Color accent, bool enabled, bool mixed)
        {
            string status = mixed ? "混合" : enabled ? "已启用" : "未启用";
            Color statusColor = mixed
                ? new Color(0.78f, 0.50f, 0.12f, 0.95f)
                : enabled
                    ? Color.Lerp(new Color(0.10f, 0.12f, 0.16f, 0.96f), accent, 0.62f)
                    : new Color(0.30f, 0.32f, 0.36f, 0.82f);
            EditorGUI.DrawRect(rect, statusColor);
            GUI.Label(rect, status, ESEditorPresentation.MetaStyle);
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
            string controller = ResolveController(property.name, shaderName);
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
                if (string.Equals(ResolveController(dependent.name, shaderName), property.name, StringComparison.Ordinal)
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
            return IsToggle(name) || IsModeFeature(name);
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
            string hint = PropertyHint(property.name, (editor.target as Material)?.shader?.name);
            bool resetRequested = ESCompositeCodingHelper.DrawProperty(
                editor,
                property,
                displayName,
                showReset,
                !showReset || !IsDefault(property, editor),
                hint);
            if (resetRequested) Reset(property, editor);
        }

        #endregion
    }
}
