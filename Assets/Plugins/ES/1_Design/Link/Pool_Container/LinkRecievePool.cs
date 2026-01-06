using ES;
using Sirenix.OdinInspector;
using Sirenix.Serialization.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{

    [Serializable]
    public class LinkReceivePool : SafeKeyGroup<Type, IReceiveLink> /**/
    {
        public override string Editor_ShowDes => "Link收发安全键组";
        
        public void SendLink<Link>(Link link)
        {
            var links = GetGroupDirectly(typeof(Link));
            links.ApplyBuffers();
            int count = links.ValuesNow.Count;
            for(int i=0;i< count; i++)
            {
                if (links.ValuesNow[i] is IReceiveLink<Link> irl)
                {
                    if (irl is UnityEngine.Object ob)
                    {
                        if (ob != null) irl.OnLink(link);
                        else links.TryRemove(irl);
                    }
                    else if (irl != null) irl.OnLink(link);
                }
                else Remove(typeof(Link), null);
            }
        }
        public void AddReceive<Link>(IReceiveLink<Link> e)
        {
            Add(typeof(Link), e);
        }
        public void RemoveReceive<Link>(IReceiveLink<Link> e) 
        {
            Remove(typeof(Link), e);
        }
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public void AddReceive<Link>(Action<Link> e)
        {
            Add(typeof(Link), e.MakeReceive());
        }
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public void RemoveReceive<Link>(Action<Link> e)
        {
            Remove(typeof(Link), e.MakeReceive());
        }
    }

    
}

