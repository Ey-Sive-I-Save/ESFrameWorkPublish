using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES.Samples{
    [ESCreatePath("数据信息", "SO表格示例数据信息")]
    public sealed class ExampleSoTableInfo : SoDataInfo
    {
        [LabelText("显示名")]
        public string displayName = "示例Info";

        [LabelText("数量")]
        public int count = 10;

        [LabelText("倍率")]
        public float ratio = 1.5f;

        [LabelText("启用")]
        public bool enabledInGame = true;

        [LabelText("品质")]
        public ExampleSoTableQuality quality = ExampleSoTableQuality.Epic;

        [LabelText("标签")]
        public ExampleSoTableTags tags = ExampleSoTableTags.TestOnly;

        [LabelText("掉落标签列表")]
        public List<string> dropTags = new List<string> { "金币", "材料" };

        [LabelText("数值列表")]
        public List<int> values = new List<int> { 1, 2, 3 };

        [LabelText("可选品质列表")]
        public List<ExampleSoTableQuality> qualityOptions = new List<ExampleSoTableQuality>
        {
            ExampleSoTableQuality.Rare,
            ExampleSoTableQuality.Epic
        };

        [SerializeField, LabelText("私有序列化分数")]
        private int privateScore = 88;

        [NonSerialized]
        public string runtimeOnly = "不应进入表格";

        public int PrivateScore => privateScore;
    }
}
