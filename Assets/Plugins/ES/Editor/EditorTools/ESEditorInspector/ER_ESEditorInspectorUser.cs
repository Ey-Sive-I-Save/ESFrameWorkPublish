using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public class ER_ESEditorInspectorUser : EditorRegister_FOR_Singleton<ESEditorInspectorUser>
    {
        public override int Order => EditorRegisterOrder.Level1.GetHashCode();
        private static bool init = true;
        Comparer<ESEditorInspectorUser> comp;
        public List<ESEditorInspectorUser> users = new List<ESEditorInspectorUser>();
        public override void Handle(ESEditorInspectorUser singleton)
        {
            if (init)
            {
                init = false;
                comp = Comparer<ESEditorInspectorUser>.Create((a, b) => a.Order - b.Order);
                UnityEditor.Editor.finishedDefaultHeaderGUI += (ed) =>
                {

                    if (ed.targets.Length == 1)
                    {
                        var g = ed.targets[0];
                        foreach (var i in users)
                        {
                            if (i.Apply(g))
                            {
                                break;
                            }
                        }
                    }
                };
            }
            users.Add(singleton);
            users.Sort(comp);
        }

    }

}
