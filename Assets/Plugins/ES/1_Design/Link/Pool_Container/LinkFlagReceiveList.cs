using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
namespace ES
{
    public class LinkFlagReceiveList<LinkFlag>
    {
        public SafeNormalList<IReceiveFlagLink<LinkFlag>> IRS = new SafeNormalList<IReceiveFlagLink<LinkFlag>>();
        public IReceiveFlagLink<LinkFlag> cache;
        public LinkFlag LastFlag;
        public LinkFlag DefaultFlag=default;
        public void Init(LinkFlag defaultFlag)
        {
            DefaultFlag = defaultFlag;
        }
        public void SendLink(LinkFlag link)
        {
            IRS.ApplyBuffers();
            if (!LastFlag.Equals(link))
            {
                int count = IRS.ValuesNow.Count;
                for (int i = 0; i < count; i++)
                {
                    cache = IRS.ValuesNow[i];
                    if (cache is UnityEngine.Object ob)
                    {
                        if (ob != null) cache.OnLink(LastFlag,link);
                        else IRS.Remove(cache);
                    }
                    else if (cache != null) cache.OnLink(LastFlag, link);
                    else IRS.Remove(cache);
                }
                LastFlag = link;
            }
        }
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        private void Internal_TryRemove(IReceiveFlagLink<LinkFlag> ir)
        {
            IRS.Remove(ir);
        }
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public void AddReceive(ref LinkFlag nowFlag, IReceiveFlagLink<LinkFlag> e)
        {
            if (nowFlag.Equals(LastFlag))
            {
                //没啥好说的
            }
            else
            {
                e.OnLink(nowFlag,LastFlag);
                nowFlag = LastFlag;
            }
            IRS.Add(e);
        }
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public void RemoveReceive(ref LinkFlag nowFlag, IReceiveFlagLink<LinkFlag> e)
        {
            if (nowFlag.Equals(DefaultFlag))
            {
                //没啥好说的
            }
            else
            {
                nowFlag = DefaultFlag;
                e.OnLink(DefaultFlag);
            }
            IRS.Remove(e);
        }

        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public void AddReceive(ref LinkFlag nowFlag, Action<LinkFlag, LinkFlag> e)
        {
            AddReceive(ref nowFlag,e.MakeReceive<LinkFlag>());
        }
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public void RemoveReceive(ref LinkFlag nowFlag, Action<LinkFlag, LinkFlag> e)
        {
            RemoveReceive(ref nowFlag, e.MakeReceive<LinkFlag>());
        }
    }
}
