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
    /*public interface IESOriginalModule<in Host> : IESOriginalModule where Host : IESOringinHosting
    {
        bool Start(Host host);//开始-
    }*/

    /// <summary>
    /// ES模块核心接口：
    /// 
    /// **核心职责**：
    /// - 作为功能单元，被Host托管并按需启用/禁用/更新
    /// - 实现 IESWithLife 的完整生命周期（TryEnableSelf/TryDisableSelf/TryUpdateSelf/TryDestroySelf）
    /// - 维护自身的启用状态（EnabledSelf）与活动状态（Signal_IsActiveAndEnable）
    /// 
    /// **生命周期流程**：
    /// 1. 创建：new XxxModule()
    /// 2. 注册：module._TryRegisterToHost(host)  → Signal_HasSubmit = true
    /// 3. 启动：host 调用 module.Start()        → HasStart = true
    /// 4. 启用：host 调用 TryEnableSelf()        → Signal_IsActiveAndEnable = true, OnEnable()
    /// 5. 更新：host 每帧调用 TryUpdateSelf()    → Update()
    /// 6. 禁用：host 调用 TryDisableSelf()       → Signal_IsActiveAndEnable = false, OnDisable()
    /// 7. 销毁：host 调用 TryDestroySelf()       → HasDestroy = true, OnDestroy()
    /// 
    /// **状态标志说明**：
    /// - EnabledSelf：自身是否希望被启用（可由外部设置，如UI勾选框）
    /// - Signal_IsActiveAndEnable：当前是否真正活跃（受Host启用状态与EnabledSelf共同影响）
    /// - Signal_HasSubmit：是否已注册到Host
    /// - HasStart / HasDestroy：生命周期阶段标记
    /// - Singal_Dirty：用于延迟更新/批处理的脏标记
    /// 
    /// **典型实现模式**：
    /// ```csharp
    /// public class MyFeatureModule : BaseESModule, IESModule<MyHost>
    /// {
    ///     public MyHost GetHost { get; private set; }
    ///     
    ///     public ESTryResult _TryRegisterToHost(MyHost host)
    ///     {
    ///         if (Signal_HasSubmit) return ESTryResult.ReTry;
    ///         GetHost = host;
    ///         Signal_HasSubmit = true;
    ///         host._TryAddToListOnly(this);
    ///         return ESTryResult.Success;
    ///     }
    ///     
    ///     protected override void OnEnable()
    ///     {
    ///         // 订阅Host的事件/消息
    ///         GetHost.OnSomeEvent += HandleEvent;
    ///     }
    ///     
    ///     protected override void OnDisable()
    ///     {
    ///         // 取消订阅
    ///         GetHost.OnSomeEvent -= HandleEvent;
    ///     }
    ///     
    ///     protected override void Update()
    ///     {
    ///         // 每帧逻辑
    ///     }
    /// }
    /// ```
    /// 
    /// **注意事项**：
    /// - Module 不应直接持有UnityEngine.Object的强引用（如Transform/GameObject），
    ///   应通过Host提供的接口访问，避免Host销毁后Module成为悬空引用
    /// - _TryTestActiveAndEnable / _TryTestInActiveAndDisable 是框架内部调用的带条件检查方法，
    ///   外部应使用 TryEnableSelf / TryDisableSelf
    /// </summary>
    public interface IESModule : IESOriginalModule, IESWithLife
    {
        //这个是模块专属哈
        #region 模块专属功能区
        bool EnabledSelf { get; set; }

        void _TryTestActiveAndEnable();//带条件尝试启用

        void _TryTestInActiveAndDisable();//带条件尝试禁用
        ESTryResult _TryRegisterToHost(IESOringinHosting host);//带条件尝试开始

        bool Signal_HasSubmit { get; set; }
        bool Singal_Dirty { get; set; }
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
                Singal_Dirty = true;
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

        public bool Singal_Dirty { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; } = true;
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
