using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    //Module For Message Provider 


    #region 应用信息更新系
    [Serializable, TypeRegistryItem("信息应用-抽象定义")]
    public abstract class MessageProviderModule_MessageUpdateLink_AB : ESUIMessageProviderModule, IReceiveLink<Link_MessageProvider>
    {
        [ESBoolOption("依赖Panel的信息提供更新", "依赖自己的")]
        public bool UseSelfProvider = true;
        [ESBoolOption("仅启用时更新", "实时更新"), SerializeField, HideInPlayMode]
        private bool UpdatableAlways = true;
        private int InitableCounter = 2;//最大支持次数
        public void OnLink(Link_MessageProvider link)
        {
            ApplyMessage(link.provider);
        }
        /// <summary>
        /// 如果需要，可以 配合 "messageKey"来获得想要的数据并且应用 
        /// </summary>
        /// <param name="provider"></param>
        public abstract void ApplyMessage(IMessageProvider provider);

        protected override void OnEnable()
        {
            base.OnEnable();
            if (UpdatableAlways)
            {
                if (UseSelfProvider)
                {
                    MyDomain.LinkReceive.AddReceive(this);
                }
                else
                {
                    MyCore.MyPanel.MessageProviderDomain.LinkReceive.AddReceive(this);
                }
            }
            if (InitableCounter > 0)
            {
                InitableCounter--;
                if (UseSelfProvider)
                {
                    var pro = MyDomain.GetMainMessageProvider();
                    if (pro != null)
                    {
                        ApplyMessage(pro);
                    }
                }
                else
                {
                    var pro = MyCore.MyPanel.MessageProviderDomain.GetMainMessageProvider();
                    if (pro != null)
                    {
                        ApplyMessage(pro);
                    }
                }
            }
        }
        protected override void OnDisable()
        {
            base.OnDisable();
            if (UpdatableAlways)
            {
                if (UseSelfProvider)
                {
                    MyDomain.LinkReceive.RemoveReceive(this);
                }
                else
                {
                    MyCore.MyPanel.MessageProviderDomain.LinkReceive.RemoveReceive(this);
                }
            }
            InitableCounter++;
        }
    }
    #endregion
}
