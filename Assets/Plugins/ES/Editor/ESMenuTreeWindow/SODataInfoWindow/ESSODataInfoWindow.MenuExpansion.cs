using System.Reflection;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using UnityEditor;

namespace ES
{
    public partial class ESSODataInfoWindow
    {
            private string pendingExpandMenuPath_ = "";

            public void RequestExpandMenuAfterRefresh(string menuPath)
            {
                pendingExpandMenuPath_ = menuPath;
            }

            private void ApplyPendingMenuExpansion()
            {
                if (pendingExpandMenuPath_.IsNullOrWhitespace())
                    return;

                string menuPath = pendingExpandMenuPath_;
                pendingExpandMenuPath_ = "";

                EditorApplication.delayCall += () =>
                {
                    if (UsingWindow != this || MenuTree == null)
                        return;

                    if (!MenuItems.TryGetValue(menuPath, out var item) || item == null)
                        return;

                    SetMenuItemExpanded(item, true);
                    MenuTree.Selection.Add(item);
                    Repaint();
                };
            }

            private static void SetMenuItemExpanded(OdinMenuItem item, bool expanded)
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var type = item.GetType();
                var property = type.GetProperty("Toggled", flags)
                    ?? type.GetProperty("IsExpanded", flags)
                    ?? type.GetProperty("Expanded", flags);

                if (property != null && property.CanWrite && property.PropertyType == typeof(bool))
                {
                    property.SetValue(item, expanded);
                    return;
                }

                var field = type.GetField("Toggled", flags)
                    ?? type.GetField("IsExpanded", flags)
                    ?? type.GetField("Expanded", flags);

                if (field != null && field.FieldType == typeof(bool))
                    field.SetValue(item, expanded);
            }
    }
}
