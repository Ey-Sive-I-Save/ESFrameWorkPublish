using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VehicleController))]
    [Serializable, TypeRegistryItem("可骑乘")]
    public class EntityMountable : MonoBehaviour
    {
        [Title("匹配点")]
        [LabelText("Match点")]
        public Transform matchPoint;

        [LabelText("载具控制器")]
        [Required]
        public VehicleController vehicleController;

        [Title("武器")]
        [LabelText("武器挂点")]
        public Transform weaponMountPoint;

        [LabelText("允许挂载武器")]
        public bool allowWeapon = true;

        [Title("同步")]
        public bool alignRiderPosition = true;
        public bool alignRiderRotation = true;

        [Title("输入")]
        public bool allowInput = true;

        [ShowInInspector, ReadOnly]
        public Entity rider;

        public event Action<Entity> OnMounted;
        public event Action<Entity> OnUnmounted;

        public bool IsMounted => rider != null;
        public bool IsReady => vehicleController != null && vehicleController.IsReady;

        private void Reset()
        {
            matchPoint = transform;
            weaponMountPoint = transform;
            vehicleController = GetComponent<VehicleController>();
        }

        private void OnValidate()
        {
            if (matchPoint == null) matchPoint = transform;
            if (weaponMountPoint == null) weaponMountPoint = matchPoint;
            if (vehicleController == null)
                vehicleController = GetComponent<VehicleController>();
        }

        /// <param name="skipImmediateSync">
        /// true = 跳过立即传送（由外部 MatchTarget 负责渐近对齐，对齐完成后再调用 SyncRider）。
        /// false（默认）= 立即将 Rider 传送到 matchPoint。
        /// </param>
        public void Mount(Entity target, bool skipImmediateSync = false)
        {
            if (target == null || !IsReady)
            {
                Debug.LogError("[EntityMountable] 骑乘失败：缺少已就绪的 VehicleController。", this);
                return;
            }

            rider = target;
            EnsureMatchPoint();
            if (!skipImmediateSync)
                SyncRider(force: true);
            OnMounted?.Invoke(target);
        }

        public void Unmount()
        {
            var last = rider;
            rider = null;
            vehicleController?.ClearDriverInput();
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

        /// <summary>
        /// 将骑手的世界空间驾驶意图交给载具。座位不再直接写载具 Transform；
        /// VehicleController 在自身 Rigidbody/KCC 阶段统一提交最终物理结果。
        /// </summary>
        public bool SubmitDriverInput(Entity target, Vector3 moveInput, Vector3 lookInput)
        {
            if (rider != target || !IsReady)
                return false;

            if (allowInput)
                vehicleController.SetDriverInput(moveInput, lookInput);
            else
                vehicleController.ClearDriverInput();
            return true;
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
            // ★ 使用 TransientPosition/TransientRotation（KCC 物理层真实位置），
            //   不能用 motor.Transform.position（插值渲染位置，会被 PostSimulation 重置）。
            Vector3    pos = alignRiderPosition ? matchPoint.position : motor.TransientPosition;
            Quaternion rot = alignRiderRotation ? matchPoint.rotation : motor.TransientRotation;

            // 统一提交到 Rider 的 KCC 物理边界；不在普通挂载调用栈直接争写 Motor。
            rider.kcc.QueueMatchTargetPose(pos, rot, releaseAfterApply: true);
        }
    }
}
