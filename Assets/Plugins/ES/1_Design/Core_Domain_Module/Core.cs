
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Reflection;
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
    public abstract class Core : MonoBehaviour, ICore, IPreviewCollector
    {
        #region 模块表
        [NonSerialized, HideInInspector]
        public Dictionary<Type, IModule> ModuleTables = new Dictionary<Type, IModule>();

        [ShowInInspector, TabGroup("常规", "模块表", TextColor = "@ESDesignUtility.ColorSelector.Color_04"), ReadOnly, HideLabel, PropertyOrder(5)]
        [ListDrawerSettings(IsReadOnly = true, DraggableItems = false, DefaultExpandedState = true, ShowFoldout = true)]
        private List<string> ModuleTableDebugView => BuildModuleTableDebugView();

        private List<string> BuildModuleTableDebugView()
        {
            var lines = new List<string>(ModuleTables.Count);
            foreach (var pair in ModuleTables)
            {
                string keyName = pair.Key != null ? pair.Key.Name : "<null>";
                string valueName = pair.Value != null ? pair.Value.GetType().Name : "<null>";
                lines.Add($"{keyName} -> {valueName}");
            }

            if (lines.Count == 0)
            {
                lines.Add("<空>");
            }

            return lines;
        }

        public KeyType GetMoudle<KeyType>() where KeyType : class, IModule, new()
        {
            if (ModuleTables.TryGetValue(typeof(KeyType), out var module))
            {
                return module as KeyType;
            }
            var moduleNew = new KeyType();
            bool registered = false;
            for (int i = 0; i < domains.Count; i++)
            {
                var d = domains[i];
                
                if (d != null&&d.GetType()==moduleNew.DomainType)
                {
                    d.TryAddModuleRuntimeWithoutTypeMatch(moduleNew);
                    registered = true;
                }
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!registered)
            {
                Debug.LogWarning($"[Core] Module {typeof(KeyType).Name} created but no matching domain was registered. DomainType: {moduleNew.DomainType?.Name}");
            }
#endif
            return moduleNew;
        }
        public T GetMoudle<ABKey,T>() where ABKey:class,IModule where T : class, IModule, new()
        {
            if (ModuleTables.TryGetValue(typeof(ABKey), out var module))
            {
                return module as T;
            }
            var moduleNew = new T();
            bool registered = false;
            for (int i = 0; i < domains.Count; i++)
            {
                var d = domains[i];

                if (d != null && d.GetType() == moduleNew.DomainType)
                {
                    d.TryAddModuleRuntimeWithoutTypeMatch(moduleNew);
                    registered = true;
                }
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!registered)
            {
                Debug.LogWarning($"[Core] Module {typeof(T).Name} created but no matching domain was registered. DomainType: {moduleNew.DomainType?.Name}");
            }
#endif
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
        public IReadOnlyList<IDomain> Domains => domains;

        public void CollectPreviewElements(
            List<IPreviewElement> normalProviders,
            List<IPreviewElement> singleProviders)
        {
#if UNITY_EDITOR
            EnsureEditorPreviewDomainRelationships(domains);
#endif
            CollectPreviewElements(domains, normalProviders, singleProviders);
#if UNITY_EDITOR
            CollectPreviewElementsFromSerializedDomainFields(normalProviders, singleProviders);
#endif

            if (ModuleTables == null) return;

            foreach (var module in ModuleTables.Values)
            {
                AddPreviewElement(module, normalProviders, singleProviders);
            }
        }

#if UNITY_EDITOR
        private void EnsureEditorPreviewDomainRelationships(IEnumerable<IDomain> domainList)
        {
            if (domainList == null)
                return;

            foreach (var domain in domainList)
                domain?._Editor_RegisterAllButOnlyCreateRelationship(this);
        }

        private void CollectPreviewElementsFromSerializedDomainFields(
            List<IPreviewElement> normalProviders,
            List<IPreviewElement> singleProviders)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = GetType();

            while (type != null && type != typeof(MonoBehaviour))
            {
                FieldInfo[] fields = type.GetFields(flags);
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (!typeof(IDomain).IsAssignableFrom(field.FieldType))
                        continue;

                    object value = field.GetValue(this);
                    if (value is IDomain domain)
                    {
                        domain._Editor_RegisterAllButOnlyCreateRelationship(this);
                        AddPreviewElement(domain, normalProviders, singleProviders);
                    }
                }

                type = type.BaseType;
            }
        }
#endif

        private static void CollectPreviewElements(
            IEnumerable<IDomain> domains,
            List<IPreviewElement> normalProviders,
            List<IPreviewElement> singleProviders)
        {
            if (domains == null) return;

            foreach (var domain in domains)
            {
                AddPreviewElement(domain, normalProviders, singleProviders);
            }
        }

        private static void AddPreviewElement(
            object obj,
            List<IPreviewElement> normalProviders,
            List<IPreviewElement> singleProviders)
        {
            if (obj is not IPreviewElement provider) return;
            if (!provider.CanPreview) return;

            if (provider.IsSingleArea)
                singleProviders.Add(provider);
            else
                normalProviders.Add(provider);
        }


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
                if (domains.Contains(domain))
                    return;

                domain._RegisterThisDomainToCore(this);
                if (domain.Core_Base != this)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[Core] Domain {domain.GetType().Name} can not register to core {GetType().Name}. Check Domain generic Core type.");
#endif
                    return;
                }
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
