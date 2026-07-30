using ES;
using Sirenix.Serialization.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// LinkReceiveList
    ///
    /// 针对单一 Link 类型的简单接收列表：
    /// - 内部使用 SafeNormalList 提供"延迟增删 + ApplyBuffers"机制，
    ///   保证在派发过程中也可以安全地添加 / 移除监听；
    /// - 适合用作本地事件总线或模块内部消息分发。
    /// </summary>
    /// <typeparam name="Link">传递的链接数据的类型。</typeparam>
    public sealed class LinkReceiveList<Link>
    {
        #region 字段 (Fields)

        /// <summary>
        /// 接收者列表，使用 SafeNormalList 支持派发期间安全增删。
        /// </summary>
        private readonly LinkSubscriptionList<IReceiveLink<Link>> _receivers;
        private readonly List<IPoolableAuto> _pendingRecycle = new List<IPoolableAuto>(4);

        #endregion

        public int SubscriberCount => _receivers.Count;

        public LinkReceiveList(int receiverCapacity = 4)
        {
            _receivers = new LinkSubscriptionList<IReceiveLink<Link>>(receiverCapacity);
        }

        public void ReserveReceivers(int capacity) => _receivers.Reserve(capacity);

        #region 核心功能 (Core Functionality)

        /// <summary>
        /// 发送链接通知。
        /// 通知所有有效的接收者指定的链接数据。
        /// </summary>
        /// <param name="link">链接数据。</param>
        public void SendLink(Link link)
        {
            _receivers.BeginDispatch();
            RecyclePending();
            try
            {
                int count = _receivers.ValuesNow.Count;
                for (int i = 0; i < count; i++)
                {
                    IReceiveLink<Link> currentReceiver = _receivers.ValuesNow[i];
                    if (currentReceiver is UnityEngine.Object ob)
                    {
                        if (ob != null) currentReceiver.OnLink(link);
                        else _receivers.Remove(currentReceiver);
                    }
                    else if (currentReceiver != null) currentReceiver.OnLink(link);
                    else _receivers.Remove(currentReceiver);
                }
            }
            finally
            {
                _receivers.EndDispatch();
            }
        }

        #endregion
        #region 接收者管理 (Receiver Management)

        /// <summary>
        /// 尝试移除指定的接收者（内部使用）。
        /// </summary>
        /// <param name="receiver">要移除的接收者。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Internal_TryRemove(IReceiveLink<Link> receiver)
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
        /// 添加接收者。
        /// </summary>
        /// <param name="receiver">要添加的接收者。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AddReceiver(IReceiveLink<Link> receiver)
        {
            return _receivers.Add(receiver);
        }

        /// <summary>
        /// 移除接收者。
        /// </summary>
        /// <param name="receiver">要移除的接收者。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveReceiver(IReceiveLink<Link> receiver)
        {
            if (_receivers.Remove(receiver))
            {
                ScheduleRecycle(receiver);
                return true;
            }

            return false;
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

