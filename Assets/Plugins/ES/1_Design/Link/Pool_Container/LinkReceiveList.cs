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
        private readonly List<IReceiveLink<Link>> _pendingRecycle = new List<IReceiveLink<Link>>(4);

        #endregion

        public int SubscriberCount => _receivers.Count;

        /// <summary>
        /// Optional diagnostics hook. A receiver failure is isolated and never interrupts this
        /// dispatch or a later receiver. The hook itself is also isolated.
        /// </summary>
        public Action<IReceiveLink<Link>, Link, Exception> OnReceiverException { get; set; }

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
            try
            {
                int count = _receivers.ValuesNow.Count;
                for (int i = 0; i < count; i++)
                {
                    IReceiveLink<Link> currentReceiver = _receivers.ValuesNow[i];
                    if (currentReceiver is UnityEngine.Object ob)
                    {
                        if (ob != null) NotifyReceiver(currentReceiver, link);
                        else _receivers.Remove(currentReceiver);
                    }
                    else if (currentReceiver != null) NotifyReceiver(currentReceiver, link);
                    else _receivers.Remove(currentReceiver);
                }
            }
            finally
            {
                _receivers.EndDispatch();
                RecyclePending();
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

        private void ScheduleRecycle(IReceiveLink<Link> receiver)
        {
            if (receiver is IPoolableAuto poolable && !poolable.IsRecycled)
            {
                _pendingRecycle.Add(receiver);
            }
        }

        private void RecyclePending()
        {
            int count = _pendingRecycle.Count;
            if (count == 0) return;
            for (int i = 0; i < count; i++)
            {
                IReceiveLink<Link> receiver = _pendingRecycle[i];
                if (IsCurrentlySubscribed(receiver))
                    continue;

                if (receiver is IPoolableAuto poolable && !poolable.IsRecycled)
                {
                    poolable.TryAutoPushedToPool();
                }
            }
            _pendingRecycle.Clear();
        }

        private bool IsCurrentlySubscribed(IReceiveLink<Link> receiver)
        {
            if (receiver == null)
                return false;

            List<IReceiveLink<Link>> current = _receivers.ValuesNow;
            for (int i = 0; i < current.Count; i++)
            {
                if (ReferenceEquals(current[i], receiver))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 添加接收者。
        /// </summary>
        /// <param name="receiver">要添加的接收者。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AddReceiver(IReceiveLink<Link> receiver)
        {
            if (!_receivers.Add(receiver))
                return false;

            // Outside dispatch, make the subscription effective immediately so callers can use
            // SubscriberCount as a reliable zero-subscriber fast path. During dispatch the shared
            // Link contract still defers the addition until the current round has completed.
            if (!_receivers.IsDispatching)
                _receivers.ApplyBuffers();
            return true;
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
                bool isDispatching = _receivers.IsDispatching;
                if (!isDispatching)
                    _receivers.ApplyBuffers();
                ScheduleRecycle(receiver);
                if (!isDispatching)
                    RecyclePending();
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
            bool isDispatching = _receivers.IsDispatching;
            if (!isDispatching)
                _receivers.ApplyBuffers();
            for (int i = 0; i < _receivers.ValuesNow.Count; i++)
            {
                IReceiveLink<Link> receiver = _receivers.ValuesNow[i];
                if (receiver is IPoolableAuto poolable && !poolable.IsRecycled)
                    _pendingRecycle.Add(receiver);
            }
            _receivers.Clear();
            if (!isDispatching)
                RecyclePending();
        }

        /// <summary>
        /// 手动应用缓冲区中的更改。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ApplyBuffers()
        {
            ApplyBuffersAndRecycle();
        }

        private void NotifyReceiver(IReceiveLink<Link> receiver, Link link)
        {
            try
            {
                receiver.OnLink(link);
            }
            catch (Exception exception)
            {
                try
                {
                    OnReceiverException?.Invoke(receiver, link, exception);
                }
                catch
                {
                    // Diagnostics must never affect the source event.
                }
            }
        }

        #endregion
    }
}

