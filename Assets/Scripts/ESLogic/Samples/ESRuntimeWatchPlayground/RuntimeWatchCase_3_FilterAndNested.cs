using System;
using Sirenix.OdinInspector;
using UnityEngine;

#pragma warning disable 0414

namespace ES
{
    [AddComponentMenu("【ES】/开发与验证/示例/Runtime Watch Case 3 - Filter And Nested")]
    public class RuntimeWatchCase_3_FilterAndNested : MonoBehaviour
    {
        [Title("筛选控制")]
        [SerializeField, LabelText("显示 Runtime 观察项")]
        private bool showRuntime = true;

        [SerializeField, LabelText("显示临时分类")]
        private bool showTemporary = true;

        [SerializeField, LabelText("实时更新嵌套数据")]
        private bool liveUpdate = true;

        [SerializeField, LabelText("要求的根 Tag")]
        private string requiredRootTag = "Player";

        [SerializeField, LabelText("嵌套演示数据")]
        private RuntimeWatchCaseNestedData nested = new RuntimeWatchCaseNestedData();

        [ESRuntimeWatch("案例/3 控制", "ShowIf 开关", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("ShowIf 开关")]
        public bool ShowRuntime
        {
            get => showRuntime;
            set => showRuntime = value;
        }

        [ESRuntimeWatch("案例/3 控制", "实时更新", category: ESRuntimeWatchAttribute.CategoryPerformance)]
        [LabelText("实时更新")]
        public bool LiveUpdate
        {
            get => liveUpdate;
            set => liveUpdate = value;
        }

        [ESRuntimeWatch("案例/3 筛选", "对象路径", showIf: "@this.showRuntime", category: ESRuntimeWatchAttribute.CategoryScene)]
        [LabelText("对象路径")]
        public string ObjectPath => BuildPath(transform);

        [ESRuntimeWatch("案例/3 筛选", "根 Tag", showIf: "@this.showRuntime", category: ESRuntimeWatchAttribute.CategoryScene)]
        [LabelText("根 Tag")]
        public string RootTag => transform.root != null ? transform.root.tag : gameObject.tag;

        [ESRuntimeWatch("案例/3 筛选", "Tag 过滤演示", requiredTag: "Player", showIf: "@this.showRuntime", category: ESRuntimeWatchAttribute.CategoryCharacter)]
        [LabelText("Tag 过滤演示")]
        public bool PassPlayerTag => RootTag == requiredRootTag;

        [ESRuntimeWatch("案例/3 临时", "临时开关", showIf: "@this.showTemporary", category: ESRuntimeWatchAttribute.CategoryTemporary)]
        [LabelText("临时开关")]
        public bool TemporaryVisible => showTemporary;

        [ESRuntimeWatch("案例/3 嵌套", "嵌套数据", showIf: "@this.showRuntime", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("嵌套数据")]
        public RuntimeWatchCaseNestedData Nested => nested;

        private void Update()
        {
            if (!liveUpdate)
                return;

            if (nested == null)
                nested = new RuntimeWatchCaseNestedData();

            nested.visible = showRuntime;
            nested.ownerName = name;
            nested.frame = Time.frameCount;
            nested.position = transform.position;
            nested.distanceToOrigin = transform.position.magnitude;
            nested.active = gameObject.activeInHierarchy;
        }

        [ESRuntimeWatch("案例/3 控制", category: ESRuntimeWatchAttribute.CategoryTemporary)]
        [Button("切换临时分类")]
        [LabelText("切换临时分类")]
        public void ToggleTemporary()
        {
            showTemporary = !showTemporary;
        }

        [ESRuntimeWatch("案例/3 控制", category: ESRuntimeWatchAttribute.CategoryPerformance)]
        [Button("切换实时更新")]
        [LabelText("切换实时更新")]
        public void ToggleLiveUpdate()
        {
            liveUpdate = !liveUpdate;
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
        public class RuntimeWatchCaseNestedData
        {
            [LabelText("嵌套可见")]
            public bool visible = true;

            [ESRuntimeWatch("案例/3 嵌套/字段", "嵌套 Owner", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
            [LabelText("嵌套 Owner")]
            public string ownerName;

            [ESRuntimeWatch("案例/3 嵌套/字段", "嵌套帧号", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryPerformance)]
            [LabelText("嵌套帧号")]
            public int frame;

            [ESRuntimeWatch("案例/3 嵌套/字段", "嵌套位置", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
            [LabelText("嵌套位置")]
            public Vector3 position;

            [ESRuntimeWatch("案例/3 嵌套/字段", "嵌套激活", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryScene)]
            [LabelText("嵌套激活")]
            public bool active;

            [ESRuntimeWatch("案例/3 嵌套/属性", "到原点距离", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryPerformance)]
            [LabelText("到原点距离")]
            public float distanceToOrigin;

            [ESRuntimeWatch("案例/3 嵌套/属性", "是否远离原点", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryPerformance)]
            [LabelText("是否远离原点")]
            public bool FarFromOrigin => distanceToOrigin > 5f;
        }
    }
}

#pragma warning restore 0414
