using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
#if UNITY_EDITOR
#endif


/*Channel 只是一个枚举或者静态类*/
[Serializable]
public class LinkReceiveChannelPool<Channel,Link> 
{
    [HideLabel]
    public SafeKeyGroup<Channel, IReceiveChannelLink<Channel,Link>> CIRS = new SafeKeyGroup<Channel, IReceiveChannelLink<Channel, Link>>();
    public IReceiveChannelLink<Channel,Link> cache;
    public void SendLink(Channel c,Link link)
    {
        CIRS.ApplyBuffers();
        if(CIRS.Groups.TryGetValue(c,out var irs))
        {
            int count = irs.ValuesNow.Count;
            for (int i = 0; i < count; i++)
            {
                cache = irs.ValuesNow[i];
                if (cache is UnityEngine.Object ob)
                {
                    if (ob != null) cache.OnLink(c,link);
                    else irs.Remove(cache);
                }
                else if (cache != null) cache.OnLink(c,link);
                else irs.Remove(cache);
            }
        }
    }
    [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
    private void Internal_TryRemove(Channel channel, IReceiveChannelLink<Channel,Link> ir)
    {
        CIRS.Remove(channel,ir);
    }
    [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
    public void AddReceive(Channel channel, IReceiveChannelLink<Channel,Link> e)
    {
        CIRS.Add(channel,e);
    }
    [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
    public void RemoveReceive(Channel channel, IReceiveChannelLink<Channel, Link> e)
    {
        CIRS.Remove(channel,e);
    }

    [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
    public void AddReceive(Channel channel, Action<Channel, Link> e)
    {
        CIRS.Add(channel, e.MakeReceive());
    }
    [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
    public void RemoveReceive(Channel channel, Action<Channel, Link> e)
    {
        CIRS.Remove(channel,e.MakeReceive());
    }
}
