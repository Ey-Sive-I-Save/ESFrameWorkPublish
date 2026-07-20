using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
#if UNITY_EDITOR
#endif

/*Channel 只是一个枚举或者静态类*/
/// <summary>
/// LinkReceiveChannelPool
///
/// 多通道 Link 接收者容器，按通道分组管理接收者集合。
/// 功能特性：
/// - 使用 SafeKeyGroup 按通道 (Channel) 分组存储接收者；
/// - 支持多通道并发分发，每个通道独立管理接收者列表；
/// - 自动清理已销毁的 Unity 对象接收者；
/// - 提供通道级别的添加/移除接收者操作；
/// - 适用于需要按通道分类管理事件监听的复杂系统。
/// </summary>
/// <typeparam name="Channel">通道标识的类型，通常为枚举。</typeparam>
/// <typeparam name="Link">传递的链接数据的类型。</typeparam>
[Serializable]
public class LinkReceiveChannelPool<Channel, Link>
{
    #region 字段 (Fields)

    /// <summary>
    /// 通道接收者分组，使用 SafeKeyGroup 按通道组织支持派发期间安全增删的接收者列表。
    /// </summary>
    [HideLabel]
    private SafeKeyGroup<Channel, IReceiveChannelLink<Channel, Link>> _channelReceivers = new SafeKeyGroup<Channel, IReceiveChannelLink<Channel, Link>>();
    private readonly List<IPoolableAuto> _pendingRecycle = new List<IPoolableAuto>(4);
    private readonly List<ActionReceiverRecord> _actionReceivers = new List<ActionReceiverRecord>(4);

    private struct ActionReceiverRecord
    {
        public Channel Channel;
        public ReceiveChannelLink<Channel, Link> Receiver;
    }

    public LinkReceiveChannelPool()
    {
        _channelReceivers.SetAutoCreateOnAccess(false);
    }

    #endregion

    #region 核心功能 (Core Functionality)

    /// <summary>
    /// 发送指定通道的链接通知。
    /// 通知该通道下所有有效的接收者。
    /// </summary>
    /// <param name="channel">目标通道。</param>
    /// <param name="link">链接数据。</param>
    public void SendLink(Channel channel, Link link)
    {
        if (!_channelReceivers.Groups.TryGetValue(channel, out var receivers))
        {
            RecyclePending();
            return;
        }

        receivers.ApplyBuffers();
        RecyclePending();
        int count = receivers.ValuesNow.Count;
        for (int i = 0; i < count; i++)
        {
            IReceiveChannelLink<Channel, Link> currentReceiver = receivers.ValuesNow[i];
            if (currentReceiver is UnityEngine.Object ob)
            {
                if (ob != null)
                {
                    currentReceiver.OnLink(channel, link);
                }
                else
                {
                    receivers.Remove(currentReceiver);
                }
            }
            else if (currentReceiver != null)
            {
                currentReceiver.OnLink(channel, link);
            }
            else
            {
                receivers.Remove(currentReceiver);
            }
        }
    }

    #endregion

    #region 接收者管理 (Receiver Management)

    /// <summary>
    /// 尝试移除指定通道的接收者（内部使用）。
    /// </summary>
    /// <param name="channel">通道标识。</param>
    /// <param name="receiver">要移除的接收者。</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Internal_TryRemove(Channel channel, IReceiveChannelLink<Channel, Link> receiver)
    {
        RemoveReceiver(channel, receiver);
    }

    private void ScheduleRecycle(object receiver)
    {
        if (receiver is IPoolableAuto poolable && !poolable.IsRecycled)
        {
            _pendingRecycle.Add(poolable);
        }
    }

    private void RecyclePending()
    {
        int count = _pendingRecycle.Count;
        if (count == 0) return;
        for (int i = 0; i < count; i++)
        {
            var poolable = _pendingRecycle[i];
            if (poolable != null && !poolable.IsRecycled)
            {
                poolable.TryAutoPushedToPool();
            }
        }
        _pendingRecycle.Clear();
    }

    /// <summary>
    /// 添加指定通道的接收者。
    /// </summary>
    /// <param name="channel">通道标识。</param>
    /// <param name="receiver">要添加的接收者。</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddReceiver(Channel channel, IReceiveChannelLink<Channel, Link> receiver)
    {
        _channelReceivers.Add(channel, receiver);
    }

    /// <summary>
    /// 移除指定通道的接收者。
    /// </summary>
    /// <param name="channel">通道标识。</param>
    /// <param name="receiver">要移除的接收者。</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveReceiver(Channel channel, IReceiveChannelLink<Channel, Link> receiver)
    {
        if (_channelReceivers.Groups.TryGetValue(channel, out var receivers))
        {
            receivers.Remove(receiver);
        }
        ScheduleRecycle(receiver);
    }

    /// <summary>
    /// 添加基于 Action 的指定通道接收者。
    /// </summary>
    /// <param name="channel">通道标识。</param>
    /// <param name="action">要添加的 Action 委托。</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddReceiver(Channel channel, Action<Channel, Link> action)
    {
        var receiver = action.MakeReceive();
        _actionReceivers.Add(new ActionReceiverRecord { Channel = channel, Receiver = receiver });
        _channelReceivers.Add(channel, receiver);
    }

    /// <summary>
    /// 移除基于 Action 的指定通道接收者。
    /// </summary>
    /// <param name="channel">通道标识。</param>
    /// <param name="action">要移除的 Action 委托。</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveReceiver(Channel channel, Action<Channel, Link> action)
    {
        for (int i = _actionReceivers.Count - 1; i >= 0; i--)
        {
            var record = _actionReceivers[i];
            if (EqualityComparer<Channel>.Default.Equals(record.Channel, channel) && record.Receiver.action == action)
            {
                _actionReceivers.RemoveAt(i);
                RemoveReceiver(channel, record.Receiver);
                return;
            }
        }

        if (_channelReceivers.Groups.TryGetValue(channel, out var receivers))
        {
            for (int i = 0; i < receivers.ValuesNow.Count; i++)
            {
                var receiver = receivers.ValuesNow[i];
                if (receiver is ReceiveChannelLink<Channel, Link> receiveLink && receiveLink.action == action)
                {
                    RemoveReceiver(channel, receiver);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 清除所有通道的接收者。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        _channelReceivers.ApplyBuffers();
        RecyclePending();
        foreach (var pair in _channelReceivers.Groups)
        {
            var receivers = pair.Value;
            for (int i = 0; i < receivers.ValuesNow.Count; i++)
            {
                if (receivers.ValuesNow[i] is IPoolableAuto poolable && !poolable.IsRecycled)
                {
                    poolable.TryAutoPushedToPool();
                }
            }
        }
        _channelReceivers.Clear();
        _actionReceivers.Clear();
    }

    /// <summary>
    /// 手动应用缓冲区中的更改。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ApplyBuffers()
    {
        _channelReceivers.ApplyBuffers();
        RecyclePending();
    }

    #endregion
}
