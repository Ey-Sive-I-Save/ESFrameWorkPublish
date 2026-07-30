using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES{

    #region ========== 基础Link接收器 ==========

    /// <summary>
    /// 🎯 基础Link消息接收器
    /// 将 Action<Link> 适配为 IReceiveLink<Link> 接口
    /// 通过对象池复用，减少GC分配
    /// </summary>
    /// <typeparam name="Link">消息类型</typeparam>
    public sealed class ReceiveLink<Link> : IReceiveLink<Link>, IPoolableAuto
    {
        /// <summary>
        /// 📦 对象池单例 - 自动管理实例复用
        /// </summary>
        public static ESSimplePool<ReceiveLink<Link>> poolSingleton = new ESSimplePool<ReceiveLink<Link>>(() => new ReceiveLink<Link>(null));

        /// <summary>
        /// 🎯 核心Action委托 - 实际的消息处理逻辑
        /// </summary>
        public Action<Link> action;

        /// <summary>
        /// ♻️ 回收状态标记
        /// </summary>
        public bool IsRecycled { get; set; }

        /// <summary>
        /// 📨 接收Link消息
        /// </summary>
        /// <param name="link">接收到的消息</param>
        public void OnLink(Link link)
        {
            action?.Invoke(link);
        }

        /// <summary>
        /// 🔄 重置为池化状态
        /// </summary>
        public void OnResetAsPoolable()
        {
            action = null;
        }

        /// <summary>
        /// ♻️ 尝试自动推入对象池
        /// </summary>
        public void TryAutoPushedToPool()
        {
            poolSingleton.PushToPool(this);
        }

        /// <summary>
        /// 🏗️ 构造器
        /// </summary>
        /// <param name="action">消息处理Action</param>
        public ReceiveLink(Action<Link> action)
        {
            this.action = action;
        }

        /// <summary>
        /// 🔄 隐式转换操作符 - 语法糖
        /// Action<Link> 自动转换为 ReceiveLink<Link>
        /// </summary>
        public static implicit operator ReceiveLink<Link>(Action<Link> action)
        {
            var rl = poolSingleton.GetInPool();
            rl.action = action;
            return rl;
        }

        /// <summary>
        /// 🔍 相等性比较 - 基于Action委托
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is ReceiveLink<Link> rl)
            {
                return rl?.action == action;
            }
            return base.Equals(obj);
        }

        /// <summary>
        /// 🏷️ 哈希码 - 基于Action委托
        /// </summary>
        public override int GetHashCode()
        {
            return action?.GetHashCode() ?? 0;
        }
    }

    #endregion

    #region ========== 状态变化Link接收器 ==========

    /// <summary>
    /// 🔄 状态变化Link接收器
    /// 将 Action<LinkFlag, LinkFlag> 适配为 IReceiveStateLink<LinkFlag> 接口
    /// 专门处理前后状态变化的消息
    /// </summary>
    /// <typeparam name="LinkFlag">状态类型</typeparam>
    public sealed class ReceiveStateLink<LinkFlag> : IReceiveStateLink<LinkFlag>, IPoolableAuto
    {
        /// <summary>
        /// 📦 对象池单例 - 自动管理实例复用
        /// </summary>
        public static ESSimplePool<ReceiveStateLink<LinkFlag>> poolSingleton = new ESSimplePool<ReceiveStateLink<LinkFlag>>(() => new ReceiveStateLink<LinkFlag>(null));

        /// <summary>
        /// 🔄 状态变化Action委托 - 处理前后状态
        /// </summary>
        public Action<LinkFlag, LinkFlag> action;

        /// <summary>
        /// ♻️ 回收状态标记
        /// </summary>
        public bool IsRecycled { get; set; }

        /// <summary>
        /// 📊 接收状态变化消息
        /// </summary>
        /// <param name="ago">之前的状态</param>
        /// <param name="now">当前的状态</param>
        public void OnLink(LinkFlag ago, LinkFlag now)
        {
            action?.Invoke(ago, now);
        }

        /// <summary>
        /// 🔄 重置为池化状态
        /// </summary>
        public void OnResetAsPoolable()
        {
            action = null;
        }

        /// <summary>
        /// ♻️ 尝试自动推入对象池
        /// </summary>
        public void TryAutoPushedToPool()
        {
            poolSingleton.PushToPool(this);
        }

        /// <summary>
        /// 🏗️ 构造器
        /// </summary>
        /// <param name="action">状态变化处理Action</param>
        public ReceiveStateLink(Action<LinkFlag, LinkFlag> action)
        {
            this.action = action;
        }

        /// <summary>
        /// 🔄 隐式转换操作符 - 语法糖
        /// Action<LinkFlag, LinkFlag> 自动转换为 ReceiveFlagLink<LinkFlag>
        /// </summary>
        public static implicit operator ReceiveStateLink<LinkFlag>(Action<LinkFlag, LinkFlag> action)
        {
            var rl = poolSingleton.GetInPool();
            rl.action = action;
            return rl;
        }

        /// <summary>
        /// 🔍 相等性比较 - 基于Action委托
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is ReceiveStateLink<LinkFlag> rl)
            {
                return rl?.action == action;
            }
            return base.Equals(obj);
        }

        /// <summary>
        /// 🏷️ 哈希码 - 基于Action委托
        /// </summary>
        public override int GetHashCode()
        {
            return action?.GetHashCode() ?? 0;
        }
    }

    #endregion

    #region ========== 通道Link接收器 ==========

    /// <summary>
    /// 📡 通道Link接收器
    /// 将 Action<Channel, Link> 适配为 IReceiveChannelLink<Channel, Link> 接口
    /// 支持多通道消息路由和分发
    /// </summary>
    /// <typeparam name="Channel">通道类型</typeparam>
    /// <typeparam name="Link">消息类型</typeparam>
    public sealed class ReceiveChannelLink<Channel, Link> : IReceiveChannelLink<Channel, Link>, IPoolableAuto
    {
        /// <summary>
        /// 📦 对象池单例 - 自动管理实例复用
        /// </summary>
        public static ESSimplePool<ReceiveChannelLink<Channel, Link>> poolSingleton = new ESSimplePool<ReceiveChannelLink<Channel, Link>>(() => new ReceiveChannelLink<Channel, Link>(null, default));

        /// <summary>
        /// ♻️ 回收状态标记
        /// </summary>
        public bool IsRecycled { get; set; }

        /// <summary>
        /// 📡 通道消息Action委托 - 处理通道和消息
        /// </summary>
        public Action<Channel, Link> action;

        /// <summary>
        /// 🎯 默认通道 - 当只有消息没有通道时使用
        /// </summary>
        public Channel defaultChannel;

        /// <summary>
        /// 📨 接收消息（使用默认通道）
        /// </summary>
        /// <param name="link">接收到的消息</param>
        public void OnLink(Link link)
        {
            // 🎯 使用明确的默认channel，而不是default(Channel)
            action?.Invoke(defaultChannel, link);
        }

        /// <summary>
        /// 📡 接收通道消息
        /// </summary>
        /// <param name="channel">消息通道</param>
        /// <param name="link">接收到的消息</param>
        public void OnLink(Channel channel, Link link)
        {
            action?.Invoke(channel, link);
        }

        /// <summary>
        /// 🔄 重置为池化状态
        /// </summary>
        public void OnResetAsPoolable()
        {
            action = null;
            defaultChannel = default;
        }

        /// <summary>
        /// ♻️ 尝试自动推入对象池
        /// </summary>
        public void TryAutoPushedToPool()
        {
            poolSingleton.PushToPool(this);
        }

        /// <summary>
        /// 🏗️ 构造器
        /// </summary>
        /// <param name="action">通道消息处理Action</param>
        /// <param name="defaultChannel">默认通道值</param>
        public ReceiveChannelLink(Action<Channel, Link> action, Channel defaultChannel = default)
        {
            this.action = action;
            this.defaultChannel = defaultChannel;
        }

        /// <summary>
        /// 🔄 隐式转换操作符 - 语法糖
        /// Action<Channel, Link> 自动转换为 ReceiveChannelLink<Channel, Link>
        /// </summary>
        public static implicit operator ReceiveChannelLink<Channel, Link>(Action<Channel, Link> action)
        {
            var rl = poolSingleton.GetInPool();
            rl.action = action;
            rl.defaultChannel = default; // 🎯 可以在这里设置特定的默认值
            return rl;
        }

        /// <summary>
        /// 🔍 相等性比较 - 基于Action委托
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is ReceiveChannelLink<Channel, Link> rl)
            {
                return rl?.action == action;
            }
            return base.Equals(obj);
        }

        /// <summary>
        /// 🏷️ 哈希码 - 基于Action委托
        /// </summary>
        public override int GetHashCode()
        {
            return action?.GetHashCode() ?? 0;
        }
    }
    #endregion


 #region ========== 无参数Link接收器 ==========

    /// <summary>
    /// 🔔 无参数Link接收器
    /// 将 Action 适配为 IReceiveLinkNoParam 接口
    /// 通过对象池复用，减少GC分配
    /// </summary>
    public sealed class ReceiveLinkNoParam : IReceiveLinkNoParam, IPoolableAuto
    {
        /// <summary>
        /// 📦 对象池单例 - 自动管理实例复用
        /// </summary>
        public static ESSimplePool<ReceiveLinkNoParam> poolSingleton = new ESSimplePool<ReceiveLinkNoParam>(() => new ReceiveLinkNoParam(null),initCount:50,maxCount:500);

        /// <summary>
        /// 🔔 核心Action委托 - 无参数的消息处理逻辑
        /// </summary>
        public Action action;

        /// <summary>
        /// ♻️ 回收状态标记
        /// </summary>
        public bool IsRecycled { get; set; }

        /// <summary>
        /// 📡 接收无参数Link消息
        /// </summary>
        public void OnLink()
        {
            action?.Invoke();
        }

        /// <summary>
        /// 🔄 重置为池化状态
        /// </summary>
        public void OnResetAsPoolable()
        {
            action = null;
        }

        /// <summary>
        /// ♻️ 尝试自动推入对象池
        /// </summary>
        public void TryAutoPushedToPool()
        {
            poolSingleton.PushToPool(this);
        }

        /// <summary>
        /// 🏗️ 构造器
        /// </summary>
        /// <param name="action">无参数消息处理Action</param>
        public ReceiveLinkNoParam(Action action)
        {
            this.action = action;
        }

        /// <summary>
        /// 🔄 隐式转换操作符 - 语法糖
        /// Action 自动转换为 ReceiveLinkNoParam
        /// </summary>
        public static implicit operator ReceiveLinkNoParam(Action action)
        {
            var rl = poolSingleton.GetInPool();
            rl.action = action;
            return rl;
        }

        /// <summary>
        /// 🔍 相等性比较 - 基于Action委托
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is ReceiveLinkNoParam rl)
            {
                return rl?.action == action;
            }
            return base.Equals(obj);
        }

        /// <summary>
        /// 🏷️ 哈希码 - 基于Action委托
        /// </summary>
        public override int GetHashCode()
        {
            return action?.GetHashCode() ?? 0;
        }
    }

    #endregion
    
    
       /// <summary>
    /// 🛠️ Link接收器创建扩展方法
    /// 提供流畅的API用于创建各种类型的接收器
    /// </summary>
    public static class ReceiveLinkMaker
    {
        /// <summary>
        /// 🎯 创建基础Link接收器
        /// </summary>
        /// <typeparam name="LinkType">消息类型</typeparam>
        /// <param name="action">消息处理Action</param>
        /// <returns>配置好的接收器实例</returns>
        public static ReceiveLink<LinkType> MakeReceive<LinkType>(this Action<LinkType> action)
        {
            var rl = ReceiveLink<LinkType>.poolSingleton.GetInPool();
            rl.action = action;
            return rl;
        }

        /// <summary>
        /// 📡 创建通道Link接收器
        /// </summary>
        /// <typeparam name="Channel">通道类型</typeparam>
        /// <typeparam name="LinkType">消息类型</typeparam>
        /// <param name="action">通道消息处理Action</param>
        /// <param name="defaultChannel">默认通道值</param>
        /// <returns>配置好的接收器实例</returns>
        public static ReceiveChannelLink<Channel, LinkType> MakeReceive<Channel, LinkType>(this Action<Channel, LinkType> action, Channel defaultChannel = default)
        {
            var rl = ReceiveChannelLink<Channel, LinkType>.poolSingleton.GetInPool();
            rl.action = action;
            rl.defaultChannel = defaultChannel;
            return rl;
        }

        /// <summary>
        /// 🔄 创建状态变化Link接收器
        /// </summary>
        /// <typeparam name="LinkState">状态类型</typeparam>
        /// <param name="action">状态变化处理Action</param>
        /// <returns>配置好的接收器实例</returns>
        public static ReceiveStateLink<LinkState> MakeReceive<LinkState>(this Action<LinkState, LinkState> action)
        {
            var rl = ReceiveStateLink<LinkState>.poolSingleton.GetInPool();
            rl.action = action;
            return rl;
        }

        /// <summary>
        /// 🔔 创建无参数Link接收器
        /// </summary>
        /// <param name="action">无参数消息处理Action</param>
        /// <returns>配置好的接收器实例</returns>
        public static ReceiveLinkNoParam MakeReceive(this Action action)
        {
            var rl = ReceiveLinkNoParam.poolSingleton.GetInPool();
            rl.action = action;
            return rl;
        }
    }
}
