using Sirenix.Utilities.Editor;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.Rendering.DebugUI.Table;

namespace ES
{
    public class ESTrackTimerToolbar : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<ESTrackTimerToolbar, UxmlTraits> { }
        public Button PreviewButton = new Button();
        public Button ReStartButton = new Button();
        public Button LastBlockButton = new Button();
        public Button NextBlockButton = new Button();
        public Button PlayButton = new Button();
        public TextField TimeLabel = new TextField();
        public DropdownField SelectOtherTimeLine = new DropdownField();

        public Label Name=new Label();

        public Button Setting = new Button();

        public ESTrackTimerToolbar()
        {
            this.style.flexDirection = FlexDirection.Row;
            this.style.color = Color.gray;
            Parivate_AddButton(PreviewButton, null, 60);
            Parivate_AddButton(ReStartButton, EditorIcons.Refresh.Raw);
            Parivate_AddButton(LastBlockButton, EditorIcons.ArrowLeft.Raw);
            Parivate_AddButton(PlayButton, EditorIcons.Play.Raw);
            Parivate_AddButton(NextBlockButton, EditorIcons.ArrowRight.Raw);


            PreviewButton.text = "预览";
            PreviewButton.style.color = Color.white;

            {
                TimeLabel.label = "";

                var input = TimeLabel.Q<VisualElement>("unity-text-input");
                TimeLabel.style.height = 30;
                TimeLabel.style.fontSize = 15;
                TimeLabel.style.color = Color.white;
                input.style.color = Color.white;
                
                input.AddToClassList("normalBlock");
                TimeLabel.value = "0:00";

                var ele = TimeLabel.Q<TextElement>();
                input.style.flexGrow = 1; input.style.flexShrink = 1;
                ele.style.width = Length.Percent(100); // 宽度100%
                ele.style.height = Length.Percent(100);
                input.style.paddingTop = 1;
                input.style.paddingLeft = 3;
                input.style.paddingBottom = 1;

                TimeLabel.isReadOnly = true;
                // textInputElement.style.height = Length.Percent(100); // 高度100%
                /*ele.style.width = input.style.width.value.value*0.85f;
                ele.style.flexShrink = input.style.height.value.value * 0.85f;
    */
                Add(TimeLabel);
            }

            {
                
                SelectOtherTimeLine.choices.Add("测试选择");
                SelectOtherTimeLine.choices.Add("测试选择2");
                Add(SelectOtherTimeLine);
            }

            {
                Name.text = "轴名";
                Name.style.height = 30;
                Name.style.minWidth = 100;
                Name.style.color = Color.white;
                Add(Name);
                Name.AddToClassList("normalBlock");
            }

            {
                Parivate_AddButton(Setting, EditorIcons.SettingsCog.Raw,30);
                Setting.style.position = Position.Absolute;
                Setting.style.right = 0;
                Setting.style.top = 0;
            }
            
        }
        private void Parivate_AddButton(Button button, Texture2D te, float width = 45, float height = 30)
        {
            if (te != null) button.style.backgroundImage = te;
            button.style.width = width;
            button.style.height = height;
            Add(button);
        }
    }
}
