using Sirenix.Utilities.Editor;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    public class ESTrackCreatorToolbar : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<ESTrackCreatorToolbar, UxmlTraits> { }

        public Button CreateButton = new Button();
        public Label CreateHintLabel = new Label();

        public ESTrackCreatorToolbar()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.paddingLeft = 8;
            style.backgroundColor = ESTrackViewTheme.SecondarySurface;
            Private_AddButton(CreateButton, EditorIcons.Plus.Raw, 26, 26, "添加轨道或片段");
            CreateHintLabel.text = "添加轨道 / 片段";
            CreateHintLabel.tooltip = "点击加号后，根据当前时间轴上下文选择轨道或片段类型。";
            CreateHintLabel.style.marginLeft = 6;
            CreateHintLabel.style.fontSize = 10;
            CreateHintLabel.style.color = ESTrackViewTheme.MutedText;
            CreateHintLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            Add(CreateHintLabel);
        }
        private void Private_AddButton(Button button, Texture2D te, float width = 45, float height = 30, string tooltip = null)
        {
            if (te != null) button.style.backgroundImage = te;
            if (!string.IsNullOrEmpty(tooltip)) button.tooltip = tooltip;
            button.style.width = width;
            button.style.height = height;
            button.style.borderTopLeftRadius = 3;
            button.style.borderTopRightRadius = 3;
            button.style.borderBottomLeftRadius = 3;
            button.style.borderBottomRightRadius = 3;
            ESTrackViewTheme.ApplyStandardButton(button);
            button.RegisterCallback<PointerEnterEvent>(_ =>
            {
                button.style.backgroundColor = ESTrackViewTheme.ButtonHoverBackground;
            });
            button.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                button.style.backgroundColor = ESTrackViewTheme.ButtonBackground;
            });
            Add(button);
        }

        internal void RefreshTheme()
        {
            style.backgroundColor = ESTrackViewTheme.SecondarySurface;
            CreateHintLabel.style.color = ESTrackViewTheme.MutedText;
            ESTrackViewTheme.ApplyStandardButton(CreateButton);
            MarkDirtyRepaint();
        }
    }
}
