using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    public class ESGraphView_Part_InspectorView : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<ESGraphView_Part_InspectorView, UxmlTraits> { }

        Editor editorForNode;
        Editor editorForContainer;
        public DropdownField dropdown = new DropdownField();
        public VisualElement buttonsView = new VisualElement();
        public VisualElement ForNodePanel;
        public VisualElement ForContainerPanel;
        public List<TabButton> buttons = new List<TabButton>();
        public List<VisualElement> elements = new List<VisualElement>();
        public ESGraphView_Part_InspectorView()
        {
            
            dropdown.choices.Add("");
            ForNodePanel = new VisualElement();
            ForContainerPanel = new VisualElement();


            AddTab("节点", ForNodePanel);
            AddTab("容器", ForContainerPanel);
            //buttonsView.style.backgroundColor = new StyleColor(Color.gray);
            buttonsView.style.flexDirection = FlexDirection.Row;
            this.style.flexDirection = FlexDirection.Column;

            this.Add(buttonsView);
            this.Add(ForNodePanel);
            this.Add(ForContainerPanel);
         

            SelectTab(0);
            

        }
        public void AddTab(string name,VisualElement element)
        {
            var button = new TabButton(name, element);
            int length = buttonsView.childCount;
            button.style.width = 50;
            button.style.height = 20;
            buttonsView.Add(button);
            buttons.Add(button);
            elements.Add(element);
            element.style.position = Position.Absolute;
            element.style.left = 0;
            element.style.top =20;
            button.RegisterCallback<ClickEvent>((ev) =>
            {
                SelectTab(length);
            });
        }
        public void SelectTab(int index)
        {
            for(int i = 0; i < buttons.Count; i++)
            {
                var b = buttons[i];
                if (b != null)
                {
                    b.style.color = new StyleColor() { value = index==i? Color.yellow : Color.gray };
                }
                var ele = elements[i];
                if (ele != null)
                {
                    ele.visible = index == i;
                }
            }
        }

        internal void SetContainerGlobal(INodeContainer container)
        {
            if (container is UnityEngine.Object uo)
            {
                ForContainerPanel.Clear();
                Debug.Log("显示Container的Inspector面板");
                UnityEngine.Object.DestroyImmediate(editorForContainer);


                editorForContainer = Editor.CreateEditor(uo);

                var contain = new IMGUIContainer(() =>
                 {
                     editorForContainer.OnInspectorGUI();
                 });

                ForContainerPanel.Add(contain);
            }

        }
        internal void UpdateSelectionNode(BaseNodeViewer nodeView)
        {
            if (ForNodePanel != null)
            {
                ForNodePanel.Clear();
            }
            Debug.Log("显示节点的Inspector面板");
            UnityEngine.Object.DestroyImmediate(editorForNode);

            var runner = nodeView.Runner;
            if (runner is UnityEngine.Object UOrunner)
            {
                editorForNode = Editor.CreateEditor(UOrunner);

                var node = new IMGUIContainer(() =>
                {
                    if (nodeView != null && nodeView.Runner != null)
                    {
                        editorForNode.OnInspectorGUI();
                    }
                });
                ForNodePanel.Add(node);
            }



        }
    }

}
