using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable]
    public sealed class ExampleSoTableDialogueLine
    {
        [LabelText("行ID")]
        public string rowKey = "line_001";

        [LabelText("说话人")]
        public string speaker = "旁白";

        [LabelText("对白文本")]
        public string text = "这里是一句对白。";

        [LabelText("持续秒数")]
        public float duration = 2.5f;

        [LabelText("品质")]
        public ExampleSoTableQuality quality = ExampleSoTableQuality.Normal;

        [LabelText("嵌套配置")]
        public ExampleSoTableNestedConfig nestedConfig = new ExampleSoTableNestedConfig();
    }

    [ESCreatePath("数据信息", "SO表格示例对白Info")]
    public sealed class ExampleSoTableDialogueInfo : SoDataInfo
    {
        [LabelText("章节名")]
        public string chapterName = "第一章";

        [LabelText("场景名")]
        public string sceneName = "入口";

        [LabelText("启用")]
        public bool enabledInGame = true;

        [LabelText("对白行列表")]
        public List<ExampleSoTableDialogueLine> lines = new List<ExampleSoTableDialogueLine>
        {
            new ExampleSoTableDialogueLine
            {
                rowKey = "line_001",
                speaker = "队长",
                text = "准备进入第一关。",
                duration = 2.0f
            },
            new ExampleSoTableDialogueLine
            {
                rowKey = "line_002",
                speaker = "系统",
                text = "目标已更新。",
                duration = 1.5f,
                quality = ExampleSoTableQuality.Rare
            }
        };
    }
}
