#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES.TestAssets
{
    /// <summary>
    /// Bounded showcase camera interaction. It owns only camera pose and never creates UI.
    /// Right-drag or Alt-left-drag orbits, middle-drag pans, wheel zooms, F focuses the
    /// selected case and Home restores the authored camera pose.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ESCompositeShaderShowcaseCameraRig : MonoBehaviour
    {
        [SerializeField] private ESCompositeShaderTestAnimator animator;
        [SerializeField] private float orbitSensitivity = 4.5f;
        [SerializeField] private float panSensitivity = 0.0025f;
        [SerializeField] private float zoomSensitivity = 2.2f;
        [SerializeField] private float minDistance = 0.4f;
        [SerializeField] private float maxDistance = 80f;
        [SerializeField] private bool interactionEnabled = true;

        private Camera cameraComponent;
        private Transform focusTarget;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private float initialOrthographicSize;
        private Vector3 pivot;
        private float distance;
        private float yaw;
        private float pitch;

        public bool InteractionEnabled => interactionEnabled;

        private void Awake()
        {
            cameraComponent = GetComponent<Camera>();
            if (animator == null)
                animator = FindObjectOfType<ESCompositeShaderTestAnimator>();
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            initialOrthographicSize = cameraComponent != null ? cameraComponent.orthographicSize : 5f;
            pivot = transform.position + transform.forward * 5f;
            distance = Mathf.Clamp(Vector3.Distance(transform.position, pivot), minDistance, maxDistance);
            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = NormalizePitch(angles.x);
        }

        private void Update()
        {
            if (!interactionEnabled || cameraComponent == null)
                return;
            if (Input.GetKeyDown(KeyCode.Home))
                ResetCamera();
            if (Input.GetKeyDown(KeyCode.F))
                FocusSelected();
            if (IsPointerOverUi())
                return;

            bool orbit = Input.GetMouseButton(1) || (Input.GetKey(KeyCode.LeftAlt) && Input.GetMouseButton(0));
            if (orbit)
            {
                yaw += Input.GetAxisRaw("Mouse X") * orbitSensitivity;
                pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * orbitSensitivity, -80f, 80f);
                ApplyOrbit();
            }

            if (Input.GetMouseButton(2))
            {
                Vector3 right = transform.right * (-Input.GetAxisRaw("Mouse X") * panSensitivity * distance);
                Vector3 up = transform.up * (-Input.GetAxisRaw("Mouse Y") * panSensitivity * distance);
                pivot += right + up;
                ApplyOrbit();
            }

            float wheel = Input.GetAxisRaw("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) > 0.0001f)
            {
                if (cameraComponent.orthographic)
                    cameraComponent.orthographicSize = Mathf.Clamp(cameraComponent.orthographicSize - wheel * zoomSensitivity, 0.25f, 100f);
                else
                {
                    distance = Mathf.Clamp(distance * Mathf.Pow(0.82f, wheel * 10f), minDistance, maxDistance);
                    ApplyOrbit();
                }
            }
        }

        public void SetInteractionEnabled(bool enabled)
        {
            interactionEnabled = enabled;
        }

        public void FocusSelected()
        {
            if (animator == null)
                animator = FindObjectOfType<ESCompositeShaderTestAnimator>();
            Transform selected = animator != null ? animator.GetSelectedPresentationTarget() : null;
            if (selected == null)
                return;
            focusTarget = selected;
            pivot = selected.position;
            Renderer renderer = selected.GetComponentInChildren<Renderer>();
            float radius = renderer != null ? renderer.bounds.extents.magnitude : 1.5f;
            distance = Mathf.Clamp(Mathf.Max(radius * 3f, minDistance), minDistance, maxDistance);
            if (cameraComponent.orthographic)
                cameraComponent.orthographicSize = Mathf.Clamp(Mathf.Max(radius * 2.4f, 0.5f), 0.25f, 100f);
            ApplyOrbit();
        }

        public void ResetCamera()
        {
            focusTarget = null;
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            if (cameraComponent != null)
                cameraComponent.orthographicSize = initialOrthographicSize;
            pivot = transform.position + transform.forward * 5f;
            distance = Mathf.Clamp(Vector3.Distance(transform.position, pivot), minDistance, maxDistance);
            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = NormalizePitch(angles.x);
        }

        private void ApplyOrbit()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.rotation = rotation;
            if (!cameraComponent.orthographic)
                transform.position = pivot - rotation * Vector3.forward * distance;
            else
                transform.position = pivot - rotation * Vector3.forward * distance;
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private static float NormalizePitch(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            return angle;
        }
    }
}
#endif
