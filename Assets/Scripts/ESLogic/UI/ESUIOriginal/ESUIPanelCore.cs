using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public class ESUIPanelCore : ESUIElement
    {
        [LabelText("原型参数池"),GUIColor("@ESStaticLogicUtility.ColorSelector.ColorForESValue")]
        public ArchPool archPool = new ArchPool();
        [LabelText("全部UI元素"),ReadOnly,ShowInInspector] 
        public Dictionary<string, ESUIElement> AllElements = new Dictionary<string, ESUIElement>();
#if UNITY_EDITOR
        [ValueDropdown("ElementKeys",AppendNextDrawer =true),LabelText("元素测试")]
        public string testElement = "测试";
#endif 
        public override ESUIPanelCore MyPanel { get { if (dirty) GetMyParentAndRegisteThis(); return _myParentPanel??this; } set { _myParentPanel = value; } }
        public ESUIRoot MyRoot { get { if (dirty) GetMyParentAndRegisteThis();if (_myRoot != null) return _myRoot; if (_myParentPanel != null) return _myParentPanel.MyRoot; return null; } set { _myRoot = value; } }
        [SerializeField, LabelText("所属根节点")] private ESUIRoot _myRoot;
        public List<string> ElementKeys()
        {
            List<string> strings = new List<string>(5);
            var es = transform.GetComponentsInChildren<ESUIElement>();
            foreach (var i in es)
            {
                
                if (i.RegisterToPanel&&i!=this)
                {
                    AddKeyReleThis(i,strings);
                }
            }
            return strings;
        }
        public void AddKeyReleThis(ESUIElement element, List<string> toAdd,string post="")
        {
            var parent = element._GetCompoentInParentExcludeSelf<ESUIPanelCore>();
            if (parent == this||parent==null) toAdd.Add(element.RegisterKey+post);
            else AddKeyReleThis(parent,toAdd,"/"+ element.RegisterKey + post); 
        }
        public string GetKeyReleThis(ESUIElement element,string post = "")
        {
            var parent = element._GetCompoentInParentExcludeSelf<ESUIPanelCore>();
            if (parent == this || parent == null) return element.RegisterKey+post;
            else return GetKeyReleThis(parent, "/" + element.RegisterKey + post);
        }

        protected override void OnBeforeAwakeRegister()
        {
            base.OnBeforeAwakeRegister();
            archPool.Init();
        }
        protected override void OnEnable()
        {
            base.OnEnable();
            archPool.Enable();
        }
        protected override void OnDisable()
        {
            base.OnDisable();
            archPool.Disable();
        }
        [Button("注册全部元素")]
        public void RegisterAllElements()
        {
            var es = transform._GetCompoentsInChildExcludeSelf<ESUIElement>();
            foreach (var i in es)
            {
                i.GetMyParentAndRegisteThis();
             
 
            }
        }
        public void _UnRegisterElement(ESUIElement i)
        {
            if (AllElements.TryGetValue(i.RegisterKey, out var e))
            {
                if (e == i)
                {
                    AllElements.Remove(i.RegisterKey);
                }
            }
            else
            {

            }
        }
        public void _RegisterElement(ESUIElement i)
        {
            if (AllElements.TryGetValue(i.RegisterKey, out var e))
            {

            }
            else
            {
                AllElements.Add(i.RegisterKey, i);
            }

        }


        public override ESUIPanelCore GetMyParentAndRegisteThis()
        {
            var root = this._GetCompoentInParentExcludeSelf<ESUIRoot>();
            if (root != null)
            {
                _myRoot = root;
                if (RegisterToPanel)
                {
                    _myRoot._RegisterPanel(this);
                }
            }
            return base.GetMyParentAndRegisteThis();
        }
    }
}

