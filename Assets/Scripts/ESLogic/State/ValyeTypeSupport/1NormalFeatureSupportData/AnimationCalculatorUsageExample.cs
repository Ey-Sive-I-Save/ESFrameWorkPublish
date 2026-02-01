using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using ES;
using System.Collections.Generic;
using System.Collections;

namespace ES.Examples
{
    /// <summary>
    /// AnimationClipPlayableCalculator使用示例 - 享元模式
    /// 配置数据共享,运行时数据独立
    /// </summary>
    public class AnimationCalculatorUsageExample : MonoBehaviour
    {
        [Header("示例Clip")]
        public AnimationClip idleClip;
        public AnimationClip walkClip;
        public AnimationClip runClip;
        public AnimationClip sprintClip;
        
        [Header("8方向Clip")]
        public AnimationClip[] movementClips = new AnimationClip[8];
        
        [Header("表情Clip")]
        public AnimationClip smileClip;
        public AnimationClip angryClip;
        public AnimationClip sadClip;
        
        [Header("共享配置(享元)")]
        public BlendTree1DCalculator sharedSpeedBlend;
        
        private PlayableGraph _graph;
        private AnimationPlayableOutput _output;
        private StateContext _context;
        
        // 运行时数据 - 每个实例独立
        private AnimationCalculatorRuntime _runtime;
        
        // 演示多个角色共享同一个配置
        private Dictionary<int, AnimationCalculatorRuntime> _multipleCharacters 
            = new Dictionary<int, AnimationCalculatorRuntime>();
        
        private void Start()
        {
            _context = new StateContext();
            CreateGraph();
            
            // 示例1: 单个角色使用
            Example1_SingleCharacter();
            
            // 示例2: 多个角色共享配置(享元模式)
            Example2_FlyweightPattern();
        }
        
        private void CreateGraph()
        {
            _graph = PlayableGraph.Create("AnimationCalculatorExample");
            _output = AnimationPlayableOutput.Create(_graph, "Animation", GetComponent<Animator>());
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        }
        
        #region 示例1: 单个角色使用
        
        private void Example1_SingleCharacter()
        {
            // 创建配置(可序列化,可共享)
            var calculator = new BlendTree1DCalculator
            {
                parameterName = "Speed",
                smoothTime = 0.15f,
                samples = new BlendTree1DCalculator.ClipSampleForBlend1D[]
                {
                    new() { clip = idleClip, threshold = 0f },
                    new() { clip = walkClip, threshold = 2f },
                    new() { clip = runClip, threshold = 5f },
                    new() { clip = sprintClip, threshold = 8f }
                }
            };
            
            // 创建运行时数据(独立实例)
            _runtime = calculator.CreateRuntimeData();
            
            // 初始化
            Playable output = Playable.Null;
            if (calculator.InitializeRuntime(_runtime, _graph, ref output))
            {
                _output.SetSourcePlayable(output);
                _graph.Play();
                
                Debug.Log("✓ 单角色示例启动");
                Debug.Log("  配置对象: " + calculator.GetHashCode());
                Debug.Log("  运行时对象: " + _runtime.GetHashCode());
            }
        }
        
        #endregion
        
        #region 示例2: 享元模式
        
        private void Example2_FlyweightPattern()
        {
            // 1个配置对象(享元)
            var sharedCalculator = new BlendTree1DCalculator
            {
                parameterName = "Speed",
                smoothTime = 0.15f,
                samples = new BlendTree1DCalculator.ClipSampleForBlend1D[]
                {
                    new() { clip = idleClip, threshold = 0f },
                    new() { clip = walkClip, threshold = 2f },
                    new() { clip = runClip, threshold = 5f },
                    new() { clip = sprintClip, threshold = 8f }
                }
            };
            
            Debug.Log("========== 享元模式演示 ==========");
            Debug.Log($"共享配置对象ID: {sharedCalculator.GetHashCode()}");
            
            // 创建10个角色,每个角色有独立的运行时数据
            for (int i = 0; i < 10; i++)
            {
                // 每个角色创建自己的运行时数据
                var runtime = sharedCalculator.CreateRuntimeData();
                _multipleCharacters[i] = runtime;
                
                // 所有角色使用同一个配置对象
                Playable output = Playable.Null;
                sharedCalculator.InitializeRuntime(runtime, _graph, ref output);
                
                Debug.Log($"  角色{i}: 运行时对象ID={runtime.GetHashCode()}");
            }
            
            Debug.Log("✓ 享元模式演示完成");
            Debug.Log($"  配置对象: 1个 (内存节省: {9 * System.Runtime.InteropServices.Marshal.SizeOf(sharedCalculator)} bytes)");
            Debug.Log($"  运行时对象: 10个");
            Debug.Log($"  优势: 配置数据共享,运行时数据独立,无状态污染");
        }
        
        #endregion
        
        #region 更新逻辑
        
        private void Update()
        {
            if (_runtime == null || !_runtime.IsInitialized)
                return;
            
            // 更新输入参数
            UpdateInputParameters();
            
            // 使用配置对象更新运行时数据
            var calculator = new BlendTree1DCalculator
            {
                parameterName = "Speed",
                smoothTime = 0.15f
            };
            
            // 零GC更新权重
            calculator.UpdateWeights(_runtime, _context, Time.deltaTime);
        }
        
        private void UpdateInputParameters()
        {
            // 示例: 速度参数
            float speed = Input.GetKey(KeyCode.LeftShift) ? 8f : 
                         Input.GetKey(KeyCode.LeftControl) ? 2f : 5f;
            Vector2 input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
            speed *= input.magnitude;
            _context.SetFloat("Speed", speed);
        }
        
        #endregion
        
        private void OnDestroy()
        {
            // 清理所有运行时数据
            _runtime?.Cleanup();
            
            foreach (var runtime in _multipleCharacters.Values)
            {
                runtime?.Cleanup();
            }
            
            if (_graph.IsValid())
                _graph.Destroy();
        }
        
        #region 性能对比
        
        [ContextMenu("性能对比: 享元 vs 非享元")]
        private void PerformanceComparison()
        {
            Debug.Log("========== 内存占用对比 ==========");
            
            // 配置数据大小估算
            var sampleConfig = new BlendTree1DCalculator
            {
                samples = new BlendTree1DCalculator.ClipSampleForBlend1D[4]
            };
            
            int configSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(BlendTree1DCalculator));
            Debug.Log($"单个配置对象: ~{configSize} bytes");
            
            Debug.Log("");
            Debug.Log("100个角色场景:");
            Debug.Log($"  非享元模式: {configSize * 100 / 1024f:F2} KB (每个角色1个配置)");
            Debug.Log($"  享元模式: {configSize / 1024f:F2} KB (所有角色共享1个配置)");
            Debug.Log($"  节省内存: {configSize * 99 / 1024f:F2} KB (99%)");
            
            Debug.Log("");
            Debug.Log("运行时数据:");
            Debug.Log("  每个角色: 独立的Playable + 权重数组");
            Debug.Log("  无法共享: 必须保持独立状态");
            Debug.Log("  享元优势: 仅配置共享,运行时隔离");
        }
        
        #endregion
    }
}
