using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    public interface IReceiveLink
    {

    }
    public interface IReceiveLink<in Link> : IReceiveLink
    {
        void OnLink(Link link);

    }
    public interface IReceiveFlagLink<in LinkFlag> : IReceiveLink<LinkFlag>
    {
        void OnLink(LinkFlag ago,LinkFlag now);
        void IReceiveLink<LinkFlag>.OnLink(LinkFlag now)
        {
            OnLink(default, now);
        }
    }
    public interface IReceiveChannelLink<in Channel, in Link> : IReceiveLink<Link>
    {
        void OnLink(Channel channel, Link link);
        void IReceiveLink<Link>.OnLink(Link link)
        {
            OnLink(default, link);
        }
    }
}