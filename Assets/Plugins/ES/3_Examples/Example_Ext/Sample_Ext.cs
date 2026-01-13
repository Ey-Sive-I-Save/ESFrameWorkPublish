using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    public class Sample_Ext : MonoBehaviour
    {
        public enum SampleEnum : short
        {
            A=-50, B=0, C=100, D=185
        }
        public int i;
        [Button]
        public void DebugHash()
        {
            Debug.Log(SampleEnum.A.GetHashCode());
            Debug.Log(SampleEnum.B.GetHashCode());
            Debug.Log(SampleEnum.C.GetHashCode());
            Debug.Log(SampleEnum.D.GetHashCode());
        }
    }
}
