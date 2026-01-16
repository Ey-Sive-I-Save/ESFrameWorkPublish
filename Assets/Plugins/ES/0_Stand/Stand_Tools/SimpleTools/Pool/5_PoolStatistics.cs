using System;
using UnityEngine;

namespace ES
{

#if UNITY_EDITOR

#endif

    /// <summary>
    /// 对象池统计信息
    /// </summary>
    // 统计信息仅在编辑器下启用，发布时无损耗

    public class PoolStatistics : IJumpableElement<string>
    {
        private static JumpSafeKeyGroup<string, PoolStatistics> _globalStatisticsGroup;
        private static bool _isDirty = true;

        /// <summary>
        /// 全局统计信息组（懒加载，支持 Dirty 重置）
        /// </summary>
        public static JumpSafeKeyGroup<string, PoolStatistics> GlobalStatisticsGroup
        {
            get
            {
                if (_isDirty || _globalStatisticsGroup == null)
                {
                    _globalStatisticsGroup = new JumpSafeKeyGroup<string, PoolStatistics>();
                    _isDirty = false;
                }
                return _globalStatisticsGroup;
            }
        }

        /// <summary>
        /// 标记全局统计信息组为脏，下次访问时重新初始化
        /// </summary>
        public static void MarkGlobalStatisticsGroupDirty()
        {
            _isDirty = true;
        }

        public bool IsValid = true;
        public string PoolDisplayName;
        public string GroupName;
        public int TotalCreated;      // 总创建数
        public int TotalGets;         // 总获取次数
        public int TotalReturns;      // 总回收次数
        public int CurrentPooled;     // 当前池中数量
        public int CurrentActive;     // 当前活跃数量
        public int PeakActive;        // 峰值活跃数量
        public int DiscardedCount;    // 被丢弃的数量（超出容量）

        public string CurrentGroup { get; set; }

        public void ResetExceptCreated()
        {
            TotalGets = 0;
            TotalReturns = 0;
            CurrentPooled = 0;
            CurrentActive = 0;
            PeakActive = 0;
            DiscardedCount = 0;
        }



        public override string ToString()
        {
            return $"Created:{TotalCreated} Gets:{TotalGets} Returns:{TotalReturns} Pooled:{CurrentPooled} Active:{CurrentActive} Peak:{PeakActive} Discarded:{DiscardedCount}";
        }
    }
}