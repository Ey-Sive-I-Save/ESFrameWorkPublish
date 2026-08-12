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

        [LabelText("载具控制器（驾驶座可选）")]
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
        public bool IsReady => matchPoint != null;
        public bool CanDrive => allowInput && vehicleController != null && vehicleController.IsReady;

        /// <summary>座位空闲且驾驶权可申请时才允许进入；不在状态进入后再抢占其他骑手。</summary>
        public bool CanMount(Entity target)
        {
            if (target == null || !IsReady || (rider != null && rider != target))
                return false;

            return !allowInput
                   || (CanDrive && vehicleController.CanAcquireDriver(this, target));
        }

        private void Reset()
        {
            matchPoint = transform;
            weaponMountPoint = transform;
            vehicleController = GetComponentInParent<VehicleController>();
        }

        private void OnValidate()
        {
            if (matchPoint == null) matchPoint = transform;
            if (weaponMountPoint == null) weaponMountPoint = matchPoint;
            if (vehicleController == null)
                vehicleController = GetComponentInParent<VehicleController>();
        }

        private void OnDisable()
        {
            // 载具对象池回收或座位被禁用时，不能把驾驶权遗留给下一次租用。
            if (rider != null)
                Unmount();
        }

        /// <param name="skipImmediateSync">
        /// true = 跳过立即传送（由外部 MatchTarget 负责渐近对齐，对齐完成后再调用 SyncRider）。
        /// false（默认）= 立即将 Rider 传送到 matchPoint。
        /// </param>
        public bool Mount(Entity target, bool skipImmediateSync = false)
        {
            if (!CanMount(target))
            {
                Debug.LogWarning("[EntityMountable] 骑乘失败：座位不可用、已被占用或驾驶权已被其他座位持有。", this);
                return false;
            }

            EnsureMatchPoint();
            if (allowInput && !vehicleController.TryAcquireDriver(this, target))
                return false;

            rider = target;
            if (!skipImmediateSync)
                SyncRider(force: true);
            InvokeRiderEvent(OnMounted, target, "OnMounted");
            return true;
        }

        public void Unmount()
        {
            var last = rider;
            rider = null;
            if (allowInput && vehicleController != null)
                vehicleController.ReleaseDriver(this, last);
            if (last != null)
            {
                InvokeRiderEvent(OnUnmounted, last, "OnUnmounted");
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
        public bool TrySetDriverInput(Entity target, Vector3 moveInput, Vector3 lookInput, float verticalInput = 0f)
        {
            if (rider != target || !CanDrive)
                return false;

            return vehicleController.TrySetDriverInput(this, target, moveInput, lookInput, verticalInput);
        }

        /// <summary>输入路由被禁用时只允许当前骑手清空自己座位的驾驶意图。</summary>
        public bool ClearDriverInput(Entity target)
        {
            if (rider != target || vehicleController == null)
                return false;

            return vehicleController.ClearDriverInput(this, target);
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

        /// <summary>单个订阅者异常只能记录，不能截断座位释放和其他订阅者的补偿逻辑。</summary>
        private void InvokeRiderEvent(Action<Entity> callbacks, Entity target, string eventName)
        {
            if (callbacks == null)
                return;

            Delegate[] handlers = callbacks.GetInvocationList();
            for (int i = 0; i < handlers.Length; i++)
            {
                try
                {
                    ((Action<Entity>)handlers[i]).Invoke(target);
                }
                catch (Exception exception)
                {
                    Debug.LogException(new Exception("[EntityMountable] " + eventName + " subscriber failed.", exception), this);
                }
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
