using System.Collections;
using System;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// Universal Odin presentation for the compact ESField vocabulary. The drawer adds only
    /// ES metadata and emphasis; the next drawer keeps full ownership of the actual value UI.
    /// </summary>
    public sealed class ESFieldAttributeDrawer : OdinAttributeDrawer<ESFieldAttribute>
    {
        private readonly GUIContent nextLabelContent = new GUIContent();
        private bool presentationInitialized;
        private ESFieldLevel cachedLevel;
        private bool cachedRequired;
        private string cachedHint;
        private string cachedMeta;
        private string cachedPrefix;
        private string cachedSuffix;
        private string sourceLabelText;
        private string sourceLabelTooltip;
        private Texture sourceLabelImage;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            EnsurePresentationCache();
            // A plain [ESField] intentionally has zero visual overhead after the one-time
            // cache build. Odin keeps its normal fast path for fields with no ES presentation.
            if (string.IsNullOrEmpty(cachedMeta))
            {
                CallNextDrawer(label);
                return;
            }

            bool empty = IsEmpty(Property?.ValueEntry?.WeakSmartValue);
            ESStatusKind status = cachedRequired && empty
                ? ESStatusKind.Error
                : cachedLevel == ESFieldLevel.Normal
                    ? ESStatusKind.None
                    : ESStatusKind.Info;

            if (!string.IsNullOrEmpty(cachedMeta))
            {
                Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                ESFieldRow.DrawStatus(row, status, cachedMeta, EditorStyles.miniLabel);
            }

            CallNextDrawer(PrepareLabel(label));
        }

        private void EnsurePresentationCache()
        {
            if (presentationInitialized)
                return;

            presentationInitialized = true;
            cachedLevel = Attribute.Level;
            cachedRequired = Attribute.Required;
            cachedHint = string.IsNullOrWhiteSpace(Attribute.Hint) ? null : Attribute.Hint.Trim();
            string levelText = cachedLevel == ESFieldLevel.Core
                ? "核心"
                : cachedLevel == ESFieldLevel.Important
                    ? "重点"
                    : string.Empty;
            cachedPrefix = string.IsNullOrEmpty(levelText) ? string.Empty : levelText + " · ";
            cachedSuffix = cachedRequired ? " *" : string.Empty;
            cachedMeta = levelText;
            if (cachedRequired)
                cachedMeta = string.IsNullOrEmpty(cachedMeta) ? "必填" : cachedMeta + " · 必填";
            if (!string.IsNullOrEmpty(cachedHint))
                cachedMeta = string.IsNullOrEmpty(cachedMeta)
                    ? cachedHint
                    : cachedMeta + " · " + cachedHint;
        }

        private GUIContent PrepareLabel(GUIContent label)
        {
            if (label == null)
                return null;

            string labelText = label.text ?? string.Empty;
            string labelTooltip = label.tooltip ?? string.Empty;
            if (string.Equals(sourceLabelText, labelText, StringComparison.Ordinal)
                && string.Equals(sourceLabelTooltip, labelTooltip, StringComparison.Ordinal)
                && sourceLabelImage == label.image)
                return nextLabelContent;

            sourceLabelText = labelText;
            sourceLabelTooltip = labelTooltip;
            sourceLabelImage = label.image;
            nextLabelContent.text = cachedPrefix + labelText + cachedSuffix;
            nextLabelContent.tooltip = string.IsNullOrEmpty(cachedHint)
                ? labelTooltip
                : string.IsNullOrWhiteSpace(labelTooltip)
                    ? cachedHint
                    : cachedHint + "\n" + labelTooltip;
            nextLabelContent.image = label.image;
            return nextLabelContent;
        }

        private static bool IsEmpty(object value)
        {
            if (value == null)
                return true;
            if (value is string text)
                return string.IsNullOrWhiteSpace(text);
            if (value is UnityEngine.Object unityObject)
                return unityObject == null;
            if (value is IList list)
                return list.Count == 0;
            if (value is bool enabled)
                return !enabled;
            return false;
        }
    }
}
