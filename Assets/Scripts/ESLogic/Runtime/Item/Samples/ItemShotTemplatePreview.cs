using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [DisallowMultipleComponent]
    [AddComponentMenu("ES/Item/样例/飞行物模板预览")]
    public sealed class ItemShotTemplatePreview : MonoBehaviour
    {
        [Title("通用 Item 模板")]
        [LabelText("自动补齐 Item")]
        public bool ensureItem = true;

        [LabelText("进入播放自动发射")]
        public bool launchOnStart = true;

        [LabelText("目标点")]
        public Transform targetPoint;

        [Title("飞行物配置")]
        [LabelText("瞄准模式")]
        public ShotAimMode aimMode = ShotAimMode.MustHit;

        [LabelText("阻挡模式")]
        public ShotBlockMode blockMode = ShotBlockMode.WorldOnly;

        [LabelText("速度")]
        public float speed = 12f;

        [LabelText("加速度")]
        public float acceleration = 30f;

        [LabelText("最大速度")]
        public float maxSpeed = 18f;

        [LabelText("发射延迟")]
        [MinValue(0)]
        public float launchDelay = 0.25f;

        [LabelText("预热时间")]
        [MinValue(0)]
        public float warmupTime = 0.2f;

        [LabelText("锁头开始")]
        [MinValue(0)]
        public float trackingStartTime = 0.1f;

        [LabelText("锁头持续")]
        public float trackingDuration = -1f;

        [LabelText("转向速度")]
        [MinValue(0)]
        public float turnSpeed = 540f;

        [LabelText("寿命")]
        public float lifeTime = 5f;

        [LabelText("命中半径")]
        public float hitRadius = 0.25f;

        [LabelText("命中层")]
        public LayerMask hitLayers = ~0;

        [LabelText("使用重力")]
        public bool useGravity;

        [LabelText("朝向速度方向")]
        public bool orientToVelocity = true;

        [Title("模板分层")]
        [ShowInInspector, ReadOnly, LabelText("模型节点")]
        private Transform ModelRoot => FindChild("Model_模型");

        [ShowInInspector, ReadOnly, LabelText("碰撞节点")]
        private Transform CollisionRoot => FindChild("Collision_碰撞");

        [ShowInInspector, ReadOnly, LabelText("表现节点")]
        private Transform VfxRoot => FindChild("VFX_表现");

        [ShowInInspector, ReadOnly, LabelText("调试节点")]
        private Transform DebugRoot => FindChild("Debug_调试");

        private Item _item;
        private ItemShotModule _shotModule;

        private void Reset()
        {
            RebuildTemplate();
        }

        private void OnValidate()
        {
            if (!gameObject.scene.IsValid())
                return;

            hitRadius = Mathf.Max(0.01f, hitRadius);
            speed = Mathf.Max(0f, speed);
            maxSpeed = Mathf.Max(speed, maxSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            lifeTime = Mathf.Max(0.01f, lifeTime);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += RebuildTemplateIfAlive;
                return;
            }
#endif
            RebuildTemplate();
        }

        private void Awake()
        {
            RebuildTemplate();
            ConfigureRuntimeShot();
        }

        private void Start()
        {
            if (!launchOnStart)
                return;

            LaunchPreview();
        }

        private void OnDrawGizmos()
        {
            Vector3 position = transform.position;
            Vector3 forward = transform.forward.sqrMagnitude > 0.001f ? transform.forward : Vector3.forward;

            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
            Gizmos.DrawLine(position, position + forward * 0.9f);
            Gizmos.DrawWireSphere(position, Mathf.Max(0.05f, hitRadius));

            Gizmos.color = new Color(1f, 0.7f, 0.15f, 0.9f);
            Gizmos.DrawWireCube(position + forward * 0.35f, new Vector3(0.18f, 0.18f, 0.7f));

            if (targetPoint == null)
                return;

            Gizmos.color = aimMode == ShotAimMode.MustHit
                ? new Color(1f, 0.25f, 0.2f, 0.9f)
                : new Color(0.25f, 1f, 0.35f, 0.9f);
            Gizmos.DrawLine(position, targetPoint.position);
            Gizmos.DrawWireSphere(targetPoint.position, Mathf.Max(0.1f, hitRadius));
        }

        [Button("重建模板分层")]
        public void RebuildTemplate()
        {
            if (ensureItem && GetComponent<Item>() == null)
                gameObject.AddComponent<Item>();

            EnsureModelRoot();
            EnsureCollisionRoot();
            EnsureVfxRoot();
            EnsureDebugRoot();
        }

        [Button("运行时发射")]
        public void LaunchPreview()
        {
            ConfigureRuntimeShot();
            if (_shotModule == null)
                return;

            if (targetPoint != null)
                _shotModule.LaunchTo(targetPoint, aimMode == ShotAimMode.MustHit);
            else
                _shotModule.Launch(transform.forward);
        }

        private void ConfigureRuntimeShot()
        {
            _item = GetComponent<Item>();
            if (_item == null)
                return;

            _shotModule = _item.GetMoudle<ItemShotModule>();
            _shotModule.aimMode = aimMode;
            _shotModule.blockMode = blockMode;
            _shotModule.hitLayers = hitLayers;
            _shotModule.castRadius = hitRadius;
            _shotModule.config = BuildMotionConfig();
        }

        private ShotMotionConfig BuildMotionConfig()
        {
            ShotMotionFlags flags = ShotMotionFlags.ClampSpeed;
            if (useGravity)
                flags |= ShotMotionFlags.UseGravity;
            if (orientToVelocity)
                flags |= ShotMotionFlags.OrientToVelocity;

            return new ShotMotionConfig
            {
                speed = speed,
                acceleration = acceleration,
                maxSpeed = maxSpeed,
                maxLifetime = lifeTime,
                launchDelay = launchDelay,
                warmupTime = warmupTime,
                arriveDistance = hitRadius,
                drag = 0f,
                turnSpeedDegrees = turnSpeed,
                trackingStartTime = trackingStartTime,
                trackingDuration = trackingDuration,
                gravity = Physics.gravity,
                flags = flags
            };
        }

        private void EnsureModelRoot()
        {
            Transform root = EnsureChild("Model_模型");
            Transform body = root.Find("Shot_Body_弹体");
            if (body == null)
            {
                GameObject bodyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                bodyObject.name = "Shot_Body_弹体";
                bodyObject.transform.SetParent(root, false);
                Collider bodyCollider = bodyObject.GetComponent<Collider>();
                if (bodyCollider != null)
                    DestroyTemplateObject(bodyCollider);
                body = bodyObject.transform;
            }

            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.localScale = new Vector3(0.18f, 0.45f, 0.18f);
        }

        private void EnsureCollisionRoot()
        {
            Transform root = EnsureChild("Collision_碰撞");
            SphereCollider sphere = root.GetComponent<SphereCollider>();
            if (sphere == null)
                sphere = root.gameObject.AddComponent<SphereCollider>();

            sphere.isTrigger = true;
            sphere.radius = hitRadius;

            Rigidbody body = GetComponent<Rigidbody>();
            if (body == null)
                body = gameObject.AddComponent<Rigidbody>();

            body.isKinematic = true;
            body.useGravity = false;
        }

        private void EnsureVfxRoot()
        {
            Transform root = EnsureChild("VFX_表现");
            ParticleSystem particles = root.GetComponent<ParticleSystem>();
            if (particles == null)
                particles = root.gameObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.startLifetime = 0.35f;
            main.startSpeed = 0.25f;
            main.startSize = 0.08f;
            main.maxParticles = 64;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 24f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.08f;
        }

        private void EnsureDebugRoot()
        {
            Transform root = EnsureChild("Debug_调试");
            Transform targetHint = root.Find("TargetHint_目标提示");
            if (targetHint == null)
            {
                GameObject hint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hint.name = "TargetHint_目标提示";
                hint.transform.SetParent(root, false);
                Collider hintCollider = hint.GetComponent<Collider>();
                if (hintCollider != null)
                    DestroyTemplateObject(hintCollider);
                targetHint = hint.transform;
            }

            targetHint.localPosition = new Vector3(0f, 0f, 1.5f);
            targetHint.localScale = Vector3.one * Mathf.Max(0.08f, hitRadius * 0.5f);
        }

        private Transform EnsureChild(string childName)
        {
            Transform child = transform.Find(childName);
            if (child != null)
                return child;

            GameObject childObject = new GameObject(childName);
            childObject.transform.SetParent(transform, false);
            return childObject.transform;
        }

        private Transform FindChild(string childName)
        {
            return transform != null ? transform.Find(childName) : null;
        }

        private static void DestroyTemplateObject(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

#if UNITY_EDITOR
        private void RebuildTemplateIfAlive()
        {
            if (this == null || gameObject == null)
                return;

            RebuildTemplate();
        }
#endif
    }
}
