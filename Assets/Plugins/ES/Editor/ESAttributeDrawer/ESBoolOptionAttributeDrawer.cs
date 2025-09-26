using ES;
using Sirenix.OdinInspector.Editor.ValueResolvers;
using Sirenix.OdinInspector.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Sirenix.Utilities.Editor;

namespace ES
{
    #region ESBoolOption-ESbool两级描述
    public class ESBoolOptionDrawer : OdinAttributeDrawer<ESBoolOption, bool>
    {
        //使用解析器
        private ValueResolver<string> trueLabelResolver;
        private ValueResolver<string> falseLabelResolver;
        //初始化解析器
        protected override void Initialize()
        {
            // 解析动态标签文本（支持 $ 和 @ 表达式）
            falseLabelResolver = ValueResolver.GetForString(Property, Attribute.FalseLabel);
            trueLabelResolver = ValueResolver.GetForString(Property, Attribute.TrueLabel);
        }
        protected override void DrawPropertyLayout(GUIContent label)
        {
            EditorGUILayout.Space(5);
            string trueText = trueLabelResolver.GetValue() ?? Property.Name + ">是";
            string falseText = falseLabelResolver.GetValue() ?? Property.Name + ">否";
            EditorGUILayout.BeginHorizontal(GUILayout.Height(30));
            {
                // 放弃绘制标签

                //GUILayout.Space(leftSpace);
                // 绘制 "False" 按钮
                var rect = EditorGUILayout.GetControlRect();
                rect.height = 25;
                rect.y += 3;
                var rectLeft = new Rect(rect.x + 0.1f * rect.width, rect.y, rect.width * 0.4f, rect.height);
                var rectRight = new Rect(rect.x + 0.6f * rect.width, rect.y, rect.width * 0.4f, rect.height);
                var rectStart = new Rect(rect.x-5, rect.y, rect.width * 0.1f, rect.height);
                
                
                
                
                SirenixEditorGUI.DrawBorders(rect,5);
               // EditorGUI.DrawRect(rectStart, Color.yellow._WithAlpha(0.5f));

                ValueEntry.SmartValue = EditorGUI.Toggle(rectStart, ValueEntry.SmartValue);

                GUIHelper.PushColor(ValueEntry.SmartValue ? Color.gray : GUI.color);
                if (GUI.Button(rectLeft, falseText))
                {
                    ValueEntry.SmartValue = false; // 假按钮绘制
                }
                GUIHelper.PopColor();
                GUIHelper.PushColor(ValueEntry.SmartValue ? GUI.color : Color.gray);
                if (GUI.Button(rectRight, trueText))
                {
                    ValueEntry.SmartValue = true; // 真按钮绘制
                }
                GUIHelper.PopColor();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(1);




        }

    }
    #endregion
}
