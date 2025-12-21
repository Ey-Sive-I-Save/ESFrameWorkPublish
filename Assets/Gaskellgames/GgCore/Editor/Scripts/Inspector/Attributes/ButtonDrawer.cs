#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Gaskellgames.EditorOnly
{
    /// <summary>
    /// Code updated by Gaskellgames
    /// Original code from 'EditorCools': https://github.com/datsfain/EditorCools
    /// </summary>

    public class ButtonDrawer
    {
        private readonly List<IGrouping<string, InspectorButton>> ButtonGroups;

        public ButtonDrawer(object target)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var methods = target.GetType().GetMethods(flags);
            var buttons = new List<InspectorButton>();
            var rowNumber = 0;

            foreach (MethodInfo method in methods)
            {
                var buttonAttribute = method.GetCustomAttribute<ButtonAttribute>();
                if (buttonAttribute == null)
                {
                    continue;
                }

                buttons.Add(new InspectorButton(method, buttonAttribute));
            }

            ButtonGroups = buttons.GroupBy(button =>
            {
                var attribute = button.ButtonAttribute;
                if (attribute.Row == "")
                {
                    return $"__{rowNumber++}";
                }

                return attribute.Row;
            }).ToList();
        }

        public void DrawButtons(IEnumerable<object> targets)
        {
            foreach (var buttonGroup in ButtonGroups)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    foreach (var button in buttonGroup)
                    {
                        button.Draw(targets);
                    }
                }
            }
        }
        
    } // class end
} 
#endif