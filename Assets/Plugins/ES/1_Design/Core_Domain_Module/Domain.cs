using Sirenix.OdinInspector;
using Sirenix.Serialization;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;


namespace ES
{
    public interface IDomain : IESHosting
    {
        Core Core_Base { get; }
        //编辑器情况下的链接创建
        void _Editor_RegisterAllButOnlyCreateRelationship(ICore core_);
        void _RegisterThisDomainToCore(ICore core);
        void TryRemoveNullModules(bool rightnow = false);
        void TryAddModuleRunTimeWithoutTypeMatch(IModule module);
        void TryRemoveModuleRunTimeWithoutTypeMatch(IModule Module);
        void FixedUpdateExpand();
    }
    public interface IDomain<Core_> : IDomain
    {
        //给Module抽象定义用
        Core_ MyCore { get; }

    }
    [Serializable]
    public abstract class Domain<Core_, Module_> : IESHosting<Module_>, IDomain<Core_> where Core_ : Core where Module_ : class, IModule, IESModule
    {
        #region 总重要信息

        [HideInInspector]
        public Core_ myCore;

        public Core Core_Base => myCore;

        public Core_ MyCore { get => myCore; }

        [BoxGroup("扩展模块集"), OdinSerialize, HideLabel]
        public SafeNormalList<Module_> MyModules = new SafeNormalList<Module_>();

        #endregion


        #region 只读便捷属性

        //模块的IEnumable
        [HideInInspector]
        public IEnumerable<Module_> ModulesIEnumable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return MyModules.ValuesNow; }
        }
        #endregion

        #region 补充逻辑_一般编辑器或者底层使用

        //单纯绑引用（编辑器模式有用）
        public void _Editor_RegisterAllButOnlyCreateRelationship(ICore core_)
        {
            if (core_ is Core_ use)
            {
                this.myCore = use;
                foreach (var i in ModulesIEnumable)
                {
                    i._SetDomainCreateRelationshipOnly(this);
                }
            }
        }

        //设置显示绑定核心的颜色
        protected virtual Color _Editor_CoreColorGetter()
        {
            return Color.green;
        }
        public void TryRemoveModuleFromListOnly(IESModule module)
        {
            if (module is Module_ m)
            {
                MyModules.Remove(m);
            }
        }
        public void TryAddModuleToListOnly(IESModule module)
        {
            if (module is Module_ m)
            {
                MyModules.Add(m);
            }
        }

        //尝试移除全部的空模块(一般在编辑器使用)--(立刻清理还是留到)
        public void TryRemoveNullModules(bool rightNow = false)
        {

            foreach (var i in MyModules.ValuesNow)
            {
                if (i == null)
                {
                    MyModules.Remove(i);
                }
            }
            if (rightNow) MyModules.ApplyBuffers();
        }

        #endregion

        #region 初始化构建_不用改

        //注册到核心
        public void _RegisterThisDomainToCore(ICore core)
        {
            if (core is Core_ use)
            {
                this.myCore = use;
                _AwakeRegisterAllModules();
            }
        }

        //注册默认模块(一般不用改
        public virtual void _AwakeRegisterAllModules()
        {
            foreach (var i in ModulesIEnumable)
            {
                i._TryRegisterToHost(this);
            }
        }

        #endregion

        #region 控制子模块(非必要不重写)

        //更新子模块
        public virtual void UpdateAsHosting()
        {
            MyModules.ApplyBuffers();
            int count = MyModules.ValuesNow.Count;
            for (int i = 0; i < count; i++)
            {
                var use = MyModules.ValuesNow[i];
                if (use.Signal_Dirty && TestModuleStateBefoUpdate(use) == ESTryResult.Fail) { }
                else use.TryUpdateSelf();
            }

        }

        //启用子模块
        public virtual void EnableAsHosting()
        {
            int count = MyModules.ValuesNow.Count;
            for (int i = 0; i < count; i++)
            {
                var use = MyModules.ValuesNow[i];
                use._TryTestActiveAndEnable();
            }
        }

        //禁用子模块
        public virtual void DisableAsHosting()
        {
            int count = MyModules.ValuesNow.Count;
            for (int i = 0; i < count; i++)
            {
                var use = MyModules.ValuesNow[i];
                use._TryTestInActiveAndDisable();
            }
        }

        public virtual void DestroyAsHosting()
        {
            int count = MyModules.ValuesNow.Count;
            for (int i = 0; i < count; i++)
            {
                var use = MyModules.ValuesNow[i];
                use.TryDestroySelf();
            }
        }
        #endregion

        #region 生命周期(一般不需要调用)

        //正在活动的标识
        public bool Signal_IsActiveAndEnable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => myCore.enabled;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { myCore.enabled = value; }
        }

        //尝试启用
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryEnableSelf()
        {
            OnEnable();
        }

        //尝试禁用
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryDisableSelf()
        {
            OnDisable();
        }

        //尝试更新
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryUpdateSelf()
        {
            if (CanUpdating)
            {
                Update();
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryDestroySelf()
        {
            OnDestroy();
            int count = MyModules.ValuesNow.Count;
            for (int i = 0; i < count; i++)
            {
                var use = MyModules.ValuesNow[i];
                use.TryDestroySelf();
            }
        }
        #endregion

        #region 重写逻辑

        //是否可Update更新
        public virtual bool CanUpdating => true;




        //Start时
        protected virtual void Start() { }

        //Update时
        protected virtual void Update()
        {
            UpdateAsHosting();
        }

        //启用时
        protected virtual void OnEnable()
        {
            EnableAsHosting();
        }

        //禁用时
        protected virtual void OnDisable()
        {
#if UNITY_EDITOR
            /*if (ESEditorRuntimePartMaster_OB.IsQuit) return;*/ //建议退出程序时忽略掉这个
#endif
            DisableAsHosting();
        }

        protected virtual void OnDestroy()
        {
            foreach (var i in MyModules.ValuesNow)
            {
                i.TryDestroySelf();//销毁
            }
        }
        #endregion

        #region 手动补充逻辑

        //为了节约性能，采用委托
        public Action OnFixedUpdate = () => { };

        public virtual void FixedUpdateExpand()
        {
            OnFixedUpdate?.Invoke();
        }
        #endregion

        #region 常用功能

        //添加模块

        public void TryAddModuleRunTimeWithoutTypeMatch(IModule Module)
        {
            if (Module is Module_ use)
            {
                TryAddModuleRunTime(use);
            }
        }
        [Button("添加实时模块"),HideInEditorMode]
        public void TryAddModuleRunTime(Module_ use)
        {
            if (use._TryRegisterToHost(this) == ESTryResult.Succeed)
            {
                MyModules.Add(use);
                use.Signal_HasSubmit = true;
                use.EnabledSelf = true;
            };
        }
        //移除模块
        public void TryRemoveModuleRunTimeWithoutTypeMatch(IModule Module)
        {
            if (Module is Module_ use)
            {
                use.Signal_HasSubmit = false;//自己包含移除功能
                use.EnabledSelf = false;
            }
        }
        public T FindMyModule<T>()
        {
            foreach (var i in ModulesIEnumable)
            {
                if (i is T t) return t;
            }
            return default;
        }

        public void _TryAddToListOnly(IESModule module)
        {
            if (module is Module_ use && !MyModules.ValuesNow.Contains(use))
            {
                MyModules.Add(use);
            }
        }
        public void _TryRemoveFromListOnly(IESModule module)
        {
            if (module is Module_ use && MyModules.ValuesNow.Contains(use))
            {
                MyModules.Remove(use);
            }
        }

        public ESTryResult TestModuleStateBefoUpdate(Module_ module)
        {
            if (module.Signal_HasSubmit)
            {

                if (!module.HasStart)
                {
                    module.HasStart = true;
                    module.Start();
                }
            }
            else
            {
                module._TryTestInActiveAndDisable();
                _TryRemoveFromListOnly(module);
                if (!module.HasDestroy)
                {
                    module.HasDestroy = true;
                    module.OnDestroy();
                }
                return ESTryResult.Fail;
            }
            module.Signal_Dirty = false;
            return ESTryResult.Succeed;
        }
    }


    #endregion

}
