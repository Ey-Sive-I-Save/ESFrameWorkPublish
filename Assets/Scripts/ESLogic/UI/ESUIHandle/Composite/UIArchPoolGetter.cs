using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    public interface IUIArchPoolGetter
    {
        public ArchPool Get(ESUIElement on, ESUIElement from);
    }

    [Serializable, TypeRegistryItem("UIArch_自己的父Panel的池")]
    public class UIArchPoolGetter_OnPanel : IUIArchPoolGetter
    {
        public ArchPool Get(ESUIElement on, ESUIElement from)
        {
            return on.MyPanel?.archPool ?? from.MyPanel.archPool;
        }
    }


    [Serializable, TypeRegistryItem("UIArch_作用来源的父Panel的池")]
    public class UIArchPoolGetter_FromPanel : IUIArchPoolGetter
    {
        public ArchPool Get(ESUIElement on, ESUIElement from)
        {
            return from.MyPanel.archPool??on.MyPanel?.archPool;
        }
    }

    [Serializable, TypeRegistryItem("UIArch_手动引用Panel的池")]
    public class UIArchPoolGetter_FromPanelRefer : IUIArchPoolGetter
    {
        [LabelText("手动引用Panel")]
        public ESUIPanelCore panel;
        public ArchPool Get(ESUIElement on, ESUIElement from)
        {
            return from.MyPanel.archPool ?? on.MyPanel?.archPool;
        }
    }
}
