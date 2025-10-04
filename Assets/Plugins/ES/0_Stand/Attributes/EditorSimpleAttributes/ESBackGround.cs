using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    #region 背景
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public class ESBackGroundAttribute : Attribute
    {
        public string colorString;
        public float WithAlpha = 1;
        public ESBackGroundAttribute()
        {
            colorString = "white";
            WithAlpha = 0.5f;
        }
        // 支持多种构造方式

        public ESBackGroundAttribute(string colorSTR)
        {
            colorString = colorSTR;
        }
        public ESBackGroundAttribute(string colorSTR, float withAlpha = 1)
        {
            colorString = colorSTR;
            this.WithAlpha = withAlpha;
        }
    }

    #endregion
}
