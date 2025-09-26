
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
#if UNITY_EDITOR
#endif
using UnityEngine;

namespace ES
{
    /*核心功能*/
    public interface ICore
    {
    }
    [DefaultExecutionOrder(-2)]//顺序在前
    public abstract class Core : MonoBehaviour, ICore
    {
        #region 模块表
        [NonSerialized,ShowInInspector,TabGroup("常规","模块表",TextColor = "@ESDesignUtility.ColorSelector.Color_04"),ReadOnly,HideLabel,PropertyOrder(5),HideReferenceObjectPicker]
        public Dictionary<Type, IModule> ModuleTables = new Dictionary<Type, IModule>();
        public KeyType GetMoudle<KeyType>() where KeyType : class, IModule, new()
        {
            if (ModuleTables.TryGetValue(typeof(KeyType), out var module))
            {
                return module as KeyType;
            }
            var moduleNew = new KeyType();
            for (int i = 0; i < domains.Count; i++)
            {
                var d = domains[i];
                
                if (d != null&&d.GetType()==moduleNew.DomainType)
                {
                    d.TryAddModuleRunTimeWithoutTypeMatch(moduleNew);
                }
            }
            return moduleNew;
        }
        public T GetMoudle<ABKey,T>() where ABKey:class,IModule where T : class, IModule, new()
        {
            if (ModuleTables.TryGetValue(typeof(ABKey), out var module))
            {
                return module as T;
            }
            var moduleNew = new T();
            for (int i = 0; i < domains.Count; i++)
            {
                var d = domains[i];

                if (d != null && d.GetType() == moduleNew.DomainType)
                {
                    d.TryAddModuleRunTimeWithoutTypeMatch(moduleNew);
                }
            }
            return moduleNew;
        }
        #endregion

        #region 检查器专属

        //域颜色赋予
        public Color Editor_DomainTabColor(IDomain domain)
        {
            if (domain == null) return Color.gray*1.25f;
            else return Color.yellow;
        }



        //编辑器模式下的临时关联
#if UNITY_EDITOR
        [ContextMenu("<ES>创建临时关系")]
        public void CreateCacheRelationship()
        {
            var all = GetComponentsInChildren<IDomain>();
            foreach (var i in all)
            {
                i._Editor_RegisterAllButOnlyCreateRelationship(this);
            }
        }
#endif

        #endregion

        #region 补充信息

        //使用的域
        protected List<IDomain> domains = new List<IDomain>(3);



        #endregion

        #region Awake流程

        //Awake回调
        protected virtual void Awake()
        {
            _DoAwake();
        }
        public void _DoAwake()
        {
            OnBeforeAwakeRegister();
            OnAwakeRegisterOnly();
            OnAfterAwakeRegister();
        }

        //注册扩展Domain前发生前
        protected virtual void OnBeforeAwakeRegister()
        {

        }
        //仅用于手动注册
        protected virtual void OnAwakeRegisterOnly()
        {
            //RegisterDomain(xxx1);  //修改后的方案
        }
        //注册扩展Domain发生的事
        protected virtual void OnAfterAwakeRegister()
        {
            
        }

        #endregion

        #region 标准重写逻辑

        

        protected virtual void Update()
        {
            for(int i = 0; i < domains.Count; i++)
            {
                domains[i].TryUpdateSelf();
            }
        }

        protected virtual void OnEnable()
        {
            for (int i = 0; i < domains.Count; i++)
            {
                domains[i].TryEnableSelf();
            }
        }

        protected virtual void OnDisable()
        {
#if UNITY_EDITOR
           /* if (ESEditorRuntimePartMaster_OB.IsQuit) return;*/  //建议写一个退出游戏时忽略
#endif
            for (int i = 0; i < domains.Count; i++)
            {
                domains[i].TryDisableSelf();
            }
        }

        protected virtual void OnDestroy()
        {
#if UNITY_EDITOR
            /* if (ESEditorRuntimePartMaster_OB.IsQuit) return;*/
#endif
            for (int i = 0; i < domains.Count; i++)
            {
                domains[i].TryDestroySelf();
            }
        }

        #endregion

        #region 常用功能

        //手动注册
        public void RegisterDomain(IDomain domain)
        {
            if (domain != null)
            {
                domain._RegisterThisDomainToCore(this);
                domains.Add(domain);
            }
        }

        #endregion

        #region 自主扩展案例
        /*private void FixedUpdate()
        {
            for (int i = 0; i < domains.Count; i++)
            {
                domains[i].FixedUpdate();
            }
        }*/

        #endregion

    }





}
