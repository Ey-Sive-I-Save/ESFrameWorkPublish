using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 按 Link 类型分组的低频扩展事件池。
    /// 它只用于 Boot、插件和诊断等明确域的低频消息，不能替代业务主链或高频数据通道。
    /// </summary>
    [Serializable]
    public sealed class LinkReceivePool
    {
        private const int DefaultTypeCapacity = 8;
        private const int DefaultReceiverCapacity = 4;

        private readonly Dictionary<Type, LinkSubscriptionList<IReceiveLink>> receiversByType;
        private readonly int receiverCapacity;

        public int MessageTypeCount => receiversByType.Count;

        public LinkReceivePool(int typeCapacity = DefaultTypeCapacity, int receiverCapacity = DefaultReceiverCapacity)
        {
            if (typeCapacity < 0) throw new ArgumentOutOfRangeException(nameof(typeCapacity));
            if (receiverCapacity < 0) throw new ArgumentOutOfRangeException(nameof(receiverCapacity));

            receiversByType = new Dictionary<Type, LinkSubscriptionList<IReceiveLink>>(typeCapacity);
            this.receiverCapacity = receiverCapacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AddReceiver<Link>(IReceiveLink<Link> receiver)
        {
            if (receiver == null)
                return false;

            Type linkType = typeof(Link);
            if (!receiversByType.TryGetValue(linkType, out LinkSubscriptionList<IReceiveLink> receivers))
            {
                receivers = new LinkSubscriptionList<IReceiveLink>(receiverCapacity);
                receiversByType.Add(linkType, receivers);
            }

            return receivers.Add(receiver);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveReceiver<Link>(IReceiveLink<Link> receiver)
        {
            return receiver != null
                && receiversByType.TryGetValue(typeof(Link), out LinkSubscriptionList<IReceiveLink> receivers)
                && receivers.Remove(receiver);
        }

        /// <summary>
        /// 向指定消息类型的当前订阅快照同步派发。异常直接向上传播。
        /// </summary>
        public void SendLink<Link>(Link link)
        {
            if (!receiversByType.TryGetValue(typeof(Link), out LinkSubscriptionList<IReceiveLink> receivers))
                return;

            receivers.BeginDispatch();
            try
            {
                int count = receivers.ValuesNow.Count;
                for (int i = 0; i < count; i++)
                {
                    IReceiveLink receiver = receivers.ValuesNow[i];
                    if (receiver is IReceiveLink<Link> typedReceiver)
                    {
                        if (typedReceiver is UnityEngine.Object unityObject)
                        {
                            if (unityObject != null)
                                typedReceiver.OnLink(link);
                            else
                                receivers.Remove(receiver);
                        }
                        else
                        {
                            typedReceiver.OnLink(link);
                        }
                    }
                    else
                    {
                        // 类型桶只能由 AddReceiver<Link> 写入；出现不匹配表示调用方破坏了契约。
                        receivers.Remove(receiver);
                    }
                }
            }
            finally
            {
                receivers.EndDispatch();
            }
        }

        public void ReserveMessageTypes(int capacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            receiversByType.EnsureCapacity(capacity);
        }

        /// <summary>为一个已知消息类型预建接收者表，避免首个订阅发生分配。</summary>
        public void ReserveMessageType<Link>(int capacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Type linkType = typeof(Link);
            if (!receiversByType.TryGetValue(linkType, out LinkSubscriptionList<IReceiveLink> receivers))
            {
                receiversByType.Add(linkType, new LinkSubscriptionList<IReceiveLink>(capacity));
                return;
            }

            receivers.Reserve(capacity);
        }

        public int GetSubscriberCount<Link>()
        {
            return receiversByType.TryGetValue(typeof(Link), out LinkSubscriptionList<IReceiveLink> receivers)
                ? receivers.Count
                : 0;
        }

        public void ApplyBuffers()
        {
            foreach (LinkSubscriptionList<IReceiveLink> receivers in receiversByType.Values)
                receivers.ApplyBuffers();
        }

        public void Clear()
        {
            foreach (LinkSubscriptionList<IReceiveLink> receivers in receiversByType.Values)
                receivers.Clear();
        }
    }
}
