using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


namespace ES
{
    public class LinkReceiveChannelList<Channel, Link>
    {
        public SafeNormalList<IReceiveChannelLink<Channel, Link>> IRS = new SafeNormalList<IReceiveChannelLink<Channel, Link>>();
        public IReceiveChannelLink<Channel, Link> cache;

        public void SendLink(Channel channel, Link link)
        {
            IRS.ApplyBuffers();

            int count = IRS.ValuesNow.Count;
            for (int i = 0; i < count; i++)
            {
                cache = IRS.ValuesNow[i];
                if (cache is UnityEngine.Object ob)
                {
                    if (ob != null) cache.OnLink(channel, link);
                    else IRS.Remove(cache);
                }
                else if (cache != null) cache.OnLink(channel, link);
                else IRS.Remove(cache);
            }
        }
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        private void Internal_TryRemove(IReceiveChannelLink<Channel, Link> receive)
        {
            IRS.Remove(receive);
        }
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public void AddReceive(IReceiveChannelLink<Channel, Link> receive)
        {
            IRS.Add(receive);
        }
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public void RemoveReceive(IReceiveChannelLink<Channel, Link> receive)
        {
            IRS.Remove(receive);
        }
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public void AddReceive(Action<Channel, Link> e)
        {
            IRS.Add(e.MakeReceive());
        }
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public void RemoveReceive(Action<Channel, Link> e)
        {
            IRS.Remove(e.MakeReceive());
        }
    }
    //
}
