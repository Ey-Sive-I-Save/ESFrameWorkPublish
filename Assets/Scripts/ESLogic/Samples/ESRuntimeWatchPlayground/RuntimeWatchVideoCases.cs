using System;
using Sirenix.OdinInspector;
using UnityEngine;

#pragma warning disable 0414

namespace ES
{
    public enum RuntimeWatchVideoState
    {
        Idle,
        Patrol,
        Combat,
        Disabled
    }

    [AddComponentMenu("ES Samples/Runtime Watch Video Case 1 - Basic Types")]
    public class RuntimeWatchVideoCase_1_BasicTypes : MonoBehaviour
    {
        [SerializeField] private bool visible = true;
        [SerializeField] private bool liveUpdate = true;

        [ESRuntimeWatch("视频案例/1 基础类型", "布尔开关", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        public bool boolValue = true;

        [ESRuntimeWatch("视频案例/1 基础类型", "字符串", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        public string stringValue = "RuntimeWatch";

        [ESRuntimeWatch("视频案例/1 基础类型", "Int32", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        public int intValue = 10;

        [ESRuntimeWatch("视频案例/1 基础类型", "Float", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryPerformance)]
        public float floatValue = 1.25f;

        [ESRuntimeWatch("视频案例/1 基础类型", "Double", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryPerformance)]
        public double doubleValue = 2.5d;

        [ESRuntimeWatch("视频案例/1 基础类型", "Long", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        public long longValue = 1000;

        [ESRuntimeWatch("视频案例/1 基础类型", "Short", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        public short shortValue = 12;

        [ESRuntimeWatch("视频案例/1 基础类型", "Byte", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        public byte byteValue = 8;

        [ESRuntimeWatch("视频案例/1 基础类型", "UInt32", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        public uint uintValue = 32;

        [ESRuntimeWatch("视频案例/1 基础类型", "Enum状态", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        public RuntimeWatchVideoState state = RuntimeWatchVideoState.Idle;

        [ESRuntimeWatch("视频案例/1 只读类型", "位置", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        public Vector3 Position => transform.position;

        [ESRuntimeWatch("视频案例/1 只读类型", "旋转Y", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        public float Yaw => transform.eulerAngles.y;

        [ESRuntimeWatch("视频案例/1 可写属性", "可写属性Int", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        public int WritableScore { get; set; } = 100;

        private void Update()
        {
            if (!liveUpdate)
                return;

            floatValue = Mathf.PingPong(Time.time, 5f);
            doubleValue = Math.Round(Math.Sin(Time.time) * 10d, 3);
            longValue = Time.frameCount;
            state = (RuntimeWatchVideoState)(Time.frameCount / 120 % 4);
        }
    }

    [AddComponentMenu("ES Samples/Runtime Watch Video Case 2 - Methods")]
    public class RuntimeWatchVideoCase_2_Methods : MonoBehaviour
    {
        [SerializeField] private bool visible = true;
        [SerializeField] private int counter;
        [SerializeField] private string message = "ready";
        [SerializeField] private RuntimeWatchVideoState state = RuntimeWatchVideoState.Idle;

        [ESRuntimeWatch("视频案例/2 方法", "当前计数", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        public int Counter => counter;

        [ESRuntimeWatch("视频案例/2 方法", "消息", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        public string Message => message;

        [ESRuntimeWatch("视频案例/2 方法", "读取诊断文本", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        public string GetDebugText()
        {
            return $"{name} | {state} | counter={counter} | message={message}";
        }

        [ESRuntimeWatch("视频案例/2 方法", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [Button("计数+1")]
        public void AddCounter()
        {
            counter++;
            message = "counter + 1";
        }

        [ESRuntimeWatch("视频案例/2 方法", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [Button("重置计数")]
        public void SetField_ResetCounter()
        {
            counter = 0;
            message = "reset";
        }

        [ESRuntimeWatch("视频案例/2 方法", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [Button("设置计数")]
        public void SetField_SetCounter(int value)
        {
            counter = Mathf.Max(0, value);
            message = "set counter";
        }

        [ESRuntimeWatch("视频案例/2 方法", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [Button("设置显示")]
        public void SetField_SetVisible(bool value)
        {
            visible = value;
            message = "set visible";
        }

        [ESRuntimeWatch("视频案例/2 方法", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryPerformance)]
        [Button("按倍率增加")]
        public void SetField_AddByScale(float value)
        {
            counter += Mathf.RoundToInt(value * 10f);
            message = "add by scale";
        }

        [ESRuntimeWatch("视频案例/2 方法", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [Button("设置消息")]
        public void SetField_SetMessage(string value)
        {
            message = string.IsNullOrWhiteSpace(value) ? "<empty>" : value;
        }

        [ESRuntimeWatch("视频案例/2 方法", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [Button("设置状态")]
        public void SetField_SetState(RuntimeWatchVideoState value)
        {
            state = value;
            message = "state changed";
        }

        [ESRuntimeWatch("视频案例/2 方法", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [Button("切换显示")]
        public void ToggleVisible()
        {
            visible = !visible;
        }
    }

    [AddComponentMenu("ES Samples/Runtime Watch Video Case 3 - Filter And Nested")]
    public class RuntimeWatchVideoCase_3_FilterAndNested : MonoBehaviour
    {
        [SerializeField] private bool showRuntime = true;
        [SerializeField] private bool showTemporary = true;
        [SerializeField] private string requiredRootTag = "Player";
        [SerializeField] private RuntimeWatchVideoNestedData nested = new RuntimeWatchVideoNestedData();

        [ESRuntimeWatch("视频案例/3 筛选", "对象路径", showIf: "@this.showRuntime", category: ESRuntimeWatchAttribute.CategoryScene)]
        public string ObjectPath => BuildPath(transform);

        [ESRuntimeWatch("视频案例/3 筛选", "根Tag", showIf: "@this.showRuntime", category: ESRuntimeWatchAttribute.CategoryScene)]
        public string RootTag => transform.root != null ? transform.root.tag : gameObject.tag;

        [ESRuntimeWatch("视频案例/3 筛选", "Tag过滤演示", requiredTag: "Player", showIf: "@this.showRuntime", category: ESRuntimeWatchAttribute.CategoryCharacter)]
        public bool PassPlayerTag => RootTag == requiredRootTag;

        [ESRuntimeWatch("视频案例/3 临时", "临时开关", showIf: "@this.showTemporary", category: ESRuntimeWatchAttribute.CategoryTemporary)]
        public bool TemporaryVisible => showTemporary;

        [ESRuntimeWatch("视频案例/3 嵌套", "嵌套数据", showIf: "@this.showRuntime", category: ESRuntimeWatchAttribute.CategoryDebug)]
        public RuntimeWatchVideoNestedData Nested => nested;

        private void Update()
        {
            if (nested == null)
                nested = new RuntimeWatchVideoNestedData();

            nested.visible = showRuntime;
            nested.ownerName = name;
            nested.frame = Time.frameCount;
            nested.position = transform.position;
            nested.distanceToOrigin = transform.position.magnitude;
            nested.active = gameObject.activeInHierarchy;
        }

        [ESRuntimeWatch("视频案例/3 临时", showIf: "@this.showRuntime", category: ESRuntimeWatchAttribute.CategoryTemporary)]
        [Button("切换临时分类")]
        public void ToggleTemporary()
        {
            showTemporary = !showTemporary;
        }

        private static string BuildPath(Transform target)
        {
            if (target == null)
                return "<null>";

            string path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = target.name + "/" + path;
            }

            return path;
        }

        [Serializable]
        public class RuntimeWatchVideoNestedData
        {
            public bool visible = true;

            [ESRuntimeWatch("视频案例/3 嵌套/字段", "嵌套Owner", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
            public string ownerName;

            [ESRuntimeWatch("视频案例/3 嵌套/字段", "嵌套帧号", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryPerformance)]
            public int frame;

            [ESRuntimeWatch("视频案例/3 嵌套/字段", "嵌套位置", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
            public Vector3 position;

            [ESRuntimeWatch("视频案例/3 嵌套/字段", "嵌套激活", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
            public bool active;

            [ESRuntimeWatch("视频案例/3 嵌套/属性", "到原点距离", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryPerformance)]
            public float distanceToOrigin;

            [ESRuntimeWatch("视频案例/3 嵌套/属性", "是否远离原点", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryPerformance)]
            public bool FarFromOrigin => distanceToOrigin > 5f;
        }
    }

    [AddComponentMenu("ES Samples/Runtime Watch Video Case 4 - Unity Types")]
    public class RuntimeWatchVideoCase_4_UnityTypes : MonoBehaviour
    {
        [SerializeField] private bool visible = true;
        [SerializeField] private Transform target;
        [SerializeField] private GameObject targetObject;
        [SerializeField] private LayerMask layerMask = -1;

        [ESRuntimeWatch("视频案例/4 Unity类型", "Vector2", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        public Vector2 vector2Value = new Vector2(1f, 2f);

        [ESRuntimeWatch("视频案例/4 Unity类型", "Vector3", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        public Vector3 vector3Value = new Vector3(1f, 2f, 3f);

        [ESRuntimeWatch("视频案例/4 Unity类型", "Vector4", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        public Vector4 vector4Value = new Vector4(1f, 2f, 3f, 4f);

        [ESRuntimeWatch("视频案例/4 Unity类型", "Color", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryUI)]
        public Color colorValue = new Color(0.2f, 0.7f, 1f, 1f);

        [ESRuntimeWatch("视频案例/4 Unity类型", "Quaternion", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        public Quaternion rotationValue = Quaternion.identity;

        [ESRuntimeWatch("视频案例/4 Unity类型", "Rect", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryUI)]
        public Rect rectValue = new Rect(0f, 0f, 128f, 64f);

        [ESRuntimeWatch("视频案例/4 Unity类型", "Bounds", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        public Bounds boundsValue = new Bounds(Vector3.zero, Vector3.one * 2f);

        [ESRuntimeWatch("视频案例/4 Unity引用", "Target Transform", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        public Transform Target => target;

        [ESRuntimeWatch("视频案例/4 Unity引用", "Target GameObject", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        public GameObject TargetObject => targetObject != null ? targetObject : gameObject;

        [ESRuntimeWatch("视频案例/4 Unity引用", "LayerMask", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        public LayerMask LayerMaskValue => layerMask;

        [ESRuntimeWatch("视频案例/4 Unity属性", "子物体数量", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        public int ChildCount => transform.childCount;

        [ESRuntimeWatch("视频案例/4 Unity属性", "是否有目标", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        public bool HasTarget => target != null || targetObject != null;

        private void Update()
        {
            float time = Time.time;
            vector2Value = new Vector2(Mathf.Sin(time), Mathf.Cos(time));
            vector3Value = transform.position;
            vector4Value = new Vector4(transform.position.x, transform.position.y, transform.position.z, time);
            rotationValue = transform.rotation;
            boundsValue = new Bounds(transform.position, Vector3.one * (1f + Mathf.PingPong(time, 2f)));
        }

        [ESRuntimeWatch("视频案例/4 Unity方法", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        [Button("吸附到原点")]
        public void SnapToOrigin()
        {
            transform.position = Vector3.zero;
        }

        [ESRuntimeWatch("视频案例/4 Unity方法", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        [Button("设置高度")]
        public void SetField_SetHeight(float value)
        {
            Vector3 position = transform.position;
            position.y = value;
            transform.position = position;
        }
    }
}

#pragma warning restore 0414
