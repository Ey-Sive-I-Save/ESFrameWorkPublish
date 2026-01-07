using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static Unity.Burst.Intrinsics.X86.Avx;


namespace ES {
    public abstract class ESEditorInspectorUser 
    {
        public virtual int Order { get; }
        public abstract bool Apply(UnityEngine.Object ob);
    }
    public class ER_ESEditorInspectorUser : EditorRegister_FOR_Singleton<ESEditorInspectorUser>
    {
        public override int Order => EditorRegisterOrder.Level1.GetHashCode();
        private bool init = true;
        Comparer<ESEditorInspectorUser> comp;
        public List<ESEditorInspectorUser> users = new List<ESEditorInspectorUser>();
        public override void Handle(ESEditorInspectorUser singleton)
        {
            if (init)
            {
                init = false;
                comp = Comparer<ESEditorInspectorUser>.Create((a,b)=>a.Order-b.Order);
                Editor.finishedDefaultHeaderGUI += (ed) => {

                    if (ed.targets.Length == 1)
                    {
                        var g = ed.targets[0];
                        foreach(var i in users)
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

    //项目资源绘制-( GUID 和 QuickPather)
    public class ResHelper_GUIDAndPather : ESEditorInspectorUser
    {
        public override bool Apply(UnityEngine.Object ob)
        {
           // Debug.Log(""+ob+ ob.GetType());
            
            if(ob.GetType().IsSubclassOf(typeof(VisualGUIDrawerSO)))
            {
                return false;
            }
            string path = AssetDatabase.GetAssetPath(ob);
            if (!string.IsNullOrEmpty(path))
            {
                string guid= AssetDatabase.AssetPathToGUID(path);
                EditorGUILayout.LabelField("GUID："+guid);
                if (GUILayout.Button("复制GUID到粘贴板")) {

                    ESDesignUtility.SafeEditor.Wrap_SystemCopyBuffer(guid);
                };

                EditorGUILayout.LabelField("路径：" + path);
                if (GUILayout.Button("复制路径到粘贴板"))
                {
                    ESDesignUtility.SafeEditor.Wrap_SystemCopyBuffer(path);
                };
                return false;
            }

            return false;
        }
    }
}
