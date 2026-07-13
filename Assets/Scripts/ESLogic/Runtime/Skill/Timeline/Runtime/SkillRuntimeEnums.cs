using System;
using UnityEngine;

namespace ES
{
    [Serializable]
    public enum TrackRuntimeTargetSourceMode
    {
        [InspectorName("引用技能目标包")]
        ReferenceSkill = 0,

        [InspectorName("拷贝技能目标包")]
        CopySkill = 1,

        [InspectorName("创建空目标包")]
        NewEmpty = 2,

        [InspectorName("拷贝技能目标包并用表达式覆盖主目标")]
        CopySkillAndOverrideMainTarget = 3,

        [InspectorName("创建空目标包并用表达式设置主目标")]
        NewAndSetMainTargetByExpression = 4
    }

    [Serializable]
    public enum ClipRuntimeTargetSourceMode
    {
        [InspectorName("引用轨道目标包")]
        ReferenceTrack = 0,

        [InspectorName("引用技能目标包")]
        ReferenceSkill = 1,

        [InspectorName("拷贝轨道目标包")]
        CopyTrack = 2,

        [InspectorName("拷贝技能目标包")]
        CopySkill = 3,

        [InspectorName("创建空目标包")]
        NewEmpty = 4,

        [InspectorName("拷贝轨道目标包并用表达式覆盖主目标")]
        CopyTrackAndOverrideMainTarget = 5,

        [InspectorName("拷贝技能目标包并用表达式覆盖主目标")]
        CopySkillAndOverrideMainTarget = 6,

        [InspectorName("创建空目标包并用表达式设置主目标")]
        NewAndSetMainTargetByExpression = 7
    }

    [Serializable]
    public enum RuntimeTargetWriteBackTarget
    {
        [InspectorName("不写回")]
        None = 0,

        [InspectorName("写回轨道目标包")]
        Track = 1,

        [InspectorName("写回技能目标包")]
        Skill = 2,

        [InspectorName("写回技能和轨道目标包")]
        SkillAndTrack = 3
    }

    [Serializable]
    public enum RuntimeTargetWriteBackTiming
    {
        [InspectorName("不写回")]
        None = 0,

        [InspectorName("进入片段时写回")]
        OnEnter = 1,

        [InspectorName("退出片段时写回")]
        OnExit = 2,

        [InspectorName("进入和退出都写回")]
        OnEnterAndExit = 3
    }

    [Serializable]
    public enum SkillCastInterruptMode
    {
        [InspectorName("不可取消")]
        NotCancelable = 0,

        [InspectorName("可主动取消")]
        ManualCancelable = 1,

        [InspectorName("受击可取消")]
        HitCancelable = 2,

        [InspectorName("任意打断")]
        AnyInterrupt = 3
    }

    [Serializable]
    public enum SkillChargeMode
    {
        [InspectorName("不使用次数")]
        None = 0,

        [InspectorName("固定次数")]
        FixedCharges = 1,

        [InspectorName("随时间恢复")]
        RechargeOverTime = 2,

        [InspectorName("共享能量")]
        SharedEnergy = 3
    }

    [Serializable]
    public enum SkillCameraSupportMode
    {
        [InspectorName("不控制相机")]
        None = 0,

        [InspectorName("瞬时相机反馈")]
        InstantFeedback = 1,

        [InspectorName("持续相机控制")]
        ContinuousControl = 2,

        [InspectorName("轨道回调控制")]
        TrackCallback = 3
    }
}
