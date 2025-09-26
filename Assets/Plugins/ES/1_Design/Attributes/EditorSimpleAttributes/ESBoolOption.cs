using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    #region bool两级描述

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class ESBoolOption : Attribute
    {
        public string FalseLabel;
        public string TrueLabel;

        public ESBoolOption(string forTrue, string forFalse)
        {
            this.FalseLabel = forFalse;
            this.TrueLabel = forTrue;
        }
    }
    #endregion
}
