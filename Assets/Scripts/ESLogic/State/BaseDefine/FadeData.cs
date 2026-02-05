namespace ES
{
    /// <summary>
    /// 淡入/淡出运行时数据。
    /// 用于在状态机淡入淡出过程中保存权重插值所需的最小状态。
    /// </summary>
    /// <remarks>
    /// 状态机使用流程（只读说明，不影响运行时）：
    /// 1) 进入状态时，StateMachine.ApplyFadeIn() 从对象池获取实例并写入初始参数；
    /// 2) 更新时，StateMachine.UpdatePipelineFades() 递增 elapsedTime 并计算权重；
    /// 3) 完成时，StateMachine 将其回收到对象池，避免GC；
    /// 4) 淡出时同理，startWeight 用于从当前权重向 0 过渡。
    /// </remarks>
    public class StateFadeData : IPoolableAuto
    {
        /// <summary>
        /// StateFadeData 对象池（不设上限）
        /// </summary>
        public static readonly ESSimplePool<StateFadeData> Pool = new ESSimplePool<StateFadeData>(
            factoryMethod: () => new StateFadeData(),
            resetMethod: (obj) => obj.OnResetAsPoolable(),
            initCount: 32,
            maxCount: -1,
            poolDisplayName: "StateFadeData Pool"
        );

        /// <summary>
        /// 对象回收标记（对象池内部使用）
        /// </summary>
        public bool IsRecycled { get; set; }

        /// <summary>
        /// 已经过的时间（秒）
        /// </summary>
        public float elapsedTime;

        /// <summary>
        /// 淡入/淡出持续时间（秒）
        /// </summary>
        public float duration;

        /// <summary>
        /// Playable 槽位索引（用于直接写权重）
        /// </summary>
        public int slotIndex;

        /// <summary>
        /// 起始权重（用于淡出时从此权重向 0 插值）
        /// </summary>
        public float startWeight = 1f;

        /// <summary>
        /// 重置对象状态（回收到对象池前调用）
        /// </summary>
        public void OnResetAsPoolable()
        {
            elapsedTime = 0f;
            duration = 0f;
            slotIndex = 0;
            startWeight = 1f;
        }

        /// <summary>
        /// 尝试回收到对象池（由状态机在淡入/淡出完成时触发）
        /// </summary>
        public void TryAutoPushedToPool()
        {
            if (!IsRecycled)
            {
                IsRecycled = true;
                Pool.PushToPool(this);
            }
        }
    }
}
