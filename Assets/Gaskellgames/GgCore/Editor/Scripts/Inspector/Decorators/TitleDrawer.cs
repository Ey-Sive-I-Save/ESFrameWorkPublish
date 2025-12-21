#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com: https://github.com/Gaskellgames
    /// </summary>
    
    [CustomPropertyDrawer(typeof(TitleAttribute), true)]
    public class TitleDecorator : DecoratorDrawer
    {
        #region Variables

        private TitleAttribute attributeAsType;
        
        private GUIStyle titleStyle;
        private GUIStyle subTitleStyle;
        private int singleLine = (int)EditorGUIUtility.singleLineHeight; // ~18 pixels
        private int gap = (int)EditorGUIUtility.standardVerticalSpacing;
        private int titleHeight = 12;
        private int subTitleHeight = 10;
        private int lineHeight = 1;

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Height

        public override float GetHeight()
        {
            // cache default values
            attributeAsType = attribute as TitleAttribute;
            if (attributeAsType == null) { return singleLine; }
            
            SetupStyles(attributeAsType.alignment);
            
            int title = !string.IsNullOrWhiteSpace(attributeAsType.heading) ? titleHeight + gap : 0;
            int subTitle = !string.IsNullOrWhiteSpace(attributeAsType.subHeading) ? subTitleHeight + gap : 0;
            
            return singleLine + title + subTitle;
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region OnGUI

        public override void OnGUI(Rect position)
        {
            // cache default values
            float yPosition = position.yMin + (singleLine * 0.5f);
            
            // draw title
            if (!string.IsNullOrWhiteSpace(attributeAsType.heading))
            {
                Rect titleRect = new Rect(position.xMin, yPosition, position.width, titleHeight);
                EditorGUI.LabelField(titleRect, attributeAsType.heading, titleStyle);
                yPosition += titleHeight;
                yPosition += gap + gap;
            }
            
            // draw subtitle
            if (!string.IsNullOrWhiteSpace(attributeAsType.subHeading))
            {
                Rect subTitleRect = new Rect(position.xMin, yPosition, position.width, subTitleHeight);
                EditorGUI.LabelField(subTitleRect, attributeAsType.subHeading, subTitleStyle);
                yPosition += subTitleHeight;
                yPosition += gap;
            }

            // draw line
            Rect lineRect = new Rect(position.xMin, yPosition, position.width, lineHeight);
            EditorGUI.DrawRect(lineRect, attributeAsType.darkColour);
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Private Functions

        private void SetupStyles(Alignment alignment)
        {
            // title
            titleStyle = new GUIStyle();
            titleStyle.fontSize = titleHeight;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = attributeAsType.colour;
            
            // subtitle
            subTitleStyle = new GUIStyle();
            subTitleStyle.fontSize = subTitleHeight;
            subTitleStyle.fontStyle = FontStyle.Normal;
            subTitleStyle.normal.textColor = attributeAsType.darkColour;
            
            // alignment
            switch (alignment)
            {
                case Alignment.Left:
                    titleStyle.alignment = TextAnchor.MiddleLeft;
                    subTitleStyle.alignment = TextAnchor.MiddleLeft;
                    break;
                case Alignment.Center:
                    titleStyle.alignment = TextAnchor.MiddleCenter;
                    subTitleStyle.alignment = TextAnchor.MiddleCenter;
                    break;
                case Alignment.Right:
                    titleStyle.alignment = TextAnchor.MiddleRight;
                    subTitleStyle.alignment = TextAnchor.MiddleRight;
                    break;
            }
        }

        #endregion

    } // class end
}

#endif