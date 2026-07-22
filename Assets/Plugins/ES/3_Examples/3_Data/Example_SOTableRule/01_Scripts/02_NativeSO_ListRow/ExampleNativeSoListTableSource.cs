using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES.Samples{
    [Serializable]
    public sealed class ExampleNativeSoRewardRow : IString
    {
        [LabelText("行ID")]
        public string rowKey = "reward_001";

        [LabelText("奖励名")]
        public string rewardName = "金币奖励";

        [LabelText("数量")]
        public int amount = 100;

        [LabelText("品质")]
        public ExampleSoTableQuality quality = ExampleSoTableQuality.Normal;

        [LabelText("标签")]
        public ExampleSoTableTags tags = ExampleSoTableTags.Stackable;

        [LabelText("嵌套配置")]
        public ExampleSoTableNestedConfig nestedConfig = new ExampleSoTableNestedConfig();

        public string GetSTR()
        {
            return rowKey;
        }

        public void SetSTR(string str)
        {
            rowKey = str;
        }
    }

    [CreateAssetMenu(fileName = "ExampleNativeSoListTableSource", menuName = "ES/示例/SO表格规则/普通SO-List行测试源")]
    public sealed class ExampleNativeSoListTableSource : ScriptableObject, IString, IESRowBindingProvider
    {
        [LabelText("SO ID")]
        public string itemId = "native_list_holder_001";

        [LabelText("显示名")]
        public string displayName = "普通SO持有List";

        [LabelText("启用")]
        public bool enabledInGame = true;

        [LabelText("奖励行列表")]
        [ESRowContainer("rowKey", "rowKey")]
        public List<ExampleNativeSoRewardRow> rewards = new List<ExampleNativeSoRewardRow>
        {
            new ExampleNativeSoRewardRow
            {
                rowKey = "reward_001",
                rewardName = "通关金币",
                amount = 100,
                quality = ExampleSoTableQuality.Normal
            },
            new ExampleNativeSoRewardRow
            {
                rowKey = "reward_002",
                rewardName = "稀有宝箱",
                amount = 1,
                quality = ExampleSoTableQuality.Rare,
                tags = ExampleSoTableTags.Tradable
            }
        };

        public string GetSTR()
        {
            return itemId;
        }

        public void SetSTR(string str)
        {
            itemId = str;
        }

        public ESRowBindingRule GetRowBindingRule()
        {
            return new ESRowBindingRule
            {
                targetMode = ESRowTargetMode.ObjectListElement,
                rowKeyColumnName = "rowKey",
                listFieldPath = nameof(rewards),
                containerKind = ESRowContainerKind.List,
                elementKeyFieldPath = nameof(ExampleNativeSoRewardRow.rowKey),
                createMissingElement = true,
                allowEmptyRowKey = false
            };
        }
    }
}
