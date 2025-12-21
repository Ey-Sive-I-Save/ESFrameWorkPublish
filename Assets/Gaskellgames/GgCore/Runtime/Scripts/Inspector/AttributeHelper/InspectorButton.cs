using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Gaskellgames
{
    /// <summary>
    /// Code updated by Gaskellgames
    /// Original code from 'EditorCools': https://github.com/datsfain/EditorCools
    /// </summary>

    public class InspectorButton
    {
        private readonly string DisplayName;
        private readonly MethodInfo Method;
        public readonly ButtonAttribute ButtonAttribute;

        public InspectorButton(MethodInfo method, ButtonAttribute buttonAttribute)
        {
            ButtonAttribute = buttonAttribute;
            DisplayName = method.Name.NicifyName();
            Method = method;
        }

        internal void Draw(IEnumerable<object> targets)
        {
            if (!GUILayout.Button(DisplayName)) { return; }

            foreach (object target in targets)
            {
                Method?.Invoke(target, null);
            }
        }
    } // class end
}