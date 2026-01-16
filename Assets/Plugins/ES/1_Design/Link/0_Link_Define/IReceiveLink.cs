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
    public interface IReceiveFlagLink<in LinkFlag> : IReceiveLink<LinkFlag>
    {
        void OnLink(LinkFlag ago,LinkFlag now);
        void IReceiveLink<LinkFlag>.OnLink(LinkFlag now)
        {
            OnLink(default, now);
        }
    }
    public interface IReceiveChannelLink<in Channel, in Link> : IReceiveLink<Link>
    {
        void OnLink(Channel channel, Link link);
        void IReceiveLink<Link>.OnLink(Link link)
        {
            OnLink(default, link);
        }
    }
}