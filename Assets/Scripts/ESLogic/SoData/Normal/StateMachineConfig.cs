using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    [CreateAssetMenu(fileName = "StateMachineConfig", menuName = "ES/StateMachineConfig")]
    public class StateMachineConfig : ESEditorGlobalSo<StateMachineConfig> 
    {
        [TabGroup("禁用跳转许可")]
        [HideLabel]
        public StateMachineDisableTransitionPermissionMap disableTransitionPermissionMap = new StateMachineDisableTransitionPermissionMap();

        // ==================== MatchTarget 全局策略 ====================

        [TabGroup("MatchTarget全局")]
        [LabelText("重施加策略"), InlineProperty, HideLabel]
        [Tooltip("整个状态机共享同一套重施加阈值，不在每个状态上单独配置")]
        public MatchTargetReapplySettings matchTargetReapply = MatchTargetReapplySettings.Default;
    }

    // ==================== MatchTarget 全局重施加设置 ====================

    /// <summary>
    /// MatchTarget 重施加全局策略（配置在 StateMachineConfig SO 上，整个项目共用一套）。
    /// <para>原来散落在各 State / ClimbModule 的 allowMatchTargetReapply 等字段已统一到这里。</para>
    /// </summary>
    [Serializable]
    public class MatchTargetReapplySettings
    {
        [LabelText("允许重施加")]
        [Tooltip("启用后：Animator 已在 matching 时，若满足距离/角度/间隔阈值，会重新调用 MatchTarget\n适合需要持续逼近目标的攀爬/交互场景")]
        public bool allow;

        [LabelText("最小间隔(秒)"), Range(0f, 0.5f), ShowIf("allow")]
        [Tooltip("两次重施加之间的最短时间间隔，防止每帧都触发")]
        public float interval;

        [LabelText("最小距离(米)"), Range(0f, 1f), ShowIf("allow")]
        [Tooltip("目标点位置变化超过此距离才触发重施加（sqrMagnitude，零 GC）")]
        public float minDistance;

        [LabelText("最小角度(°)"), Range(0f, 30f), ShowIf("allow")]
        [Tooltip("目标点旋转变化超过此角度才触发重施加（Dot 替代 acos，零 GC）")]
        public float minAngle;

        public static MatchTargetReapplySettings Default => new MatchTargetReapplySettings
        {
            allow       = false,
            interval    = 0.05f,
            minDistance  = 0.02f,
            minAngle    = 2f
        };
    }

    [Serializable,TypeRegistryItem("禁止跳转许可映射")]
    public class StateMachineDisableTransitionPermissionMap : RelationMaskEnumMap<StateSupportFlags> 
    {  
        [Button("使用默认配置")]
        public void StartDefaultConfigure()
        {
            InitEnumDefault();

            // Dead：禁止跳转到任何非 Dead 的状态
            AddRelations(StateSupportFlags.Dead, new[]
            {
                StateSupportFlags.Grounded,
                StateSupportFlags.Crouched,
                StateSupportFlags.Prone,
                StateSupportFlags.Swimming,
                StateSupportFlags.Flying,
                StateSupportFlags.Mounted,
                StateSupportFlags.Transition,
                StateSupportFlags.Climbing,
                StateSupportFlags.SpecialInteraction,
                StateSupportFlags.Observer
            });

            // Transition：禁止切换到战斗/机动/载具类
            AddRelations(StateSupportFlags.Transition, new[]
            {
                StateSupportFlags.Crouched,
                StateSupportFlags.Prone,
                StateSupportFlags.Swimming,
                StateSupportFlags.Flying,
                StateSupportFlags.Mounted,
                StateSupportFlags.Dead,
                StateSupportFlags.Climbing,
                StateSupportFlags.SpecialInteraction,
                StateSupportFlags.Observer
            });

            // Swimming：禁止切换到飞行/骑乘/趴伏/下蹲
            AddRelations(StateSupportFlags.Swimming, new[]
            {
                StateSupportFlags.Flying,
                StateSupportFlags.Mounted,
                StateSupportFlags.Prone,
                StateSupportFlags.Crouched,
                StateSupportFlags.Climbing
            });

            // Flying：禁止切换到游泳/骑乘/趴伏/下蹲
            AddRelations(StateSupportFlags.Flying, new[]
            {
                StateSupportFlags.Swimming,
                StateSupportFlags.Mounted,
                StateSupportFlags.Prone,
                StateSupportFlags.Crouched,
                StateSupportFlags.Climbing
            });

            // Mounted：禁止切换到游泳/飞行/趴伏/下蹲
            AddRelations(StateSupportFlags.Mounted, new[]
            {
                StateSupportFlags.Swimming,
                StateSupportFlags.Flying,
                StateSupportFlags.Prone,
                StateSupportFlags.Crouched,
                StateSupportFlags.Climbing
            });

            // Prone：禁止切换到游泳/飞行/骑乘
            AddRelations(StateSupportFlags.Prone, new[]
            {
                StateSupportFlags.Swimming,
                StateSupportFlags.Flying,
                StateSupportFlags.Mounted,
                StateSupportFlags.Climbing
            });

            // Crouched：禁止切换到游泳/飞行/骑乘
            AddRelations(StateSupportFlags.Crouched, new[]
            {
                StateSupportFlags.Swimming,
                StateSupportFlags.Flying,
                StateSupportFlags.Mounted,
                StateSupportFlags.Climbing
            });

            // Climbing：禁止切换到游泳/飞行/骑乘/趴伏/下蹲
            AddRelations(StateSupportFlags.Climbing, new[]
            {
                StateSupportFlags.Swimming,
                StateSupportFlags.Flying,
                StateSupportFlags.Mounted,
                StateSupportFlags.Prone,
                StateSupportFlags.Crouched
            });

            // SpecialInteraction：禁止切换到游泳/飞行/骑乘/趴伏/下蹲/攀爬
            AddRelations(StateSupportFlags.SpecialInteraction, new[]
            {
                StateSupportFlags.Swimming,
                StateSupportFlags.Flying,
                StateSupportFlags.Mounted,
                StateSupportFlags.Prone,
                StateSupportFlags.Crouched,
                StateSupportFlags.Climbing
            });

            // Observer：禁止切换到游泳/飞行/骑乘/趴伏/下蹲/攀爬/特殊交互/过场
            AddRelations(StateSupportFlags.Observer, new[]
            {
                StateSupportFlags.Swimming,
                StateSupportFlags.Flying,
                StateSupportFlags.Mounted,
                StateSupportFlags.Prone,
                StateSupportFlags.Crouched,
                StateSupportFlags.Climbing,
                StateSupportFlags.SpecialInteraction,
                StateSupportFlags.Transition
            });
        }
    }
}
