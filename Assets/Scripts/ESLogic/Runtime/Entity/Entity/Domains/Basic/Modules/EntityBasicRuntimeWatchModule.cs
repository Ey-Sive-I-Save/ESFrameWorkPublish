using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("基础模块/RuntimeWatch验证模块")]
    public class EntityBasicRuntimeWatchModule : EntityBasicModuleBase
    {
        [Title("RuntimeWatch 验证")]
        [LabelText("启用采样")]
        public bool enableSampling = true;

        [LabelText("显示调试项")]
        public bool showWatchItems = true;

        [LabelText("Tag 过滤")]
        public string requiredRootTag = "Player";

        [ESRuntimeWatch("Entity/Base", "RW模块已提交", showIf: "@this.showWatchItems")]
        public bool watchModuleSubmitted;

        [ESRuntimeWatch("Entity/Base", "RW模块帧号", showIf: "@this.showWatchItems")]
        public int watchFrame;

        [LabelText("嵌套观测数据")]
        public RuntimeWatchSampleData sample = new RuntimeWatchSampleData();

        [ESRuntimeWatch("Entity/Base/Properties", "Module采样次数", showIf: "@this.showWatchItems")]
        public int SampleCount => sample != null ? sample.sampleCount : -1;

        [ESRuntimeWatch("Entity/Base/Properties", "Module实体名", showIf: "@this.showWatchItems")]
        public string EntityName => sample != null ? sample.entityName : "<空>";

        [ESRuntimeWatch("Entity/Base/Properties", "根Tag匹配", showIf: "@this.showWatchItems")]
        public bool IsRequiredRootTag => sample != null && sample.rootTag == requiredRootTag;

        public override void Start()
        {
            base.Start();
            RefreshSample();
        }

        protected override void Update()
        {
            if (!enableSampling)
                return;

            RefreshSample();
        }

        [ESRuntimeWatch("Entity/Base/Methods", "刷新样例数据", showIf: "@this.showWatchItems")]
        [Button("刷新 RuntimeWatch 样例数据")]
        public void RefreshSample()
        {
            watchModuleSubmitted = Signal_HasSubmit;
            watchFrame = Time.frameCount;

            if (sample == null)
                sample = new RuntimeWatchSampleData();

            sample.visible = showWatchItems;
            sample.entityName = MyCore != null ? MyCore.name : "<无Entity>";
            sample.rootTag = MyCore != null && MyCore.transform != null && MyCore.transform.root != null
                ? MyCore.transform.root.tag
                : string.Empty;
            sample.activeInHierarchy = MyCore != null && MyCore.gameObject.activeInHierarchy;
            sample.position = MyCore != null ? MyCore.transform.position : Vector3.zero;
            sample.domainLinked = MyDomain != null;
            sample.moduleEnabled = Signal_IsActiveAndEnable;
            sample.sampleCount++;
        }

        [ESRuntimeWatch("Entity/Base/Methods", showIf: "@this.showWatchItems")]
        [Button("重置采样次数")]
        public void SetField_ResetRuntimeWatchSampleCount()
        {
            if (sample == null)
                sample = new RuntimeWatchSampleData();

            sample.sampleCount = 0;
        }

        [ESRuntimeWatch("Entity/Base/Methods", showIf: "@this.showWatchItems")]
        [Button("设置采样次数")]
        public void SetField_SetRuntimeWatchSampleCount(int value)
        {
            if (sample == null)
                sample = new RuntimeWatchSampleData();

            sample.sampleCount = Mathf.Max(0, value);
        }

        [Serializable]
        public class RuntimeWatchSampleData
        {
            public bool visible = true;

            [ESRuntimeWatch("Entity/Base/Nested", "实体名", showIf: "@this.visible")]
            public string entityName; 

            [ESRuntimeWatch("Entity/Base/Nested", "根Tag", requiredTag: "Player", showIf: "@this.visible")]
            public string rootTag;

            [ESRuntimeWatch("Entity/Base/Nested", "层级激活", showIf: "@this.visible")]
            public bool activeInHierarchy;

            [ESRuntimeWatch("Entity/Base/Nested", "当前位置", showIf: "@this.visible")]
            public Vector3 position;

            [ESRuntimeWatch("Entity/Base/Nested", "Domain已链接", showIf: "@this.visible")]
            public bool domainLinked;

            [ESRuntimeWatch("Entity/Base/Nested", "Module启用", showIf: "@this.visible")]
            public bool moduleEnabled;

            [ESRuntimeWatch("Entity/Base/Nested", "采样次数", showIf: "@this.visible")]
            public int sampleCount;

            [ESRuntimeWatch("Entity/Base/Nested/Properties", "采样是否超过10", showIf: "@this.visible")]
            public bool SampleOverTen => sampleCount > 10;
        }
    }
}
