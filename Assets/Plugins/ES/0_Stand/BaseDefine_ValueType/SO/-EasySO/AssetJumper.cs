using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    [CreateAssetMenu(fileName = "跳转到",menuName = "ESSO/跳转者")]
    [ESOnlyEditorSO("AssetJumper 只服务编辑器资产跳转定位，不应进入运行时构建或AB资源包。")]
    public class AssetJumper : ESSO
    {
        [LabelText("跳转到")]
        public UnityEngine.Object Target;
    }
}
