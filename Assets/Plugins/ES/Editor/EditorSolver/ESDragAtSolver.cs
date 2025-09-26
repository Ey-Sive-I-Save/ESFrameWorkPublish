using ES;
using Sirenix.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace ES
{
    /// <summary>
    /// DragAtSolver 专门为拖动资源并放置 提供了便捷的工具
    /// </summary>
    public class ESDragAtSolver
    {
        private float defaultHeight = 40;
        private bool isDraging = false;
        public static Color canReceiveColor = Color.black._WithAlpha(0.25f);
        public static Color isReceivingColor = Color.black._WithAlpha(0.8f);
        public static Color normalColor = Color.yellow._WithAlpha(0.1f);
        public virtual void SetHeight(int defaultHeight=40)
        {
            this.defaultHeight = defaultHeight;
        }
        public virtual bool Update(out UnityEngine.Object[] users, Rect? defaultArea = null, Event ev = null)
        {
            //刷新绘制区域
            EditorGUILayout.Space(0);
            //获取所在原始空间
            Rect orSpace = GUILayoutUtility.GetLastRect();

            if (defaultArea != null)
            {
                orSpace = defaultArea.Value;
            }
            
            Rect area = (defaultArea!=null&&defaultArea.Value.height>2)?defaultArea.Value : orSpace.SetYMax(orSpace.yMin + defaultHeight);
            users = DragAndDrop.objectReferences;

            ev ??= Event.current;
            if (users.Length > 0)
            {
                if (ev.type == EventType.DragExited || ev.type == EventType.MouseUp)
                {
                    isDraging = false;
                }
                if (!isDraging && ev.type == EventType.DragUpdated)
                {
                    isDraging = true;
                }
                if (isDraging)
                {
                    if (area.Contains(ev.mousePosition))
                    {
                        EditorGUI.DrawRect(area, isReceivingColor);
                    }
                    else
                    {
                        EditorGUI.DrawRect(area, canReceiveColor);
                    }
                }
                if (ev.type == EventType.DragUpdated || ev.type == EventType.DragPerform)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;

                    if (ev.type == EventType.DragPerform && area.Contains(ev.mousePosition))
                    {
                        DragAndDrop.AcceptDrag();
                        users = DragAndDrop.objectReferences;
                        return true;
                    }
                }
            }
            else
            {
                isDraging = false;
                EditorGUI.DrawRect(area, normalColor);
            }
            users = null;
            return false;
          
        }
    }
}
/*
            private float Height = 40;
        private bool drag = false;
        bool refresh = true;
        private ESAssetRefer target;
        private ResSourceSearchKey key;
        protected override void DrawPropertyLayout(GUIContent label)
        {
            EditorGUILayout.Space(0);
            var space = GUILayoutUtility.GetLastRect();
            var startY1 = space.yMax;
            target = this.ValueEntry.SmartValue ??= new ESAssetRefer();
            key = target.key ??= new ResSourceSearchKey();

            // this.CallNextDrawer(label); 
            Rect rect = space.SetYMax(space.yMin + Height);

            this.Property.FindChild((n) => n.Name == "key", false)?.Draw(new GUIContent("a"));
            this.Property.FindChild((n) => n.Name == "vAsset", false)?.Draw(new GUIContent("b"));


            var cc = Event.current;
            if (cc.type == EventType.DragExited || cc.type == EventType.MouseUp)
            {
                drag = false;
            }
            if (!drag && cc.type == EventType.DragUpdated)
            {
                drag = true;
            }
            if (drag)
            {
                if (rect.Contains(cc.mousePosition))
                {
                    EditorGUI.DrawRect(rect, Color.black._WithAlpha(0.8f));
                }
                else
                {
                    EditorGUI.DrawRect(rect, Color.black._WithAlpha(0.25f));
                }
            }
            else
            {
                EditorGUI.DrawRect(rect, Color.yellow._WithAlpha(0.10f));
            }
            if (cc.type == EventType.DragUpdated || cc.type == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;

                if (cc.type == EventType.DragPerform && rect.Contains(cc.mousePosition))
                {
                    DragAndDrop.AcceptDrag();
                    var use = DragAndDrop.objectReferences[0];


                    string prePath = AssetDatabase.GetAssetPath(use);
                    if (prePath == null) return;
                    string nowPath = prePath;
                    bool start = true;
                    while (nowPath != prePath || start)
                    {
                        start = false;
                        if (nowPath != null && !nowPath.IsNullOrWhitespace())
                        {
                            var ai = AssetImporter.GetAtPath(nowPath);
                            if (ai == null || ai.assetBundleName == null || ai.assetBundleName.IsNullOrWhitespace())
                            {
                                prePath = nowPath.ToString();
                                nowPath = nowPath._KeepBeforeByLast("/");
                            }
                            else
                            {
                                var ab = ai.assetBundleName;

                                key.AssetPath = use.name;
                                key.OwnerAssetBundle = ab;
                                target.EditorOnly_SetVAsset(use);
                                // Property.ValueEntry.va = " AB名: " + ab + " ，资源名 ： " + use.name;
                                break;
                            }

                        }
                    }
                    cc.Use();


                }

            }
            if (cc.type == EventType.KeyDown)
            {
                if (cc.keyCode == KeyCode.Space) refresh = true;
            }
            if (refresh)
            {
                refresh = false;
                var u = target.EditorOnly_GetVAsset();
                if (u != null)
                {
                    ApplyObject(u);
                }
            }
        }

        private void ApplyObject(UnityEngine.Object use)
        {
            if (use != null)
            {
                string prePath = AssetDatabase.GetAssetPath(use);
                if (prePath == null) return;
                string nowPath = prePath;
                bool start = true;
                while (nowPath != prePath || start)
                {
                    start = false;
                    if (nowPath != null && !nowPath.IsNullOrWhitespace())
                    {
                        var ai = AssetImporter.GetAtPath(nowPath);
                        if (ai == null || ai.assetBundleName == null || ai.assetBundleName.IsNullOrWhitespace())
                        {
                            prePath = nowPath.ToString();
                            nowPath = nowPath._KeepBeforeByLast("/");
                        }
                        else
                        {
                            var ab = ai.assetBundleName;

                            key.AssetPath = use.name;
                            key.OwnerAssetBundle = ab;
                            target.EditorOnly_SetVAsset(use);
                            // Property.ValueEntry.va = " AB名: " + ab + " ，资源名 ： " + use.name;
                            break;
                        }

                    }
                }
            }
        }
           */
