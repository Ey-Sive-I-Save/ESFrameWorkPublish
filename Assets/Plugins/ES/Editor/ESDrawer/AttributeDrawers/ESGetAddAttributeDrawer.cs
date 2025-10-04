using ES;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace ES {
    #region ESGetOrAdd 

    public class ESGetOrAddDrawer : OdinAttributeDrawer<ESGetAdd>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            EditorGUILayout.BeginHorizontal();
            this.CallNextDrawer(label);
            int i = 0;
            Component comPO = this.Property.FindParent((f) => {
                if (f == null||f.ValueEntry==null) return false;
                var yv = f.ValueEntry.TypeOfValue; i++;
                return yv.IsSubclassOf(typeof(Component));
            }, false)?.ValueEntry.WeakSmartValue as Component;
            if (this.Property.BaseValueEntry.TypeOfValue.IsSubclassOf(typeof(Component)))
            {
                if (((Component)this.Property.ValueEntry.WeakSmartValue) == null)
                {
                    if (comPO != null)
                    {
                        GUIHelper.PushColor(Color.yellow._WithAlpha(0.6f));
                        bool b = SirenixEditorGUI.SDFIconButton("",10,SdfIconType.ArrowRepeat);
                        bool b2 = SirenixEditorGUI.SDFIconButton("", 10, SdfIconType.Broadcast);
                        GUIHelper.PopColor();
                        if (b)
                        {
                            TryGetOrAdd(comPO, this.Attribute.option, this.Property.ValueEntry);
                        }
                        else if (b2)
                        {
                            GenericMenu menu = new GenericMenu();
                            menu.AddItem(new GUIContent(ESGetAddOption.SelfOnly._Get_ATT_ESStringMessage()), this.Attribute.option == ESGetAddOption.SelfOnly, () => { TryGetOrAdd(comPO, ESGetAddOption.SelfOnly, this.Property.ValueEntry); });
                            menu.AddItem(new GUIContent(ESGetAddOption.ContainsParent._Get_ATT_ESStringMessage()), this.Attribute.option == ESGetAddOption.ContainsParent, () => { TryGetOrAdd(comPO, ESGetAddOption.ContainsParent, this.Property.ValueEntry); });
                            menu.AddItem(new GUIContent(ESGetAddOption.ContainsSon._Get_ATT_ESStringMessage()), this.Attribute.option == ESGetAddOption.ContainsSon, () => { TryGetOrAdd(comPO, ESGetAddOption.ContainsSon, this.Property.ValueEntry); });
                            menu.AddItem(new GUIContent(ESGetAddOption.ContainsParentAndSon._Get_ATT_ESStringMessage()), this.Attribute.option == ESGetAddOption.ContainsParentAndSon, () => { TryGetOrAdd(comPO, ESGetAddOption.ContainsParentAndSon, this.Property.ValueEntry); });

                            menu.ShowAsContext();
                        }
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }
        public void TryGetOrAdd(Component go, ESGetAddOption option, IPropertyValueEntry entry)
        {
            Component cNow = null;
            if (option == ESGetAddOption.SelfOnly)
            {
                cNow = go.gameObject.GetComponent(entry.BaseValueType);
            }
            else if (option == ESGetAddOption.ContainsParent)
            {
                cNow = go.gameObject.GetComponentInParent(entry.BaseValueType);
            }
            else if (option == ESGetAddOption.ContainsSon)
            {
                cNow = go.gameObject.GetComponentInChildren(entry.BaseValueType);
            }
            else if (option == ESGetAddOption.ContainsParentAndSon)
            {
                cNow = go.gameObject.GetComponentInParent(entry.BaseValueType) ?? go.gameObject.GetComponentInChildren(entry.BaseValueType);
            }

            if (cNow != null)
            {
                entry.WeakSmartValue = cNow;
                EditorUtility.SetDirty(go);
            }
            else
            {
                cNow = go.gameObject.AddComponent(entry.BaseValueType);
                entry.WeakSmartValue = cNow;
                EditorUtility.SetDirty(go);
            }
        }
    }

    #endregion
}
