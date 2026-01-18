using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public partial class ExampleCore : Core
    {
        #region 域
        [TabGroup("扩展域", "【原始】", TabLayouting = TabLayouting.MultiRow), HideLabel]
        public ExampleNormalDomain OriginalDomain;


        [TabGroup("扩展域", "特殊", TextColor = "@Editor_DomainTabColor(SpecialDomain)")]
        [SerializeReference, InlineProperty, HideLabel]
        //显性声明扩展域
        public ExampleSpecialDomain SpecialDomain;

        
        protected override void OnAwakeRegisterOnly()
        {
            base.OnAwakeRegisterOnly();
            RegisterDomain(OriginalDomain);
            RegisterDomain(SpecialDomain);
            
        }
        #endregion

        #region 
        [TabGroup("常规", "属性")]
        public float fff = 666;
        #endregion
        

    }
    [Serializable]
    public class ExampleNormalDomain : Domain<ExampleCore, ExampleNormalModule> {
        public float f;
        public float tt;
    }
    [Serializable]
    public abstract class ExampleNormalModule : Module<ExampleCore, ExampleNormalDomain> { }

    [Serializable]
    public class ExampleSpecialDomain : Domain<ExampleCore, ExampleSpecialModule> { }
    [Serializable]
    public abstract class ExampleSpecialModule : Module<ExampleCore, ExampleSpecialDomain> { }
}
