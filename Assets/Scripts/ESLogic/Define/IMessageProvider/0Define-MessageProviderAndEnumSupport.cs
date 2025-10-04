using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*   MessageProvider 
 *   可以配合<MessageProcesser> 
     信息获取源 - 支持 ES的多语言
     声明这个接口和预先准备的多种数据,一般这些数据是不随时间影响的持久数据或者状态量
     方便后续进行UI显示，数值获取 
     和 SharedData密不可分，也是 ES制定的标准
     
     甚至还可能有KeyGroup支持

     String 
     Float
     Int
     Bool
     其他的自己实现接口
     
 
 */
namespace ES
{

    public interface IMessageProvider
    {
        public string GetMessage(MessageStringKey k, EnumCollect.LanguageType language = EnumCollect.LanguageType.NotClear, int hepler = default)
        {
            if (this is IMessageStringProvider stringProvider)
            {
                return stringProvider.GetMessage(k, language, hepler);
            }
            return "NULL";
        }
        public int GetMessage(MessageIntKey k, EnumCollect.LanguageType language = EnumCollect.LanguageType.NotClear, int hepler = default) {
            if (this is IMessageIntProvider intProvider)
            {
                return intProvider.GetMessage(k, language, hepler);
            }
            return -1;
        }
        public float GetMessage(MessageFloatKey k, EnumCollect.LanguageType language = EnumCollect.LanguageType.NotClear, int hepler = default) {
            if (this is IMessageFloatProvider floatProvider)
            {
                return floatProvider.GetMessage(k, language, hepler);
            }
            return -1f;
        }
        public bool GetMessage(MessageBoolKey k, EnumCollect.LanguageType language = EnumCollect.LanguageType.NotClear, int hepler = default) {
            if (this is IMessageBoolProvider boolProvider)
            {
                return boolProvider.GetMessage(k, language, hepler);
            }
            return false;
        }
        public Sprite GetMessage(MessageSpriteKey k, EnumCollect.LanguageType language = EnumCollect.LanguageType.NotClear, int hepler = default)
        {
            if (this is IMessageSpriteProvider spro)
            {
                return spro.GetMessage(k, language, hepler);
            }
            return null;
        }
    }
    
    [Serializable,TypeRegistryItem("信息提供注册")]//各种类型的注册器
    public abstract class IMessageProv_Reg_Ab
    {
        public abstract IMessageProvider Registe { get; }
    }
    public struct Link_MessageProvider 
    {
        public string key;
        public IMessageProvider provider;
        public bool isMain;
    }
    public interface IMessageProvider<out Back> : IMessageProvider
    {
        Back GetBack();
    }
    //                                返回类型 键类型 第二判据(可以忽略)
    public interface IMessageProvider< Back, Key, Hepler> : IMessageProvider
    {
        public Back GetMessage(Key k, EnumCollect.LanguageType language = EnumCollect.LanguageType.NotClear, Hepler hepler = default);
    }

    #region 取出键类型
    //专属于字符串类型的数据
    public enum MessageStringKey
    {
        [InspectorName("【通用】值")] DefaultString,
        [InspectorName("【通用】名字")] Name,
        [InspectorName("【通用】直观描述")] Description,
        [InspectorName("【通用】内容")] Content,

        [InspectorName("【核心】故事")] Story,
        [InspectorName("【核心】头衔")] Title,
        [InspectorName("【核心】效果")] Effect,
        [InspectorName("【核心】任务")] Quest
    }
    //专属于浮点数类型的数据
    public enum MessageFloatKey
    {
        [InspectorName("【通用】值")] DefaultFloat,
        [InspectorName("【通用】伤害")] Damage,
        [InspectorName("【通用】概率")] Rate,
        [InspectorName("【通用】进度")] Progress,

        [InspectorName("【核心】暴击概率")] Core_CriRate,
        [InspectorName("【核心】速度")] Core_Speed,
        [InspectorName("【核心】生命力")] Core_Health,
        [InspectorName("【核心】魔法")] Core_Magic,
        [InspectorName("【核心】耐力")] Core_Stamina,
        [InspectorName("【核心】增量")] Core_Gain​
    }

    //专属于浮点数类型的数据
    public enum MessageIntKey
    {
        [InspectorName("【通用】值")] DefaultInt,
        [InspectorName("【通用】数量")] Count,
        [InspectorName("【通用】阶段")] Phase,

        [InspectorName("【核心】等级")] Core_Level,
        [InspectorName("【核心】智能")] Core_​Intelligence,

    }


    //专属于布尔值类型的数据
    public enum MessageBoolKey
    {
        [InspectorName("【通用】值")] DefaultBool,
        [InspectorName("【通用】活动")] IsActive,
        [InspectorName("【通用】加载完毕")] IsLoaded,
        [InspectorName("【通用】选中")] IsSelected,
        [InspectorName("【通用】完成")] IsCompleted,


        [InspectorName("【核心】可交互")] Core_IsInteractable,
        [InspectorName("【核心】会飞")] Core_​CanFly,
        [InspectorName("【核心】是Boss级别")] Core_​IsBoss,


        [InspectorName("【视觉】可见")] Vision_IsVisible,
        [InspectorName("【视觉】高亮")] Vision_IsHignLight,
    }
    //专属于精灵图类型的数据
    public enum MessageSpriteKey
    {
        // 通用类精灵
        [InspectorName("【通用】值")] DefaultIcon,
        [InspectorName("【通用】高亮的")] Highlighted,
    }

    #endregion

    #region 常规提供源
    //等待认领
    public interface IMessageIntProvider : IMessageProvider<int, MessageIntKey, int>
    {

    }
    public interface IMessageFloatProvider : IMessageProvider<float, MessageFloatKey, int>
    {

    }
    public interface IMessageBoolProvider : IMessageProvider<bool, MessageBoolKey, int>
    {

    }
    #endregion
}

