using System;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    public struct Link_ESOutputOpStart
    {
        public ESRuntimeTargetPack target;
        public ESOpSupport scopeSupport;
        public ESOpSupport hostSupport;

        public Link_ESOutputOpStart(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            this.target = target;
            this.scopeSupport = scopeSupport;
            this.hostSupport = hostSupport;
        }
    }

    [Serializable]
    public struct Link_ESOutputOpStop
    {
        public ESRuntimeTargetPack target;
        public ESOpSupport scopeSupport;
        public ESOpSupport hostSupport;

        public Link_ESOutputOpStop(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            this.target = target;
            this.scopeSupport = scopeSupport;
            this.hostSupport = hostSupport;
        }
    }

    /// <summary>
    /// Base class for output Ops.
    /// Op stays polymorphic, parameters are concrete:
    /// target = current runtime target pack
    /// scopeSupport = short-lived runtime scope
    /// hostSupport = long-lived host support
    /// </summary>
    [Serializable]
    public abstract class ESOutputOp :
        IReceiveLink<Link_ESOutputOpStart>,
        IReceiveLink<Link_ESOutputOpStop>
    {
        public bool Enabled = true;

        [LabelText("必须触发停止")]
        public bool MustTriggerStop = false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void _TryStartOp(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            if (Enabled)
                StartOperation(target, scopeSupport, hostSupport);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void _TryStopOp(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            if (Enabled || MustTriggerStop)
                StopOperation(target, scopeSupport, hostSupport);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static ESOpSupport RuntimeSupport(ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            return scopeSupport != null ? scopeSupport : hostSupport;
        }

        protected abstract void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport);

        protected virtual void StopOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnLink(Link_ESOutputOpStart link)
        {
            _TryStartOp(link.target, link.scopeSupport, link.hostSupport);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnLink(Link_ESOutputOpStop link)
        {
            _TryStopOp(link.target, link.scopeSupport, link.hostSupport);
        }
    }
}
