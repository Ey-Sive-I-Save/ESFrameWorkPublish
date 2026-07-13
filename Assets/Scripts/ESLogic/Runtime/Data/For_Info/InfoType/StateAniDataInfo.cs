using UnityEngine;
using Sirenix.OdinInspector;

namespace ES
{
    /// <summary>
    /// 动画状态数据信息 - State 的享元数据配置。
    /// 运行时只通过 sharedData 进入状态系统，避免历史遗留的重复配置壳继续造成歧义。
    /// </summary>
    [ESCreatePath("数据信息", "动画状态数据信息")]
    public class StateAniDataInfo : SoDataInfo
    {
        [HideLabel]
        public StateSharedData sharedData = new StateSharedData();
 
        internal void Initialize()
        {
            InitializeRuntime();
        }

        /// <summary>
        /// 运行时初始化 - 递归初始化所有子成员（统一入口）。
        /// </summary>
        public void InitializeRuntime()
        {
            sharedData?.InitializeRuntime();
        }
    }
}
