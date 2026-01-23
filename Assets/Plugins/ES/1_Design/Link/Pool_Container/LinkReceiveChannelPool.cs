using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    /// 通道接收者分组，使用 SafeKeyGroup 按通道组织接收者列表，确保线程安全。
    /// </summary>
    [HideLabel]
    private SafeKeyGroup<Channel, IReceiveChannelLink<Channel, Link>> _channelReceivers = new SafeKeyGroup<Channel, IReceiveChannelLink<Channel, Link>>();

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
        _channelReceivers.ApplyBuffers();
        if (_channelReceivers.Groups.TryGetValue(channel, out var receivers))
        {
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
        _channelReceivers.Remove(channel, receiver);
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
        _channelReceivers.Remove(channel, receiver);
    }

    /// <summary>
    /// 添加基于 Action 的指定通道接收者。
    /// </summary>
    /// <param name="channel">通道标识。</param>
    /// <param name="action">要添加的 Action 委托。</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddReceiver(Channel channel, Action<Channel, Link> action)
    {
        _channelReceivers.Add(channel, action.MakeReceive());
    }

    /// <summary>
    /// 移除基于 Action 的指定通道接收者。
    /// </summary>
    /// <param name="channel">通道标识。</param>
    /// <param name="action">要移除的 Action 委托。</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveReceiver(Channel channel, Action<Channel, Link> action)
    {
        _channelReceivers.Remove(channel, action.MakeReceive());
    }

    /// <summary>
    /// 清除所有通道的接收者。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        _channelReceivers.Clear();
    }

    /// <summary>
    /// 手动应用缓冲区中的更改。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ApplyBuffers()
    {
        _channelReceivers.ApplyBuffers();
    }

    #endregion
}
