using ES;
using Sirenix.OdinInspector.Editor.ValueResolvers;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace ES {

   public class ESDragAttributeDrawer : OdinAttributeDrawer<ESDragAttribute,int>
    {

        public ESDragAtSolver drag = new ESDragAtSolver();
        public ESAreaSolver area = new ESAreaSolver();

        protected override void DrawPropertyLayout(GUIContent label)
        {
            area.UpdateAtFisrt();
           
            if (drag.Update(out var users,area.TargetArea))
            {
                this.ValueEntry.SmartValue = users[0].name.Length;
            }
            this.CallNextDrawer(label);
            area.UpdateAtLast();
        }

    }
}
