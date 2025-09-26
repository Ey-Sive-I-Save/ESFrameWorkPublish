using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;

namespace ES
{
    public abstract class ESTreeCollector
    {
        public abstract ESTreeCollectorName GlobalTreeName { get; }
        public List<ESItem> Items = new List<ESItem>();
        public List<ESToolBarItem> Tools = new List<ESToolBarItem>();
        public virtual void InitTree()
        {

        }
        public void AddToolBarItem<T>(EditorIcon icon,Action<T> action,bool nextline=false)
        {
            Tools.Add(new ESToolBarItem<T>()  { ICON=icon, actionFor = action , Type_= ESToolBarItemType.EditorIcon, NextLine=nextline});
        }
        public void AddToolBarItem<T>(SdfIconType sdf, Action<T> action, bool nextline = false)
        {
            Tools.Add(new ESToolBarItem<T>()  { SDF = sdf, actionFor = action, Type_ = ESToolBarItemType.SDFIcon, NextLine = nextline });
        }
        public void AddToolBarItem<T>(string label, Action<T> action, bool nextline = false)
        {
            Tools.Add(new ESToolBarItem<T>() { text = label, actionFor = action, Type_ = ESToolBarItemType.Text, NextLine = nextline });
        }
        public void AddToolBarItem<T>(Texture2D texture, Action<T> action, bool nextline = false)
        {
            Tools.Add(new ESToolBarItem<T>() { texture = texture, actionFor = action, Type_ = ESToolBarItemType.Texture, NextLine = nextline });
        }

        public void AddToolBarItem (EditorIcon icon, Action  action, bool nextline = false)
        {
            Tools.Add(new ESToolBarItem () { ICON = icon, action = action, Type_ = ESToolBarItemType.EditorIcon, NextLine = nextline });
        }
        public void AddToolBarItem (SdfIconType sdf, Action  action, bool nextline = false)
        {
            Tools.Add(new ESToolBarItem () { SDF = sdf, action = action, Type_ = ESToolBarItemType.SDFIcon, NextLine = nextline });
        }
        public void AddToolBarItem (string label, Action  action, bool nextline = false)
        {
            Tools.Add(new ESToolBarItem () { text = label, action = action, Type_ = ESToolBarItemType.Text, NextLine = nextline });
        }
        public void AddToolBarItem (Texture2D texture, Action  action, bool nextline = false)
        {
            Tools.Add(new ESToolBarItem () { texture = texture, action = action, Type_ = ESToolBarItemType.Texture, NextLine = nextline });
        }
    }

    public abstract class ESItem
    {
        public virtual int Order => 0;
        public abstract void Click();

        public virtual object Select() => this;
        public string ParentName;
        public string Name;
        public string GetName()
        {
            if (ParentName == null) return Name;
            else return ParentName +"/"+ Name;
        }

        public OdinMenuItem menuItem;
        public virtual void BuildSelfChildItem()
        {
            
        }
        public void AddChildItem(string name,Action<OdinMenuItem> actionClick)
        {
            var path = menuItem.GetFullPath() + "/" + name;
            menuItem.MenuTree.Add(path, null);
            var item = menuItem.MenuTree.GetMenuItem(path);
            if (actionClick != null)
            {
                item.OnDrawItem += (OdinMenuItem it) =>
                {
                    if (Event.current.type == EventType.MouseDown)
                    {
                        if (it.Rect.Contains(Event.current.mousePosition) && Event.current.button == 0)
                        {
                            actionClick.Invoke(it);
                        }
                    }
                };
            }
        }
    }


    public enum ESToolBarItemType
    {
        EditorIcon,SDFIcon,Text,Texture
    }
    public class ESToolBarItem
    {
        public ESToolBarItemType Type_;
        public EditorIcon ICON;
        public SdfIconType SDF;
        public string text;
        public Texture2D texture;
        public Action action;
        public bool NextLine = false;
        public void Draw(object for_)
        {

            if (Type_ == ESToolBarItemType.EditorIcon)
            {
                if (SirenixEditorGUI.ToolbarButton(ICON))
                {
                    Invoke(for_);
                }
            }
            else if (Type_ == ESToolBarItemType.SDFIcon)
            {
                if (SirenixEditorGUI.ToolbarButton(SDF))
                {
                    Invoke(for_);
                }
            }
            else if (Type_ == ESToolBarItemType.Text)
            {
                if (SirenixEditorGUI.ToolbarButton(text))
                {
                    Invoke(for_);
                }

            }
            else if (Type_ == ESToolBarItemType.Texture)
            {
                if (SirenixEditorGUI.ToolbarButton(new GUIContent(texture)))
                {
                    Invoke(for_);
                }

            }

        }
        public virtual void Invoke(object obj)
        {
            action?.Invoke();
        }
         
    }
    public class ESToolBarItem<For> : ESToolBarItem
    {
        public Action<For> actionFor;
        public override void Invoke(object obj)
        {
            if(obj is For _for)
            {
                actionFor?.Invoke(_for);
            }
        }
    
    }

    public class ESItemDefineAttribute : Attribute
    {
        public ESTreeCollectorName Tree;
        public string ParentName;
        public string Name;
        public ESItemDefineAttribute(ESTreeCollectorName tree, string name, string parent = null)
        {
            Tree = tree;
            Name = name;
            ParentName = parent;
        }
    }

    public class EditorRigister_ESTreeCollector : EditorRegister_FOR_Singleton<ESTreeCollector>
    {
        //第一个操作
        public override int Order => 1;

        public override void Handle(ESTreeCollector singleton)
        {
            singleton.InitTree();
            ESTreeMenuBuilder.Collectors.Add(singleton.GlobalTreeName, singleton);
        }
    }

    public class ER_ESItem : EditorRegister_FOR_Singleton<ESItem>
    {
        public override int Order => 2;

        public override void Handle(ESItem singleton)
        {
            var att = singleton.GetType().GetCustomAttribute<ESItemDefineAttribute>();
            if (att != null)
            {
                if (ESTreeMenuBuilder.Collectors.TryGetValue(att.Tree, out var collector))
                {
                    singleton.ParentName = att.ParentName;
                    singleton.Name = att.Name;
                    collector.Items.Add(singleton);
                }
            }
        }
    }
}


