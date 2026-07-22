using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public class ER_ESEditorInspectorUser : EditorRegister_FOR_Singleton<ESEditorInspectorUser>
    {
        public override int Order => EditorRegisterOrder.Level1.GetHashCode();
        private static bool init = true;
        private static ER_ESEditorInspectorUser activeRegister;
        Comparer<ESEditorInspectorUser> comp;
        public List<ESEditorInspectorUser> users = new List<ESEditorInspectorUser>();
        public override void Handle(ESEditorInspectorUser singleton)
        {
            activeRegister = this;
            if (init)
            {
                init = false;
                comp = Comparer<ESEditorInspectorUser>.Create((a, b) => a.Order - b.Order);
                UnityEditor.Editor.finishedDefaultHeaderGUI -= OnFinishedDefaultHeaderGUI;
                UnityEditor.Editor.finishedDefaultHeaderGUI += OnFinishedDefaultHeaderGUI;
            }
            users.Add(singleton);
            users.Sort(comp);
        }

        private static void OnFinishedDefaultHeaderGUI(UnityEditor.Editor ed)
        {
            if (activeRegister == null || ed == null || ed.targets == null || ed.targets.Length != 1)
                return;

            var target = ed.targets[0];
            foreach (var user in activeRegister.users)
            {
                if (user.Apply(target))
                    break;
            }
        }
    }

}
