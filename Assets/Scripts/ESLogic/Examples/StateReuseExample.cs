using ES;
using UnityEngine;
using Sirenix.OdinInspector;

namespace ES.Examples
{
    /// <summary>
    /// 状态复用与自定义键注册示例
    /// 演示如何使用新API快速生成状态变体
    /// </summary>
    public class StateReuseExample : MonoBehaviour
    {
        [Title("状态机引用")]
        public StateMachine stateMachine;

        [Title("动画资源")]
        public AnimationClip walkForwardClip;
        public AnimationClip walkBackwardClip;
        public AnimationClip walkLeftClip;
        public AnimationClip walkRightClip;

        [Title("攻击连招")]
        public AnimationClip attack1Clip;
        public AnimationClip attack2Clip;
        public AnimationClip attack3Clip;

        [Button("示例1: 简单Clip复用"), FoldoutGroup("示例")]
        public void Example1_SimpleClipReuse()
        {
            // 1. 创建基础移动配置
            StateSharedData walkBase = new StateSharedData
            {
                basicConfig = new StateBasicConfig
                {
                    stateName = "Walk_Base",
                    stateId = 100,
                    pipelineType = StatePipelineType.Main,
                    priority = 50
                },
                mergeData = new StateMergeData
                {
                    stateChannelMask = StateChannelMask.DoubleLeg,
                    stayLevel = StateStayLevel.Low
                },
                hasAnimation = true,
                fadeInDuration = 0.2f,
                fadeOutDuration = 0.2f
            };

            // 2. 克隆并替换动画（4个方向）
            var walkForward = walkBase.CloneWithClip("Walk_Forward", 101, walkForwardClip);
            var walkBackward = walkBase.CloneWithClip("Walk_Backward", 102, walkBackwardClip);
            var walkLeft = walkBase.CloneWithClip("Walk_Left", 103, walkLeftClip);
            var walkRight = walkBase.CloneWithClip("Walk_Right", 104, walkRightClip);

            // 3. 注册到状态机
            stateMachine.RegisterStateFromSharedData(walkForward);
            stateMachine.RegisterStateFromSharedData(walkBackward);
            stateMachine.RegisterStateFromSharedData(walkLeft);
            stateMachine.RegisterStateFromSharedData(walkRight);

            Debug.Log("✅ 示例1完成: 已注册4个移动方向状态");
        }

        [Button("示例2: 批量生成攻击连招"), FoldoutGroup("示例")]
        public void Example2_BatchAttackCombo()
        {
            // 1. 创建基础攻击配置
            StateSharedData attackBase = new StateSharedData
            {
                basicConfig = new StateBasicConfig
                {
                    pipelineType = StatePipelineType.Main,
                    priority = 80,
                    durationMode = StateDurationMode.UntilAnimationEnd
                },
                mergeData = new StateMergeData
                {
                    stateChannelMask = StateChannelMask.DoubleHand | StateChannelMask.BodySpine,
                    stayLevel = StateStayLevel.Middle
                },
                costData = new StateCostData
                {
                    
                },
                hasAnimation = true,
                fadeInDuration = 0.05f,
                fadeOutDuration = 0.1f
            };

            // 2. 准备动画数组
            AnimationClip[] comboClips = { attack1Clip, attack2Clip, attack3Clip };

            // 3. 批量克隆（使用CloneWithAnimations更简洁）
            StateSharedData[] comboStates = new StateSharedData[comboClips.Length];
            for (int i = 0; i < comboClips.Length; i++)
            {
                comboStates[i] = attackBase.CloneWithClip(
                    $"Attack_Combo{i + 1}",
                    1001 + i,
                    comboClips[i]
                );
            }

            // 4. 批量注册
            foreach (var state in comboStates)
            {
                stateMachine.RegisterStateFromSharedData(state);
            }

            Debug.Log($"✅ 示例2完成: 已注册{comboStates.Length}段攻击连招");
        }

        [Button("示例3: 自定义键注册"), FoldoutGroup("示例")]
        public void Example3_CustomKeyRegistration()
        {
            // 创建基础数据
            StateSharedData dashData = new StateSharedData
            {
                basicConfig = new StateBasicConfig
                {
                    stateName = "Dash_Default", // 默认名（会被覆盖）
                    stateId = -1, // 自动分配（会被覆盖）
                    pipelineType = StatePipelineType.Main
                },
                hasAnimation = true
            };

            // 使用自定义键注册多个实例
            stateMachine.RegisterStateFromSharedData(
                dashData,
                customStringKey: "Dash_Player1",
                customIntKey: 5001
            );

            stateMachine.RegisterStateFromSharedData(
                dashData,
                customStringKey: "Dash_Player2",
                customIntKey: 5002
            );

            stateMachine.RegisterStateFromSharedData(
                dashData,
                customStringKey: "Dash_Boss",
                customIntKey: 5999
            );

            Debug.Log("✅ 示例3完成: 同一数据注册为3个不同键的状态");
        }

        [Button("示例4: 热更新覆盖"), FoldoutGroup("示例")]
        public void Example4_HotfixOverride()
        {
            // 原始技能
            StateSharedData skillV1 = new StateSharedData
            {
                basicConfig = new StateBasicConfig
                {
                    stateName = "FireBall",
                    stateId = 8001,
                    pipelineType = StatePipelineType.Buff
                },
                hasAnimation = true
            };

            // 初始注册
            bool registered = stateMachine.RegisterStateFromSharedData(skillV1);
            Debug.Log($"V1注册: {registered}");

            // 热更新版本（修复了bug）
            StateSharedData skillV2 = skillV1.CloneWithClip(
                "FireBall", // 同名
                8001,       // 同ID
                attack1Clip // 新动画
            );

            // 覆盖注册
            bool overridden = stateMachine.RegisterStateFromSharedData(
                skillV2,
                customStringKey: "FireBall",
                customIntKey: 8001,
                allowOverride: true // 允许覆盖
            );

            Debug.Log($"✅ 示例4完成: V2覆盖注册 {overridden}");
        }

        [Button("示例5: 使用Info注册（自定义键）"), FoldoutGroup("示例")]
        public void Example5_InfoWithCustomKey()
        {
            // 假设从SO加载的Info
            StateAniDataInfo jumpInfo = CreateJumpInfo();

            // 方式1: 使用默认键
            stateMachine.RegisterStateFromInfo(jumpInfo);
            // 结果: stateName="Jump", stateId=3001

            // 方式2: 自定义String键
            stateMachine.RegisterStateFromInfo(
                jumpInfo,
                customStringKey: "Jump_High"
            );
            // 结果: stateName="Jump_High", stateId=3001

            // 方式3: 自定义双键
            stateMachine.RegisterStateFromInfo(
                jumpInfo,
                customStringKey: "Jump_DoubleJump",
                customIntKey: 3002
            );
            // 结果: stateName="Jump_DoubleJump", stateId=3002

            Debug.Log("✅ 示例5完成: Info注册的3种方式");
        }

        // 辅助方法
        private StateAniDataInfo CreateJumpInfo()
        {
            var info = ScriptableObject.CreateInstance<StateAniDataInfo>();
            info.sharedData = new StateSharedData
            {
                basicConfig = new StateBasicConfig
                {
                    stateName = "Jump",
                    stateId = 3001,
                    pipelineType = StatePipelineType.Main
                }
            };
            return info;
        }

        [Button("清理所有示例状态"), FoldoutGroup("工具")]
        public void CleanupExampleStates()
        {
            // 注意: StateMachine需要提供清理API
            Debug.Log("⚠️ 清理功能需要StateMachine实现UnregisterState API");
        }
    }
}
