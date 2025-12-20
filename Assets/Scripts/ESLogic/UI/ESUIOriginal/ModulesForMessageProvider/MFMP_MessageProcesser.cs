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
    public abstract class MessagevalueEntryModule_MessageProcesser_AB<KeyType> : MessagevalueEntryModule_MessageUpdateLink_AB, IReceiveLink<Link_MessagevalueEntrySwitch>
    {
        [LabelText("取出信息键")]
        public KeyType messageKey = default;

    }
    [Serializable, TypeRegistryItem("信息应用-TMP_Text组件-字符串")]
    public class MessagevalueEntryModule_TMP_Text_String  : MessagevalueEntryModule_MessageProcesser_AB<ValueEntryStringKey>
    {
        public override Type TableKeyType => typeof(MessagevalueEntryModule_TMP_Text_String);
        [LabelText("应用到")]
        public TMP_Text tmp_text;
        [LabelText("等待支持---字符串修饰器")]
        public string waiting;

        public override void ApplyMessage(IValueEntry valueEntry)
        {
            string str=null;
           valueEntry.HandleValueEntry(ref str,messageKey);
           tmp_text.text=str;
        }
    }

    [Serializable, TypeRegistryItem("信息应用-TMP_Text组件-浮点数")]
    public class MessagevalueEntryModule_TMP_Text_Float : MessagevalueEntryModule_MessageProcesser_AB<ValueEntryFloatKey>
    {
        public override Type TableKeyType => typeof(MessagevalueEntryModule_TMP_Text_Float);

        [LabelText("应用到")]
        public TMP_Text tmp_text;
        [LabelText("等待支持---字符串修饰器")]
        public string waiting;

        public override void ApplyMessage(IValueEntry valueEntry)
        {
            float f=0;
            valueEntry.HandleValueEntry(ref  f, messageKey);
            tmp_text.text = f.ToString();
        }
    }
    [Serializable, TypeRegistryItem("信息应用-TMP_Text组件-浮点数")]
    public class MessagevalueEntryModule_TMP_Text_Int : MessagevalueEntryModule_MessageProcesser_AB<ValueEntryIntKey>
    {
        public override Type TableKeyType => typeof(MessagevalueEntryModule_TMP_Text_Int);

        [LabelText("应用到")]
        public TMP_Text tmp_text;
        [LabelText("等待支持---字符串修饰器")]
        public string waiting;

        public override void ApplyMessage(IValueEntry valueEntry)
        {
            int int_=0;
             valueEntry.HandleValueEntry(ref int_, messageKey);
            tmp_text.text =int_.ToString();
        }
    }
    [Serializable, TypeRegistryItem("信息应用-TMP_Text组件-布尔值")]
    public class MessagevalueEntryModule_TMP_Text_Bool : MessagevalueEntryModule_MessageProcesser_AB<ValueEntryBoolKey>
    {
        public override Type TableKeyType => null;

        [LabelText("应用到")]
        public TMP_Text tmp_text;
        [LabelText("等待支持---字符串修饰器")]
        public string waiting;

        public override void ApplyMessage(IValueEntry valueEntry)
        {
            bool b=false;
            valueEntry.HandleValueEntry(ref b, messageKey);
            tmp_text.text = b.ToString();
        }
    }
    [Serializable, TypeRegistryItem("信息应用-Image组件-贴图")]
    public class MessagevalueEntryModule_Sprite_Image : MessagevalueEntryModule_MessageProcesser_AB<ValueEntrySpriteKey>
    {
        public override Type TableKeyType => typeof(MessagevalueEntryModule_Sprite_Image);
        [LabelText("应用到")]
        public Image image;
        public override void ApplyMessage(IValueEntry valueEntry)
        {
            Sprite sprite=null;
            valueEntry.HandleValueEntry(ref sprite, messageKey);
            image.sprite = sprite;
        }
    }
}
