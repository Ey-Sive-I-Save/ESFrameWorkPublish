using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// Editor-only asset drop resolver. It writes the registered enum ConfigKey into an int field,
    /// never serializes the dragged asset and never expands that asset's dependency graph.
    /// </summary>
    public sealed class ESDragToFieldSolverAttributeDrawer : OdinAttributeDrawer<ESDragToFieldSolverAttribute, int>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            Rect fieldRect = EditorGUILayout.GetControlRect();
            int nextValue = EditorGUI.IntField(fieldRect, label, ValueEntry.SmartValue);
            if (nextValue != ValueEntry.SmartValue)
                ValueEntry.SmartValue = nextValue;

            Event currentEvent = Event.current;
            if (!fieldRect.Contains(currentEvent.mousePosition))
                return;

            if (Attribute.solverOptions != ESDragToFieldSolverOptions.SimpleAssetToABSearchKey)
                return;

            if (currentEvent.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = TryResolveDraggedAsset(out _) ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                currentEvent.Use();
                return;
            }

            if (currentEvent.type != EventType.DragPerform || !TryResolveDraggedAsset(out int configEnumKey))
                return;

            DragAndDrop.AcceptDrag();
            ValueEntry.SmartValue = configEnumKey;
            currentEvent.Use();
        }

        private static bool TryResolveDraggedAsset(out int configEnumKey)
        {
            configEnumKey = 0;
            Object[] objects = DragAndDrop.objectReferences;
            if (objects == null || objects.Length != 1 || objects[0] == null)
                return false;

            string path = AssetDatabase.GetAssetPath(objects[0]);
            if (string.IsNullOrEmpty(path))
                return false;

            string guid = AssetDatabase.AssetPathToGUID(path);
            return !string.IsNullOrEmpty(guid)
                && ESAssetRegistry.TryGetByGuid(guid, out ESAssetPage page)
                && page != null
                && page.EnumKey != 0
                && (configEnumKey = page.EnumKey) != 0;
        }
    }

    /// <summary>
    /// String ConfigKey version of the same editor-only asset drop resolver.
    /// </summary>
    public sealed class ESDragToFieldSolverStringAttributeDrawer : OdinAttributeDrawer<ESDragToFieldSolverAttribute, string>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            Rect fieldRect = EditorGUILayout.GetControlRect();
            string nextValue = EditorGUI.TextField(fieldRect, label, ValueEntry.SmartValue);
            if (nextValue != ValueEntry.SmartValue)
                ValueEntry.SmartValue = nextValue;

            Event currentEvent = Event.current;
            if (!fieldRect.Contains(currentEvent.mousePosition)
                || Attribute.solverOptions != ESDragToFieldSolverOptions.SimpleAssetToABSearchKey)
                return;

            if (currentEvent.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = TryResolveDraggedAsset(out _) ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                currentEvent.Use();
                return;
            }

            if (currentEvent.type != EventType.DragPerform || !TryResolveDraggedAsset(out string configKey))
                return;

            DragAndDrop.AcceptDrag();
            ValueEntry.SmartValue = configKey;
            currentEvent.Use();
        }

        private static bool TryResolveDraggedAsset(out string configKey)
        {
            configKey = null;
            Object[] objects = DragAndDrop.objectReferences;
            if (objects == null || objects.Length != 1 || objects[0] == null)
                return false;

            string path = AssetDatabase.GetAssetPath(objects[0]);
            if (string.IsNullOrEmpty(path))
                return false;

            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)
                || !ESAssetRegistry.TryGetByGuid(guid, out ESAssetPage page)
                || page == null)
                return false;

            configKey = page.EffectiveStringKey;
            return !string.IsNullOrEmpty(configKey);
        }
    }
}
