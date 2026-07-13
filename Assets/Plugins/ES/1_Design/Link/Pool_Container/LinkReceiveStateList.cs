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
    public class LinkStateReceiveList<LinkState>
    {
        #region 字段 (Fields)

        /// <summary>
        /// 接收者列表，使用 SafeNormalList 支持派发期间安全增删。
        /// </summary>
        private SafeNormalList<IReceiveStateLink<LinkState>> _receivers = new SafeNormalList<IReceiveStateLink<LinkState>>();
        private readonly List<IPoolableAuto> _pendingRecycle = new List<IPoolableAuto>();
        private readonly List<ReceiveStateLink<LinkState>> _actionReceivers = new List<ReceiveStateLink<LinkState>>();

        /// <summary>
        /// 上一次发送的状态值，用于检测状态变化。
        /// </summary>
        public LinkState LastFlag;

        /// <summary>
        /// 默认状态值，用于初始化和移除接收者时的重置。
        /// </summary>
        public LinkState DefaultFlag = default;

        #endregion

        #region 初始化 (Initialization)

        /// <summary>
        /// 初始化容器，设置默认状态值。
        /// </summary>
        /// <param name="defaultFlag">默认状态值。</param>
        public void Init(LinkState defaultFlag)
        {
            DefaultFlag = defaultFlag;
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
            ApplyBuffersAndRecycle();
            if (!EqualityComparer<LinkState>.Default.Equals(LastFlag, link))
            {
                int count = _receivers.ValuesNow.Count;
                for (int i = 0; i < count; i++)
                {
                    IReceiveStateLink<LinkState> currentReceiver = _receivers.ValuesNow[i];
                    if (currentReceiver is UnityEngine.Object ob)
                    {
                        if (ob != null) currentReceiver.OnLink(LastFlag, link);
                        else _receivers.Remove(currentReceiver);
                    }
                    else if (currentReceiver != null) currentReceiver.OnLink(LastFlag, link);
                    else _receivers.Remove(currentReceiver);
                }
                LastFlag = link;
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
        /// 添加状态接收者，并同步当前状态。
        /// 如果当前状态与最后状态不同，会立即触发一次回调。
        /// </summary>
        /// <param name="nowFlag">当前状态的引用，会被更新为最后状态。</param>
        /// <param name="e">要添加的接收者。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddReceiver(ref LinkState nowFlag, IReceiveStateLink<LinkState> e)
        {
            if (EqualityComparer<LinkState>.Default.Equals(nowFlag, LastFlag))
            {
                // 状态一致，无需补发
            }
            else
            {
                e.OnLink(nowFlag, LastFlag);
                nowFlag = LastFlag;
            }
            _receivers.Add(e);
        }

        /// <summary>
        /// 移除状态接收者，并重置状态为默认值。
        /// </summary>
        /// <param name="nowFlag">当前状态的引用，会被重置为默认状态。</param>
        /// <param name="e">要移除的接收者。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveReceiver(ref LinkState nowFlag, IReceiveStateLink<LinkState> e)
        {
            if (EqualityComparer<LinkState>.Default.Equals(nowFlag, DefaultFlag))
            {
                // 已是默认状态，无需重置
            }
            else
            {
                nowFlag = DefaultFlag;
                e.OnLink(DefaultFlag);
            }
            _receivers.Remove(e);
            ScheduleRecycle(e);
        }

        /// <summary>
        /// 添加基于 Action 的状态接收者。
        /// </summary>
        /// <param name="nowFlag">当前状态的引用。</param>
        /// <param name="e">要添加的 Action 委托。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddReceiver(ref LinkState nowFlag, Action<LinkState, LinkState> e)
        {
            var receiver = e.MakeReceive<LinkState>();
            _actionReceivers.Add(receiver);
            AddReceiver(ref nowFlag, receiver);
        }

        /// <summary>
        /// 移除基于 Action 的状态接收者。
        /// </summary>
        /// <param name="nowFlag">当前状态的引用。</param>
        /// <param name="e">要移除的 Action 委托。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveReceiver(ref LinkState nowFlag, Action<LinkState, LinkState> e)
        {
            for (int i = _actionReceivers.Count - 1; i >= 0; i--)
            {
                var receiver = _actionReceivers[i];
                if (receiver.action == e)
                {
                    _actionReceivers.RemoveAt(i);
                    RemoveReceiver(ref nowFlag, receiver);
                    return;
                }
            }

            for (int i = 0; i < _receivers.ValuesNow.Count; i++)
            {
                var receiver = _receivers.ValuesNow[i];
                if (receiver is ReceiveStateLink<LinkState> receiveLink && receiveLink.action == e)
                {
                    RemoveReceiver(ref nowFlag, receiver);
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
