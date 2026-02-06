using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [DisallowMultipleComponent]
    [Serializable, TypeRegistryItem("可骑乘")]
    public class EntityMountable : MonoBehaviour
    {
        [Title("匹配点")]
        [LabelText("Match点")]
        public Transform matchPoint;

        [Title("武器")]
        [LabelText("武器挂点")]
        public Transform weaponMountPoint;

        [LabelText("允许挂载武器")]
        public bool allowWeapon = true;

        [Title("移动")]
        public float moveSpeed = 5f;
        public float acceleration = 10f;
        public float turnSpeed = 180f;

        [Title("重力")]
        public bool useGravity;
        public Vector3 gravity = new Vector3(0f, -9.81f, 0f);

        [Title("同步")]
        public bool alignRiderPosition = true;
        public bool alignRiderRotation = true;

        [Title("输入")]
        public bool allowInput = true;

        [ShowInInspector, ReadOnly]
        public Entity rider;

        public event Action<Entity> OnMounted;
        public event Action<Entity> OnUnmounted;

        private Vector3 _velocity;

        public bool IsMounted => rider != null;

        private void Reset()
        {
            matchPoint = transform;
            weaponMountPoint = transform;
        }

        private void OnValidate()
        {
            if (matchPoint == null) matchPoint = transform;
            if (weaponMountPoint == null) weaponMountPoint = matchPoint;
            moveSpeed = Mathf.Max(0.1f, moveSpeed);
            acceleration = Mathf.Max(0.1f, acceleration);
            turnSpeed = Mathf.Max(1f, turnSpeed);
        }

        public void Mount(Entity target)
        {
            rider = target;
            EnsureMatchPoint();
            SyncRider(force: true);
            OnMounted?.Invoke(target);
        }

        public void Unmount()
        {
            var last = rider;
            rider = null;
            if (last != null)
            {
                OnUnmounted?.Invoke(last);
            }
        }

        public void AttachWeapon(Transform weapon)
        {
            if (!allowWeapon || weapon == null) return;
            Transform target = weaponMountPoint != null ? weaponMountPoint : transform;
            weapon.SetParent(target, false);
        }

        public void DetachWeapon(Transform weapon)
        {
            if (weapon == null) return;
            if (weapon.parent == weaponMountPoint || weapon.parent == transform)
            {
                weapon.SetParent(null, true);
            }
        }

        public void TickMounted(Entity target, Vector3 moveInput, Vector3 lookInput, float deltaTime)
        {
            if (rider != target) return;

            if (allowInput)
            {
                // moveInput is world-space; keep direction consistent with player input
                Vector3 desired = Vector3.ProjectOnPlane(moveInput, transform.up);
                float desiredMag = desired.magnitude;
                if (desiredMag > 1f)
                {
                    desired /= desiredMag;
                }
                Vector3 targetVelocity = desired * moveSpeed;
                _velocity = Vector3.Lerp(_velocity, targetVelocity, 1f - Mathf.Exp(-acceleration * deltaTime));
            }
            else
            {
                _velocity = Vector3.zero;
            }

            if (useGravity)
            {
                _velocity += gravity * deltaTime;
            }

            transform.position += _velocity * deltaTime;

            if (lookInput.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookInput.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * deltaTime);
            }

            SyncRider(force: false);
        }

        private void EnsureMatchPoint()
        {
            if (matchPoint == null)
            {
                matchPoint = transform;
            }
            if (weaponMountPoint == null)
            {
                weaponMountPoint = matchPoint;
            }
        }

        private void SyncRider(bool force)
        {
            if (rider == null || rider.kcc == null || rider.kcc.motor == null) return;

            EnsureMatchPoint();
            var motor = rider.kcc.motor;
            Vector3 pos = alignRiderPosition ? matchPoint.position : motor.Transform.position;
            Quaternion rot = alignRiderRotation ? matchPoint.rotation : motor.Transform.rotation;

            motor.SetPositionAndRotation(pos, rot, true);
        }
    }
}
