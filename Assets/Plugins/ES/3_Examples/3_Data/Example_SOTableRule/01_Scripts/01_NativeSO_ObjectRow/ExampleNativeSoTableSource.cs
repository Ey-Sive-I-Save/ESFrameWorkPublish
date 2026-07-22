using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES.Samples{
    public enum ExampleSoTableQuality
    {
        [LabelText("普通")]
        Normal = 0,

        [LabelText("稀有")]
        Rare = 1,

        [LabelText("史诗")]
        Epic = 2
    }

    [Flags]
    public enum ExampleSoTableTags
    {
        [LabelText("无")]
        None = 0,

        [LabelText("可交易")]
        Tradable = 1 << 0,

        [LabelText("可堆叠")]
        Stackable = 1 << 1,

        [LabelText("测试专用")]
        TestOnly = 1 << 2
    }

    [Serializable]
    public sealed class ExampleSoTableNestedConfig
    {
        [LabelText("成长倍率")]
        public float growthMultiplier = 1.2f;

        [LabelText("备注")]
        public string note = "嵌套字段测试";
    }

    [CreateAssetMenu(fileName = "ExampleNativeSoTableSource", menuName = "ES/示例/SO表格规则/普通SO测试源")]
    public sealed class ExampleNativeSoTableSource : ScriptableObject
    {
        [LabelText("物品ID")]
        public string itemId = "native_sword_001";

        [LabelText("显示名")]
        public string displayName = "测试长剑";

        [LabelText("等级")]
        public int level = 3;

        [LabelText("价格")]
        public float price = 12.5f;

        [LabelText("启用")]
        public bool enabledInGame = true;

        [LabelText("品质")]
        public ExampleSoTableQuality quality = ExampleSoTableQuality.Rare;

        [LabelText("标签")]
        public ExampleSoTableTags tags = ExampleSoTableTags.Tradable | ExampleSoTableTags.Stackable;

        [LabelText("关键词列表")]
        public List<string> keywords = new List<string> { "武器", "近战", "测试" };

        [LabelText("等级奖励列表")]
        public List<int> levelRewards = new List<int> { 100, 200, 300 };

        [LabelText("品质候选列表")]
        public List<ExampleSoTableQuality> qualityOptions = new List<ExampleSoTableQuality>
        {
            ExampleSoTableQuality.Normal,
            ExampleSoTableQuality.Rare
        };

        [SerializeField, LabelText("内部权重")]
        private int internalWeight = 7;

        [LabelText("嵌套配置")]
        public ExampleSoTableNestedConfig nestedConfig = new ExampleSoTableNestedConfig();

        [NonSerialized]
        public string runtimeCache = "不应进入表格";

        public readonly int readonlyValue = 99;

        public int InternalWeight => internalWeight;
    }
}
