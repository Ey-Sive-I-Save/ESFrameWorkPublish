using ES;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.OdinInspector.Editor.ValueResolvers;
using Sirenix.Utilities.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace ES {
    public class ESBackGroundAttributeDrawer : OdinAttributeDrawer<ESBackGroundAttribute>
    {
       
        ValueResolver<Color> Resolver;
        private Color color;
        
        private float a;

        private ESAreaSolver area = new ESAreaSolver();
        protected override void Initialize()
        {
            base.Initialize();
            Resolver = ValueResolverCreator.GetResolver<Color>(Property,this.Attribute.colorString);
            a = this.Attribute.WithAlpha;
        }
        
        protected override void DrawPropertyLayout(GUIContent label)
        {
            color = Resolver.GetValue();
            color.a = a;


            area.UpdateAtFisrt();
            SirenixEditorGUI.DrawBorders(area.TargetArea, (int)area.TargetArea.width, 0, (int)area.TargetArea.height + 2, 0, color);
            this.CallNextDrawer(label);
            area.UpdateAtLast();
        }

    }
}
