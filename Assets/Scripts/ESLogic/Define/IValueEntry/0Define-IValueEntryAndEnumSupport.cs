using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*   IValueEntry 
 *   可以配合<IValueEntry> 
     值通道 - 支持 ES的多语言
     声明这个接口和预先准备的多种数据,一般这些数据是不随时间影响的持久数据或者状态量
     方便后续进行UI显示，数值获取 和设置
     和 SharedData密不可分，也是 ES制定的标准
     
     甚至还可能有KeyGroup支持
     
     默认支持
     String 
     Float
     Int
     Bool
     Sprite
     GameObject
     UnityObject
     SystemObject
     其他的需要自己实现接口
     
 
 */
namespace ES
{

    public interface IValueEntry
    {
        public void HandleValueEntry(ref string back ,
         ValueEntryStringKey key,
         object help=null,
         EnumCollect.Envir_LanguageType lan= 
         EnumCollect.Envir_LanguageType.NotClear,
         EnumCollect.ValueEntryGetOrSet getOrSet= 
         EnumCollect.ValueEntryGetOrSet.NotClear)
        {
            if (this is IStringValueEntry strVE)
            {
             strVE.HandleValueEntry(ref back,key,help,lan,getOrSet);
            }
        }
        public void HandleValueEntry(ref int back ,
         ValueEntryIntKey key,
         object help=null,
         EnumCollect.Envir_LanguageType lan= 
         EnumCollect.Envir_LanguageType.NotClear,
         EnumCollect.ValueEntryGetOrSet getOrSet= 
         EnumCollect.ValueEntryGetOrSet.NotClear)   {
         if (this is IIntValueEntry intVE)
            {
             intVE.HandleValueEntry(ref back,key,help,lan,getOrSet);
            }
        }
        public void HandleValueEntry(ref float back ,
         ValueEntryFloatKey key,
         object help=null,
         EnumCollect.Envir_LanguageType lan= 
         EnumCollect.Envir_LanguageType.NotClear,
         EnumCollect.ValueEntryGetOrSet getOrSet= 
         EnumCollect.ValueEntryGetOrSet.NotClear)   {  
            if (this is IFloatValueEntry floatVE)
            {
             floatVE.HandleValueEntry(ref back,key,help,lan,getOrSet);
            }
         
        }
        public void HandleValueEntry(ref bool back ,
         ValueEntryBoolKey key,
         object help=null,
         EnumCollect.Envir_LanguageType lan= 
         EnumCollect.Envir_LanguageType.NotClear,
         EnumCollect.ValueEntryGetOrSet getOrSet= 
         EnumCollect.ValueEntryGetOrSet.NotClear)   {   
            if (this is IBoolValueEntry boolVE)
            {
             boolVE.HandleValueEntry(ref back,key,help,lan,getOrSet);
            }
        }
        public void HandleValueEntry(ref Sprite back ,
         ValueEntrySpriteKey key,
         object help=null,
         EnumCollect.Envir_LanguageType lan= 
         EnumCollect.Envir_LanguageType.NotClear,
         EnumCollect.ValueEntryGetOrSet getOrSet= 
         EnumCollect.ValueEntryGetOrSet.NotClear)   {
            if (this is ISpriteValueEntry spVE)
            {
             spVE.HandleValueEntry(ref back,key,help,lan,getOrSet);
            }
          
        }
    }
    
    [Serializable,TypeRegistryItem("信息提供注册")]//各种类型的注册器抽象，只是为了多态序列化 UnityObject类型
    public abstract class IValueEntryContainer
    {
        public abstract IValueEntry GetValueEntry { get; }
    }
    //                         返回类型 键类型 
    public interface IValueEntry<Value,Key> : IValueEntry
    {
        public void HandleValueEntry(ref Value back ,
         Key key,
         object help=null,
         EnumCollect.Envir_LanguageType lan= 
         EnumCollect.Envir_LanguageType.NotClear,
         EnumCollect.ValueEntryGetOrSet getOrSet= 
         EnumCollect.ValueEntryGetOrSet.NotClear);
     }

    #region 取出键类型
    //专属于字符串类型的数据
    public enum ValueEntryStringKey
    {
        [InspectorName("【通用】默认值")] DefaultValue,
        [InspectorName("【通用】名字")] Name,
        [InspectorName("【通用】直观描述")] Description,
        [InspectorName("【通用】内容")] Content,

        [InspectorName("【核心】故事")] Core_Story,
        [InspectorName("【核心】头衔")] Core_Title,
        [InspectorName("【核心】效果")] Core_Effect,
        [InspectorName("【核心】任务")] Core_Quest
    }
    //专属于浮点数类型的数据
    public enum ValueEntryFloatKey
    {
        [InspectorName("【通用】默认值")] DefaultValue,
        [InspectorName("【通用】伤害")] Damage,
        [InspectorName("【通用】概率")] Rate,
        [InspectorName("【通用】进度")] Progress,
        [InspectorName("【核心】生命力")] Core_Health,
        [InspectorName("【核心】速度")] Core_Speed,
        [InspectorName("【核心】魔法")] Core_Magic,
        [InspectorName("【核心】暴击概率")] Core_CriRate,
        [InspectorName("【核心】耐力")] Core_Stamina,
        [InspectorName("【核心】增量")] Core_Gain​
    }

    //专属于浮点数类型的数据
    public enum ValueEntryIntKey
    {
        [InspectorName("【通用】默认值")] DefaultValue,
        [InspectorName("【通用】数量")] Count,
        [InspectorName("【通用】阶段")] Phase,

        [InspectorName("【核心】等级")] Core_Level,
        [InspectorName("【核心】智能")] Core_​Intelligence,

    }


    //专属于布尔值类型的数据
    public enum ValueEntryBoolKey
    {
        [InspectorName("【通用】默认值")] DefaultValue,
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
    public enum ValueEntrySpriteKey
    {
        // 通用类精灵
        [InspectorName("【通用】默认值")] DefaultValue,
        [InspectorName("【通用】高亮的")] Highlighted,
    }

    #endregion

    #region 常规值通道
    //等待认领
    public interface IIntValueEntry : IValueEntry<int, ValueEntryIntKey>
    {

    }
    public interface IFloatValueEntry : IValueEntry<float, ValueEntryFloatKey>
    {

    }
    public interface IBoolValueEntry : IValueEntry<bool, ValueEntryBoolKey>
    {

    }
    #endregion
}

