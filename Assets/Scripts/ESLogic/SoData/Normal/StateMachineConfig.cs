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
