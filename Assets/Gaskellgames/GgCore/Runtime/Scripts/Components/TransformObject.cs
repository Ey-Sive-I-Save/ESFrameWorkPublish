using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [AddComponentMenu("Gaskellgames/GgCore/Transform Object")]
    public class TransformObject : GgMonoBehaviour
    {
        public enum UpdateMethod
        {
            ManualLerp,
            AutoLerp,
            AutoRotate
        }
        
        [SerializeField, Required]
        [Tooltip("Reference to the target object to be transformed.")]
        private Transform targetObject;
        
        [SerializeField]
        [Tooltip("The update method to transform the target object.")]
        private UpdateMethod updateMethod = UpdateMethod.ManualLerp;
        
        [SerializeField, Required]
        [Tooltip("Reference to the start transform. [Lerp = 0]")]
        private Transform start;
        
        [SerializeField, Required]
        [Tooltip("Reference to the end transform. [Lerp = 1]")]
        private Transform end;
        
        [SerializeField, Range(0, 1, true)]
        [Tooltip("Lerp value used to blend from start to end.")]
        private float lerpValue = 0;
        
        [SerializeField, Range(0, 2, true)]
        [Tooltip("Speed to auto update lerp value.")]
        private float autoLerpSpeed = 0.5f;
        
        [SerializeField]
        [Tooltip("Speed to update rotation.")]
        private Vector3 rotationSpeed = new Vector3(0, 5, 0);

        [SerializeField, HideInLine]
        [Tooltip("Toggles whether to update or not.")]
        private bool canUpdate = false;
        
        public float LerpValue
        {
            get => lerpValue;
            set
            {
                lerpValue = Mathf.Clamp01(value);
                
                if (!targetObject || !start || !end || updateMethod == UpdateMethod.AutoRotate) { return; }
                targetObject.localPosition = Vector3.Lerp(start.localPosition, end.localPosition, lerpValue);
                targetObject.localRotation = Quaternion.Lerp(start.localRotation, end.localRotation, lerpValue);
                targetObject.localScale = Vector3.Lerp(start.localScale, end.localScale, lerpValue);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) { return; }
            LerpValue = lerpValue;
        }

        protected override void OnDrawGizmosConditional(bool selected)
        {
            if (!targetObject || !start || !end) { return; }

            Color32 defaultColor = Gizmos.color;
            
            Gizmos.color = Color.white;
            Gizmos.DrawLine(start.position, end.position);
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(start.position, 0.1f);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(end.position, 0.1f);

            Gizmos.color = defaultColor;
        }
#endif

        private void Update()
        {
            if (!canUpdate) { return; }
            
            switch (updateMethod)
            {
                case UpdateMethod.AutoLerp:
                    float newLerp = LerpValue + (autoLerpSpeed * Time.deltaTime);
                    LerpValue = 1 <= newLerp ? 0 : newLerp;
                    break;
                
                case UpdateMethod.AutoRotate:
                    targetObject.transform.Rotate(new Vector3(rotationSpeed.x, rotationSpeed.y, rotationSpeed.z) * Time.deltaTime);
                    break;
                
                case UpdateMethod.ManualLerp:
                default:
                    return;
            }
        }

        /// <summary>
        /// Toggles whether to update or not.
        /// </summary>
        /// <param name="canUpdate"></param>
        public void StartStopAutoUpdate(bool canUpdate)
        {
            this.canUpdate = canUpdate;
        }
        
    } // class end
}
