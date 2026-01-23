using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// LinkReceivePool
    ///
    /// 多类型 Link 接收者池，按 Link 类型分组管理接收者集合。
    /// 功能特性：
    /// - 继承 SafeKeyGroup<Type, IReceiveLink>，按类型 (Type) 分组存储接收者；
    /// - 支持多类型并发分发，每个类型独立管理接收者列表；
    /// - 自动清理已销毁的 Unity 对象接收者；
    /// - 适用于需要按类型分类管理事件监听的复杂系统。
    /// </summary>
    [Serializable]
    public class LinkReceivePool : SafeKeyGroup<Type, IReceiveLink> /**/
    {
        public override string Editor_ShowDes => "Link收发安全键组";

        #region 核心功能 (Core Functionality)

        /// <summary>
        /// 发送指定类型的链接通知。
        /// 通知该类型下所有有效的接收者。
        /// </summary>
        /// <typeparam name="Link">链接数据的类型。</typeparam>
        /// <param name="link">链接数据。</param>
        public void SendLink<Link>(Link link)
        {
            var links = GetGroupDirectly(typeof(Link));
            links.ApplyBuffers();
            int count = links.ValuesNow.Count;
            for (int i = 0; i < count; i++)
            {
                if (links.ValuesNow[i] is IReceiveLink<Link> irl)
                {
                    if (irl is UnityEngine.Object ob)
                    {
                        if (ob != null) irl.OnLink(link);
                        else links.Remove(irl);
                    }
                    else if (irl != null) irl.OnLink(link);
                }
                else Remove(typeof(Link), null);
            }
        }

        #endregion

        #region 接收者管理 (Receiver Management)

        /// <summary>
        /// 添加指定类型的接收者。
        /// </summary>
        /// <typeparam name="Link">链接数据的类型。</typeparam>
        /// <param name="receiver">要添加的接收者。</param>
        public void AddReceiver<Link>(IReceiveLink<Link> receiver)
        {
            Add(typeof(Link), receiver);
        }

        /// <summary>
        /// 移除指定类型的接收者。
        /// </summary>
        /// <typeparam name="Link">链接数据的类型。</typeparam>
        /// <param name="receiver">要移除的接收者。</param>
        public void RemoveReceiver<Link>(IReceiveLink<Link> receiver)
        {
            Remove(typeof(Link), receiver);
        }

        /// <summary>
        /// 添加基于 Action 的指定类型接收者。
        /// </summary>
        /// <typeparam name="Link">链接数据的类型。</typeparam>
        /// <param name="action">要添加的 Action 委托。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddReceiver<Link>(Action<Link> action)
        {
            Add(typeof(Link), action.MakeReceive());
        }

        /// <summary>
        /// 移除基于 Action 的指定类型接收者。
        /// </summary>
        /// <typeparam name="Link">链接数据的类型。</typeparam>
        /// <param name="action">要移除的 Action 委托。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveReceiver<Link>(Action<Link> action)
        {
            Remove(typeof(Link), action.MakeReceive());
        }

        #endregion
    }
}

