using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ES
{
    public partial class StateMachine
    {
        public bool BindToAnimator(Animator animator)
        {
            if (animator == null)
            {
#if STATEMACHINEDEBUG
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null)
                {
                    dbg.LogWarning($"BindToAnimator 失败：Animator 为 null | StateMachineKey={stateMachineKey}");
                }
#endif
                return false;
            }

            if (!playableGraph.IsValid())
            {
                playableGraph = PlayableGraph.Create($"StateMachine_{stateMachineKey}");
                playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                ownsPlayableGraph = true;
            }
            else
            {
                playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            }

            if (!rootMixer.IsValid())
            {
                int layerCount = (int)StateLayerType.Count;
                rootMixer = AnimationLayerMixerPlayable.Create(playableGraph, layerCount);
            }

            boundAnimator = animator;

            var driver = animator.GetComponent<StateFinalIKDriver>();
            if (driver == null)
            {
                driver = animator.gameObject.AddComponent<StateFinalIKDriver>();
            }
            driver.Bind(this, animator);

            if (!animationOutput.IsOutputValid())
            {
                animationOutput = AnimationPlayableOutput.Create(playableGraph, "StateMachine", animator);
            }
            else
            {
                animationOutput.SetTarget(animator);
            }

            animationOutput.SetSourcePlayable(rootMixer);
            animationOutput.SetWeight(1.0f);

            InitializeLayerWeights();

            const int AvatarTargetCount = 6;
            if (_sharedBoneTransforms == null)
                _sharedBoneTransforms = new Transform[AvatarTargetCount];
            if (animator.isHuman)
            {
                _sharedBoneTransforms[(int)AvatarTarget.Body]      = animator.GetBoneTransform(HumanBodyBones.Hips);
                _sharedBoneTransforms[(int)AvatarTarget.LeftFoot]  = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                _sharedBoneTransforms[(int)AvatarTarget.RightFoot] = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                _sharedBoneTransforms[(int)AvatarTarget.LeftHand]  = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                _sharedBoneTransforms[(int)AvatarTarget.RightHand] = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }

#if STATEMACHINEDEBUG
            var dbg2 = StateMachineDebugSettings.Instance;
            if (dbg2 != null && dbg2.IsRuntimeInitEnabled)
            {
                dbg2.LogRuntimeInit($"Animator绑定成功: {animator.gameObject.name}");
            }
#endif
            return true;
        }
    }
}