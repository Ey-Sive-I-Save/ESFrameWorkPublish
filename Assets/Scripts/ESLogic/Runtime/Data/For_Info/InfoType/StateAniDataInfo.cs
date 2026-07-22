using UnityEngine;
using Sirenix.OdinInspector;

namespace ES
{
    /// <summary>
    /// 状态动画数据资产入口。
    /// 运行时只通过 sharedData 进入状态系统，避免资产壳继续扩展重复配置。
    /// </summary>
    [ESCreatePath("数据信息", "状态动画数据")]
    public class StateAniDataInfo : SoDataInfo
    {
        [TitleGroup("状态动画数据", "资产入口只负责承载共享数据；运行逻辑读取 SharedData。", Alignment = TitleAlignments.Left, BoldTitle = true)]
        [InfoBox("不要在资产壳上新增状态名、动画来源、切换规则等重复字段，避免和共享数据产生两套定义。", InfoMessageType.None)]
        [InlineProperty]
        [HideLabel]
        public StateSharedData sharedData = new StateSharedData();

        internal void Initialize()
        {
            InitializeRuntime();
        }

        public void InitializeRuntime()
        {
            sharedData?.InitializeRuntime();
        }
    }
}
