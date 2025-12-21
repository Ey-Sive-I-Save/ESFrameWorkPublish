using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [AddComponentMenu("Gaskellgames/GgCore/On Object Events")]
    public class OnObjectEvents : GgMonoBehaviour
    {
        #region Variables
        
        [SerializeField]
        [Tooltip("")]
        private bool useOnStart = true;
        
        [SerializeField]
        [Tooltip("")]
        private bool useOnEnable = true;
        
        [SerializeField]
        [Tooltip("")]
        private bool useOnDisable = true;
        
        [SerializeField]
        [Tooltip("")]
        private bool useOnDestroy = true;

        [SerializeField]
        [Tooltip("")]
        private bool useOnEnter = false;
        
        [SerializeField]
        [Tooltip("")]
        private bool useOnStay = false;
        
        [SerializeField]
        [Tooltip("")]
        private bool useOnExit = false;
        
        [SerializeField]
        [Tooltip("")]
        public GgEvent<GameObject> onStart;
        
        [SerializeField]
        [Tooltip("")]
        public GgEvent<GameObject> onEnable;
        
        [SerializeField]
        [Tooltip("")]
        public GgEvent<GameObject> onDisable;
        
        [SerializeField]
        [Tooltip("")]
        public GgEvent<GameObject> onDestroy;
        
        [SerializeField]
        [Tooltip("")]
        public GgEvent<Collider> onEnter;
        
        [SerializeField]
        [Tooltip("")]
        public GgEvent<Collider> onStay;
        
        [SerializeField]
        [Tooltip("")]
        public GgEvent<Collider> onExit;

        [SerializeField]
        [Tooltip("")]
        private Color32 triggerColour = new Color32(128, 000, 223, 079);
        
        [SerializeField]
        [Tooltip("")]
        private Color32 triggerOutlineColour = new Color32(128, 000, 223, 128);

        private Collider sensorCollider;
        private bool sphereTrigger = false;
        private float sphereRadius = 1.0f;
        private Vector3 boxSize = Vector3.one;
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Gizmos
        
        protected override void OnDrawGizmosConditional(bool selected)
        {
            if (!sensorCollider) { return; }
            
            Matrix4x4 resetMatrix = Gizmos.matrix;
            Gizmos.matrix = gameObject.transform.localToWorldMatrix;

            if (sphereTrigger)
            {
                if (selected)
                {
                    Gizmos.color = triggerColour;
                    Gizmos.DrawSphere(Vector3.zero, sphereRadius);
                }
                Gizmos.color = triggerOutlineColour;
                Gizmos.DrawWireSphere(Vector3.zero, sphereRadius);
            }
            else
            {
                if (selected)
                {
                    Gizmos.color = triggerColour;
                    Gizmos.DrawCube(Vector3.zero, boxSize);
                }
                Gizmos.color = triggerOutlineColour;
                Gizmos.DrawWireCube(Vector3.zero, boxSize);
            }

            Gizmos.matrix = resetMatrix;
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Editor Loop

        private void Reset()
        {
            TrySetupCollider();
        }

        private void OnValidate()
        {
            TrySetupCollider();
        }
        
        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Game Loop
        
        private void Start()
        {
            if (!useOnStart) { return; }
            onStart?.Invoke(this.gameObject);
        }

        private void OnEnable()
        {
            if (!useOnEnable) { return; }
            onEnable?.Invoke(this.gameObject);
        }

        private void OnDisable()
        {
            if (!useOnDisable) { return; }
            onDisable?.Invoke(this.gameObject);
        }

        private void OnDestroy()
        {
            if (!useOnDestroy) { return; }
            onDestroy?.Invoke(this.gameObject);
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region OnEvents
        
        private void OnTriggerEnter(Collider other)
        {
            if (!useOnEnter) { return; }
            onEnter?.Invoke(other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!useOnStay) { return; }
            onStay?.Invoke(other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!useOnExit) { return; }
            onExit?.Invoke(other);
        }

        private void OnCollisionEnter(Collision other)
        {
            if (!useOnEnter) { return; }
            onEnter?.Invoke(other.collider);
        }

        private void OnCollisionStay(Collision other)
        {
            if (!useOnStay) { return; }
            onStay?.Invoke(other.collider);
        }

        private void OnCollisionExit(Collision other)
        {
            if (!useOnExit) { return; }
            onExit?.Invoke(other.collider);
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Private Functions

        private void TrySetupCollider()
        {
            if (!useOnEnter && !useOnStay && !useOnExit) { return; }
            
            SphereCollider sphere = gameObject.GetComponent<SphereCollider>();
            BoxCollider box = gameObject.GetComponent<BoxCollider>();
            if (!sphere && !box) { box = gameObject.AddComponent<BoxCollider>(); }

            if (box != null)
            {
                sensorCollider = box;
                sphereTrigger = false;
                boxSize = box.size;
            }
            else if (sphere != null)
            {
                sensorCollider = sphere;
                sphereTrigger = true;
                sphereRadius = sphere.radius;
            }
        }

        #endregion

    } // class end
}