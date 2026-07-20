using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


namespace ES
{
    /// <summary>
    /// LinkReceiveChannelList
    ///
    /// 通道型 Link 接收者容器，为单一通道管理一组接收者。
    /// 功能特性：
    /// - 维护一个支持派发期间安全增删的接收者列表 (SafeNormalList)；
    /// - 通过 SendLink 方法广播通道数据给所有有效接收者；
    /// - 自动清理已销毁的 Unity 对象接收者；
    /// - 支持接口和委托两种接收者类型；
    /// - 适用于需要统一管理特定通道监听者的场景。
    /// </summary>
    /// <typeparam name="Channel">通道标识的类型，通常为枚举或字符串。</typeparam>
    /// <typeparam name="Link">传递的链接数据的类型。</typeparam>
    public class LinkReceiveChannelList<Channel, Link>
    {
        #region 字段 (Fields)

        /// <summary>
        /// 接收者列表，使用 SafeNormalList 支持派发期间安全增删。
        /// </summary>
        private SafeNormalList<IReceiveChannelLink<Channel, Link>> _receivers = new SafeNormalList<IReceiveChannelLink<Channel, Link>>();
        private readonly List<IPoolableAuto> _pendingRecycle = new List<IPoolableAuto>(4);
        private readonly List<ReceiveChannelLink<Channel, Link>> _actionReceivers = new List<ReceiveChannelLink<Channel, Link>>(4);

        #endregion

        #region 核心功能 (Core Functionality)

        /// <summary>
        /// 发送通道链接通知。
        /// 通知所有有效的接收者指定的通道和链接数据。
        /// </summary>
        /// <param name="channel">通道标识。</param>
        /// <param name="link">链接数据。</param>
        public void SendLink(Channel channel, Link link)
        {
            ApplyBuffersAndRecycle();

            int count = _receivers.ValuesNow.Count;
            for (int i = 0; i < count; i++)
            {
                IReceiveChannelLink<Channel, Link> currentReceiver = _receivers.ValuesNow[i];
                if (currentReceiver is UnityEngine.Object ob)
                {
                    if (ob != null)
                    {
                        currentReceiver.OnLink(channel, link);
                    }
                    else
                    {
                        _receivers.Remove(currentReceiver);
                    }
                }
                else if (currentReceiver != null)
                {
                    currentReceiver.OnLink(channel, link);
                }
                else
                {
                    _receivers.Remove(currentReceiver);
                }
            }
        }

        #endregion
        #region 接收者管理 (Receiver Management)

        /// <summary>
        /// 尝试移除指定的接收者（内部使用）。
        /// </summary>
        /// <param name="receiver">要移除的接收者。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Internal_TryRemove(IReceiveChannelLink<Channel, Link> receiver)
        {
            RemoveReceiver(receiver);
        }

        private void ApplyBuffersAndRecycle()
        {
            _receivers.ApplyBuffers();
            RecyclePending();
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
        /// 添加通道接收者。
        /// </summary>
        /// <param name="receiver">要添加的接收者。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddReceiver(IReceiveChannelLink<Channel, Link> receiver)
        {
            _receivers.Add(receiver);
        }

        /// <summary>
        /// 移除通道接收者。
        /// </summary>
        /// <param name="receiver">要移除的接收者。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveReceiver(IReceiveChannelLink<Channel, Link> receiver)
        {
            _receivers.Remove(receiver);
            ScheduleRecycle(receiver);
        }

        /// <summary>
        /// 添加基于 Action 的通道接收者。
        /// </summary>
        /// <param name="action">要添加的 Action 委托。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddReceiver(Action<Channel, Link> action)
        {
            var receiver = action.MakeReceive();
            _actionReceivers.Add(receiver);
            _receivers.Add(receiver);
        }

        /// <summary>
        /// 移除基于 Action 的通道接收者。
        /// </summary>
        /// <param name="action">要移除的 Action 委托。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveReceiver(Action<Channel, Link> action)
        {
            for (int i = _actionReceivers.Count - 1; i >= 0; i--)
            {
                var receiver = _actionReceivers[i];
                if (receiver.action == action)
                {
                    _actionReceivers.RemoveAt(i);
                    RemoveReceiver(receiver);
                    return;
                }
            }

            for (int i = 0; i < _receivers.ValuesNow.Count; i++)
            {
                var receiver = _receivers.ValuesNow[i];
                if (receiver is ReceiveChannelLink<Channel, Link> receiveLink && receiveLink.action == action)
                {
                    RemoveReceiver(receiver);
                    return;
                }
            }
        }

        /// <summary>
        /// 清除所有接收者。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            ApplyBuffersAndRecycle();
            for (int i = 0; i < _receivers.ValuesNow.Count; i++)
            {
                if (_receivers.ValuesNow[i] is IPoolableAuto poolable && !poolable.IsRecycled)
                {
                    poolable.TryAutoPushedToPool();
                }
            }
            _receivers.Clear();
            _actionReceivers.Clear();
        }

        /// <summary>
        /// 手动应用缓冲区中的更改。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ApplyBuffers()
        {
            ApplyBuffersAndRecycle();
        }

        #endregion
    }
}
