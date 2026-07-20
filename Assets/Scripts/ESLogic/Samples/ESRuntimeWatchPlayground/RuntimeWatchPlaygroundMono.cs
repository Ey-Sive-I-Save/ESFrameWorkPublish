using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [AddComponentMenu("ES Samples/Runtime Watch Playground")]
    public class RuntimeWatchPlaygroundMono : MonoBehaviour
    {
        [Header("RuntimeWatch Sample")]
        [SerializeField] private bool enableWatch = true;
        [SerializeField] private bool enableSampling = true;
        [SerializeField] private string requiredRootTag = "Player";
        [SerializeField] private int targetSampleCount = 12;
        [SerializeField] private float tickScale = 1f;

        [ESRuntimeWatch("Mono/Direct", "启用观察", showIf: "@this.enableWatch")]
        public bool watchEnabled;

        [ESRuntimeWatch("Mono/Direct", "当前帧", showIf: "@this.enableWatch")]
        public int frame;

        [ESRuntimeWatch("Mono/Direct", "根对象名", showIf: "@this.enableWatch")]
        public string rootName;

        [ESRuntimeWatch("Mono/Direct", "根Tag", requiredTag: "Player", showIf: "@this.enableWatch")]
        public string rootTag;

        [ESRuntimeWatch("Mono/Direct", "是否目标Tag", showIf: "@this.enableWatch")]
        public bool isTargetTag;

        [ESRuntimeWatch("Mono/Direct", "Tick Scale", showIf: "@this.enableWatch")]
        public float TickScale => tickScale;

        public RuntimeWatchMonoData nested = new RuntimeWatchMonoData();

        private void OnEnable()
        {
            Refresh();
        }

        private void Update()
        {
            if (!enableSampling)
                return;

            Refresh();
        }

        [ContextMenu("ES RuntimeWatch/Refresh Sample Values")]
        public void Refresh()
        {
            watchEnabled = enableWatch;
            frame = Time.frameCount;
            rootName = transform.root != null ? transform.root.name : name;
            rootTag = transform.root != null ? transform.root.tag : string.Empty;
            isTargetTag = string.Equals(rootTag, requiredRootTag, StringComparison.Ordinal);

            if (nested == null)
                nested = new RuntimeWatchMonoData();

            nested.visible = enableWatch;
            nested.ownerName = name;
            nested.rootTag = rootTag;
            nested.position = transform.position;
            nested.worldScale = transform.lossyScale;
            nested.activeInHierarchy = gameObject.activeInHierarchy;
            nested.sampleCount++;
            nested.targetSampleCount = targetSampleCount;
        }

        [ESRuntimeWatch("Mono/Methods", "读取采样次数", showIf: "@this.enableWatch")]
        public int GetRuntimeWatchSampleCount()
        {
            return nested != null ? nested.sampleCount : -1;
        }

        [ESRuntimeWatch("Mono/Methods", showIf: "@this.enableWatch")]
        [Button("重置采样次数")]
        public void SetField_ResetRuntimeWatchSampleCount()
        {
            if (nested == null)
                nested = new RuntimeWatchMonoData();

            nested.sampleCount = 0;
        }

        [ESRuntimeWatch("Mono/Methods", showIf: "@this.enableWatch")]
        [Button("设置采样次数")]
        public void SetField_RuntimeWatchSampleCount(int value)
        {
            if (nested == null)
                nested = new RuntimeWatchMonoData();

            nested.sampleCount = Mathf.Max(0, value);
        }

        [ESRuntimeWatch("Mono/Methods", "切换采样", showIf: "@this.enableWatch")]
        [Button("切换采样开关")]
        public void ToggleSampling()
        {
            enableSampling = !enableSampling;
        }

        [ESRuntimeWatch("Mono/Direct", "根Tag是否匹配", showIf: "@this.enableWatch")]
        public bool IsRootTagMatched()
        {
            return string.Equals(rootTag, requiredRootTag, StringComparison.Ordinal);
        }

        [Serializable]
        public class RuntimeWatchMonoData
        {
            public bool visible = true;

            [ESRuntimeWatch("Mono/Nested", "对象名", showIf: "@this.visible")]
            public string ownerName;

            [ESRuntimeWatch("Mono/Nested", "根Tag", showIf: "@this.visible")]
            public string rootTag;

            [ESRuntimeWatch("Mono/Nested", "位置", showIf: "@this.visible")]
            public Vector3 position;

            [ESRuntimeWatch("Mono/Nested", "缩放", showIf: "@this.visible")]
            public Vector3 worldScale;

            [ESRuntimeWatch("Mono/Nested", "层级激活", showIf: "@this.visible")]
            public bool activeInHierarchy;

            [ESRuntimeWatch("Mono/Nested", "采样次数", showIf: "@this.visible")]
            public int sampleCount;

            [ESRuntimeWatch("Mono/Nested", "目标采样值", showIf: "@this.visible")]
            public int targetSampleCount;

            [ESRuntimeWatch("Mono/Nested", "采样偏差", showIf: "@this.visible")]
            public int SampleDelta => sampleCount - targetSampleCount;
        }
    }
}
