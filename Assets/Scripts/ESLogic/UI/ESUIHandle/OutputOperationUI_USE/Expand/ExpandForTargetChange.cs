using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

/* 
 * 这组专门用来写 有关作用对象更迭的 扩展模块
 
 
 */
namespace ES {
    [Serializable, TypeRegistryItem("0A扩展-转换作用源")]
    public class OutputOperationUI_Expand_SwitchOnFrom : IOperationUI
    {
        [SerializeReference, LabelText("作用在")]
        public ESUIElementGetter getter = new ESUIElementGetter_DirectHandRefer();

        [SerializeReference, LabelText("执行内容")]
        public IOperationUI op;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryOperation(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            var get = getter.Get(on, from);
            if (get != null)
            {
                op.TryOperation(get, from, with);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryCancel(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            var get = getter.Get(on, from);
            if (get != null)
            {
                op.TryCancel(get, from, with);
            }
        }

    }

}
