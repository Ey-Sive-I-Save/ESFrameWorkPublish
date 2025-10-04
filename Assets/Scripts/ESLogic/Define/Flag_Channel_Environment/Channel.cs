using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;



//只是一种定义 用来规范架构 师出有名 没有实际逻辑
/* Channel非常简单 ，一般就是一个带类型标识的类
 * 他旨在为同参数，同类型的事件进行一个特征鲜明的初步划分
 * 用来减少频繁事件的遍历if 损耗 和 用于决定接下来的任务走向
 * 主要的 和 Link收发事件  集成
 *  由于 逆变不支持 数据类型 所以推荐使用的是class static channel 的形式
 * 一般只对于设计层进行 大部分的零碎枚举 分布在各自的文件或者EnumCollect里
 */
public interface IChannel
{

}
public class Channel<T> : IChannel where T : Channel<T>,new()
{
    public static T channel = new T();
    
}
#region 示范以类定义--支持协变
public class Channel_Example_ItemGetter_ : IChannel
{
    public static Channel_Example_ItemGetter_ GetByPick = new Channel_Example_ItemGetter_();
    public static Channel_Example_ItemGetter_ GetBySplit = new Channel_Example_ItemGetter_();
    public static Channel_Example_ItemGetter_ GetByBuy = new Channel_Example_ItemGetter_();
    public static Channel_Example_ItemGetter_ GetByReward = new Channel_Example_ItemGetter_();
    public static Channel_Example_ItemGetter_ GetByOtherGive = new Channel_Example_ItemGetter_();
}

#endregion


#region 枚举式
//例子 收集物品时 物品获取来源
public enum Channel_Example_ItemGetter
{
    [InspectorName("拾取的")]GetByPick,
    [InspectorName("分解的")] GetBySplit,
    [InspectorName("购买的")] GetByBuy,
    [InspectorName("奖励的")] GetByReward,
    [InspectorName("被赠与的")] GetByOtherGive
}
#endregion


