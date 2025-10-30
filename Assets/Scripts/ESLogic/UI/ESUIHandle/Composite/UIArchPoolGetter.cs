using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    public interface IUIContextPoolGetter
    {
        public ContextPool Get(ESUIElement on, ESUIElement from);
    }

    [Serializable, TypeRegistryItem("UIContext_自己的父Panel的池")]
    public class UIContextPoolGetter_OnPanel : IUIContextPoolGetter
    {
        public ContextPool Get(ESUIElement on, ESUIElement from)
        {
            return on.MyPanel?.ContextPool ?? from.MyPanel.ContextPool;
        }
    }


    [Serializable, TypeRegistryItem("UIContext_作用来源的父Panel的池")]
    public class UIContextPoolGetter_FromPanel : IUIContextPoolGetter
    {
        public ContextPool Get(ESUIElement on, ESUIElement from)
        {
            return from.MyPanel.ContextPool??on.MyPanel?.ContextPool;
        }
    }

    [Serializable, TypeRegistryItem("UIContext_手动引用Panel的池")]
    public class UIContextPoolGetter_FromPanelRefer : IUIContextPoolGetter
    {
        [LabelText("手动引用Panel")]
        public ESUIPanelCore panel;
        public ContextPool Get(ESUIElement on, ESUIElement from)
        {
            return from.MyPanel.ContextPool ?? on.MyPanel?.ContextPool;
        }
    }
}
