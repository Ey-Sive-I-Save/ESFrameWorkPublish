using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace ES
{


#if UNITY_EDITOR
    public class LevelAreaDraw : OdinValueDrawer<LevelArea>
    {

        public ESAreaSolver area=new ESAreaSolver();
        public ESDragAtSolver dragAT = new ESDragAtSolver();
        bool show;
        protected override void DrawPropertyLayout(GUIContent label)
        {
            area.UpdateAtFisrt();
            this.CallNextDrawer(label);
            show = EditorGUILayout.Toggle(show);
            area.UpdateAtLast();
            

            if (show)
            {
                var rectUser = area.TargetArea;

                rectUser.y += rectUser.height;
                Debug.Log(rectUser);
                rectUser.width = 400;
                rectUser.yMax =rectUser.yMin+ 400;

                //GUILayout.BeginArea(rectUser);
                EditorGUI.DrawRect(rectUser, Color.yellow._WithAlpha(0.25f));
                // GUILayout.EndArea();

                var level = this.ValueEntry.SmartValue;
                foreach(var i in level.prefabs)
                {
                    var posOFF = i.pos - level.posCenter;
                    var bl = posOFF / 400;
                    var center = rectUser.center+ posOFF;
                    var rectUSE = new Rect(center,new Vector2(10,10));
                    EditorGUI.DrawRect(rectUSE,Color.black);
                }
                var ev = Event.current;
                if(dragAT.Update(out var uses, rectUser, ev))
                {
                    var g = uses[0] as GameObject;
                    if (g != null)
                    {
                        var posOFF = ev.mousePosition - rectUser.center;
                        level.prefabs.Add(new PosAndPrefab() { pos=level.posCenter+posOFF, prefab=g });
                    }
                }

                EditorGUILayout.Space(400);
            }
        }
    }

#endif
}
