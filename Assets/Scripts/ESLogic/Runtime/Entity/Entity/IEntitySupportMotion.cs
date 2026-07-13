using UnityEngine;

namespace ES
{
    public interface IEntitySupportMotion
    {
        bool BeforeCharacterUpdate(Entity owner, EntityKCCData kcc, float deltaTime);
        bool UpdateRotation(Entity owner, EntityKCCData kcc, ref Quaternion currentRotation, float deltaTime);
        bool UpdateVelocity(Entity owner, EntityKCCData kcc, ref Vector3 currentVelocity, float deltaTime);
    }
}
