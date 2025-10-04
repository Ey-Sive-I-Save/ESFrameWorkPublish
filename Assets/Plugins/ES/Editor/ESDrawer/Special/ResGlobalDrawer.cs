using ES;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    public class ESGlobalResSettingDrawer : OdinValueDrawer<ESGlobalResSetting>
    {
        private ESAreaSolver area = new ESAreaSolver();
        public static Color color = new Color(0.05f, 0.05f, 0.05f, 1);
        protected override void DrawPropertyLayout(GUIContent label)
        {
            area.UpdateAtFisrt();
            var rect = area.TargetArea;
            SirenixEditorGUI.DrawSolidRect(rect, color);
            this.CallNextDrawer(label);
            area.UpdateAtLast();
        }

    }
}

