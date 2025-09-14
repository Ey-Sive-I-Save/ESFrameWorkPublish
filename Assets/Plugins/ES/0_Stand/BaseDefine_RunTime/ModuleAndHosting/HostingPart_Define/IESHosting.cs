using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{
    public interface IESOringinHosting
    {

    }
    //以Hosting声明
    public interface IESHosting : IESOringinHosting, IESWithLife
    {
        #region 托管器专属
        //虚拟的
        /* SafeUpdateSet_EasyQueue_SeriNot_Dirty<IESModule> VirtualBeHosted { get; }*/
        /// <summary>
        /// 更新子
        /// </summary>
        void UpdateAsHosting();
        /// <summary>
        /// //启用子
        /// </summary>
        void EnableAsHosting();
        /// <summary>
        /// //禁用子
        /// </summary>
        void DisableAsHosting();
        /// <summary>
        /// 仅添加到列表
        /// </summary>
        /// <param name="module"></param>
        void _TryAddToListOnly(IESModule module);
        void _TryRemoveFromListOnly(IESModule module);
        #endregion
    }
    public abstract class BaseESHosting : IESHosting
    {

        #region 实现自定义帧间隔更新
        private short UpdateIntervalFrameCount = -1;
        private short SelfModelTarget = -1;
        public void ResetUpdateIntervalFrameCount(short interval = 10)
        {
            UpdateIntervalFrameCount = interval;
            if (UpdateIntervalFrameCount > 0)
            {
                SelfModelTarget = (short)UnityEngine.Random.Range(0, UpdateIntervalFrameCount);
            }
        }
        #endregion

        #region 重写逻辑
        public virtual bool CanUpdating
        {
            get { return true; }
        }

        protected virtual void Update()
        {
            if (UpdateIntervalFrameCount > 0)
            {
                if (SelfModelTarget < 0) ResetUpdateIntervalFrameCount(UpdateIntervalFrameCount);
                if (Time.frameCount % UpdateIntervalFrameCount != SelfModelTarget)
                {
                    return;
                }
            }
            UpdateAsHosting();
        }

        protected virtual void OnEnable()
        {
            if (UpdateIntervalFrameCount > 0 && SelfModelTarget < 0)
            {
                ResetUpdateIntervalFrameCount(UpdateIntervalFrameCount);
            }
            Signal_IsActiveAndEnable = true;
            EnableAsHosting();
        }

        protected virtual void OnDisable()
        {
            Signal_IsActiveAndEnable = false;
            DisableAsHosting();
        }
        #endregion

        #region 关于开关逻辑与运行状态
        public bool Signal_IsActiveAndEnable { get; set; } = false;


        public virtual void TryEnableSelf()
        {
            if (Signal_IsActiveAndEnable) return;
            OnEnable();
        }


        public virtual void TryDisableSelf()
        {
            if (!Signal_IsActiveAndEnable)
            {
                OnDisable();
            }
        }

        public void TryUpdateSelf()
        {
            if (CanUpdating && Signal_IsActiveAndEnable)
            {
                Update();
            }
        }
        #endregion

        #region 与对子的控制

        public virtual void UpdateAsHosting()
        {

        }

        public virtual void EnableAsHosting()
        {
        }

        public virtual void DisableAsHosting()
        {
        }

        public abstract void _TryAddToListOnly(IESModule module);
        public abstract void _TryRemoveFromListOnly(IESModule module);
        public abstract void TryDestroySelf();
        #endregion
    }
    //以泛型声明
    public interface IESHosting<WithModule> : IESHosting where WithModule : class, IESModule
    {
        IEnumerable<WithModule> ModulesIEnumable { get; }
        public ESTryResult TestModuleStateBefoUpdate(WithModule module);
    }
    public abstract class BaseESHosting<With> : BaseESHosting, IESHosting<With> where With : class, IESModule
    {
        #region 对特定类型的托管支持
        public abstract IEnumerable<With> ModulesIEnumable { get; }
        public override void EnableAsHosting()
        {
            if (ModulesIEnumable != null)
            {
                foreach (var i in ModulesIEnumable)
                {
                    i._TryTestActiveAndEnable();
                }
            }
            base.EnableAsHosting();
        }
        public override void DisableAsHosting()
        {
            if (ModulesIEnumable != null)
            {
                foreach (var i in ModulesIEnumable)
                {
                    i._TryTestInActiveAndDisable();
                }
            }
            base.DisableAsHosting();
        }
        public override void UpdateAsHosting()
        {
            if (ModulesIEnumable != null)
            {
                foreach (var i in ModulesIEnumable)
                {
                    //性能几乎一致？
                    if (i.Singal_Dirty&&TestModuleStateBefoUpdate(i) == ESTryResult.Fail) { }
                    else i.TryUpdateSelf();
                }
            }
            base.UpdateAsHosting();
        }

        public ESTryResult TestModuleStateBefoUpdate(With module)
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
                module.OnDestroy();
                return ESTryResult.Fail;
            }
            module.Singal_Dirty = false;
            return ESTryResult.Succeed;
        }
        #endregion
    }
}
