
using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


namespace ES {
    // UI 通用 可输出操作  ---  on 作用于  from 影响源  with 选通，执行类型 等
    public interface IOperationUI : IOutputOperation_OBSOLUTE<ESUIElement,ESUIElement, ILink_UI_OperationOptions>
    {

    }
    #region 组分定义
    [Serializable/* 变对象执行 把本身的 on 和 from 替换为这个新的 元素 和 它的 from */]
    public abstract class ESUIElementGetter
    {
        public abstract ESUIElement Get(ESUIElement on, ESUIElement from);
    }
    #endregion

  
}
