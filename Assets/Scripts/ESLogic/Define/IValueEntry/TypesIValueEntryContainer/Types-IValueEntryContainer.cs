using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    //Types-IValueEntryContainer 
    // 为了应对 UnityObject 无法多态序列化 ，为所有的ValueEntry 单独实现一个支持类型
    /*
      [Serializable, TypeRegistryItem("注册Actor信息")]
      public class MessagevalueEntry_Register_Actor : IValueEntryContainer
      {
          [LabelText("ActorInfo")]
          public ActorDataInfo actorInfo;

          public override IValueEntry GetValueEntry => actorInfo;
    }
    */
    //ActorDataInfo : IMessageXXXvalueEntry 至少实现一个即可

    

}
