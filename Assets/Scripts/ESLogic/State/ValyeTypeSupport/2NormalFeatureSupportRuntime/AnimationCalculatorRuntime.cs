using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ES
{
    /// <summary>
    /// 动画计算器统一运行时数据
    /// 设计原则：
    /// 1. 每个状态独占一个Runtime实例（不共享）
    /// 2. Runtime与Calculator绑定后不变
    /// 3. 支持运行时Clip覆盖，但索引保持固定
    /// 4. 零GC设计：所有数组预分配，避免动态分配
    /// 5. 所有Calculator类型共享此Runtime，简化类型转换
    /// 
    /// 字段使用规则：
    /// - SimpleClip: singlePlayable
    /// - BlendTree1D: mixer, playables[], lastInput, inputVelocity
    /// - BlendTree2D: mixer, playables[], lastInput2D, inputVelocity2D, triangles[]
    /// - DirectBlend: mixer, playables[], currentWeights[], targetWeights[], weightVelocities[]
    /// </summary>
    public class AnimationCalculatorRuntime
    {
        // ==================== 初始化标记 ====================
        /// <summary>
        /// 是否已初始化（绑定Calculator后为true）
        /// 初始化后Runtime与Calculator绑定不变
        /// </summary>
        public bool IsInitialized;
        
        // ==================== 通用Playable ====================
        /// <summary>
        /// Mixer - 用于BlendTree和Direct
        /// 可被父Mixer连接，支持多层级嵌套
        /// </summary>
        public AnimationMixerPlayable mixer;
        
        /// <summary>
        /// Clip数组 - 用于BlendTree和Direct
        /// 索引固定不变，支持运行时覆盖Clip内容
        /// </summary>
        public AnimationClipPlayable[] playables;
        
        /// <summary>
        /// 单个Clip - 用于SimpleClip
        /// 可运行时覆盖，保持索引0不变
        /// </summary>
        public AnimationClipPlayable singlePlayable;
        
        // ==================== 1D混合树数据 ====================
        public float lastInput;                               // 上一帧输入值
        public float inputVelocity;                           // 输入速度 (用于平滑)
        
        // ==================== 2D混合树数据 ====================
        public Vector2 lastInput2D;                           // 上一帧2D输入
        public Vector2 inputVelocity2D;                       // 2D输入速度 (用于平滑)
        public Triangle[] triangles;                          // Delaunay三角形缓存
        
        // ==================== Direct混合数据 ====================
        public float[] currentWeights;                        // 当前权重
        public float[] targetWeights;                         // 目标权重
    public float[] weightVelocities;                      // 权重变化速度（用于SmoothDamp）
    
    // ==================== 权重缓存（所有计算器通用） ====================
    public float[] weightCache;                           // 权重缓存（避免每帧查询Mixer）
    public float[] weightTargetCache;                     // 目标权重缓存
    public float[] weightVelocityCache;                   // 权重速度缓存（用于平滑）
    public bool useSmoothing = true;                      // 是否启用权重平滑
    
    // ==================== 嵌套支持 ====================
    /// <summary>
    /// 子Calculator的Runtime（用于MixerCalculator嵌套）
    /// 支持一层嵌套，推荐深度≤2层
    /// </summary>
    public AnimationCalculatorRuntime childRuntime;

    // ==================== 序列混合器数据 ====================
    /// <summary>
    /// 当前序列阶段：0=Entry, 1=Main, 2=Exit
    /// </summary>
    public int sequencePhase;

    /// <summary>
    /// 当前阶段的已运行时间
    /// </summary>
    public float phaseStartTime;

    /// <summary>
    /// 整个序列是否已完成
    /// </summary>
    public bool sequenceCompleted;
        
        // ==================== 三角形结构体 ====================
        public struct Triangle
        {
            public int i0, i1, i2;          // 顶点索引
            public Vector2 v0, v1, v2;      // 顶点坐标
        }
        
        /// <summary>
        /// 清理所有Playable资源
        /// </summary>
        public void Cleanup()
        {
            // 清理单个Playable
            if (singlePlayable.IsValid())
                singlePlayable.Destroy();
            
            // 清理Playable数组
            if (playables != null)
            {
                foreach (var p in playables)
                {
                    if (p.IsValid())
                        p.Destroy();
                }
                playables = null;
            }
            
            // 清理Mixer
            if (mixer.IsValid())
                mixer.Destroy();
            
            // 清理其他数据
            triangles = null;
            currentWeights = null;
            targetWeights = null;
            weightVelocities = null;
            weightCache = null;
            weightTargetCache = null;
            weightVelocityCache = null;
            
            IsInitialized = false;
        }
        
        /// <summary>
        /// 获取当前使用的内存大小 (字节)
        /// </summary>
        public int GetMemoryFootprint()
        {
            int size = 0;
            
            // Playable引用 (每个约16字节)
            size += 16; // singlePlayable
            size += 16; // mixer
            if (playables != null)
                size += playables.Length * 16;
            
            // float数组
            size += 4; // lastInput
            size += 4; // inputVelocity
            size += 8; // lastInput2D (Vector2)
            size += 8; // inputVelocity2D
            
            if (currentWeights != null)
                size += currentWeights.Length * 4;
            if (targetWeights != null)
                size += targetWeights.Length * 4;
            if (weightVelocities != null)
                size += weightVelocities.Length * 4;
            if (weightCache != null)
                size += weightCache.Length * 4;
            if (weightTargetCache != null)
                size += weightTargetCache.Length * 4;
            if (weightVelocityCache != null)
                size += weightVelocityCache.Length * 4;
            
            // Triangle数组
            if (triangles != null)
                size += triangles.Length * (12 + 24); // 3个int + 3个Vector2
            
            return size;
        }
    }
}
