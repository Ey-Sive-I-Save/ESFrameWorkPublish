using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace ES
{
    // Processer 系列 , 是直接应用Message的
    /// <summary>
    ///  泛型参数 一般 填入 MessageStringKey MessageIntKey MessageFloatKey MessageBoolKey
    /// </summary>
    /// <typeparam name="KeyType"></typeparam>
    [Serializable, TypeRegistryItem("信息应用-抽象定义")]
    public abstract class MessageProviderModule_MessageProcesser_AB<KeyType> : MessageProviderModule_MessageUpdateLink_AB, IReceiveLink<Link_MessageProviderSwitch>
    {
        [LabelText("取出信息键")]
        public KeyType messageKey = default;

    }
    [Serializable, TypeRegistryItem("信息应用-TMP_Text组件-字符串")]
    public class MessageProviderModule_TMP_Text_String  : MessageProviderModule_MessageProcesser_AB<MessageStringKey>
    {
        public override Type TableKeyType => typeof(MessageProviderModule_TMP_Text_String);
        [LabelText("应用到")]
        public TMP_Text tmp_text;
        [LabelText("等待支持---字符串修饰器")]
        public string waiting;

        public override void ApplyMessage(IMessageProvider provider)
        {
            tmp_text.text= provider.GetMessage(messageKey);
        }
    }

    [Serializable, TypeRegistryItem("信息应用-TMP_Text组件-浮点数")]
    public class MessageProviderModule_TMP_Text_Float : MessageProviderModule_MessageProcesser_AB<MessageFloatKey>
    {
        public override Type TableKeyType => typeof(MessageProviderModule_TMP_Text_Float);

        [LabelText("应用到")]
        public TMP_Text tmp_text;
        [LabelText("等待支持---字符串修饰器")]
        public string waiting;

        public override void ApplyMessage(IMessageProvider provider)
        {
            tmp_text.text = provider.GetMessage(messageKey).ToString();
        }
    }
    [Serializable, TypeRegistryItem("信息应用-TMP_Text组件-浮点数")]
    public class MessageProviderModule_TMP_Text_Int : MessageProviderModule_MessageProcesser_AB<MessageIntKey>
    {
        public override Type TableKeyType => typeof(MessageProviderModule_TMP_Text_Int);

        [LabelText("应用到")]
        public TMP_Text tmp_text;
        [LabelText("等待支持---字符串修饰器")]
        public string waiting;

        public override void ApplyMessage(IMessageProvider provider)
        {
            tmp_text.text = provider.GetMessage(messageKey).ToString();
        }
    }
    [Serializable, TypeRegistryItem("信息应用-TMP_Text组件-布尔值")]
    public class MessageProviderModule_TMP_Text_Bool : MessageProviderModule_MessageProcesser_AB<MessageBoolKey>
    {
        public override Type TableKeyType => null;

        [LabelText("应用到")]
        public TMP_Text tmp_text;
        [LabelText("等待支持---字符串修饰器")]
        public string waiting;

        public override void ApplyMessage(IMessageProvider provider)
        {
            tmp_text.text = provider.GetMessage(messageKey).ToString();
        }
    }
    [Serializable, TypeRegistryItem("信息应用-Image组件-贴图")]
    public class MessageProviderModule_Sprite_Image : MessageProviderModule_MessageProcesser_AB<MessageSpriteKey>
    {
        public override Type TableKeyType => typeof(MessageProviderModule_Sprite_Image);
        [LabelText("应用到")]
        public Image image;
        public override void ApplyMessage(IMessageProvider provider)
        {
            image.sprite = provider.GetMessage(messageKey);
        }
    }
}
