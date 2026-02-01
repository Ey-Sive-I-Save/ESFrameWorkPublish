using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Sirenix.OdinInspector;

namespace ES.Commercial
{
    /// <summary>
    /// 商业级IK解算器集成
    /// </summary>
    [Serializable]
    public class IKSolverLayer
    {
        [Serializable]
        public enum IKType
        {
            TwoBonesIK,     // 双骨骼IK (手臂/腿)
            LimbIK,         // 肢体IK
            FABRIK,         // FABRIK算法
            CCDIK,          // CCD IK
            LookAt,         // 注视IK
            AimIK           // 瞄准IK
        }
        
        [LabelText("IK类型")]
        public IKType type;
        
        [LabelText("权重参数名")]
        public string weightParameterName = "IKWeight";
        
        [LabelText("目标参数名")]
        public string targetParameterName = "IKTarget";
        
        [LabelText("Hint参数名")]
        [ShowIf("type", IKType.TwoBonesIK)]
        public string hintParameterName = "IKHint";
        
        [LabelText("骨骼链")]
        public Transform[] boneChain;
        
        [LabelText("末端效应器偏移")]
        public Vector3 endEffectorOffset = Vector3.zero;
        
        [NonSerialized]
        private AnimationScriptPlayable _ikPlayable;
        
        public void Setup(PlayableGraph graph, Animator animator)
        {
            // IK层通过Animation Jobs实现
            // 这里是简化示例,实际需要使用Animation C# Jobs
            Debug.Log($"Setting up {type} IK Layer");
        }
        
        public void Update(StateContext context, float deltaTime)
        {
            float weight = context.GetFloat(weightParameterName, 0f);
            var target = context.GetEntity(targetParameterName) as Transform;
            
            if (target == null || weight <= 0.001f)
                return;
            
            // 应用IK解算 (实际项目中使用Animation Jobs)
            ApplyIK(target, weight);
        }
        
        private void ApplyIK(Transform target, float weight)
        {
            switch (type)
            {
                case IKType.TwoBonesIK:
                    SolveTwoBonesIK(target.position, weight);
                    break;
                
                case IKType.LookAt:
                    SolveLookAt(target.position, weight);
                    break;
                
                // 其他IK类型...
            }
        }
        
        private void SolveTwoBonesIK(Vector3 targetPos, float weight)
        {
            if (boneChain == null || boneChain.Length < 3)
                return;
            
            Transform upper = boneChain[0];
            Transform lower = boneChain[1];
            Transform end = boneChain[2];
            
            // 简化的双骨骼IK (实际应使用更精确的算法)
            Vector3 finalPos = Vector3.Lerp(end.position, targetPos + endEffectorOffset, weight);
            
            // 在实际项目中,这里应该使用Animation C# Jobs来实现
            // 以避免在主线程修改Transform,提升性能
        }
        
        private void SolveLookAt(Vector3 targetPos, float weight)
        {
            if (boneChain == null || boneChain.Length < 1)
                return;
            
            Transform head = boneChain[0];
            Vector3 direction = targetPos - head.position;
            
            if (direction.sqrMagnitude < 0.001f)
                return;
            
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            head.rotation = Quaternion.Slerp(head.rotation, targetRotation, weight);
        }
    }
    
    /// <summary>
    /// 面部动画层
    /// </summary>
    [Serializable]
    public class FacialAnimationLayer
    {
        [Serializable]
        public class BlendShapeChannel
        {
            [LabelText("BlendShape名称")]
            public string shapeName;
            
            [LabelText("参数名")]
            public string parameterName;
            
            [LabelText("权重倍数")]
            [Range(0f, 2f)]
            public float multiplier = 1f;
            
            [LabelText("平滑时间")]
            public float smoothTime = 0.1f;
            
            [NonSerialized]
            private float _currentValue;
            
            [NonSerialized]
            private float _velocity;
            
            public void Update(SkinnedMeshRenderer renderer, StateContext context, float deltaTime)
            {
                float targetValue = context.GetFloat(parameterName, 0f) * multiplier * 100f;
                
                _currentValue = Mathf.SmoothDamp(_currentValue, targetValue, ref _velocity, smoothTime, 1000f, deltaTime);
                
                int index = renderer.sharedMesh.GetBlendShapeIndex(shapeName);
                if (index >= 0)
                {
                    renderer.SetBlendShapeWeight(index, _currentValue);
                }
            }
        }
        
        [LabelText("面部网格")]
        public SkinnedMeshRenderer facialMesh;
        
        [LabelText("BlendShape通道")]
        [ListDrawerSettings(ShowIndexLabels = true)]
        public List<BlendShapeChannel> channels = new List<BlendShapeChannel>();
        
        [LabelText("启用口型同步")]
        public bool enableLipSync = true;
        
        [LabelText("口型参数")]
        [ShowIf("enableLipSync")]
        public string lipSyncParameter = "Viseme";
        
        public void Update(StateContext context, float deltaTime)
        {
            if (facialMesh == null)
                return;
            
            foreach (var channel in channels)
            {
                channel.Update(facialMesh, context, deltaTime);
            }
            
            if (enableLipSync)
            {
                UpdateLipSync(context);
            }
        }
        
        private void UpdateLipSync(StateContext context)
        {
            int viseme = context.GetInt(lipSyncParameter, 0);
            
            // 根据Viseme值设置对应的BlendShape
            // 0: sil, 1: PP, 2: FF, 3: TH, 4: DD, 5: kk, 6: CH, 7: SS, 8: nn, 9: RR, 10: aa, 11: E, 12: I, 13: O, 14: U
            
            string[] visemeShapes = 
            {
                "vrc.v_sil", "vrc.v_pp", "vrc.v_ff", "vrc.v_th", "vrc.v_dd",
                "vrc.v_kk", "vrc.v_ch", "vrc.v_ss", "vrc.v_nn", "vrc.v_rr",
                "vrc.v_aa", "vrc.v_e", "vrc.v_ih", "vrc.v_oh", "vrc.v_ou"
            };
            
            if (viseme >= 0 && viseme < visemeShapes.Length)
            {
                int index = facialMesh.sharedMesh.GetBlendShapeIndex(visemeShapes[viseme]);
                if (index >= 0)
                {
                    facialMesh.SetBlendShapeWeight(index, 100f);
                }
            }
        }
    }
    
    /// <summary>
    /// Ragdoll物理层
    /// </summary>
    [Serializable]
    public class RagdollPhysicsLayer
    {
        [LabelText("Ragdoll根节点")]
        public Transform ragdollRoot;
        
        [LabelText("激活参数")]
        public string activeParameterName = "RagdollActive";
        
        [LabelText("混合权重")]
        public string blendWeightParameter = "RagdollBlend";
        
        [LabelText("物理材质")]
        public PhysicMaterial physicsMaterial;
        
        [NonSerialized]
        private List<Rigidbody> _ragdollRigidbodies = new List<Rigidbody>();
        
        [NonSerialized]
        private List<(Transform bone, Vector3 localPos, Quaternion localRot)> _boneStates = new List<(Transform, Vector3, Quaternion)>();
        
        [NonSerialized]
        private bool _isActive;
        
        public void Initialize()
        {
            if (ragdollRoot == null)
                return;
            
            _ragdollRigidbodies.Clear();
            _boneStates.Clear();
            
            var rbs = ragdollRoot.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rbs)
            {
                _ragdollRigidbodies.Add(rb);
                _boneStates.Add((rb.transform, rb.transform.localPosition, rb.transform.localRotation));
                
                rb.isKinematic = true; // 默认不激活
            }
        }
        
        public void Update(StateContext context)
        {
            bool shouldActivate = context.GetBool(activeParameterName, false);
            
            if (shouldActivate && !_isActive)
            {
                ActivateRagdoll();
            }
            else if (!shouldActivate && _isActive)
            {
                DeactivateRagdoll();
            }
            
            if (_isActive)
            {
                float blend = context.GetFloat(blendWeightParameter, 1f);
                BlendRagdollPose(blend);
            }
        }
        
        private void ActivateRagdoll()
        {
            _isActive = true;
            
            foreach (var rb in _ragdollRigidbodies)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }
        
        private void DeactivateRagdoll()
        {
            _isActive = false;
            
            foreach (var rb in _ragdollRigidbodies)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            
            // 恢复原始姿态
            for (int i = 0; i < _boneStates.Count; i++)
            {
                var (bone, localPos, localRot) = _boneStates[i];
                bone.localPosition = localPos;
                bone.localRotation = localRot;
            }
        }
        
        private void BlendRagdollPose(float weight)
        {
            // 混合Ragdoll物理姿态和动画姿态
            // 在实际项目中,这需要更复杂的处理
        }
        
        public void ApplyForce(Vector3 force, Vector3 position)
        {
            if (!_isActive)
                return;
            
            // 找到最近的Rigidbody并施加力
            Rigidbody closest = null;
            float minDist = float.MaxValue;
            
            foreach (var rb in _ragdollRigidbodies)
            {
                float dist = Vector3.Distance(rb.position, position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = rb;
                }
            }
            
            closest?.AddForceAtPosition(force, position, ForceMode.Impulse);
        }
    }
    
    /// <summary>
    /// 动态骨骼层 (头发/衣物)
    /// </summary>
    [Serializable]
    public class DynamicBoneLayer
    {
        [Serializable]
        public class BoneChain
        {
            [LabelText("根骨骼")]
            public Transform root;
            
            [LabelText("弹性")]
            [Range(0f, 1f)]
            public float elasticity = 0.1f;
            
            [LabelText("阻尼")]
            [Range(0f, 1f)]
            public float damping = 0.1f;
            
            [LabelText("刚度")]
            [Range(0f, 1f)]
            public float stiffness = 0.1f;
            
            [LabelText("半径")]
            public float radius = 0.05f;
            
            [NonSerialized]
            private List<(Transform bone, Vector3 velocity, Vector3 prevPos)> _particles = new List<(Transform, Vector3, Vector3)>();
            
            public void Initialize()
            {
                _particles.Clear();
                
                if (root == null)
                    return;
                
                CollectBones(root);
            }
            
            private void CollectBones(Transform bone)
            {
                _particles.Add((bone, Vector3.zero, bone.position));
                
                foreach (Transform child in bone)
                {
                    CollectBones(child);
                }
            }
            
            public void Update(float deltaTime, Vector3 gravity)
            {
                for (int i = 0; i < _particles.Count; i++)
                {
                    var (bone, velocity, prevPos) = _particles[i];
                    
                    if (bone == null)
                        continue;
                    
                    // 简化的Verlet积分
                    Vector3 currentPos = bone.position;
                    Vector3 newVelocity = velocity;
                    
                    // 重力
                    newVelocity += gravity * elasticity * deltaTime;
                    
                    // 阻尼
                    newVelocity *= (1f - damping);
                    
                    // 刚度约束
                    if (i > 0)
                    {
                        var parentBone = _particles[i - 1].bone;
                        if (parentBone != null)
                        {
                            Vector3 toParent = parentBone.position - currentPos;
                            float distance = toParent.magnitude;
                            float targetDistance = Vector3.Distance(currentPos, prevPos);
                            
                            if (distance > targetDistance)
                            {
                                Vector3 correction = toParent.normalized * (distance - targetDistance) * stiffness;
                                currentPos += correction;
                            }
                        }
                    }
                    
                    // 更新位置
                    Vector3 newPos = currentPos + newVelocity * deltaTime;
                    bone.position = newPos;
                    
                    _particles[i] = (bone, newVelocity, currentPos);
                }
            }
        }
        
        [LabelText("骨骼链")]
        [ListDrawerSettings(ShowIndexLabels = true)]
        public List<BoneChain> chains = new List<BoneChain>();
        
        [LabelText("重力")]
        public Vector3 gravity = new Vector3(0, -9.81f, 0);
        
        [LabelText("启用碰撞")]
        public bool enableCollision = true;
        
        [LabelText("碰撞球体")]
        [ShowIf("enableCollision")]
        public List<SphereCollider> colliders = new List<SphereCollider>();
        
        public void Initialize()
        {
            foreach (var chain in chains)
            {
                chain.Initialize();
            }
        }
        
        public void Update(float deltaTime)
        {
            foreach (var chain in chains)
            {
                chain.Update(deltaTime, gravity);
            }
        }
    }
}
