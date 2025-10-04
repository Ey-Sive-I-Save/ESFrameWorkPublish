using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;


namespace ES
{
    // Comparer 系列 , 一般是符合特定条件才会触发的
    [Serializable, TypeRegistryItem("信息比对-相同的信息更新导致触发事件")]
    public class MessageProviderModule_EqualWithAndDo : MessageProviderModule_MessageUpdateLink_AB
    {
        [ESBoolOption("不相等就忽略", "不相等以NotFlag执行")] public bool NotEqual_OperationWithNotFlag = false;
        [LabelText("比对持有信息目标")] public ESUIElementGetter getter = new ESUIElementGetter_Self();
        [LabelText("通过则执行"), SerializeReference] public IOutputOperationUI operation;

        public override Type TableKeyType => typeof(MessageProviderModule_EqualWithAndDo);

        public sealed override void ApplyMessage(IMessageProvider provider)
        {
            if (operation == null) return;
            var comTo = getter?.Get(MyCore, MyCore.MyPanel) ?? MyCore;
            if (comTo.MessageProviderDomain?.MainProvider == provider)
            {
                operation.TryOperation(MyCore, UseSelfProvider ? MyCore : MyCore.MyPanel,default);
            }
            else if (NotEqual_OperationWithNotFlag)
            {
                operation.TryOperation(MyCore, UseSelfProvider ? MyCore : MyCore.MyPanel, new Link_UI_NotFlag());
            }
        }
    }
}
