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
                ["rootLayoutIntent"] = new JObject
                {
                    ["mode"] = "stretch", ["anchorMinX"] = 0f, ["anchorMinY"] = 0f,
                    ["anchorMaxX"] = 1f, ["anchorMaxY"] = 1f, ["pivotX"] = 0.5f, ["pivotY"] = 0.5f
                },
                ["elements"] = new JArray()
            };
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
            JToken value = content == null ? null : content["value"];
            string layoutMode = layout.Value<string>("mode") ?? "content";
            JObject result = new JObject
            {
                ["id"] = component.Value<string>("id") ?? type,
                ["kind"] = kind,
                ["componentType"] = type,
                ["visualVariant"] = component.Value<string>("visualVariant") ?? "default",
                ["assetSlots"] = component["assetSlots"]?.DeepClone() ?? new JArray(),
                ["value"] = value ?? 0f,
                ["hasValue"] = value is JValue && ((JValue)value).Type != JTokenType.Boolean && ((JValue)value).Type != JTokenType.Null,
                ["text"] = content?.Value<string>("text") ?? string.Empty,
                ["colorToken"] = component.Value<string>("visualVariant") ?? "surface",
                ["layout"] = component.Value<string>("responsiveMode") ?? "both",
                ["width"] = layout.Value<float?>("preferredWidth") ?? 0f,
                ["height"] = layout.Value<float?>("preferredHeight") ?? 0f,
                ["minWidth"] = minSize[0],
                ["minHeight"] = minSize[1],
                ["fillWidth"] = true,
                ["interactable"] = component["interaction"] != null,
                ["layoutIntent"] = new JObject
                {
                    ["mode"] = LayoutIntentMode(layoutMode),
                    ["anchorMinX"] = bounds[0], ["anchorMinY"] = bounds[1], ["anchorMaxX"] = bounds[2], ["anchorMaxY"] = bounds[3],
                    ["pivotX"] = 0.5f, ["pivotY"] = 0.5f
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
                ["forceChildExpandWidth"] = mode == "list", ["forceChildExpandHeight"] = false
            };
        }

        private static string KindFor(string type)
        {
            switch (type)
            {
                case "text": case "subtitle": case "damage-pop": return "text";
                case "icon": case "image": case "portrait": case "focus-ring": case "marker": return "image";
                case "button": case "toggle": case "dropdown": return "button";
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
