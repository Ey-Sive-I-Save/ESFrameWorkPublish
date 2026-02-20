using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

// ============================================================================
// 文件：StateBase.Pooling.cs --0219验证通过
// 作用：StateBase 的对象池生命周期（重置/回收）与运行时资源清理。
//
// Public（本文件定义的对外成员；按模块分组，“先功能、后成员”，便于扫读）：
//
// 【对象池入口】
// - 全局对象池：public static readonly ESSimplePool<StateBase> Pool
//
// 【对象池协议字段】
// - 是否已回收：public bool IsRecycled { get; set; }
//
// 【回收/复用流程】
// - 回收/复用前重置：public void OnResetAsPoolable()
// - 满足条件时自动入池：public void TryAutoPushedToPool()
//
// Private/Internal：运行时字段清理、Playable 销毁、IK/MatchTarget 状态回收等。
// ============================================================================

namespace ES
{
    public partial class StateBase
    {
        #region 对象池支持*（IPoolableAuto）

        /// <summary>
        /// StateBase 对象池
        /// 容量: 无限制（-1表示不限制），初始预热10个对象，避免频繁GC
        /// 预热: 10个初始对象
        /// </summary>
        public static readonly ESSimplePool<StateBase> Pool = new ESSimplePool<StateBase>(
            factoryMethod: () => new StateBase(),
            initCount: 10,
            maxCount: -1,
            poolDisplayName: "StateBase Pool"
        );

        /// <summary>
        /// 对象回收标记
        /// </summary>
        public bool IsRecycled { get; set; }

        /// <summary>
        /// 重置对象状态 —— 彻底清理所有运行时数据
        /// </summary>
        public void OnResetAsPoolable()
        {
            host = null;
            _layerRuntime = null;
            _pipelineSlotIndex = -1;
            activationTime = 0f;
            hasEnterTime = 0f;
            normalizedProgress = 0f;
            totalProgress = 0f;
            loopCount = 0;
            _lastNormalizedProgress = 0f;
            baseStatus = StateBaseStatus.Never;
            _runtimePhase = StateRuntimePhase.Pre;
            _runtimePhaseManual = false;
            _resolvedRuntimeConfig = null;
            _resolvedRuntimeDirty = true;
            _ikDefaultTargetWeight = 1f;
            strKey = null;
            intKey = -1;
            stateSharedData = null;
            stateVariableData = null;

            _basicConfigCached = null;
            _phaseConfigCached = null;
            _animConfigCached = null;
            _calculatorCached = null;
            _hasAnimationCached = false;
            _hasAnimationMarkerCached = false;
            _needsProgressTrackingCached = false;
            _autoPhaseByTimeCached = false;
            _durationModeCached = StateDurationMode.Timed;
            _enableIKCached = false;
            _enableMatchTargetCached = false;
            _shouldAutoExitFromAnimation = false;
            _playableWeight = 1f;

            // IK/MatchTarget状态
            _ikActive = false;
            _matchTargetActive = false;
            _matchTargetLastAppliedPos = Vector3.zero;
            _matchTargetLastAppliedRot = Quaternion.identity;
            _matchTargetLastApplyTime = -999f;

            DestroyPlayable();
        }

        /// <summary>
        /// 尝试回收到对象池
        /// </summary>
        public void TryAutoPushedToPool()
        {
            if (!IsRecycled)
            {
                // ★ 不在这里设置 IsRecycled = true
                // PushToPool 内部流程：检查IsRecycled → resetMethod → 设置IsRecycled=true → 入栈
                // 如果提前设置，PushToPool会误判为"已回收"而拒绝入池
                Pool.PushToPool(this);
            }
        }

        #endregion
    }
}
