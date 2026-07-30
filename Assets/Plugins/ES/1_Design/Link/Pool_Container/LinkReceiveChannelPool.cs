using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ES;
using UnityEngine;

/// <summary>
/// 按 Channel 精准分发的 Link 接收池。
/// 每个 Channel 的订阅者按引用身份去重；派发期间的增删统一在下一轮生效。
/// </summary>
public sealed class LinkReceiveChannelPool<Channel, Link>
{
    private const int DefaultChannelCapacity = 8;
    private const int DefaultReceiverCapacity = 4;

    private readonly Dictionary<Channel, LinkSubscriptionList<IReceiveChannelLink<Channel, Link>>> channelReceivers;
    private readonly int receiverCapacity;

    public int ChannelCount => channelReceivers.Count;

    public LinkReceiveChannelPool(int channelCapacity = DefaultChannelCapacity, int receiverCapacity = DefaultReceiverCapacity)
    {
        if (channelCapacity < 0) throw new ArgumentOutOfRangeException(nameof(channelCapacity));
        if (receiverCapacity < 0) throw new ArgumentOutOfRangeException(nameof(receiverCapacity));

        channelReceivers = new Dictionary<Channel, LinkSubscriptionList<IReceiveChannelLink<Channel, Link>>>(channelCapacity);
        this.receiverCapacity = receiverCapacity;
    }

    /// <summary>向指定 Channel 注册接收者。重复注册返回 false。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AddReceiver(Channel channel, IReceiveChannelLink<Channel, Link> receiver)
    {
        if (receiver == null)
            return false;

        if (!channelReceivers.TryGetValue(channel, out LinkSubscriptionList<IReceiveChannelLink<Channel, Link>> receivers))
        {
            receivers = new LinkSubscriptionList<IReceiveChannelLink<Channel, Link>>(receiverCapacity);
            channelReceivers.Add(channel, receivers);
        }

        return receivers.Add(receiver);
    }

    /// <summary>注销指定 Channel 的接收者。未注册时返回 false。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool RemoveReceiver(Channel channel, IReceiveChannelLink<Channel, Link> receiver)
    {
        return receiver != null
            && channelReceivers.TryGetValue(channel, out LinkSubscriptionList<IReceiveChannelLink<Channel, Link>> receivers)
            && receivers.Remove(receiver);
    }

    /// <summary>
    /// 仅向指定 Channel 的当前订阅快照派发。回调异常直接向上抛出，不在热路径隔离。
    /// </summary>
    public void SendLink(Channel channel, Link link)
    {
        if (!channelReceivers.TryGetValue(channel, out LinkSubscriptionList<IReceiveChannelLink<Channel, Link>> receivers))
            return;

        receivers.BeginDispatch();
        try
        {
            int count = receivers.ValuesNow.Count;
            for (int i = 0; i < count; i++)
            {
                IReceiveChannelLink<Channel, Link> receiver = receivers.ValuesNow[i];
                if (receiver is UnityEngine.Object unityObject)
                {
                    if (unityObject != null)
                        receiver.OnLink(channel, link);
                    else
                        receivers.Remove(receiver);
                }
                else if (receiver != null)
                {
                    receiver.OnLink(channel, link);
                }
                else
                {
                    receivers.Remove(receiver);
                }
            }
        }
        finally
        {
            receivers.EndDispatch();
        }
    }

    /// <summary>初始化阶段预留 Channel 字典容量，避免后续首次注册扩容。</summary>
    public void ReserveChannels(int capacity)
    {
        if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        channelReceivers.EnsureCapacity(capacity);
    }

    /// <summary>为一个已知 Channel 预建接收者表，后续该 Channel 的前 capacity 次注册不扩容。</summary>
    public void ReserveChannel(Channel channel, int capacity)
    {
        if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (!channelReceivers.TryGetValue(channel, out LinkSubscriptionList<IReceiveChannelLink<Channel, Link>> receivers))
        {
            receivers = new LinkSubscriptionList<IReceiveChannelLink<Channel, Link>>(capacity);
            channelReceivers.Add(channel, receivers);
            return;
        }

        receivers.Reserve(capacity);
    }

    public int GetSubscriberCount(Channel channel)
    {
        return channelReceivers.TryGetValue(channel, out LinkSubscriptionList<IReceiveChannelLink<Channel, Link>> receivers)
            ? receivers.Count
            : 0;
    }

    /// <summary>提交所有 Channel 的待变更订阅。通常无需手动调用，SendLink 会自动提交目标 Channel。</summary>
    public void ApplyBuffers()
    {
        foreach (LinkSubscriptionList<IReceiveChannelLink<Channel, Link>> receivers in channelReceivers.Values)
            receivers.ApplyBuffers();
    }

    /// <summary>
    /// Immediately commits queued subscriptions for one Channel when it is not currently being
    /// dispatched. Lifecycle owners use this on release so an inactive pooled receiver is not
    /// retained until an unrelated future event reaches the same Channel.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ApplyChannelBuffers(Channel channel)
    {
        if (channelReceivers.TryGetValue(channel, out LinkSubscriptionList<IReceiveChannelLink<Channel, Link>> receivers))
            receivers.ApplyBuffers();
    }

    /// <summary>清空所有订阅。若某个 Channel 正在派发，其清空将在下一轮生效。</summary>
    public void Clear()
    {
        foreach (LinkSubscriptionList<IReceiveChannelLink<Channel, Link>> receivers in channelReceivers.Values)
            receivers.Clear();
    }
}
