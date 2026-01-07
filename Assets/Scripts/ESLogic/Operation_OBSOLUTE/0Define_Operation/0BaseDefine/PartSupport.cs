using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {

        [Serializable, TypeRegistryItem("缓冲参数")]
        public class BufferDataFloat
        {
            [LabelText("缓冲曲线")] public AnimationCurve curve = AnimationCurve.Constant(0, 1, 1);
            [LabelText("乘数")] public float mutipler = 1;
            [LabelText("总时间")] public float needTime = 1;
        }
    
}
