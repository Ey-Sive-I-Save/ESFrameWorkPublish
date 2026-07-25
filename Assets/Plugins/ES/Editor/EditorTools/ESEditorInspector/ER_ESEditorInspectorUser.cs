using System.Collections;
using System.Collections.Generic;
using UnityEditor;
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
            if (singleton != null && !users.Exists(user => user != null && user.GetType() == singleton.GetType()))
                users.Add(singleton);
            users.Sort(comp);
        }

        private static void OnFinishedDefaultHeaderGUI(UnityEditor.Editor ed)
        {
            if (activeRegister == null || ed == null || ed.targets == null || ed.targets.Length != 1)
                return;

            var target = ed.targets[0];

            // Import Settings 与 Imported Object 会分别创建 Editor，并触发两次 Header 回调。
            // 有实际主资产时只在 Imported Object 上绘制，确保信息区只出现一次，且类型判断拿到真实资产。
            if (target is AssetImporter importer &&
                !string.IsNullOrEmpty(importer.assetPath) &&
                AssetDatabase.LoadMainAssetAtPath(importer.assetPath) != null)
            {
                return;
            }

            foreach (var user in activeRegister.users)
            {
                if (user.Apply(target))
                    break;
            }
        }
    }

}
