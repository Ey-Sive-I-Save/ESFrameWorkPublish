#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public abstract class GgEditorWindow_MenuTree : GgEditorWindow_SplitView
    {
        #region Variables
        
        private Color32 buttonColor = new Color32(150, 150, 150, 255);
        private GgMenuTree menuTree = new GgMenuTree();
        
        #endregion

        //----------------------------------------------------------------------------------------------------
        
        #region Virtual Functions [Implement in child]

        /// <summary>
        /// Called from base class during <see cref="OnPageGUI"/>
        /// </summary>
        protected abstract GgMenuTree MenuTree();
        
        /*
        protected override MenuTree MenuTree()
        {
            return new MenuTree
            {
                header = "GgCore Samples",
                underlineColor = new Color32(223, 223, 223, 255),
                pages = new List<MenuTreePage>()
                {
                    new MenuTreePage(Page01Method, "Page 01 Name"),
                    new MenuTreePage(Page02Method, "Page 02 Name"),
                }
            };
        }
        */
        
        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Editor Loop

        protected override void OnPageGUI()
        {
            // calculate menu tree and pages
            int cacheSelection = menuTree.SelectionIndex;
            menuTree = MenuTree();
            menuTree.SelectionIndex = cacheSelection;
            
            base.OnPageGUI();
        }

        protected override void OnLeftPageGUI()
        {
            // calculate variables
            Color32 defaultColor = GUI.backgroundColor;
            float menuButtonHeight = 30;
            bool isScroll = pageHeight < (menuTree.pages?.Count * menuButtonHeight);
            float leftWindowWidth = isScroll ? resizeBarPosition - (singleLineHeight + standardSpacing) : resizeBarPosition - scrollBarOffset;
            
            if (!string.IsNullOrEmpty(menuTree.header))
            {
                // header
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(" " + menuTree.header, EditorStyles.boldLabel, GUILayout.Width(leftWindowWidth));
                EditorGUILayout.Space();

                float lineThickness = 1.0f;
                float windowPosition = 27.5f;
                Rect rect = new Rect(scrollBarOffset, windowPosition, leftWindowWidth - scrollBarOffset, lineThickness);
                EditorGUI.DrawRect(rect, menuTree.underlineColor);
            }
            else
            {
                // no header
                EditorGUILayout.Space();
            }

            if (0 < menuTree.pages?.Count)
            {
                // buttons
                GUI.backgroundColor = buttonColor;
                for (int i = 0; i < menuTree.pages.Count; i++)
                {
                    if (i == menuTree.SelectionIndex)
                    {
                        GUI.backgroundColor = InspectorExtensions.buttonSelectedColor;
                    }
                    if (GUILayout.Button(menuTree.pages[i].pageName, GUILayout.Height(menuButtonHeight), GUILayout.Width(leftWindowWidth)))
                    {
                        menuTree.SelectionIndex = i;
                    }
                    GUI.backgroundColor = buttonColor;
                }

                // reset background color
                GUI.backgroundColor = defaultColor;
            }
            EditorGUILayout.Space();
        }

        protected override void OnRightPageGUI()
        {
            menuTree.pages[menuTree.SelectionIndex].drawPageMethod();
        }

        #endregion

    } // class end
}
#endif