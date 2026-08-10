using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// Window/IMGUI companion for <see cref="ESEditorSectionNavigatorDrawer"/>.
    /// It keeps the same directory semantics when a page draws its own content instead
    /// of letting Odin build a PropertyTree group.
    /// </summary>
    public readonly struct ESEditorSectionNavigatorItem
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Tooltip;
        public readonly string StatusText;
        public readonly Color StatusColor;

        public ESEditorSectionNavigatorItem(
            string id,
            string displayName,
            string tooltip = null,
            string statusText = null,
            Color? statusColor = null)
        {
            Id = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
            Tooltip = tooltip ?? string.Empty;
            StatusText = statusText ?? string.Empty;
            StatusColor = statusColor ?? new Color(0.62f, 0.66f, 0.72f);
        }
    }

    /// <summary>
    /// Draws an adaptive business directory. It deliberately does not use toolbar or
    /// toggle-button styles: names stay visible, selected state is a thin underline,
    /// and status is only rendered when the caller supplies real status text.
    /// </summary>
    public static class ESEditorSectionNavigatorIMGUI
    {
        private const string SessionKeyPrefix = "ES.EditorSectionNavigator.Window.";
        private const float ItemHeight = 24f;
        private const float ItemHorizontalPadding = 10f;
        private const float ItemGap = 6f;
        private const float StatusGap = 4f;
        private const float RowGap = 2f;
        private static readonly GUIContent SharedContent = new GUIContent();
        private static GUIStyle normalStyle;
        private static GUIStyle selectedStyle;
        private static GUIStyle statusStyle;
        private static bool stylesInitialized;
        private static bool stylesProSkin;
        private static int cachedSkinGeneration = -1;

        public static string Draw(
            string persistenceKey,
            string currentId,
            IReadOnlyList<ESEditorSectionNavigatorItem> items)
        {
            if (items == null || items.Count == 0)
                return currentId;

            EnsureStyles();
            string selectedId = ResolveSelectedId(persistenceKey, currentId, items);
            float availableWidth = Mathf.Max(180f, EditorGUIUtility.currentViewWidth - 24f);
            int rowCount = CalculateRowCount(items, availableWidth);
            Rect totalRect = GUILayoutUtility.GetRect(0f, rowCount * ItemHeight + Mathf.Max(0, rowCount - 1) * RowGap + 2f, GUILayout.ExpandWidth(true));
            float actualWidth = Mathf.Max(180f, totalRect.width);

            float x = totalRect.x;
            float y = totalRect.y;
            for (int i = 0; i < items.Count; i++)
            {
                ESEditorSectionNavigatorItem item = items[i];
                if (string.IsNullOrEmpty(item.Id))
                    continue;

                float width = GetItemWidth(item);
                if (x > totalRect.x && x + width > totalRect.x + actualWidth)
                {
                    x = totalRect.x;
                    y += ItemHeight + RowGap;
                }

                Rect itemRect = new Rect(x, y, width, ItemHeight);
                bool selected = string.Equals(selectedId, item.Id, StringComparison.Ordinal);
                SharedContent.text = item.DisplayName;
                SharedContent.tooltip = item.Tooltip;
                if (GUI.Button(itemRect, SharedContent, selected ? selectedStyle : normalStyle))
                {
                    selectedId = item.Id;
                    PersistSelection(persistenceKey, selectedId);
                }

                if (selected && Event.current.type == EventType.Repaint)
                {
                    Rect underline = new Rect(itemRect.x + ItemHorizontalPadding, itemRect.yMax - 2f, Mathf.Max(12f, itemRect.width - ItemHorizontalPadding * 2f), 1.5f);
                    EditorGUI.DrawRect(underline, new Color(0.28f, 0.58f, 0.94f));
                }

                if (!string.IsNullOrEmpty(item.StatusText))
                {
                    SharedContent.text = item.StatusText;
                    SharedContent.tooltip = item.Tooltip;
                    Color previous = GUI.contentColor;
                    GUI.contentColor = item.StatusColor;
                    Rect statusRect = new Rect(itemRect.xMax - CalcStatusWidth(item.StatusText) - ItemHorizontalPadding, itemRect.y + 1f, CalcStatusWidth(item.StatusText), ItemHeight - 3f);
                    GUI.Label(statusRect, SharedContent, statusStyle);
                    GUI.contentColor = previous;
                }

                x += width + ItemGap;
            }

            if (Event.current.type == EventType.Repaint)
            {
                Rect divider = new Rect(totalRect.x, totalRect.yMax, totalRect.width, 1f);
                EditorGUI.DrawRect(divider, EditorGUIUtility.isProSkin
                    ? new Color(1f, 1f, 1f, 0.12f)
                    : new Color(0f, 0f, 0f, 0.12f));
            }

            return selectedId;
        }

        private static string ResolveSelectedId(
            string persistenceKey,
            string currentId,
            IReadOnlyList<ESEditorSectionNavigatorItem> items)
        {
            string safeKey = BuildSessionKey(persistenceKey);
            string persisted = SessionState.GetString(safeKey, currentId ?? string.Empty);
            if (Contains(items, persisted))
                return persisted;

            string fallback = Contains(items, currentId) ? currentId : items[0].Id;
            PersistSelection(persistenceKey, fallback);
            return fallback;
        }

        private static bool Contains(IReadOnlyList<ESEditorSectionNavigatorItem> items, string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            for (int i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].Id, id, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static int CalculateRowCount(IReadOnlyList<ESEditorSectionNavigatorItem> items, float availableWidth)
        {
            int rows = 1;
            float usedWidth = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                if (string.IsNullOrEmpty(items[i].Id))
                    continue;

                float itemWidth = GetItemWidth(items[i]);
                if (usedWidth > 0f && usedWidth + itemWidth > availableWidth)
                {
                    rows++;
                    usedWidth = 0f;
                }

                usedWidth += itemWidth + ItemGap;
            }

            return rows;
        }

        private static float GetItemWidth(ESEditorSectionNavigatorItem item)
        {
            SharedContent.text = item.DisplayName;
            SharedContent.tooltip = string.Empty;
            float textWidth = normalStyle.CalcSize(SharedContent).x;
            float statusWidth = string.IsNullOrEmpty(item.StatusText) ? 0f : StatusGap + CalcStatusWidth(item.StatusText);
            return Mathf.Clamp(textWidth + ItemHorizontalPadding * 2f + statusWidth, 64f, 220f);
        }

        private static float CalcStatusWidth(string text)
        {
            SharedContent.text = text;
            SharedContent.tooltip = string.Empty;
            return Mathf.Max(12f, statusStyle.CalcSize(SharedContent).x);
        }

        private static string BuildSessionKey(string persistenceKey)
        {
            return SessionKeyPrefix + (string.IsNullOrWhiteSpace(persistenceKey) ? "default" : persistenceKey.Trim());
        }

        private static void PersistSelection(string persistenceKey, string selectedId)
        {
            SessionState.SetString(BuildSessionKey(persistenceKey), selectedId ?? string.Empty);
        }

        private static void EnsureStyles()
        {
            int currentSkinGeneration = ESEditorPresentation.SkinGeneration;
            bool proSkin = EditorGUIUtility.isProSkin;
            if (stylesInitialized && stylesProSkin == proSkin && cachedSkinGeneration == currentSkinGeneration)
                return;

            stylesInitialized = true;
            stylesProSkin = proSkin;
            cachedSkinGeneration = currentSkinGeneration;
            normalStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset((int)ItemHorizontalPadding, (int)ItemHorizontalPadding, 1, 1),
                fixedHeight = ItemHeight,
                fontStyle = FontStyle.Normal
            };
            normalStyle.normal.textColor = proSkin ? new Color(0.72f, 0.75f, 0.80f) : new Color(0.28f, 0.31f, 0.36f);
            normalStyle.hover.textColor = proSkin ? Color.white : new Color(0.08f, 0.12f, 0.18f);

            selectedStyle = new GUIStyle(normalStyle) { fontStyle = FontStyle.Bold };
            selectedStyle.normal.textColor = proSkin ? new Color(0.62f, 0.82f, 1f) : new Color(0.08f, 0.37f, 0.70f);
            selectedStyle.hover.textColor = selectedStyle.normal.textColor;

            statusStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleRight,
                clipping = TextClipping.Clip
            };
        }
    }
}
