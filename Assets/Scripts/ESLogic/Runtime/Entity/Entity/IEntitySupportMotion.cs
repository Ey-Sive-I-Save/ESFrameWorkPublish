using UnityEngine;

namespace ES
{
    public interface IEntityKCCBeforeMotion
    {
        bool BeforeCharacterUpdate(Entity owner, EntityKCCData kcc, Vector3 initialPosition, float deltaTime);
    }

    public interface IEntityKCCRotationMotion
    {
        bool UpdateRotation(Entity owner, EntityKCCData kcc, Quaternion initialRotation, ref Quaternion currentRotation, float deltaTime);
    }

    public interface IEntityKCCVelocityMotion
    {
        bool UpdateVelocity(Entity owner, EntityKCCData kcc, Vector3 initialVelocity, ref Vector3 currentVelocity, float deltaTime);
    }
}
