using UnityEngine;

namespace ES
{
    /// <summary>
    /// 方块直升机等原型使用的速度型飞行能力。
    /// 它只形成候选速度，最终仍由 VehicleController 写入 Rigidbody/KCC。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleArcadeFlightModule : MonoBehaviour, IVehicleVelocityMotion
    {
        [Header("References")]
        public VehicleController vehicleController;

        [Header("Motion")]
        [Min(0f)] public float horizontalSpeed = 20f;
        [Min(0f)] public float climbSpeed = 12f;
        [Min(0f)] public float acceleration = 24f;
        public int velocityOrder = 50;

        private VehicleMotionRegistration registration;

        private void Reset()
        {
            vehicleController = GetComponent<VehicleController>();
        }

        private void OnValidate()
        {
            horizontalSpeed = Mathf.Max(0f, horizontalSpeed);
            climbSpeed = Mathf.Max(0f, climbSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            if (vehicleController == null)
                vehicleController = GetComponent<VehicleController>();
        }

        private void Start()
        {
            if (vehicleController == null)
                vehicleController = GetComponent<VehicleController>();

            if (vehicleController != null && !registration.IsValid)
            {
                var order = VehicleMotionOrder.Default;
                order = new VehicleMotionOrder(order.before, order.rotation, velocityOrder, order.after);
                registration = vehicleController.RegisterMotionFeature(this, order);
            }
        }

        private void OnDestroy()
        {
            if (vehicleController != null)
                vehicleController.UnregisterMotionFeature(ref registration);
        }

        public bool UpdateVehicleVelocity(
            VehicleController vehicle,
            Vector3 initialVelocity,
            ref Vector3 currentVelocity,
            float deltaTime)
        {
            if (!isActiveAndEnabled)
                return false;

            Vector3 up = vehicle.transform.up;
            Vector3 move = Vector3.ProjectOnPlane(vehicle.InputState.moveWorld, up);
            if (move.sqrMagnitude > 1f)
                move.Normalize();

            Vector3 targetVelocity = move * horizontalSpeed + up * (vehicle.InputState.verticalInput * climbSpeed);
            currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, acceleration * deltaTime);
            return true;
        }
    }
}
