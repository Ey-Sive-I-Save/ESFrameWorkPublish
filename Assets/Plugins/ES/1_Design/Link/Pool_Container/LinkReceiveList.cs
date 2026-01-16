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
    /// - 内部使用 SafeNormalList 提供“延迟增删 + ApplyBuffers”机制，
    ///   保证在派发过程中也可以安全地添加 / 移除监听；
    /// - 适合用作本地事件总线或模块内部消息分发。
    /// </summary>
    public class LinkReceiveList<Link> {  
        public SafeNormalList<IReceiveLink<Link>> IRS = new SafeNormalList<IReceiveLink<Link>>();
        public IReceiveLink<Link> cache;

        public void SendLink(Link link)
        {

            IRS.ApplyBuffers();

            int count = IRS.ValuesNow.Count;
            for (int i = 0; i < count; i++)
            {
                cache = IRS.ValuesNow[i];
                if(cache is UnityEngine.Object ob)
                {
                    if (ob != null) cache.OnLink(link);
                    else IRS.Remove(cache);
                }
                else if (cache != null) cache.OnLink(link);
                else IRS.Remove(cache);
            }
        }
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        private void Internal_TryRemove(IReceiveLink<Link> ir)
        {
            IRS.Remove(ir);
        }
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public void AddReceive(IReceiveLink<Link> e) 
        {
            IRS.Add(e);
        }
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public void RemoveReceive(IReceiveLink<Link> e)
        {
            IRS.Remove(e);
        }

        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public void AddReceive(Action<Link> e)
        {
            IRS.Add(e.MakeReceive());
        }
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public void RemoveReceive(Action<Link> e)
        {
            IRS.Remove(e.MakeReceive());
        }
    }
}

