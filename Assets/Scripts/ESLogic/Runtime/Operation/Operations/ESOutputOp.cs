using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 基于 ESRuntimeTargetPack 和 IOperationRuntimeServices 的输出 Operation 基类。
    /// Start/Stop 管理生命周期，Stop 只负责停止或清理，不保证回滚 Start 的效果。
    /// </summary>
    [Serializable]
    public abstract class ESOutputOp
    {
        public bool Enabled = true;

        [LabelText("必须触发停止")]
        public bool MustTriggerStop = false;

        /// <summary>在启用状态下尝试启动 Operation。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void _TryStartOp(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            if (Enabled)
            {
                StartOperation(target, logic);
            }
        }

        /// <summary>尝试停止 Operation；MustTriggerStop 为 true 时即使禁用也会执行。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void _TryStopOp(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            if (Enabled || MustTriggerStop)
            {
                StopOperation(target, logic);
            }
        }

        /// <summary>子类实现具体启动逻辑。</summary>
        protected abstract void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic);

        /// <summary>子类按需实现停止或清理逻辑。</summary>
        protected virtual void StopOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic) { }
    }
}
