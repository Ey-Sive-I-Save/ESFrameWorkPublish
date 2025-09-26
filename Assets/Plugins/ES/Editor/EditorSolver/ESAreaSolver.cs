using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
namespace ES
{
    /*ES Area Solver
     转为绘制区域提取而生
     */
    public class ESAreaSolver 
    {
        private float lastJudgeHeight;
        public Rect TargetArea;


        private float STARTY1;
        public virtual void UpdateAtFisrt()
        {
            EditorGUILayout.Space(1, true);
            var spaceNEW = GUILayoutUtility.GetLastRect();
            STARTY1 = spaceNEW.yMax;
            if (spaceNEW.width > 2)
            {
                TargetArea = spaceNEW.x > 0 ? spaceNEW : TargetArea;
                
            }
            TargetArea.height = lastJudgeHeight;
        }
        public Rect GetAreaRect()
        {
            return TargetArea;
        }

        public virtual void UpdateAtLast()
        {
            float startY2 = GUILayoutUtility.GetLastRect().yMax;
            float offset = startY2 - STARTY1;
            if (offset > 0)
            {
                lastJudgeHeight = offset;
            }
        }

    }
}
