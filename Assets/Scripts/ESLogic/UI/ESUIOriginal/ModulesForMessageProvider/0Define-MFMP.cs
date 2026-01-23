using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    //Module For Message valueEntry 


    #region 应用信息更新系
    [Serializable, TypeRegistryItem("信息应用-抽象定义")]
    public abstract class MessagevalueEntryModule_MessageUpdateLink_AB : ESUIMessagevalueEntryModule, IReceiveLink<Link_MessagevalueEntrySwitch>
    {
        [ESBoolOption("依赖Panel的信息提供更新", "依赖自己的")]
        public bool UseSelfvalueEntry = true;
        [ESBoolOption("仅启用时更新", "实时更新"), SerializeField, HideInPlayMode]
        private bool UpdatableAlways = true;
        private int InitableCounter = 2;//最大支持次数
        public void OnLink(Link_MessagevalueEntrySwitch link)
        {
            ApplyMessage(link.valueEntry);
        }
        /// <summary>
        /// 如果需要，可以 配合 "messageKey"来获得想要的数据并且应用 
        /// </summary>
        /// <param name="valueEntry"></param>
        public abstract void ApplyMessage(IValueEntry valueEntry);

        protected override void OnEnable()
        {
            base.OnEnable();
            if (UpdatableAlways)
            {
                if (UseSelfvalueEntry)
                {
                    MyDomain.LinkReceive.AddReceiver(this);
                }
                else
                {
                    MyCore.MyPanel.MessagevalueEntryDomain.LinkReceive.AddReceiver(this);
                }
            }
            if (InitableCounter > 0)
            {
                InitableCounter--;
                if (UseSelfvalueEntry)
                {
                    var pro = MyDomain.GetMainMessagevalueEntry();
                    if (pro != null)
                    {
                        ApplyMessage(pro);
                    }
                }
                else
                {
                    var pro = MyCore.MyPanel.MessagevalueEntryDomain.GetMainMessagevalueEntry();
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
                if (UseSelfvalueEntry)
                {
                    MyDomain.LinkReceive.RemoveReceiver(this);
                }
                else
                {
                    MyCore.MyPanel.MessagevalueEntryDomain.LinkReceive.RemoveReceiver(this);
                }
            }
            InitableCounter++;
        }
    }
    #endregion
}
