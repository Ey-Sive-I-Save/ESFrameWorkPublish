using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public abstract class _GlobalLinker
    {
        public abstract void Apply();
    }
    public abstract class GlobalLinker<Link> : _GlobalLinker, IReceiveLink<Link>
    {
        public abstract void OnLink(Link link);
        public sealed override void Apply()
        {
            GameManager.GlobalLinkPool.AddReceiver(this);
        }
    }

    public class RR_GlobalLink : RuntimeRegister_FOR_Singleton<_GlobalLinker>
    {
        public override int LoadTiming => ESAssemblyLoadTiming._1_BeforeFirstSceneLoad;

        public override void Handle(_GlobalLinker singleton)
        {
            singleton.Apply();
        }
    }
}
