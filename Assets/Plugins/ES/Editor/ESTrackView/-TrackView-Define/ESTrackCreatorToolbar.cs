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
            {
                Private_AddButton(CreateButton,EditorIcons.Plus.Raw,30);
            
            }
        }
        private void Private_AddButton(Button button, Texture2D te, float width = 45, float height = 30)
        {
            if (te != null) button.style.backgroundImage = te;
            button.style.width = width;
            button.style.height = height;
            Add(button);
        }
    }
}
