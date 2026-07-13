using ES;
using Sirenix.Utilities.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES {
    public class ESTrackCreatorToolbar : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<ESTrackCreatorToolbar, UxmlTraits> { }

        public Button CreateButton = new Button();

        public ESTrackCreatorToolbar()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.paddingLeft = 8;
            Private_AddButton(CreateButton, EditorIcons.Plus.Raw, 26, 26, "添加轨道或片段");
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
            button.style.backgroundColor = new Color(0.09f, 0.105f, 0.122f, 1f);
            button.RegisterCallback<PointerEnterEvent>(_ =>
            {
                button.style.backgroundColor = new Color(0.13f, 0.16f, 0.19f, 1f);
            });
            button.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                button.style.backgroundColor = new Color(0.09f, 0.105f, 0.122f, 1f);
            });
            Add(button);
        }
    }
}
