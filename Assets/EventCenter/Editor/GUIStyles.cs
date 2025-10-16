using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FlyRabbit.EventCenter
{
    public static class GUIStyles
    {
        /// <summary>
        /// 有错误的foldout的外观
        /// </summary>
        public static GUIStyle ErrorFoldoutStyle;
        /// <summary>
        /// 没有错误的foldout的外观
        /// </summary>
        public static GUIStyle NormalFoldoutStyle;
        /// <summary>
        /// 有错误的Label的外观
        /// </summary>
        public static GUIStyle ErrorLabelStyle;
        /// <summary>
        /// 没有错误的Label的外观
        /// </summary>
        public static GUIStyle NormalLabelStyle;

        static GUIStyles()
        {
            ErrorFoldoutStyle = new GUIStyle(EditorStyles.foldout);
            ErrorFoldoutStyle.normal.textColor = Color.red;
            ErrorFoldoutStyle.onNormal.textColor = Color.red;
            ErrorFoldoutStyle.hover.textColor = Color.red;
            ErrorFoldoutStyle.onHover.textColor = Color.red;
            ErrorFoldoutStyle.active.textColor = Color.red;
            ErrorFoldoutStyle.onActive.textColor = Color.red;
            ErrorFoldoutStyle.focused.textColor = Color.red;
            ErrorFoldoutStyle.onFocused.textColor = Color.red;

            NormalFoldoutStyle = new GUIStyle(EditorStyles.foldout);
            NormalFoldoutStyle.normal.textColor = Color.white;
            NormalFoldoutStyle.onNormal.textColor = Color.white;
            NormalFoldoutStyle.hover.textColor = Color.white;
            NormalFoldoutStyle.onHover.textColor = Color.white;
            NormalFoldoutStyle.active.textColor = Color.white;
            NormalFoldoutStyle.onActive.textColor = Color.white;
            NormalFoldoutStyle.focused.textColor = Color.white;
            NormalFoldoutStyle.onFocused.textColor = Color.white;

            ErrorLabelStyle = new GUIStyle(EditorStyles.label);
            ErrorLabelStyle.normal.textColor = Color.red;
            ErrorLabelStyle.onNormal.textColor = Color.red;
            ErrorLabelStyle.hover.textColor = Color.red;
            ErrorLabelStyle.onHover.textColor = Color.red;
            ErrorLabelStyle.active.textColor = Color.red;
            ErrorLabelStyle.onActive.textColor = Color.red;
            ErrorLabelStyle.focused.textColor = Color.red;
            ErrorLabelStyle.onFocused.textColor = Color.red;

            NormalLabelStyle = new GUIStyle(EditorStyles.label);
        }
    }
}
