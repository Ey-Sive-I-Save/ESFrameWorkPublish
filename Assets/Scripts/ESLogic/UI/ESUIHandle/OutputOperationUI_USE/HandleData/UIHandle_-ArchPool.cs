using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    [Serializable, TypeRegistryItem("0※UI操作-Context原型池", icon: SdfIconType.Vimeo)]
    public class UIHandle_ContextPool : IOperationUI
    {
        [SerializeReference, LabelText("使用的Context池", SdfIconType.Server)]
        public IUIContextPoolGetter ContextGetter;
        [SerializeReference, LabelText("参数键", SdfIconType.Keyboard)]
        public ContextKeyValue keyvalue=new ContextKeyValue(){ key="key"};
        [SerializeReference, LabelText("操作",SdfIconType.HandIndex)]
        public ContextOperation_Abstract Handler;

        public void TryOperation(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with) {
            if (Handler == null) return;
            var pool = ContextGetter?.Get(on,from);
            if (pool != null)
            {
                Handler.TryOperation(pool, keyvalue);
            }
        }

        public void TryCancel(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with) { }
    }
}
