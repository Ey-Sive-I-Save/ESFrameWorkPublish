using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static TreeEditor.TreeGroup;

public class ReceiveLink<Link> : IReceiveLink<Link>,IPoolablebAuto
{
    public static ESSimplePool<ReceiveLink<Link>> poolSingleton = new ESSimplePool<ReceiveLink<Link>>(()=>new ReceiveLink<Link>(null));
    public Action<Link> action;

    public bool IsRecycled { get ; set ; }

    public void OnLink(Link link)
    {
        action?.Invoke(link);
    }

    public void OnResetAsPoolable()
    {
        action = null;
    }

    public void TryAutoPushedToPool()
    {
        poolSingleton.PushToPool(this); 
    }

    public ReceiveLink(Action<Link> action)
    {
        this.action = action;
    }
    public static implicit operator ReceiveLink<Link>(Action<Link> action)
    {
        var rl = poolSingleton.GetInPool();
            rl.action = action;
        return rl;
    }

    public override bool Equals(object obj)
    {
        if(obj is ReceiveLink<Link> rl)
        {
            return rl?.action == action;
        }
        return base.Equals(obj);
    }
    public override int GetHashCode()
    {
        return action?.GetHashCode()??0;
    }
}
public class ReceiveFlagLink<LinkFlag> : IReceiveFlagLink<LinkFlag>, IPoolablebAuto
{
    public static ESSimplePool<ReceiveFlagLink<LinkFlag>> poolSingleton = new ESSimplePool<ReceiveFlagLink<LinkFlag>>(() => new ReceiveFlagLink<LinkFlag>(null));
    public Action<LinkFlag, LinkFlag> action;

    public bool IsRecycled { get; set; }

    public void OnLink(LinkFlag ago, LinkFlag now)
    {
        action?.Invoke(ago,now);
    }

    public void OnResetAsPoolable()
    {
        action = null;
    }

    public void TryAutoPushedToPool()
    {
        poolSingleton.PushToPool(this);
    }

    public ReceiveFlagLink(Action<LinkFlag, LinkFlag> action)
    {
        this.action = action;
    }
    public static implicit operator ReceiveFlagLink<LinkFlag>(Action<LinkFlag, LinkFlag> action)
    {
        var rl = poolSingleton.GetInPool();
        rl.action = action;
        return rl;
    }

    public override bool Equals(object obj)
    {
        if (obj is ReceiveFlagLink<LinkFlag> rl)
        {
            return rl?.action == action;
        }
        return base.Equals(obj);
    }
    public override int GetHashCode()
    {
        return action?.GetHashCode() ?? 0;
    }
}
public class ReceiveChannelLink<Channel,Link> : IReceiveChannelLink<Channel, Link>, IPoolablebAuto
{
    public static ESSimplePool<ReceiveChannelLink<Channel, Link>> poolSingleton = new  ESSimplePool<ReceiveChannelLink<Channel, Link>>(() => new ReceiveChannelLink<Channel, Link>(null));

    public bool IsRecycled { get; set; }
    public Action<Channel, Link> action;

    public void OnLink(Link link)
    {
        action?.Invoke(default,link);
    }
    public void OnLink(Channel channel, Link link)
    {
        action?.Invoke(channel,link);
    }
    public void OnResetAsPoolable()
    {
        action = null;
    }

    public void TryAutoPushedToPool()
    {
        poolSingleton.PushToPool(this);
    }

    public ReceiveChannelLink(Action<Channel,Link> action)
    {
        this.action = action;
    }
    public static implicit operator ReceiveChannelLink<Channel, Link>(Action<Channel, Link> action)
    {
        var rl = poolSingleton.GetInPool();
        rl.action = action;
        return rl;
    }

    public override bool Equals(object obj)
    {
        if (obj is ReceiveChannelLink<Channel, Link> rl)
        {
            return rl?.action == action;
        }
        return base.Equals(obj);
    }
    public override int GetHashCode()
    {
        return action?.GetHashCode() ?? 0;
    }

   
}
public static class ReceiveLinkMaker
{
    public static ReceiveLink<Link> MakeReceive<Link>(this Action<Link> action)
    {
        var rl = ReceiveLink<Link>.poolSingleton.GetInPool();
        rl.action = action;
        return rl;
    }
    public static ReceiveChannelLink<Channel, Link> MakeReceive<Channel,Link>(this Action<Channel,Link> action)
    {
        var rl = ReceiveChannelLink<Channel, Link>.poolSingleton.GetInPool();
        rl.action = action;
        return rl;
    }
    public static ReceiveFlagLink<LinkFlag> MakeReceive<LinkFlag>(this Action<LinkFlag, LinkFlag> action)
    {
        var rl = ReceiveFlagLink <LinkFlag>.poolSingleton.GetInPool();
        rl.action = action;
        return rl;
    }
}
