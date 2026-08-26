#if UNITY_EDITOR
using System;
using Newtonsoft.Json.Linq;

namespace ES.Editor
{
    /// <summary>Converts generic ScreenSpec v3 into the deterministic UGUI execution shape.</summary>
    internal static class ESUIScreenSpecAdapter
    {
        public static bool IsScreenSpecV3(string json)
        {
            JObject root = JObject.Parse(json);
            return root.Value<int?>("schemaVersion") == 3 && root["components"] is JArray;
        }

        public static string Normalize(string json)
        {
            JObject source = JObject.Parse(json);
            if (source.Value<int?>("schemaVersion") != 3 || !(source["components"] is JArray)) return json;
            string screenId = source.Value<string>("screenId") ?? "ui-panel";
            JObject result = new JObject
            {
                ["panelId"] = screenId,
                ["prefabPath"] = source.Value<string>("prefabPath") ?? $"Assets/UI/Prefabs/Generated/{screenId}.prefab",
                ["fixtureScenePath"] = source.Value<string>("fixtureScenePath") ?? $"Assets/UI/Scenes/Generated/{screenId}.unity",
                ["tokens"] = source["tokens"]?.DeepClone() ?? new JObject(),
                ["assets"] = source["assets"]?.DeepClone() ?? new JArray(),
                ["profiles"] = source["profiles"]?.DeepClone() ?? new JArray(),
                ["states"] = source["states"]?.DeepClone() ?? new JArray(),
                ["qualityGates"] = source["qualityGates"]?.DeepClone() ?? new JObject(),
                ["designContract"] = source["designContract"]?.DeepClone() ?? new JObject(),
                ["intentContract"] = source["intentContract"]?.DeepClone() ?? new JObject(),
                ["stateSemantics"] = source["stateSemantics"]?.DeepClone() ?? new JObject(),
                ["profileAvailability"] = source["profileAvailability"]?.DeepClone() ?? new JObject(),
                ["bindings"] = source["bindings"]?.DeepClone() ?? new JArray(),
                ["artifactStatus"] = source["artifactStatus"]?.DeepClone() ?? new JObject(),
                ["generationMode"] = source.Value<string>("generationMode") ?? string.Empty,
                ["rootLayoutIntent"] = new JObject
                {
                    ["mode"] = "stretch", ["anchorMinX"] = 0f, ["anchorMinY"] = 0f,
                    ["anchorMaxX"] = 1f, ["anchorMaxY"] = 1f, ["pivotX"] = 0.5f, ["pivotY"] = 0.5f
                },
                ["elements"] = new JArray()
            };
            if (source["designEvidence"] != null)
                result["designEvidence"] = source["designEvidence"].DeepClone();
            foreach (JToken component in (JArray)source["components"])
                ((JArray)result["elements"]).Add(NormalizeComponent(component));
            return result.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static JObject NormalizeComponent(JToken token)
        {
            JObject component = token as JObject ?? throw new InvalidOperationException("ScreenSpec component must be an object.");
            string type = component.Value<string>("type") ?? "frame";
            string kind = KindFor(type);
            JObject content = component["content"] as JObject;
            JObject layout = component["layout"] as JObject ?? new JObject();
            JArray bounds = layout["bounds"] as JArray ?? new JArray(0f, 0f, 1f, 1f);
            JArray minSize = layout["minSize"] as JArray ?? new JArray(0f, 0f);
            JObject anchor = layout["anchor"] as JObject ?? new JObject();
            JArray pivot = anchor["pivot"] as JArray ?? layout["pivot"] as JArray ?? new JArray(0.5f, 0.5f);
            JToken value = content == null ? null : content["value"];
            string layoutMode = layout.Value<string>("mode") ?? "content";
            string anchorStrategy = anchor.Value<string>("strategy") ?? layout.Value<string>("anchorStrategy") ?? layoutMode;
            string visualVariant = component.Value<string>("visualVariant") ?? "default";
            string colorToken = component.Value<string>("colorToken") ?? ColorTokenForVariant(visualVariant);
            JArray preferredSize = layout["preferredSize"] as JArray;
            float preferredWidth = layout.Value<float?>("preferredWidth")
                                   ?? (preferredSize != null && preferredSize.Count > 0 ? preferredSize[0].Value<float>() : 0f);
            float preferredHeight = layout.Value<float?>("preferredHeight")
                                    ?? (preferredSize != null && preferredSize.Count > 1 ? preferredSize[1].Value<float>() : 0f);
            JObject result = new JObject
            {
                ["id"] = component.Value<string>("id") ?? type,
                ["kind"] = kind,
                ["componentType"] = type,
                ["visualVariant"] = visualVariant,
                ["assetSlots"] = component["assetSlots"]?.DeepClone() ?? new JArray(),
                ["value"] = value ?? 0f,
                ["hasValue"] = value is JValue && ((JValue)value).Type != JTokenType.Boolean && ((JValue)value).Type != JTokenType.Null,
                ["text"] = content?.Value<string>("text") ?? string.Empty,
                ["colorToken"] = colorToken,
                ["typographyRole"] = component.Value<string>("typographyRole") ?? DefaultTypographyRole(type),
                ["layerRole"] = component.Value<string>("layerRole") ?? DefaultLayerRole(type, visualVariant),
                ["siblingOrder"] = component.Value<int?>("siblingOrder") ?? -1,
                // ScreenSpec v3 owns responsiveMode inside layout. Keep the
                // top-level read as a compatibility fallback for older packets,
                // but never silently collapse an authored profile into "both".
                ["layout"] = layout.Value<string>("responsiveMode")
                    ?? component.Value<string>("responsiveMode")
                    ?? "both",
                ["width"] = preferredWidth,
                ["height"] = preferredHeight,
                ["minWidth"] = minSize[0],
                ["minHeight"] = minSize[1],
                ["fillWidth"] = true,
                ["interactable"] = component["interaction"] is JObject,
                ["interaction"] = component["interaction"]?.DeepClone() ?? new JObject(),
                ["stateVariants"] = component["stateVariants"]?.DeepClone() ?? new JObject(),
                ["layoutIntent"] = new JObject
                {
                    ["mode"] = LayoutIntentMode(anchorStrategy),
                    ["anchorMinX"] = bounds[0], ["anchorMinY"] = bounds[1], ["anchorMaxX"] = bounds[2], ["anchorMaxY"] = bounds[3],
                    ["pivotX"] = pivot.Count > 0 ? pivot[0] : 0.5f, ["pivotY"] = pivot.Count > 1 ? pivot[1] : 0.5f,
                    ["anchorStrategy"] = anchorStrategy,
                    ["anchorEdge"] = anchor.Value<string>("edge") ?? layout.Value<string>("anchorEdge") ?? "none",
                    ["safeArea"] = anchor.Value<string>("safeArea") ?? layout.Value<string>("safeArea") ?? "inherit"
                },
                ["children"] = new JArray()
            };
            JObject layoutSpec = LayoutSpec(component);
            if (layoutSpec != null) result["layoutSpec"] = layoutSpec;
            JArray children = component["children"] as JArray;
            if (children != null)
                foreach (JToken child in children) ((JArray)result["children"]).Add(NormalizeComponent(child));
            return result;
        }

        private static string ColorTokenForVariant(string visualVariant)
        {
            switch ((visualVariant ?? string.Empty).ToLowerInvariant())
            {
                case "none": return "#FFFFFF";
                case "background": return "background";
                case "accent":
                case "accent-strong":
                case "selected": return "accent";
                case "muted":
                case "mutedtext": return "mutedText";
                case "danger": return "danger";
                case "feedback": return "danger";
                case "text": return "text";
                default: return "surface";
            }
        }

        private static string DefaultTypographyRole(string componentType)
        {
            switch ((componentType ?? string.Empty).ToLowerInvariant())
            {
                case "button": case "item-card": case "item-slot": case "tab-bar": return "label";
                case "counter": case "progress": case "bar": case "cooldown": return "numeric";
                case "subtitle": case "input-hint": return "caption";
                case "text": case "status-badge": case "stat-row": case "nameplate": return "body";
                default: return "none";
            }
        }

        private static string DefaultLayerRole(string componentType, string visualVariant)
        {
            string variant = (visualVariant ?? string.Empty).ToLowerInvariant();
            if (variant == "danger" || variant == "error" || variant == "feedback" || componentType == "error-state" || componentType == "loading") return "feedback";
            if (componentType == "button" || componentType == "slider" || componentType == "toggle" || componentType == "dropdown" || componentType == "input-hint") return "action";
            if (variant == "background") return "background";
            return "information";
        }

        private static JObject LayoutSpec(JObject component)
        {
            JObject layout = component["layout"] as JObject;
            JArray children = component["children"] as JArray;
            string mode = layout?.Value<string>("mode") ?? string.Empty;
            if (children == null || children.Count == 0 || (mode != "grid" && mode != "list" && mode != "flow")) return null;
            JArray padding = layout["padding"] as JArray ?? new JArray(16, 16, 16, 16);
            JArray cell = layout["cellSize"] as JArray ?? new JArray(220, 120);
            float gap = layout.Value<float?>("gap") ?? 12f;
            return new JObject
            {
                ["layoutMode"] = mode == "grid" ? "grid" : mode == "list" ? "vertical" : "horizontal",
                ["axis"] = mode == "list" ? "vertical" : "horizontal", ["gap"] = gap,
                ["paddingLeft"] = padding[0], ["paddingRight"] = padding[1], ["paddingTop"] = padding[2], ["paddingBottom"] = padding[3],
                ["columns"] = layout.Value<int?>("columns") ?? 1, ["cellWidth"] = cell[0], ["cellHeight"] = cell[1],
                ["spacingX"] = gap, ["spacingY"] = gap, ["childAlignment"] = "upper-left",
                ["controlChildWidth"] = true, ["controlChildHeight"] = true,
                ["forceChildExpandWidth"] = mode == "list", ["forceChildExpandHeight"] = false,
                ["childGeometryOwner"] = layout.Value<string>("childGeometryOwner") ?? "parent-layout-group"
            };
        }

        private static string KindFor(string type)
        {
            switch (type)
            {
                case "text": case "subtitle": case "damage-pop": return "text";
                case "icon": case "image": case "portrait": case "focus-ring": case "marker": return "image";
                case "button": case "toggle": case "dropdown": return "button";
                case "slider": return "slider";
                case "item-slot": case "item-card": return "card";
                case "list": case "grid": case "tab-bar": case "choice-list": case "reward-track": return "list";
                default: return "panel";
            }
        }

        private static string LayoutIntentMode(string mode)
        {
            switch ((mode ?? string.Empty).ToLowerInvariant())
            {
                case "center": return "centered";
                case "stretch": case "edge-docked": case "absolute": case "content": return mode;
                case "fixed": return "fixed";
                default: return "content";
            }
        }
    }
}
#endif
