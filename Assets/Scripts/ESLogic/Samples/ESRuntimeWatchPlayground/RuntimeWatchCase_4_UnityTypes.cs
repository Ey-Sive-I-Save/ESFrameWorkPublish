using Sirenix.OdinInspector;
using UnityEngine;

#pragma warning disable 0414

namespace ES
{
    [AddComponentMenu("【ES】/开发与验证/示例/Runtime Watch Case 4 - Unity Types")]
    public class RuntimeWatchCase_4_UnityTypes : MonoBehaviour
    {
        [Title("Unity 类型演示")]
        [SerializeField, LabelText("显示观察项")]
        private bool visible = true;

        [SerializeField, LabelText("实时更新（关闭后保留手动设值）")]
        private bool liveUpdate = true;

        [SerializeField, LabelText("目标 Transform")]
        private Transform target;

        [SerializeField, LabelText("目标 GameObject")]
        private GameObject targetObject;

        [SerializeField, LabelText("Layer Mask")]
        private LayerMask layerMask = -1;

        [ESRuntimeWatch("案例/4 控制", "显示观察项", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("显示观察项")]
        public bool Visible
        {
            get => visible;
            set => visible = value;
        }

        [ESRuntimeWatch("案例/4 控制", "实时更新", category: ESRuntimeWatchAttribute.CategoryPerformance)]
        [LabelText("实时更新")]
        public bool LiveUpdate
        {
            get => liveUpdate;
            set => liveUpdate = value;
        }

        [ESRuntimeWatch("案例/4 Unity类型", "Vector2", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        [LabelText("Vector2")]
        public Vector2 vector2Value = new Vector2(1f, 2f);

        [ESRuntimeWatch("案例/4 Unity类型", "Vector3", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        [LabelText("Vector3")]
        public Vector3 vector3Value = new Vector3(1f, 2f, 3f);

        [ESRuntimeWatch("案例/4 Unity类型", "Vector4", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        [LabelText("Vector4")]
        public Vector4 vector4Value = new Vector4(1f, 2f, 3f, 4f);

        [ESRuntimeWatch("案例/4 Unity类型", "Color", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryUI)]
        [LabelText("Color")]
        public Color colorValue = new Color(0.2f, 0.7f, 1f, 1f);

        [ESRuntimeWatch("案例/4 Unity类型", "Quaternion", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        [LabelText("Quaternion")]
        public Quaternion rotationValue = Quaternion.identity;

        [ESRuntimeWatch("案例/4 Unity类型", "Rect", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryUI)]
        [LabelText("Rect")]
        public Rect rectValue = new Rect(0f, 0f, 128f, 64f);

        [ESRuntimeWatch("案例/4 Unity类型", "Bounds", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        [LabelText("Bounds")]
        public Bounds boundsValue = new Bounds(Vector3.zero, Vector3.one * 2f);

        [ESRuntimeWatch("案例/4 Unity引用", "Target Transform", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        [LabelText("Target Transform")]
        public Transform Target => target;

        [ESRuntimeWatch("案例/4 Unity引用", "Target GameObject", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        [LabelText("Target GameObject")]
        public GameObject TargetObject => targetObject != null ? targetObject : gameObject;

        [ESRuntimeWatch("案例/4 Unity引用", "LayerMask", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        [LabelText("LayerMask")]
        public LayerMask LayerMaskValue => layerMask;

        [ESRuntimeWatch("案例/4 Unity属性", "子物体数量", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        [LabelText("子物体数量")]
        public int ChildCount => transform.childCount;

        [ESRuntimeWatch("案例/4 Unity属性", "是否有目标", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        [LabelText("是否有目标")]
        public bool HasTarget => target != null || targetObject != null;

        private void Update()
        {
            if (!liveUpdate)
                return;

            float time = Time.time;
            vector2Value = new Vector2(Mathf.Sin(time), Mathf.Cos(time));
            vector3Value = transform.position;
            vector4Value = new Vector4(transform.position.x, transform.position.y, transform.position.z, time);
            rotationValue = transform.rotation;
            boundsValue = new Bounds(transform.position, Vector3.one * (1f + Mathf.PingPong(time, 2f)));
        }

        [ESRuntimeWatch("案例/4 Unity方法", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        [Button("吸附到原点")]
        [LabelText("吸附到原点")]
        public void SnapToOrigin()
        {
            transform.position = Vector3.zero;
        }

        [ESRuntimeWatch("案例/4 Unity方法", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        [Button("设置高度")]
        [LabelText("设置高度")]
        public void SetField_SetHeight(float value)
        {
            Vector3 position = transform.position;
            position.y = value;
            transform.position = position;
        }

        [ESRuntimeWatch("案例/4 控制", category: ESRuntimeWatchAttribute.CategoryPerformance)]
        [Button("切换实时更新")]
        [LabelText("切换实时更新")]
        public void ToggleLiveUpdate()
        {
            liveUpdate = !liveUpdate;
        }

        public void ConfigureShowcaseTarget(Transform showcaseTarget)
        {
            target = showcaseTarget;
            targetObject = showcaseTarget != null ? showcaseTarget.gameObject : null;
        }

        public bool HasShowcaseTarget(Transform showcaseTarget)
        {
            return target == showcaseTarget
                && targetObject == (showcaseTarget != null ? showcaseTarget.gameObject : null);
        }
    }
}

#pragma warning restore 0414
