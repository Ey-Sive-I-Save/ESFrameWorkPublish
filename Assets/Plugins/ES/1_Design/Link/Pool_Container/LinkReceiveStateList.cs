using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// LinkStateReceiveList
    ///
    /// 状态型 Link 的订阅容器类，用于管理状态变化的通知。
    /// 功能特性：
    /// - 维护一个支持派发期间安全增删的接收者列表 (SafeNormalList)；
    /// - 当状态从 LastFlag 变化到新值时，通知所有监听者 (oldValue, newValue)；
    /// - 新增监听者会立即收到一次补发回调，以同步当前状态。
    /// </summary>
    /// <typeparam name="LinkState">状态的类型，必须支持 Equals 方法进行比较。</typeparam>
    public sealed class LinkStateReceiveList<LinkState>
    {
        #region 字段 (Fields)

        /// <summary>
        /// 接收者列表：引用身份去重，派发期间增删统一在下一轮生效。
        /// </summary>
        private readonly LinkSubscriptionList<IReceiveStateLink<LinkState>> _receivers;
        private readonly List<IPoolableAuto> _pendingRecycle = new List<IPoolableAuto>(4);

        /// <summary>
        /// 上一次发送的状态值，用于检测状态变化。
        /// </summary>
        public LinkState LastFlag;

        /// <summary>
        /// 默认状态值，用于初始化和移除接收者时的重置。
        /// </summary>
        public LinkState DefaultFlag = default;

        #endregion

        public int SubscriberCount => _receivers.Count;

        public LinkStateReceiveList(int receiverCapacity = 4)
        {
            _receivers = new LinkSubscriptionList<IReceiveStateLink<LinkState>>(receiverCapacity);
        }

        public void ReserveReceivers(int capacity) => _receivers.Reserve(capacity);

        #region 初始化 (Initialization)

        /// <summary>
        /// 初始化容器，设置默认状态值。
        /// </summary>
        /// <param name="defaultFlag">默认状态值。</param>
        public void Init(LinkState defaultFlag)
        {
            DefaultFlag = defaultFlag;
            LastFlag = defaultFlag;
        }

        #endregion

        #region 核心功能 (Core Functionality)

        /// <summary>
        /// 发送状态变化通知。
        /// 如果新状态与上一次不同，则通知所有有效的接收者。
        /// </summary>
        /// <param name="link">新的状态值。</param>
        public void SendLink(LinkState link)
        {
            if (EqualityComparer<LinkState>.Default.Equals(LastFlag, link))
                return;

            LinkState previous = LastFlag;
            // 状态权威必须在任何回调前提交，回调重入时读取到的始终是新状态。
            LastFlag = link;
            _receivers.BeginDispatch();
            RecyclePending();
            try
            {
                int count = _receivers.ValuesNow.Count;
                for (int i = 0; i < count; i++)
                {
                    IReceiveStateLink<LinkState> currentReceiver = _receivers.ValuesNow[i];
                    if (currentReceiver is UnityEngine.Object ob)
                    {
                        if (ob != null) currentReceiver.OnLink(previous, link);
                        else _receivers.Remove(currentReceiver);
                    }
                    else if (currentReceiver != null) currentReceiver.OnLink(previous, link);
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
        /// <param name="ir">要移除的接收者。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Internal_TryRemove(IReceiveStateLink<LinkState> ir)
        {
            _receivers.Remove(ir);
            ScheduleRecycle(ir);
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
        /// 添加状态接收者。重复订阅被拒绝；非派发期间注册会立即同步当前状态。
        /// 派发期间注册仍严格在下一轮生效，不会插入当前快照。
        /// </summary>
        /// <param name="e">要添加的接收者。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AddReceiver(IReceiveStateLink<LinkState> e)
        {
            if (!_receivers.Add(e))
                return false;

            if (!_receivers.IsDispatching)
            {
                _receivers.ApplyBuffers();
                e.OnLink(DefaultFlag, LastFlag);
            }

            return true;
        }

        /// <summary>
        /// 移除状态接收者。移除不修改全局状态。
        /// </summary>
        /// <param name="e">要移除的接收者。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveReceiver(IReceiveStateLink<LinkState> e)
        {
            if (!_receivers.Remove(e))
                return false;
            ScheduleRecycle(e);
            return true;
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
