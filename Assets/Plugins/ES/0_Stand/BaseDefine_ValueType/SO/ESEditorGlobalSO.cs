using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public interface IESGlobalData
    {
        
    }
    public class ESEditorGlobalSo<This> : ESSO,IESGlobalData where This : ESEditorGlobalSo<This>
    {
        #region 单例声明
        public static This Instance
        {
            get
            {
                //已经确定

                if (_instance != null)
                {
                    return _instance;
                };
                if (AllCaches.Count > 0)
                {
                    foreach (var i in AllCaches)
                    {
                        //先找确定的
                        if (i != null && i.HasConfirm) return _instance = i;
                    }
                    foreach (var i in AllCaches)
                    {
                        //只要能用就行
                        if (i != null) return _instance = i.ConfirmThisOnly();
                    }

                }
                bool hasReactive = HasReactiveTable.TryGetValue(typeof(This), out var bo);
                if (!hasReactive || !bo) //未定义或者未呈
                {
                    HasReactiveTable[typeof(This)] = true;
#if UNITY_EDITOR
                    if (EditorUtility.DisplayDialog
                        ("全局配置缺失，准备创建", "检测不到可用的【" + typeof(This) + "】SO数据,准备新建一个，是否继续", "创建"))
                    {
                        string path = ESStandUtility.SafeEditor.Wrap_OpenSelectorFolderPanel(title: "创建数据到：");
                        path = path._KeepAfterByLast("Asset", true);

                        if (AssetDatabase.IsValidFolder(path))
                        {
                            var first = ESStandUtility.SafeEditor.CreateSOAsset<This>(path, "全局数据" + typeof(This).Name);
                            first.TryConfirmSwitchThis();
                        }
                    }
#endif
                }
                return _instance;
            }
            set
            {
                _instance = value;
            }

        }
        [LabelText("被选中为主数据"), BoxGroup("showGlobal", LabelText = "关于全局数据", VisibleIf = "SHOW"), ReadOnly, PropertyOrder(-3)]
        public bool HasConfirm = false;//选定一个
        [ShowInInspector, BoxGroup("showGlobal"), LabelText("选中单例"), ReadOnly]
        private static This _instance;
        [ShowInInspector, BoxGroup("showGlobal"), LabelText("该类型全部数据"), ListDrawerSettings(HideAddButton = true, HideRemoveButton = true), InlineButton("TryConfirmSwitchThis", "选中为主数据")]
        private static HashSet<This> AllCaches = new HashSet<This>();
        private static Action<This> OnConfirmOneSO = (who) => { };



        private static Dictionary<Type, bool> HasReactiveTable = new Dictionary<Type, bool>();//敏感互动表(自动创建)
        [NonSerialized]
        public Func<bool> SHOW_Global;
        private bool SHOW()
        {
            return SHOW_Global?.Invoke() ?? true;
        }
        private void OnDestroy()
        {

            if (HasConfirm)
            {
                HasReactiveTable[typeof(This)] = false;
            }
        }
        public override void OnEditorInitialized()
        {
            base.OnEditorInitialized();
            if (this is This use)
            {

                if (HasConfirm) _instance = use;
                if (!AllCaches.Contains(use))
                {
                    AllCaches.Add(use);
                    OnConfirmOneSO += Delegate_OnConfirmOneSO;
                    HasReactiveTable[typeof(This)] = false;
                }
            }
        }
        public void RunTimeAwake()
        {
            if (_instance != null && _instance != this)
            {
                return;
            }
            if (_instance == null)
            {
                ConfirmThisOnly();
                _instance = this as This;
                // 可在此处添加其他初始化逻辑
            }
        }
        internal This ConfirmThisOnly()
        {
            HasConfirm = true;
            return this as This;
        }
        internal void TryConfirmSwitchThis()
        {
            if (this is This use)
            {

                HasConfirm = true;
                _instance = use;
                OnConfirmOneSO?.Invoke(use);
            }
        }
        private void Delegate_OnConfirmOneSO(This who)
        {
            if (who != this as This)
            {
                HasConfirm = false;
                ESStandUtility.SafeEditor.Wrap_SetDirty(this);
            }
        }
        #endregion
    }
}
