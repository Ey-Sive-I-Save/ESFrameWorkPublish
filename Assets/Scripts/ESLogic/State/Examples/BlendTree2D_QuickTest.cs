using ES;
using UnityEngine;

namespace ES.Examples
{
    /// <summary>
    /// 2D混合树快速测试 - 演示如何正确使用2D自由混合
    /// 挂载到有Animator组件的GameObject上即可运行
    /// </summary>
    public class BlendTree2D_QuickTest : MonoBehaviour
    {
        [Header("必需组件")]
        public Animator animator;

        [Header("动画Clips - 8方向移动")]
        public AnimationClip idleClip;
        public AnimationClip walkForwardClip;
        public AnimationClip walkBackwardClip;
        public AnimationClip walkLeftClip;
        public AnimationClip walkRightClip;
        public AnimationClip walkForwardLeftClip;
        public AnimationClip walkForwardRightClip;
        public AnimationClip walkBackwardLeftClip;
        public AnimationClip walkBackwardRightClip;

        [Header("运行时状态")]
        [SerializeField] private Vector2 currentInput;
        [SerializeField] private bool isRunning = false;

        private StateMachine stateMachine;
        private const string MOVE_STATE_KEY = "Move2D";

        void Start()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (animator == null)
            {
                Debug.LogError("[2DBlendTest] 未找到Animator组件！");
                return;
            }

            InitializeStateMachine();
        }

        void Update()
        {
            if (!isRunning || stateMachine == null)
                return;

            // 1. 获取输入（WASD或方向键）
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            currentInput = new Vector2(horizontal, vertical);

            // 2. ★ 关键步骤：通过Basic流水线的移动模块设置移动（自动更新SpeedX/SpeedZ）
            var entity = stateMachine.hostEntity;
            if (entity != null && entity.basicDomain != null)
            {
                // 遍历所有模块找到移动模块（Basic流水线标准方式）
                EntityBasicMoveRotateModule moveModule = null;
                foreach (var module in entity.basicDomain.MyModules.ValuesNow)
                {
                    if (module is EntityBasicMoveRotateModule m)
                    {
                        moveModule = m;
                        break;
                    }
                }
                
                if (moveModule != null)
                {
                    // 体感设置：0.5=走路, 1.0=奔跑（按住Shift）
                    float inputMagnitude = currentInput.magnitude;
                    float speedMultiplier = 0f;
                    
                    if (inputMagnitude > 0.01f)
                    {
                        bool isRunning = Input.GetKey(KeyCode.LeftShift);
                        speedMultiplier = isRunning ? 1.0f : 0.5f;
                    }
                    
                    // 设置移动向量到移动模块（移动模块会自动更新StateMachine的SpeedX和SpeedZ）
                    Vector3 moveWorld = new Vector3(currentInput.x, 0f, currentInput.y) * speedMultiplier;
                    moveModule.SetMoveWorld(moveWorld);
                }
            }

            // 3. ★ 关键步骤：更新状态机（内部会调用UpdateWeights）
            stateMachine.UpdateStateMachine();

            // 4. 动画已自动通过PlayableGraph输出到Animator
        }

        void OnDestroy()
        {
            stateMachine?.Dispose();
        }

        /// <summary>
        /// 初始化状态机并创建2D混合树
        /// </summary>
        private void InitializeStateMachine()
        {
            // 检查Clips
            if (idleClip == null || walkForwardClip == null)
            {
                Debug.LogError("[2DBlendTest] 请至少指定Idle和WalkForward Clip！");
                return;
            }

            // 创建状态机
            stateMachine = new StateMachine
            {
                stateMachineKey = "Test2DBlend"
            };

            // 创建2D自由方向混合计算器
            var calculator = new StateAnimationMixCalculatorForBlendTree2DFreeformDirectional
            {
                // ★ 指定参数键（使用枚举参数SpeedX和SpeedZ）
                parameterX = "SpeedX",
                parameterY = "SpeedZ",
                smoothTime = 0.1f,  // 平滑时间

                // 定义采样点（至少需要3个）
                samples = CreateSamples()
            };

            // 创建状态
            var moveState = new StateBase();
            moveState.stateSharedData = new StateSharedData();
            moveState.stateSharedData.hasAnimation = true;
            moveState.stateSharedData.animationConfig = new StateAnimationConfigData
            {
                calculator = calculator
            };
            moveState.stateSharedData.InitializeRuntime();

            // 创建Entity
            var entity = new Entity();

            // 初始化状态机并绑定Animator
            stateMachine.Initialize(entity, animator);

            // 注册状态
            stateMachine.RegisterState(MOVE_STATE_KEY, moveState, StatePipelineType.Main);

            // 启动状态机
            stateMachine.StartStateMachine();

            // 激活移动状态
            if (stateMachine.TryActivateState(MOVE_STATE_KEY))
            {
                isRunning = true;
                Debug.Log("[2DBlendTest] ✓ 2D混合树已启动！使用WASD或方向键控制");
            }
            else
            {
                Debug.LogError("[2DBlendTest] ✗ 激活状态失败");
            }
        }

        /// <summary>
        /// 创建采样点数组 - 根据实际Clip自动配置
        /// </summary>
        private StateAnimationMixCalculatorForBlendTree2D.ClipSample2D[] CreateSamples()
        {
            var samples = new System.Collections.Generic.List<StateAnimationMixCalculatorForBlendTree2D.ClipSample2D>();

            // 中心 - Idle
            if (idleClip != null)
            {
                samples.Add(new StateAnimationMixCalculatorForBlendTree2D.ClipSample2D
                {
                    position = Vector2.zero,
                    clip = idleClip
                });
            }

            // 前方（0, 1）
            if (walkForwardClip != null)
            {
                samples.Add(new StateAnimationMixCalculatorForBlendTree2D.ClipSample2D
                {
                    position = new Vector2(0, 1),
                    clip = walkForwardClip
                });
            }

            // 后方（0, -1）
            if (walkBackwardClip != null)
            {
                samples.Add(new StateAnimationMixCalculatorForBlendTree2D.ClipSample2D
                {
                    position = new Vector2(0, -1),
                    clip = walkBackwardClip
                });
            }

            // 左侧（-1, 0）
            if (walkLeftClip != null)
            {
                samples.Add(new StateAnimationMixCalculatorForBlendTree2D.ClipSample2D
                {
                    position = new Vector2(-1, 0),
                    clip = walkLeftClip
                });
            }

            // 右侧（1, 0）
            if (walkRightClip != null)
            {
                samples.Add(new StateAnimationMixCalculatorForBlendTree2D.ClipSample2D
                {
                    position = new Vector2(1, 0),
                    clip = walkRightClip
                });
            }

            // 左前方（-0.707, 0.707）
            if (walkForwardLeftClip != null)
            {
                samples.Add(new StateAnimationMixCalculatorForBlendTree2D.ClipSample2D
                {
                    position = new Vector2(-0.707f, 0.707f),
                    clip = walkForwardLeftClip
                });
            }

            // 右前方（0.707, 0.707）
            if (walkForwardRightClip != null)
            {
                samples.Add(new StateAnimationMixCalculatorForBlendTree2D.ClipSample2D
                {
                    position = new Vector2(0.707f, 0.707f),
                    clip = walkForwardRightClip
                });
            }

            // 左后方（-0.707, -0.707）
            if (walkBackwardLeftClip != null)
            {
                samples.Add(new StateAnimationMixCalculatorForBlendTree2D.ClipSample2D
                {
                    position = new Vector2(-0.707f, -0.707f),
                    clip = walkBackwardLeftClip
                });
            }

            // 右后方（0.707, -0.707）
            if (walkBackwardRightClip != null)
            {
                samples.Add(new StateAnimationMixCalculatorForBlendTree2D.ClipSample2D
                {
                    position = new Vector2(0.707f, -0.707f),
                    clip = walkBackwardRightClip
                });
            }

            if (samples.Count < 3)
            {
                Debug.LogError("[2DBlendTest] 至少需要3个采样点（当前只有" + samples.Count + "个）");
            }

            Debug.Log($"[2DBlendTest] 创建了{samples.Count}个采样点");
            return samples.ToArray();
        }

        void OnGUI()
        {
            if (!isRunning)
                return;

            GUI.Box(new Rect(10, 10, 300, 100), "2D Blend Tree Test");
            GUI.Label(new Rect(20, 35, 280, 20), $"Input: ({currentInput.x:F2}, {currentInput.y:F2})");
            GUI.Label(new Rect(20, 55, 280, 20), "Control: WASD or Arrow Keys");
            GUI.Label(new Rect(20, 75, 280, 20), $"Running States: {stateMachine.runningStates.Count}");
        }
    }
}
