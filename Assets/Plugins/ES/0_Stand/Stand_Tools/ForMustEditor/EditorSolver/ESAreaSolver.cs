using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
namespace ES
{
    /*ES Area Solver
     转为绘制区域提取而生
     */
    public class ESAreaSolver : ESEditorSolver
    {
        private float lastJudgeHeight;
        public Rect TargetArea;
        public bool DrawBackground;
        public bool DrawBorder;
        public Color BackgroundColor = new Color(0.18f, 0.48f, 0.75f, 0.08f);
        public Color BorderColor = new Color(0.18f, 0.48f, 0.75f, 0.85f);
        public float BorderWidth = 1f;


        private float STARTY1;

        public ESAreaSolver InitSolver(
            bool drawBackground = false,
            bool drawBorder = false,
            Color? backgroundColor = null,
            Color? borderColor = null,
            float borderWidth = 1f)
        {
            DrawBackground = drawBackground;
            DrawBorder = drawBorder;
            if (backgroundColor.HasValue) BackgroundColor = backgroundColor.Value;
            if (borderColor.HasValue) BorderColor = borderColor.Value;
            BorderWidth = Mathf.Max(0f, borderWidth);
            return CompleteInitSolver<ESAreaSolver>();
        }

        public void DrawBackgroundNow()
        {
#if UNITY_EDITOR
            DrawBackgroundNow(BackgroundColor);
#endif
        }

        public void DrawBackgroundNow(Color backgroundColor)
        {
#if UNITY_EDITOR
            var rect = GetAreaRect();
            if (rect.width <= 2 || rect.height <= 2)
                return;

            EditorGUI.DrawRect(rect, backgroundColor);
#endif
        }

        public void DrawBorderNow()
        {
#if UNITY_EDITOR
            DrawBorderNow(BorderColor, BorderWidth);
#endif
        }

        public void DrawBorderNow(Color borderColor, float borderWidth = 1f)
        {
#if UNITY_EDITOR
            var rect = GetAreaRect();
            if (rect.width <= 2 || rect.height <= 2 || borderWidth <= 0)
                return;

            DrawRectOutline(rect, borderColor, borderWidth);
#endif
        }

        public void DrawAreaVisualNow()
        {
#if UNITY_EDITOR
            if (DrawBackground)
                DrawBackgroundNow();
            if (DrawBorder)
                DrawBorderNow();
#endif
        }

        public void DrawAreaVisualNow(Color? backgroundColor, Color? borderColor, float borderWidth = 1f)
        {
#if UNITY_EDITOR
            if (backgroundColor.HasValue)
                DrawBackgroundNow(backgroundColor.Value);
            if (borderColor.HasValue)
                DrawBorderNow(borderColor.Value, borderWidth);
#endif
        }

        public virtual void UpdateAtFisrt()
        {
#if UNITY_EDITOR
            EditorGUILayout.Space(1, true);
            var spaceNEW = GUILayoutUtility.GetLastRect();
            STARTY1 = spaceNEW.yMax;
            if (spaceNEW.width > 2)
            {
                TargetArea = spaceNEW;
                
            }
            TargetArea.height = lastJudgeHeight;
#endif
        }
        public Rect GetAreaRect()
        {
            return TargetArea;
        }

        public virtual void UpdateAtLast()
        {
#if UNITY_EDITOR
            float startY2 = GUILayoutUtility.GetLastRect().yMax;
            float offset = startY2 - STARTY1;
            if (offset > 0)
            {
                lastJudgeHeight = offset;
                TargetArea.height = lastJudgeHeight;
            }
            DrawAreaVisualNow();
#endif
        }

#if UNITY_EDITOR
        private static void DrawRectOutline(Rect rect, Color color, float width)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }
#endif

    }
}
