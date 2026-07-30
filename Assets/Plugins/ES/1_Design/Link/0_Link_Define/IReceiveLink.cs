using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Link 收信端标记接口。
    /// 
    /// Link 体系将“事件 / 消息”抽象为 Link，对应的接收方实现本接口族，
    /// 再由 LinkReceiveList / LinkReceivePool 等容器统一派发，
    /// 避免到处手写 C# 事件字段，便于做可视化与对象池优化。
    /// </summary>
    public interface IReceiveLink
    {

    }
    public interface IReceiveLink<in Link> : IReceiveLink
    {
        /// <summary>
        /// 收到一个 Link 消息。
        /// 约定：实现类应保持逻辑轻量，避免在回调中做阻塞 IO 或复杂控制流。
        /// </summary>
        void OnLink(Link link);

    }
    public interface IReceiveStateLink<in LinkState> : IReceiveLink<LinkState>
    {
        void OnLink(LinkState ago,LinkState now);
        void IReceiveLink<LinkState>.OnLink(LinkState now)
        {
            OnLink(default(LinkState), now);
        }
    }
    public interface IReceiveChannelLink<in Channel, in Link> : IReceiveLink<Link>
    {
        void OnLink(Channel channel, Link link);
        void IReceiveLink<Link>.OnLink(Link link)
        {
            OnLink(default(Channel), link);
        }
    }
    /// <summary>
    /// 🔔 无参数Link接收器 - 简单通知事件
    /// 用于不需要传递数据的纯通知场景，如心跳、状态同步等
    /// </summary>
    public interface IReceiveLinkNoParam : IReceiveLink
    {
        /// <summary>
        /// 📡 收到无参数Link消息
        /// 触发简单的通知或状态更新逻辑
        /// </summary>
        void OnLink();
    }

    /// <summary>
    /// ActiveLinkList 的标准接收协议。
    ///
    /// 一个接收者被某个 ActiveLinkList 实际激活或禁用时，分别只会收到一次对应回调。
    /// 接收者不保存“全局是否激活”的状态：同一对象可同时被多个 ActiveLinkList 持有，
    /// 某个列表中的状态应通过该 ActiveLinkList 的 IsActive 方法查询。
    /// </summary>
    public interface IReceiveActiveLink : IReceiveLink
    {
        /// <summary>
        /// 当前 ActiveLinkList 首次激活此接收者后调用。
        /// </summary>
        void OnLinkEnable();

        /// <summary>
        /// 当前 ActiveLinkList 实际禁用此接收者后调用。
        /// </summary>
        void OnLinkDisable();
    }
}
