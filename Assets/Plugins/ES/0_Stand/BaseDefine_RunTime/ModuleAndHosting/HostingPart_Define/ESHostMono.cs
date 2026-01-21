using ES;
using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
//虚拟托管脚本是一个托管器
//他不支持特定类型，可以容纳任意的模块并且控制他们的生命周期
    [TypeRegistryItem("纯虚拟托管脚本")]
    public abstract class ESHostingMono_AB : MonoBehaviour, IESHosting
    {
        #region 显示控制和信息

        [ShowInInspector, LabelText("控制自身启用状态"), PropertyOrder(-1), FoldoutGroup("作为托管器")] public bool EnabledSelfControl { get => enabled; set { if (value) TryEnableSelf(); else TryDisableSelf(); } }
        [ShowInInspector, LabelText("显示活动状态"), PropertyOrder(-1), FoldoutGroup("作为托管器"), GUIColor("@ESDesignUtility.ColorSelector.ColorForUpdating")]
        public bool IsActiveAndEnableShow { get => Signal_IsActiveAndEnable; }

        #endregion

        #region 自定义间隔帧更新
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

        #region 控制子模块
        public virtual void UpdateAsHosting()
        {

        }
        public virtual void EnableAsHosting()
        {

        }
        public virtual void DisableAsHosting()
        {

        }
        #endregion

        #region 生命周期
        public bool Signal_IsActiveAndEnable { get; set; }

        public void TryEnableSelf()
        {
            if (Signal_IsActiveAndEnable) return;
            enabled = true;
        }
        public void TryDisableSelf()
        {
            if (Signal_IsActiveAndEnable)
            {
                enabled = false;
            }
        }
        public void TryDestroySelf()
        {
            Destroy(gameObject);
        }

        public void TryUpdateSelf()
        {
            if (CanUpdating && Signal_IsActiveAndEnable)
            {
                Update();
            }
        }
        #endregion

        #region 重写逻辑
        public virtual bool CanUpdating => true;

     

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
            Signal_IsActiveAndEnable = true;
            EnableAsHosting();
        }
        protected virtual void OnDisable()
        {
           
#if UNITY_EDITOR
            /*if () return;*/ //建议写一下如果程序已经退出就直接终止
#endif
           
            Signal_IsActiveAndEnable = false;
            DisableAsHosting();
        }

        public abstract void _TryAddToListOnly(IESModule module);
        public abstract void _TryRemoveFromListOnly(IESModule module);

        #endregion

    }

    [TypeRegistryItem("虚拟+带类型的托管脚本基类")]
    public abstract class ESHostingMono_AB<USE_Module> : ESHostingMono_AB, IESHosting<USE_Module> where USE_Module : class, IESModule
    {
        
        public virtual IEnumerable<USE_Module> ModulesIEnumable { get; }
        #region 重写控制子模块
        public override void UpdateAsHosting()
        {
            if (ModulesIEnumable != null)
            {
                foreach (var i in ModulesIEnumable)
                {
                    if (i.Signal_Dirty &&TestModuleStateBefoUpdate(i) == ESTryResult.Fail) { }
                    else i.TryUpdateSelf();
                }
            }
            base.UpdateAsHosting();
        }
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

        public abstract void _RemoveModuleFromList(USE_Module use);

        public  ESTryResult TestModuleStateBefoUpdate(USE_Module module)
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
            module.Signal_Dirty = false;
            return ESTryResult.Succeed;
        }

        #endregion
    }
   
}