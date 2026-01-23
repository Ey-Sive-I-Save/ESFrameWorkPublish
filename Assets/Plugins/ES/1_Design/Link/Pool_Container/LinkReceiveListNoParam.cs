using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// LinkReceiveListNoParam
    ///
    /// 针对无参数 Link 的专用接收列表：
    /// - 专门为 IReceiveLinkNoParam 接口优化，无需传递参数；
    /// - 内部使用 SafeNormalList 提供"延迟增删 + ApplyBuffers"机制，
    ///   保证在派发过程中也可以安全地添加 / 移除监听；
    /// - 适合用作简单通知事件，如心跳、状态同步等纯通知场景。
    /// </summary>
    public class LinkReceiveListNoParam
    {
        #region 字段 (Fields)

        /// <summary>
        /// 接收者列表，使用 SafeNormalList 确保线程安全。
        /// </summary>
        private SafeNormalList<IReceiveLinkNoParam> _receivers = new SafeNormalList<IReceiveLinkNoParam>();

        #endregion

        #region 核心功能 (Core Functionality)

        /// <summary>
        /// 发送无参数链接通知。
        /// 通知所有有效的接收者，无需传递数据。
        /// </summary>
        public void SendLink()
        {
            _receivers.ApplyBuffers();

            int count = _receivers.ValuesNow.Count;
            for (int i = 0; i < count; i++)
            {
                IReceiveLinkNoParam currentReceiver = _receivers.ValuesNow[i];
                if (currentReceiver is UnityEngine.Object ob)
                {
                    if (ob != null) currentReceiver.OnLink();
                    else _receivers.Remove(currentReceiver);
                }
                else if (currentReceiver != null) currentReceiver.OnLink();
                else _receivers.Remove(currentReceiver);
            }
        }

        #endregion

        #region 接收者管理 (Receiver Management)

        /// <summary>
        /// 尝试移除指定的接收者（内部使用）。
        /// </summary>
        /// <param name="receiver">要移除的接收者。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Internal_TryRemove(IReceiveLinkNoParam receiver)
        {
            _receivers.Remove(receiver);
        }

        /// <summary>
        /// 添加无参数接收者。
        /// </summary>
        /// <param name="receiver">要添加的接收者。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddReceiver(IReceiveLinkNoParam receiver)
        {
            _receivers.Add(receiver);
        }

        /// <summary>
        /// 移除无参数接收者。
        /// </summary>
        /// <param name="receiver">要移除的接收者。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveReceiver(IReceiveLinkNoParam receiver)
        {
            _receivers.Remove(receiver);
        }

        /// <summary>
        /// 添加基于 Action 的无参数接收者。
        /// </summary>
        /// <param name="action">要添加的 Action 委托。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddReceiver(Action action)
        {
            _receivers.Add(action.MakeReceive());
        }

        /// <summary>
        /// 移除基于 Action 的无参数接收者。
        /// </summary>
        /// <param name="action">要移除的 Action 委托。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveReceiver(Action action)
        {
            _receivers.Remove(action.MakeReceive());
        }

        /// <summary>
        /// 清除所有接收者。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _receivers.Clear();
        }

        /// <summary>
        /// 手动应用缓冲区中的更改。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ApplyBuffers()
        {
            _receivers.ApplyBuffers();
        }

        #endregion
    }
}