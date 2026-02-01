using UnityEngine;
using UnityEngine.Playables;

namespace ES
{
    /// <summary>
    /// StateParameter使用示例
    /// 展示如何使用枚举+字符串混合参数系统
    /// </summary>
    public class StateParameter_UsageExample : MonoBehaviour
    {
        // ==================== 示例1: 使用枚举参数（推荐，高性能） ====================
        
        public void Example1_UsingEnumParameter()
        {
            var calculator = new BlendTree1DCalculator
            {
                // 直接使用枚举 - 高性能，类型安全，支持重构
                parameterName = StateDefaultFloatParameter.Speed,
                samples = new[]
                {
                    new BlendTree1DCalculator.ClipSampleForBlend1D
                    {
                        clip = Resources.Load<AnimationClip>("Idle"),
                        threshold = 0f
                    },
                    new BlendTree1DCalculator.ClipSampleForBlend1D
                    {
                        clip = Resources.Load<AnimationClip>("Walk"),
                        threshold = 2f
                    },
                    new BlendTree1DCalculator.ClipSampleForBlend1D
                    {
                        clip = Resources.Load<AnimationClip>("Run"),
                        threshold = 5f
                    }
                }
            };
            
            // 在Context中设置参数
            var context = new StateContext();
            context.SetFloat(StateDefaultFloatParameter.Speed, 3.5f);
            
            // Calculator内部会自动通过枚举获取参数
            var runtime = calculator.CreateRuntimeData();
            var graph = PlayableGraph.Create("Example1");
            Playable output = default;
            calculator.InitializeRuntime(runtime, graph, ref output);
            calculator.UpdateWeights(runtime, context, Time.deltaTime);
            
            Debug.Log("✓ 示例1: 枚举参数 - 高性能，类型安全");
        }
        
        // ==================== 示例2: 使用字符串参数（动态场景） ====================
        
        public void Example2_UsingStringParameter()
        {
            var calculator = new BlendTree1DCalculator
            {
                // 使用字符串 - 适用于动态生成的参数名
                parameterName = "DynamicParameter_" + Random.Range(0, 100),
                samples = new[]
                {
                    new BlendTree1DCalculator.ClipSampleForBlend1D
                    {
                        clip = Resources.Load<AnimationClip>("Idle"),
                        threshold = 0f
                    },
                    new BlendTree1DCalculator.ClipSampleForBlend1D
                    {
                        clip = Resources.Load<AnimationClip>("Run"),
                        threshold = 5f
                    }
                }
            };
            
            var context = new StateContext();
            context.SetFloat("DynamicParameter_42", 2.5f);
            
            Debug.Log("✓ 示例2: 字符串参数 - 灵活，适用于动态场景");
        }
        
        // ==================== 示例3: 2D混合树使用枚举参数 ====================
        
        public void Example3_2DBlendWithEnum()
        {
            var calculator = new BlendTree2DFreeformDirectionalCalculator
            {
                // 2D参数使用字符串
                parameterX = "DirectionX",
                parameterY = "DirectionY",
                samples = new[]
                {
                    new BlendTree2DCalculator.ClipSample2D
                    {
                        clip = Resources.Load<AnimationClip>("Walk_Forward"),
                        position = new Vector2(0, 1)
                    },
                    new BlendTree2DCalculator.ClipSample2D
                    {
                        clip = Resources.Load<AnimationClip>("Walk_Back"),
                        position = new Vector2(0, -1)
                    },
                    new BlendTree2DCalculator.ClipSample2D
                    {
                        clip = Resources.Load<AnimationClip>("Walk_Left"),
                        position = new Vector2(-1, 0)
                    },
                    new BlendTree2DCalculator.ClipSample2D
                    {
                        clip = Resources.Load<AnimationClip>("Walk_Right"),
                        position = new Vector2(1, 0)
                    }
                }
            };
            
            var context = new StateContext();
            context.SetFloat("DirectionX", 0.5f);
            context.SetFloat("DirectionY", 0.8f);
            
            Debug.Log("✓ 示例3: 2D混合树 - 枚举参数支持");
        }
        
        // ==================== 示例4: 混合使用（枚举+字符串） ====================
        
        public void Example4_MixedParameters()
        {
            // 第一个Mixer使用枚举
            var upperBodyCalculator = new DirectBlendCalculator
            {
                clips = new[]
                {
                    new DirectBlendCalculator.DirectClip
                    {
                        clip = Resources.Load<AnimationClip>("Attack"),
                        weightParameter = "AttackWeight", // 字符串参数
                        defaultWeight = 0f
                    },
                    new DirectBlendCalculator.DirectClip
                    {
                        clip = Resources.Load<AnimationClip>("Block"),
                        weightParameter = "BlockWeight", // 字符串参数
                        defaultWeight = 0f
                    }
                }
            };
            
            // 第二个Mixer使用自定义字符串
            var lowerBodyCalculator = new BlendTree1DCalculator
            {
                parameterName = "CustomMoveSpeed", // 自定义参数名
                samples = new[]
                {
                    new BlendTree1DCalculator.ClipSampleForBlend1D
                    {
                        clip = Resources.Load<AnimationClip>("Idle"),
                        threshold = 0f
                    },
                    new BlendTree1DCalculator.ClipSampleForBlend1D
                    {
                        clip = Resources.Load<AnimationClip>("Run"),
                        threshold = 5f
                    }
                }
            };
            
            var context = new StateContext();
            context.SetFloat("AttackWeight", 0.7f);
            context.SetFloat("BlockWeight", 0.3f);
            context.SetFloat("CustomMoveSpeed", 2.5f);
            
            Debug.Log("✓ 示例4: 混合参数 - 枚举与字符串共存");
        }
        
        // ==================== 示例5: StateParameter的优势对比 ====================
        
        public void Example5_PerformanceComparison()
        {
            var context = new StateContext();
            
            // === 方式1: 旧方式 - 字符串硬编码 ===
            // 缺点：拼写错误、不支持重构、无智能提示
            context.SetFloat("Speed", 3.5f);
            float speed1 = context.GetFloat("Speed", 0f); // 可能拼错
            
            // === 方式2: 新方式 - 枚举参数 ===
            // 优点：类型安全、支持重构、智能提示、性能优化
            context.SetFloat(StateDefaultFloatParameter.Speed, 3.5f);
            float speed2 = context.GetFloat(StateDefaultFloatParameter.Speed, 0f);
            
            // === 方式3: StateParameter - 自动切换 ===
            StateParameter param1 = StateDefaultFloatParameter.Speed;      // 使用枚举
            StateParameter param2 = "CustomParameter";            // 使用字符串
            
            // 两者都能正常工作
            float value1 = context.GetFloat(param1.GetStringKey, 0f);
            float value2 = context.GetFloat(param2.GetStringKey, 0f);
            
            Debug.Log($"✓ 示例5: 性能对比");
            Debug.Log($"  - 枚举参数: {param1.GetStringKey} (类型安全)");
            Debug.Log($"  - 字符串参数: {param2.GetStringKey} (灵活性)");
        }
        
        // ==================== 示例6: 扩展StateDefaultParameter枚举 ====================
        
        public void Example6_ExtendingEnum()
        {
            // 如果需要添加新参数，只需在StateDefaultParameter枚举中添加即可
            // 例如：
            // public enum StateDefaultParameter
            // {
            //     ...
            //     JumpHeight,        // 新增跳跃高度
            //     ClimbSpeed,        // 新增攀爬速度
            //     SwimDepth,         // 新增游泳深度
            // }
            
            // 然后就可以直接使用
            var context = new StateContext();
            
            // 假设已添加
            // context.SetFloat(StateDefaultParameter.JumpHeight, 2.5f);
            // context.SetFloat(StateDefaultParameter.ClimbSpeed, 1.2f);
            
            Debug.Log("✓ 示例6: 扩展枚举 - 在StateDefaultParameter中添加新值");
        }
        
        // ==================== 示例7: 实际应用 - 角色动画系统 ====================
        
        public class CharacterAnimationSystem : MonoBehaviour
        {
            private PlayableGraph _graph;
            private StateContext _context;
            private BlendTree1DCalculator _moveCalculator;
            private AnimationCalculatorRuntime _moveRuntime;
            
            public void Initialize()
            {
                _graph = PlayableGraph.Create("Character");
                _context = new StateContext();
                
                // 使用枚举配置移动动画
                _moveCalculator = new BlendTree1DCalculator
                {
                    parameterName = StateDefaultFloatParameter.Speed, // 使用枚举
                    smoothTime = 0.15f,
                    samples = new[]
                    {
                        new BlendTree1DCalculator.ClipSampleForBlend1D
                        {
                            clip = Resources.Load<AnimationClip>("Idle"),
                            threshold = 0f
                        },
                        new BlendTree1DCalculator.ClipSampleForBlend1D
                        {
                            clip = Resources.Load<AnimationClip>("Walk"),
                            threshold = 2f
                        },
                        new BlendTree1DCalculator.ClipSampleForBlend1D
                        {
                            clip = Resources.Load<AnimationClip>("Run"),
                            threshold = 5f
                        },
                        new BlendTree1DCalculator.ClipSampleForBlend1D
                        {
                            clip = Resources.Load<AnimationClip>("Sprint"),
                            threshold = 8f
                        }
                    }
                };
                
                _moveRuntime = _moveCalculator.CreateRuntimeData();
                Playable output = default;
                _moveCalculator.InitializeRuntime(_moveRuntime, _graph, ref output);
                
                var playableOutput = UnityEngine.Animations.AnimationPlayableOutput.Create(_graph, "Output", this.GetComponent<Animator>());
                playableOutput.SetSourcePlayable(output);
                _graph.Play();
            }
            
            public void UpdateMovement(float moveSpeed)
            {
                // 使用枚举设置参数 - 类型安全
                _context.SetFloat(StateDefaultFloatParameter.Speed, moveSpeed);
                
                // 更新动画权重
                _moveCalculator.UpdateWeights(_moveRuntime, _context, Time.deltaTime);
            }
            
            public void Cleanup()
            {
                _moveRuntime.Cleanup();
                _graph.Destroy();
            }
        }
        
        public void Example7_RealWorldUsage()
        {
            var characterSystem = gameObject.AddComponent<CharacterAnimationSystem>();
            characterSystem.Initialize();
            
            // 模拟移动速度变化
            characterSystem.UpdateMovement(3.5f); // Walk
            
            Debug.Log("✓ 示例7: 实际应用 - 角色动画系统集成");
        }
        
        // ==================== 性能总结 ====================
        
        [ContextMenu("性能对比测试")]
        public void PerformanceTest()
        {
            var context = new StateContext();
            int iterations = 100000;
            
            // 测试1: 枚举参数
            var sw1 = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                context.SetFloat(StateDefaultFloatParameter.Speed, i % 10);
                float value = context.GetFloat(StateDefaultFloatParameter.Speed, 0f);
            }
            sw1.Stop();
            
            // 测试2: 字符串参数
            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                context.SetFloat("Speed", i % 10);
                float value = context.GetFloat("Speed", 0f);
            }
            sw2.Stop();
            
            Debug.Log("=== StateParameter性能对比 ===");
            Debug.Log($"枚举参数: {sw1.ElapsedMilliseconds}ms ({iterations}次)");
            Debug.Log($"字符串参数: {sw2.ElapsedMilliseconds}ms ({iterations}次)");
            Debug.Log($"性能差异: {((float)sw2.ElapsedMilliseconds / sw1.ElapsedMilliseconds - 1) * 100:F1}%");
            Debug.Log("\n推荐使用：");
            Debug.Log("- 常用参数 → StateDefaultParameter枚举（高性能）");
            Debug.Log("- 动态参数 → string字符串（灵活性）");
        }
    }
}
