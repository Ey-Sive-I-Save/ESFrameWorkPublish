using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;


namespace ES
{
    [TypeRegistryItem("空模块")]
    public interface IModule : IESModule
    {
        public Core Core_Object { get; }
        public void _SetDomainCreateRelationshipOnly(IDomain Domain);
        public void FixedUpdateExpand();
        public Type DomainType { get; }
    }
    [TypeRegistryItem("抽象模块定义")]
    public abstract class Module<Core_, Domain_> : BaseESModule<Domain_>, IModule where Core_ : Core where Domain_ : class, IDomain<Core_>
    {

        #region 总重要信息
        [/*绑定扩展域*/ NonSerialized]
        public Domain_ MyDomain;
        [/*绑定核心*/ NonSerialized]
        public Core_ MyCore;
        #endregion

        #region 只读便捷属性
        public Type DomainType => typeof(Domain_);
        public abstract Type TableKeyType { get; }
        public Core Core_Object => MyCore;
        public sealed override Domain_ GetHost //重写-还是获取核心
        {
            get => MyDomain;
        }
        public override bool HostEnable => MyCore?.isActiveAndEnabled ?? false;

        #endregion

        #region 补充逻辑
        public sealed override bool CanUpdating
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return true;
            }
        }

        public sealed override void _SetHost(Domain_ host)
        {
            MyDomain = host;
            MyCore = host.MyCore;
            Awake();
            Signal_Dirty = true;
            Signal_HasSubmit = true;
        }
        public sealed override void TryDestroySelf()
        {
            Signal_HasSubmit = false;
            Signal_Dirty = true;
            //不再等待
            if (!Signal_IsActiveAndEnable)
            {
                if (HasDestroy)
                {

                }
                else
                {
                    HasDestroy = true;
                    OnDestroy();
                }
            }
        }
        #endregion

        #region 可直接重写扩展逻辑(关键)
        /*  
         *  开始时(对于一次域和模块的绑定-只会进行一次)
          protected override void Start()
          {
              base.Start();
              &初始化数据，创建对象
              &整个模块周期只执行一次
        // 初始化/添加时  Submit->
          }
        *  启用时(从禁用到启用进行--和脚本几乎一致)
          protected override void OnEnable()
          {
              base.OnEnable();
              &可配合OnDisable重复触发
              &注销委托
              &仅启用时相关逻辑的开启
          }
          protected override void OnDisable()
          {
              base.OnDisable();
              &可配合OnEnable重复触发
              &注销委托
              &仅启用时相关逻辑的关闭
          }
          protected override void Update()
          {   
              &启用时每帧执行
              base.Update();
          }

          protected override void OnDestroy()
          {
              &被销毁，解除绑定,整个生命周期只有一次(可重复)
              base.OnDestroy();
               // 物体销毁/移除时  Submit->
               
          }*/

        protected virtual void Awake()
        {
            if (TableKeyType != null)
            {
                MyCore.ModuleTables[this.TableKeyType] = this;
            }
        }
        public override void OnDestroy()
        {
            if (TableKeyType != null)
            {
                if (MyCore.ModuleTables[this.TableKeyType] == this)
                {
                    MyCore.ModuleTables.Remove(TableKeyType);
                };
            }
        }
        #endregion

        #region 自主扩展手动委托功能(为了性能考虑)
        public virtual void FixedUpdateExpand()
        {

        }
        #endregion

        #region 辅助功能
        //一般编辑器模式会用--用来单纯链接而不进行逻辑运行
        public void _SetDomainCreateRelationshipOnly(IDomain Domain)
        {
            if (Domain is Domain_ domain_)
            {
                this.MyDomain = domain_;
                Awake();
            }
        }
        #endregion
    }



}