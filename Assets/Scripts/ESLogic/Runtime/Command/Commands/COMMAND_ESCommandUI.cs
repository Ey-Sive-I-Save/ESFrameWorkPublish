using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.UICanvasGroupState)]
    public sealed class ESCommand_UI_CanvasGroupState : ESCommand
    {
        [LabelText("\u76ee\u6807\u753b\u5e03\u7ec4")]
        public CanvasGroup target;

        [LabelText("\u53ef\u89c1")]
        public bool visible = true;

        [LabelText("\u53ef\u4ea4\u4e92")]
        public bool interactable = true;

        [LabelText("\u963b\u6321\u5c04\u7ebf")]
        public bool blocksRaycasts = true;

        public override string CommandName
        {
            get { return "\u8bbe\u7f6e\u753b\u5e03\u7ec4"; }
        }

        public override void Invoke()
        {
            if (target == null)
                return;

            target.alpha = visible ? 1f : 0f;
            target.interactable = interactable;
            target.blocksRaycasts = blocksRaycasts;
        }
    }
}
