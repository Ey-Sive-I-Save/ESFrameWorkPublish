using Sirenix.OdinInspector;
using UnityEngine;

#pragma warning disable 0414

namespace ES
{
    [AddComponentMenu("ES Samples/Runtime Watch Video Case 2 - Methods")]
    public class RuntimeWatchVideoCase_2_Methods : MonoBehaviour
    {
        [Title("方法调用状态")]
        [SerializeField, LabelText("显示观察项")]
        private bool visible = true;

        [SerializeField, LabelText("计数器")]
        private int counter;

        [SerializeField, LabelText("消息")]
        private string message = "ready";

        [SerializeField, LabelText("状态")]
        private RuntimeWatchVideoState state = RuntimeWatchVideoState.Idle;

        [ESRuntimeWatch("视频案例/2 控制", "显示观察项", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("显示观察项")]
        public bool Visible
        {
            get => visible;
            set => visible = value;
        }

        [ESRuntimeWatch("视频案例/2 方法", "当前计数", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("当前计数")]
        public int Counter => counter;

        [ESRuntimeWatch("视频案例/2 方法", "消息", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("消息")]
        public string Message => message;

        [ESRuntimeWatch("视频案例/2 方法", "读取诊断文本", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("读取诊断文本")]
        public string GetDebugText()
        {
            return $"{name} | {state} | counter={counter} | message={message}";
        }

        [ESRuntimeWatch("视频案例/2 方法", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [Button("计数 +1")]
        [LabelText("计数 +1")]
        public void AddCounter()
        {
            counter++;
            message = "counter + 1";
        }

        [ESRuntimeWatch("视频案例/2 方法", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [Button("重置计数")]
        [LabelText("重置计数")]
        public void SetField_ResetCounter()
        {
            counter = 0;
            message = "reset";
        }

        [ESRuntimeWatch("视频案例/2 方法", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [Button("设置计数")]
        [LabelText("设置计数")]
        public void SetField_SetCounter(int value)
        {
            counter = Mathf.Max(0, value);
            message = "set counter";
        }

        [ESRuntimeWatch("视频案例/2 控制", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [Button("设置显示")]
        [LabelText("设置显示")]
        public void SetField_SetVisible(bool value)
        {
            visible = value;
            message = "set visible";
        }

        [ESRuntimeWatch("视频案例/2 方法", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryPerformance)]
        [Button("按倍率增加")]
        [LabelText("按倍率增加")]
        public void SetField_AddByScale(float value)
        {
            counter += Mathf.RoundToInt(value * 10f);
            message = "add by scale";
        }

        [ESRuntimeWatch("视频案例/2 方法", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [Button("设置消息")]
        [LabelText("设置消息")]
        public void SetField_SetMessage(string value)
        {
            message = string.IsNullOrWhiteSpace(value) ? "<empty>" : value;
        }

        [ESRuntimeWatch("视频案例/2 方法", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [Button("设置状态")]
        [LabelText("设置状态")]
        public void SetField_SetState(RuntimeWatchVideoState value)
        {
            state = value;
            message = "state changed";
        }

        [ESRuntimeWatch("视频案例/2 控制", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [Button("切换显示")]
        [LabelText("切换显示")]
        public void ToggleVisible()
        {
            visible = !visible;
        }
    }
}

#pragma warning restore 0414
