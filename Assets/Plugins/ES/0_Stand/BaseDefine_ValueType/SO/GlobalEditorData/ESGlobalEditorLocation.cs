using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    //为快速定位准备的匹配信息
    [CreateAssetMenu(fileName = "全局资产定位",menuName = "资产定位")]
    public class ESGlobalEditorLocation : ESEditorGlobalSo<ESGlobalEditorLocation>
    {
        [LabelText("快速定位资产")]
        public Dictionary<string, UnityEngine.Object> Assets = new Dictionary<string, UnityEngine.Object>();


    }
}
