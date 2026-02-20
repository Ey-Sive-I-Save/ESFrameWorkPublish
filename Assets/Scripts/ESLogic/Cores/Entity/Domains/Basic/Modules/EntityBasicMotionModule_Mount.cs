using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("基础骑乘模块")]
    public class EntityBasicMountModule : EntityBasicModuleBase, IEntitySupportMotion
    {
        [Title("开关")]
        public bool enableMount = true;

        [Title("状态")]
        [ReadOnly] public bool mountHold;

        [Title("检测")]
        [LabelText("射线起点(可选)")]
        public Transform rayOrigin;

        [LabelText("检测距离")]
        public float mountDistance = 2f;

        [LabelText("检测层")]
        public LayerMask mountLayerMask = ~0;

        [LabelText("触发器命中")]
        public QueryTriggerInteraction mountQuery = QueryTriggerInteraction.Ignore;

        [Title("骑乘目标")]
        [ReadOnly] public EntityMountable currentMount;

        [LabelText("骑乘状态名")]
        public string Mount_StateName = "骑乘";

        private StateBase _mountState;
        private StateMachine sm;

        public override void Start()
        {
            base.Start();
            if (MyCore != null && MyCore.stateDomain != null && MyCore.stateDomain.stateMachine != null)
            {
                sm = MyCore.stateDomain.stateMachine;
                _mountState = sm.GetStateByString(Mount_StateName);
            }
            if (MyCore != null)
            {
                MyCore.kcc.mountModule = this;
            }
        }

        public void SetMount(bool enable)
        {
            if (!enableMount || _mountState == null) return;

            if (enable)
            {
                TryEnterMount();
            }
            else
            {
                ExitMount();
            }
        }

        public void ToggleMount()
        {
            if (!enableMount || _mountState == null) return;

            if (_mountState.baseStatus == StateBaseStatus.Running)
            {
                ExitMount();
            }
            else
            {
                TryEnterMount();
            }
        }

        private void TryEnterMount()
        {
            if (currentMount != null) return;

            var mountable = FindMountable();
            if (mountable == null) return;

            if (sm.TryActivateState(_mountState))
            {
                currentMount = mountable;
                currentMount.Mount(MyCore);
                mountHold = true;
            }
        }

        private void ExitMount()
        {
            if (currentMount != null)
            {
                currentMount.Unmount();
                currentMount = null;
            }

            if (_mountState != null && _mountState.baseStatus == StateBaseStatus.Running)
            {
                sm.TryDeactivateState(Mount_StateName);
            }

            mountHold = false;
        }

        private EntityMountable FindMountable()
        {
            if (MyCore == null) return null;

            Transform origin = rayOrigin != null ? rayOrigin : MyCore.transform;
            Vector3 dir = origin.forward;
            if (Physics.Raycast(origin.position, dir, out RaycastHit hit, mountDistance, mountLayerMask, mountQuery))
            {
                return hit.collider.GetComponentInParent<EntityMountable>();
            }

            return null;
        }

        protected override void Update()
        {
            if (MyCore == null || !enableMount) return;

            if (currentMount == null)
            {
                mountHold = false;
                return;
            }

            if (_mountState == null || _mountState.baseStatus != StateBaseStatus.Running)
            {
                ExitMount();
                return;
            }

            mountHold = true;
            MyCore.SetLocomotionSupportFlags(StateSupportFlags.Mounted);
            currentMount.TickMounted(MyCore, MyCore.kcc.moveInput, MyCore.kcc.lookInput, Time.deltaTime);
        }

        public bool BeforeCharacterUpdate(Entity owner, EntityKCCData kcc, float deltaTime)
        {
            return false;
        }

        public bool UpdateRotation(Entity owner, EntityKCCData kcc, ref Quaternion currentRotation, float deltaTime)
        {
            if (!enableMount || currentMount == null) return false;
            return true;
        }

        public bool UpdateVelocity(Entity owner, EntityKCCData kcc, ref Vector3 currentVelocity, float deltaTime)
        {
            if (!enableMount || currentMount == null) return false;
            currentVelocity = Vector3.zero;
            return true;
        }

        public override void OnDestroy()
        {
            if (MyCore != null && MyCore.kcc.mountModule == this)
            {
                MyCore.kcc.mountModule = null;
            }
            base.OnDestroy();
        }
    }
}
