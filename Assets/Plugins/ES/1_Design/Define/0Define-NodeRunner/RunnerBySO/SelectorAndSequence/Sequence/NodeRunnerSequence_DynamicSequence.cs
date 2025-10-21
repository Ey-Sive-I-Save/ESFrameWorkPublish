using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace ES {
    [CreateNodeRunnerSoMenu(NodeEnvironment.None, "队列", "动态数目队列")]
    public class NodeRunnerSelector_DynamicSequence : NodeSequnence_ConfirmNodes
    {
        [OnValueChanged("PortChange"),PropertyRange(1,10)]
        public int NUM = 2;
        public void PortChange()
        {
            
            Editor_RefreshNode(5,NUM);
        }
        public override bool EnableDrawIMGUI => true;
        public override void DrawIMGUI()
        {
#if UNITY_EDITOR
            int a = NUM;
            NUM = EditorGUILayout.IntSlider("动态序列数目",NUM,1,10);
            if (a != NUM)
            {
                PortChange();
                EditorUtility.SetDirty(this);
            }
            base.DrawIMGUI();
#endif
        }
        public override int PortNum => NUM;
        public override string GetOptionName()
        {
            return $"动态数目<{NUM}>队列";
        }
    }
}
