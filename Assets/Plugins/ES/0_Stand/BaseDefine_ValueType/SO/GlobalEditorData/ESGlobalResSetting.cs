using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace ES {
    [HideMonoScript]
    //为快速定位准备的匹配信息
    [CreateAssetMenu(fileName = "全局资源管理设置",menuName = "全局SO/全局资源管理设置")]
    public class ESGlobalResSetting : ESEditorGlobalSo<ESGlobalResSetting>
    {
        public string TEST = "测试用";
        public override void OnEditorInitialized()
        {
            base.OnEditorInitialized();
            this.SHOW_Global = () => { return Selection.activeObject == this; };
        }
    }
}
