#define TEST

using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// ES模块化架构的核心接口定义
/// 
/// **设计哲学**：
/// - 将复杂对象的功能拆分为多个独立Module，每个Module负责单一职责
/// - 通过Hosting机制统一管理Module的生命周期，避免Update回调爆炸
/// - 支持泛型Host约束，实现强类型的Module-Host交互
/// </summary>
namespace ES
{
    /// <summary>
    /// Module 原始标记接口，用于类型约束
    /// </summary>
    public interface IESOriginalModule
    {

    }
    public interface IESModule : IESOriginalModule, IESWithLife
    {
        //这个是模块专属哈
        #region 模块专属功能区
        bool EnabledSelf { get; set; }

        void _TryTestActiveAndEnable();//带条件尝试启用

        void _TryTestInActiveAndDisable();//带条件尝试禁用
        ESTryResult _TryRegisterToHost(IESOringinHosting host);//带条件尝试开始

        bool Signal_HasSubmit { get; set; }
        bool Signal_Dirty { get; set; }
        void _SetHost(IESOringinHosting host);
        bool HasStart { get; set; }
        bool HasDestroy { get; set; }
        void Start();
        void OnDestroy();
        #endregion 
    }

    public interface IESModule<Host> : IESModule where Host : class, IESOringinHosting
    {
        #region 托管声明
        Host GetHost { get; }
        ESTryResult _TryRegisterToHost(Host host);
        void _SetHost(Host host);
        //显式实现
        ESTryResult IESModule._TryRegisterToHost(IESOringinHosting host)
        {
            if (Signal_HasSubmit) return ESTryResult.ReTry;
            return _TryRegisterToHost(host as Host);
        }
        //显式实现
        void IESModule._SetHost(IESOringinHosting host)
        {
            _SetHost(host as Host);
        }
        #endregion
    }

    public abstract class BaseESModule : IESModule
    {
        #region 显示控制状态
        [ShowInInspector, LabelText("控制自身启用状态"), PropertyOrder(-1)] public bool EnabledSelfControl { get => EnabledSelf; set { if (value) TryEnableSelf(); else TryDisableSelf(); } }
        [ShowInInspector, LabelText("显示活动状态")]
        public bool IsActiveAndEnableShow { get => Signal_IsActiveAndEnable; }
        #endregion

        #region 重写逻辑
        //启用时逻辑
        public virtual bool CanUpdating => true;
        protected virtual void OnEnable()
        {
        }
        //禁用时逻辑
        protected virtual void OnDisable()
        {
        }
        //更新时逻辑
        protected virtual void Update()
        {
        }
        public virtual void Start()
        {

        }
        public virtual void OnDestroy()
        {

        }
        #endregion

        #region 关于开关逻辑与运行状态
        public bool Signal_IsActiveAndEnable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _isActiveAndEnable; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value != _isActiveAndEnable)
                {
                    _isActiveAndEnable = value;
                    if (value) OnEnable();
                    else OnDisable();
                }
            }
        }
        [NonSerialized]
        private bool _isActiveAndEnable = false;
        public bool EnabledSelf { get => _enableSelf; set { _enableSelf = value; StateTestForSelfEnable(); } }
        public abstract bool HostEnable { get; }
        private void StateTestForSelfEnable()
        {
            if (_hasSubmit&& HostEnable)//只有在Submit时，才有权造成因为Self引起的状态改变
            {
                Signal_Dirty = true;
                if (_isActiveAndEnable && !_enableSelf) _TryTestInActiveAndDisable();
                else if (!_isActiveAndEnable && _enableSelf) _TryTestActiveAndEnable();
            }
        }

        [SerializeField, HideInInspector] private bool _enableSelf = true;
        public virtual void TryEnableSelf()
        {
            EnabledSelf = true;
        }
        public virtual void TryDisableSelf()
        {
            EnabledSelf = false;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void _TryTestActiveAndEnable()
        {
            if (Signal_IsActiveAndEnable || !EnabledSelf) return;//打开需要限制
            Signal_IsActiveAndEnable = true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void _TryTestInActiveAndDisable()
        {
            Signal_IsActiveAndEnable = false;//关闭肯定能关
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryUpdateSelf()
        {
            if (CanUpdating && Signal_IsActiveAndEnable)
            {
                Update();
            }
        }

        public void HardUpdate()
        {
            //无条件
            Update();
        }
        #endregion

        #region 关于提交SubMit

        public bool Signal_HasSubmit
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _hasSubmit; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value != _hasSubmit)
                {
                    _hasSubmit = value;
                }
            }
        }

        public bool Signal_Dirty { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; } = true;
        public bool HasStart { get; set; }
        public bool HasDestroy { get; set; }

        private bool _hasSubmit = false;

        /*可能会因为对象销毁和自己被移除队列时，触发它*/
        public abstract void TryDestroySelf();//踢出

        public ESTryResult _TryRegisterToHost(IESOringinHosting host)
        {
            if (Signal_HasSubmit) return ESTryResult.ReTry;
            if (host != null)
            {
                _SetHost(host);
                Signal_HasSubmit = true;
                return ESTryResult.Succeed;
            }
            return ESTryResult.Fail;
        }

        public virtual void _SetHost(IESOringinHosting host)
        {

        }


        #endregion
    }
    
    public abstract class BaseESModule<Host> : BaseESModule, IESModule<Host> where Host : class, IESOringinHosting
    {
        #region 与自己的Host关联

        public virtual ESTryResult _TryRegisterToHost(Host host)
        {
            if (Signal_HasSubmit) return ESTryResult.ReTry;
            if (host != null)
            {
                _SetHost(host);
                Signal_HasSubmit = true;
                return ESTryResult.Succeed;
            }
            return ESTryResult.Fail;
        }

        public abstract void _SetHost(Host host);

        public virtual Host GetHost { get; }

        public sealed override void _SetHost(IESOringinHosting host)
        {
            if (host is Host h) _SetHost(h);
        }

        #endregion
    }
    
}
