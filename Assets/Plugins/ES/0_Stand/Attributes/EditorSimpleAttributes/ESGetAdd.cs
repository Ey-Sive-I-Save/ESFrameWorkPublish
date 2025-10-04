using ES;
#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
#endif
using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

//纯编辑器显示 的 特性定义
namespace ES
{
    #region ESGetAdd

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property,AllowMultiple = false)]
    public class ESGetAdd : Attribute
    {
        public ESGetAddOption option;
        public ESGetAdd()
        {
            option = ESGetAddOption.SelfOnly;
        }
        public ESGetAdd(ESGetAddOption option_)
        {
            option = option_;
        }
    }
    public enum ESGetAddOption
    {
        [ESMessage("自己")] SelfOnly,
        [ESMessage("包含父级")] ContainsParent,
        [ESMessage("包含子级")] ContainsSon,
        [ESMessage("包含父和子")] ContainsParentAndSon
    }

    #endregion
}

