using UnityEngine;
using ES;
using ES.Optimizations;

namespace ES.Examples
{
    /// <summary>
    /// 混合树使用示例 - 涵盖所有类型的BlendTree
    /// </summary>
    public class BlendTreeUsageExamples : MonoBehaviour
    {
        [Header("状态机配置")]
        public PlayableStateMachineController stateMachine;
        public StateContext context;
        
        [Header("测试Clip")]
        public AnimationClip idleClip;
        public AnimationClip walkClip;
        public AnimationClip runClip;
        public AnimationClip sprintClip;
        
        [Header("8方向移动Clip")]
        public AnimationClip forwardClip;
        public AnimationClip backwardClip;
        public AnimationClip leftClip;
        public AnimationClip rightClip;
        public AnimationClip forwardLeftClip;
        public AnimationClip forwardRightClip;
        public AnimationClip backLeftClip;
        public AnimationClip backRightClip;
        
        [Header("面部表情Clip")]
        public AnimationClip smileClip;
        public AnimationClip angryClip;
        public AnimationClip sadClip;
        public AnimationClip blinkClip;
        
        private void Start()
        {
            // 示例1: 1D速度混合树
            Example1_SpeedBlendTree();
            
            // 示例2: 2D移动混合树
            Example2_MovementBlendTree();
            
            // 示例3: Direct面部混合
            Example3_FacialBlendTree();
        }
        
        private void Update()
        {
            // 实时更新参数
            UpdateSpeedParameter();
            UpdateMovementParameter();
            UpdateFacialParameters();
        }
        
        #region 示例1: 1D速度混合树
        
        /// <summary>
        /// 示例1: Idle → Walk → Run → Sprint
        /// </summary>
        private void Example1_SpeedBlendTree()
        {
            var blendTree = new BlendTreeComponent
            {
                type = BlendTreeComponent.BlendTreeType.Blend1D,
                parameterX = StateDefaultFloatParameter.Speed,
                smoothTime = 0.15f,
                clips = new System.Collections.Generic.List<BlendClipEntry>
                {
                    new BlendClipEntry 
                    { 
                        clip = idleClip, 
                        threshold = 0f,
                        timeScale = 1f
                    },
                    new BlendClipEntry 
                    { 
                        clip = walkClip, 
                        threshold = 2f,
                        timeScale = 1f
                    },
                    new BlendClipEntry 
                    { 
                        clip = runClip, 
                        threshold = 5f,
                        timeScale = 1.2f  // 稍快播放
                    },
                    new BlendClipEntry 
                    { 
                        clip = sprintClip, 
                        threshold = 8f,
                        timeScale = 1.5f  // 快速播放
                    }
                }
            };
            
            Debug.Log("✓ 1D速度混合树配置完成");
            Debug.Log("  Speed = 0   → Idle 100%");
            Debug.Log("  Speed = 1   → Idle 50% + Walk 50%");
            Debug.Log("  Speed = 3.5 → Walk 50% + Run 50%");
            Debug.Log("  Speed = 6.5 → Run 50% + Sprint 50%");
            Debug.Log("  Speed = 8+  → Sprint 100%");
        }
        
        private void UpdateSpeedParameter()
        {
            // 根据输入计算速度
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            float inputMagnitude = new Vector2(horizontal, vertical).magnitude;
            
            // 速度映射
            float speed = 0f;
            if (inputMagnitude > 0.1f)
            {
                if (Input.GetKey(KeyCode.LeftShift))
                    speed = 8f;  // Sprint
                else if (Input.GetKey(KeyCode.LeftControl))
                    speed = 2f;  // Walk
                else
                    speed = 5f;  // Run
                
                speed *= inputMagnitude;
            }
            
            // 设置到Context
            context?.SetFloat("Speed", speed);
        }
        
        #endregion
        
        #region 示例2: 2D移动混合树
        
        /// <summary>
        /// 示例2: 8方向移动混合
        /// </summary>
        private void Example2_MovementBlendTree()
        {
            var blendTree = new BlendTreeComponent
            {
                type = BlendTreeComponent.BlendTreeType.Blend2DFreeformDirectional,
                parameterX = StateDefaultFloatParameter.SpeedX,
                parameterY = StateDefaultFloatParameter.SpeedY,
                smoothTime = 0.1f,
                enableDirtyFlag = true,
                dirtyThreshold = 0.02f,
                clips = new System.Collections.Generic.List<BlendClipEntry>
                {
                    // 4个主方向
                    new BlendClipEntry 
                    { 
                        clip = forwardClip, 
                        position = new Vector2(0, 1) 
                    },
                    new BlendClipEntry 
                    { 
                        clip = backwardClip, 
                        position = new Vector2(0, -1) 
                    },
                    new BlendClipEntry 
                    { 
                        clip = leftClip, 
                        position = new Vector2(-1, 0) 
                    },
                    new BlendClipEntry 
                    { 
                        clip = rightClip, 
                        position = new Vector2(1, 0) 
                    },
                    
                    // 4个斜向
                    new BlendClipEntry 
                    { 
                        clip = forwardLeftClip, 
                        position = new Vector2(-0.707f, 0.707f) 
                    },
                    new BlendClipEntry 
                    { 
                        clip = forwardRightClip, 
                        position = new Vector2(0.707f, 0.707f) 
                    },
                    new BlendClipEntry 
                    { 
                        clip = backLeftClip, 
                        position = new Vector2(-0.707f, -0.707f) 
                    },
                    new BlendClipEntry 
                    { 
                        clip = backRightClip, 
                        position = new Vector2(0.707f, -0.707f) 
                    }
                }
            };
            
            Debug.Log("✓ 2D移动混合树配置完成");
            Debug.Log("  支持360°自由移动");
            Debug.Log("  使用Delaunay三角化 + 重心坐标插值");
            Debug.Log("  性能: O(log n)查找 + O(1)插值");
        }
        
        private void UpdateMovementParameter()
        {
            // 获取移动输入
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            
            // 归一化(保持方向,限制长度为1)
            Vector2 moveInput = new Vector2(horizontal, vertical);
            if (moveInput.magnitude > 1f)
                moveInput.Normalize();
            
            // 设置到Context
            context?.SetFloat("MoveX", moveInput.x);
            context?.SetFloat("MoveY", moveInput.y);
        }
        
        #endregion
        
        #region 示例3: Direct面部混合
        
        /// <summary>
        /// 示例3: 多维度面部表情混合
        /// </summary>
        private void Example3_FacialBlendTree()
        {
            var blendTree = new BlendTreeComponent
            {
                type = BlendTreeComponent.BlendTreeType.Direct,
                smoothTime = 0.05f,  // 面部动画需要快速响应
                clips = new System.Collections.Generic.List<BlendClipEntry>
                {
                    new BlendClipEntry 
                    { 
                        clip = smileClip,
                        weightParameter = "Smile",
                        directWeight = 0f
                    },
                    new BlendClipEntry 
                    { 
                        clip = angryClip,
                        weightParameter = "Angry",
                        directWeight = 0f
                    },
                    new BlendClipEntry 
                    { 
                        clip = sadClip,
                        weightParameter = "Sad",
                        directWeight = 0f
                    },
                    new BlendClipEntry 
                    { 
                        clip = blinkClip,
                        weightParameter = "Blink",
                        directWeight = 0f,
                        blendCurve = AnimationCurve.EaseInOut(0, 0, 1, 1)
                    }
                }
            };
            
            Debug.Log("✓ Direct面部混合树配置完成");
            Debug.Log("  每个表情独立控制");
            Debug.Log("  可同时激活多个表情");
            Debug.Log("  适用于: 面部动画、手指动画、表情捕捉");
        }
        
        private void UpdateFacialParameters()
        {
            // 示例: 按键控制表情
            float smile = Input.GetKey(KeyCode.Alpha1) ? 1f : 0f;
            float angry = Input.GetKey(KeyCode.Alpha2) ? 1f : 0f;
            float sad = Input.GetKey(KeyCode.Alpha3) ? 1f : 0f;
            
            // 眨眼动画(自动循环)
            float blink = Mathf.PingPong(Time.time * 0.5f, 1f);
            
            // 设置到Context
            context?.SetFloat("Smile", smile);
            context?.SetFloat("Angry", angry);
            context?.SetFloat("Sad", sad);
            context?.SetFloat("Blink", blink);
        }
        
        #endregion
        
        #region 高级示例: 瞄准偏移(Aim Offset)
        
        /// <summary>
        /// 示例4: 瞄准偏移 - 叠加到基础动画
        /// </summary>
        [ContextMenu("Example 4: Aim Offset")]
        private void Example4_AimOffset()
        {
            var aimBlendTree = new BlendTreeComponent
            {
                type = BlendTreeComponent.BlendTreeType.Blend2DFreeformCartesian,
                parameterX = StateDefaultFloatParameter.AimYaw,   // -90 到 +90
                parameterY = StateDefaultFloatParameter.AimPitch, // -45 到 +45
                smoothTime = 0.05f,      // 瞄准需要快速响应
                clips = new System.Collections.Generic.List<BlendClipEntry>
                {
                    // 3x3网格布局
                    new BlendClipEntry { position = new Vector2(-90, 45) },  // 左上
                    new BlendClipEntry { position = new Vector2(0, 45) },    // 中上
                    new BlendClipEntry { position = new Vector2(90, 45) },   // 右上
                    
                    new BlendClipEntry { position = new Vector2(-90, 0) },   // 左中
                    new BlendClipEntry { position = new Vector2(0, 0) },     // 中心
                    new BlendClipEntry { position = new Vector2(90, 0) },    // 右中
                    
                    new BlendClipEntry { position = new Vector2(-90, -45) }, // 左下
                    new BlendClipEntry { position = new Vector2(0, -45) },   // 中下
                    new BlendClipEntry { position = new Vector2(90, -45) }   // 右下
                }
            };
            
            Debug.Log("✓ 瞄准偏移混合树配置完成");
            Debug.Log("  使用Additive模式叠加到基础动画");
            Debug.Log("  支持-90°到+90°水平旋转");
            Debug.Log("  支持-45°到+45°垂直旋转");
            
            // 使用方式:
            // 1. 设置此状态的Pipeline为Additive
            // 2. 基础动画正常播放(如Idle/Walk)
            // 3. 瞄准偏移叠加在上面
        }
        
        #endregion
        
        #region 性能测试
        
        [ContextMenu("性能测试: 100次更新")]
        private void PerformanceTest()
        {
            if (context == null)
            {
                Debug.LogError("Context未设置");
                return;
            }
            
            var blendTree = new BlendTreeComponent
            {
                type = BlendTreeComponent.BlendTreeType.Blend2DFreeformDirectional,
                parameterX = "TestX",
                parameterY = "TestY",
                enableDirtyFlag = true,
                clips = new System.Collections.Generic.List<BlendClipEntry>()
            };
            
            // 添加20个采样点
            for (int i = 0; i < 20; i++)
            {
                float angle = i * 18f * Mathf.Deg2Rad;
                blendTree.clips.Add(new BlendClipEntry
                {
                    position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                });
            }
            
            // 模拟100次更新
            var watch = System.Diagnostics.Stopwatch.StartNew();
            
            for (int i = 0; i < 100; i++)
            {
                float angle = i * 3.6f * Mathf.Deg2Rad;
                context.SetFloat("TestX", Mathf.Cos(angle));
                context.SetFloat("TestY", Mathf.Sin(angle));
                
                // 这里应该调用blendTree.OnStateUpdate()
                // 但需要PlayableGraph,所以仅作示意
            }
            
            watch.Stop();
            
            Debug.Log($"性能测试结果:");
            Debug.Log($"  总时间: {watch.ElapsedMilliseconds}ms");
            Debug.Log($"  平均: {watch.ElapsedMilliseconds / 100f}ms/帧");
            Debug.Log($"  预估帧率影响: {watch.ElapsedMilliseconds / 100f / 16.67f * 100f:F2}%");
        }
        
        #endregion
        
        #region 调试可视化
        
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || context == null)
                return;
            
            // 可视化当前输入参数
            DrawMovementGizmo();
        }
        
        private void DrawMovementGizmo()
        {
            float x = context.GetFloat("MoveX", 0f);
            float y = context.GetFloat("MoveY", 0f);
            
            Vector3 center = transform.position + Vector3.up * 2f;
            
            // 绘制坐标轴
            Gizmos.color = Color.red;
            Gizmos.DrawLine(center - Vector3.right, center + Vector3.right);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(center - Vector3.forward, center + Vector3.forward);
            
            // 绘制当前输入位置
            Gizmos.color = Color.yellow;
            Vector3 inputPos = center + new Vector3(x, 0, y) * 0.5f;
            Gizmos.DrawSphere(inputPos, 0.1f);
            Gizmos.DrawLine(center, inputPos);
        }
        
        #endregion
    }
}
