using System;
using Sirenix.OdinInspector;
using UnityEngine;

#pragma warning disable 0414

namespace ES
{
    [AddComponentMenu("【ES】/开发与验证/示例/Runtime Watch Case 1 - Basic Types")]
    public class RuntimeWatchCase_1_BasicTypes : MonoBehaviour
    {
        [Title("ShowIf 与实时写入")]
        [SerializeField, LabelText("显示观察项")]
        private bool visible = true;

        [SerializeField, LabelText("实时更新（关闭后保留手动设值）")]
        private bool liveUpdate = true;

        [Title("可写基础类型")]
        [ESRuntimeWatch("案例/1 控制", "显示观察项", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("显示观察项")]
        public bool Visible
        {
            get => visible;
            set => visible = value;
        }

        [ESRuntimeWatch("案例/1 控制", "实时更新", category: ESRuntimeWatchAttribute.CategoryPerformance)]
        [LabelText("实时更新")]
        public bool LiveUpdate
        {
            get => liveUpdate;
            set => liveUpdate = value;
        }

        [ESRuntimeWatch("案例/1 基础类型", "布尔开关", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("布尔开关")]
        public bool boolValue = true;

        [ESRuntimeWatch("案例/1 基础类型", "字符串", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("字符串")]
        public string stringValue = "RuntimeWatch";

        [ESRuntimeWatch("案例/1 基础类型", "Int32", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("Int32")]
        public int intValue = 10;

        [ESRuntimeWatch("案例/1 基础类型", "Float", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryPerformance)]
        [LabelText("Float")]
        public float floatValue = 1.25f;

        [ESRuntimeWatch("案例/1 基础类型", "Double", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryPerformance)]
        [LabelText("Double")]
        public double doubleValue = 2.5d;

        [ESRuntimeWatch("案例/1 基础类型", "Long", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("Long")]
        public long longValue = 1000;

        [ESRuntimeWatch("案例/1 基础类型", "Short", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("Short")]
        public short shortValue = 12;

        [ESRuntimeWatch("案例/1 基础类型", "Byte", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("Byte")]
        public byte byteValue = 8;

        [ESRuntimeWatch("案例/1 基础类型", "UInt32", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("UInt32")]
        public uint uintValue = 32;

        [ESRuntimeWatch("案例/1 基础类型", "Enum 状态", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("Enum 状态")]
        public RuntimeWatchCaseState state = RuntimeWatchCaseState.Idle;

        [Title("只读与可写属性")]
        [ESRuntimeWatch("案例/1 只读类型", "位置", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        [LabelText("当前位置")]
        public Vector3 Position => transform.position;

        [ESRuntimeWatch("案例/1 只读类型", "旋转 Y", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
        [LabelText("旋转 Y")]
        public float Yaw => transform.eulerAngles.y;

        [ESRuntimeWatch("案例/1 可写属性", "可写属性 Int", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("可写属性 Int")]
        public int WritableScore { get; set; } = 100;

        private void Update()
        {
            if (!liveUpdate)
                return;

            floatValue = Mathf.PingPong(Time.time, 5f);
            doubleValue = Math.Round(Math.Sin(Time.time) * 10d, 3);
            longValue = Time.frameCount;
            state = (RuntimeWatchCaseState)(Time.frameCount / 120 % 4);
        }

        [ESRuntimeWatch("案例/1 控制", category: ESRuntimeWatchAttribute.CategoryPerformance)]
        [Button("切换实时更新")]
        [LabelText("切换实时更新")]
        public void ToggleLiveUpdate()
        {
            liveUpdate = !liveUpdate;
        }
    }
}

#pragma warning restore 0414
