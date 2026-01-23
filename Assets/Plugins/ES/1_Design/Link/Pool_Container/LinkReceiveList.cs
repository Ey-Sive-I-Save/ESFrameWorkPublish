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
    public class LinkReceiveList<Link>
    {
        #region 字段 (Fields)

        /// <summary>
        /// 接收者列表，使用 SafeNormalList 确保线程安全。
        /// </summary>
        private SafeNormalList<IReceiveLink<Link>> _receivers = new SafeNormalList<IReceiveLink<Link>>();

        #endregion

        #region 核心功能 (Core Functionality)

        /// <summary>
        /// 发送链接通知。
        /// 通知所有有效的接收者指定的链接数据。
        /// </summary>
        /// <param name="link">链接数据。</param>
        public void SendLink(Link link)
        {
            _receivers.ApplyBuffers();

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

        #endregion
        #region 接收者管理 (Receiver Management)

        /// <summary>
        /// 尝试移除指定的接收者（内部使用）。
        /// </summary>
        /// <param name="receiver">要移除的接收者。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Internal_TryRemove(IReceiveLink<Link> receiver)
        {
            _receivers.Remove(receiver);
        }

        /// <summary>
        /// 添加接收者。
        /// </summary>
        /// <param name="receiver">要添加的接收者。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddReceiver(IReceiveLink<Link> receiver)
        {
            _receivers.Add(receiver);
        }

        /// <summary>
        /// 移除接收者。
        /// </summary>
        /// <param name="receiver">要移除的接收者。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveReceiver(IReceiveLink<Link> receiver)
        {
            _receivers.Remove(receiver);
        }

        /// <summary>
        /// 添加基于 Action 的接收者。
        /// </summary>
        /// <param name="action">要添加的 Action 委托。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddReceiver(Action<Link> action)
        {
            _receivers.Add(action.MakeReceive());
        }

        /// <summary>
        /// 移除基于 Action 的接收者。
        /// </summary>
        /// <param name="action">要移除的 Action 委托。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveReceiver(Action<Link> action)
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

