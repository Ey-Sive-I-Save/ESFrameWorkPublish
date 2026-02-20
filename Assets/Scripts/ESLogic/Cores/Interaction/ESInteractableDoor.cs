using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    public class ESInteractableDoor : ESInteractable
    {
        [Title("Door")]
        public Transform doorPivot;

        public Vector3 localOpenAxis = Vector3.up;

        public float openAngle = 90f;

        public float openSpeed = 180f;

        public bool invertOpenDirection = false;

        [ShowInInspector, ReadOnly]
        public bool isOpen;

        private Quaternion _closedLocalRotation;
        private Quaternion _openLocalRotation;

        private void Awake()
        {
            if (doorPivot == null)
            {
                doorPivot = transform;
            }

            _closedLocalRotation = doorPivot.localRotation;
            float dir = invertOpenDirection ? -1f : 1f;
            Vector3 axis = localOpenAxis.sqrMagnitude > 0.001f ? localOpenAxis.normalized : Vector3.up;
            _openLocalRotation = _closedLocalRotation * Quaternion.AngleAxis(openAngle * dir, axis);
        }

        private void Update()
        {
            if (doorPivot == null) return;
            Quaternion target = isOpen ? _openLocalRotation : _closedLocalRotation;
            doorPivot.localRotation = Quaternion.RotateTowards(doorPivot.localRotation, target, openSpeed * Time.deltaTime);
        }

        public override void OnInteractCompleted(Entity entity, bool success)
        {
            base.OnInteractCompleted(entity, success);
            if (success)
            {
                isOpen = !isOpen;
            }
        }
    }
}
