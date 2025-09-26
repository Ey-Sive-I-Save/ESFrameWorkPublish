using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    [CreateAssetMenu(fileName = "跳转到",menuName = "ESSO/跳转者")]
    public class AssetJumper : ESSO
    {
        [LabelText("跳转到")]
        public UnityEngine.Object Target;
    }
}
