using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;


namespace ES {
    [Serializable, TypeRegistryItem("原始扩展-可选中事件")]
    public class OriginalModule_SelectEvent : ESUIOriginalModule, IReceiveChannelLink<Channel_InputPointerEvent, Link_InputPointerEvent>
    {
        public override Type TableKeyType =>typeof( OriginalModule_SelectEvent);
        [LabelText("获得EMS-指针点下-单接收")]
        public EMS_PointerDown_LinkSingle ems;
        [LabelText("点击事件触发"),SerializeReference]
        public IOutputOperationUI outputOperation;
        public void OnLink(Channel_InputPointerEvent channel, Link_InputPointerEvent link)
        {
            outputOperation?.TryOperation(MyCore, MyCore.MyPanel,default);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ems.AddRecieve(this);
        }
        protected override void OnDisable()
        {
            base.OnDisable();
            ems.RemoveRecieve(this);
        }
    }

    [Serializable, TypeRegistryItem("原始扩展-可选中事件ssssss")]
    public class OriginalModule_SelectEvent2 : ESUIOriginalModule,IReceiveChannelLink_Context_String
    {
        public override Type TableKeyType => null;
        public string ContextKey = "name";
        public TMP_Text text;
   
        protected override void OnEnable()
        {
            base.OnEnable();
            MyCore.MyPanel.ContextPool.LinkRCL_String.AddReceive(ContextKey,this);
        }
        protected override void OnDisable()
        {
            base.OnDisable();
            MyCore.MyPanel.ContextPool.LinkRCL_String.RemoveReceive(ContextKey, this);
        }



        public void OnLink(string channel, Link_ContextEvent_StringChange link)
        {
            text.text = link.Value_Now;
        }
    }
}
