using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Sirenix.OdinInspector;

namespace ES
{
    /// <summary>
    /// 基于Playable的多流水线动画状态机控制器
    /// 核心控制器,管理三条流水线的运行和混合
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayableStateMachineController : MonoBehaviour
    {
        [Title("状态机配置")]
        [LabelText("状态机数据")]
        [Required]
        [AssetsOnly]
        public StateMachineData stateMachineData;

        [LabelText("Clip表覆盖")]
        [Tooltip("运行时覆盖默认Clip表")]
        public AnimationClipTable runtimeClipTableOverride;

        [Title("运行时状态")]
        [LabelText("是否已初始化")]
        [ShowInInspector]
        [ReadOnly]
        private bool _isInitialized = false;

        [LabelText("是否正在运行")]
        [ShowInInspector]
        [ReadOnly]
        private bool _isRunning = false;

        // ============ 核心组件 ============
        private Animator _animator;
        private PlayableGraph _graph;
        private AnimationMixerPlayable _rootMixer;

        // 三条流水线
        private StatePipeline _basicPipeline;
        private StatePipeline _mainPipeline;
        private StatePipeline _buffPipeline;

        // 核心系统
        private StateContext _context;
        private CostManager _costManager;
        private MemoizationSystem _memoSystem;

        // Clip表
        private AnimationClipTable _activeClipTable;

        // 时间追踪
        private float _currentTime;

        // 事件
        public event Action<int, StatePipelineType> OnStateEntered;
        public event Action<int, StatePipelineType> OnStateExited;
        public event Action<int, int, StatePipelineType> OnStateTransitioned;

        private void Awake()
        {
            if (stateMachineData != null && stateMachineData.autoStart)
            {
                Initialize();
            }
        }

        /// <summary>
        /// 初始化状态机
        /// </summary>
        [Button("初始化状态机", ButtonSizes.Large)]
        [PropertySpace(10)]
        public void Initialize()
        {
            if (_isInitialized)
            {
                Debug.LogWarning("State machine already initialized!");
                return;
            }

            if (stateMachineData == null)
            {
                Debug.LogError("StateMachineData is null! Cannot initialize.");
                return;
            }

            // 获取Animator组件
            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                Debug.LogError("Animator component not found!");
                return;
            }

            // 初始化核心系统
            InitializeCoreSystems();

            // 创建Playable Graph
            CreatePlayableGraph();

            // 初始化流水线
            InitializePipelines();

            // 加载Clip表
            LoadClipTables();

            // 初始化默认参数
            InitializeDefaultParameters();

            _isInitialized = true;

            Log("State machine initialized successfully");
        }

        private void InitializeCoreSystems()
        {
            _context = new StateContext();
            _costManager = new CostManager();
            _memoSystem = new MemoizationSystem();
            _currentTime = 0f;
        }

        private void CreatePlayableGraph()
        {
            // 创建Playable Graph
            _graph = PlayableGraph.Create($"StateMachine_{stateMachineData.machineName}");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            // 创建根混合器 - 用于混合三条流水线
            _rootMixer = AnimationMixerPlayable.Create(_graph, 3);

            // 创建输出并连接到Animator
            var output = AnimationPlayableOutput.Create(_graph, "Animation", _animator);
            output.SetSourcePlayable(_rootMixer);
        }

        private void InitializePipelines()
        {
            // 创建基本线
            _basicPipeline = new StatePipeline(StatePipelineType.Basic, _graph);
            _graph.Connect(_basicPipeline.GetMixer(), 0, _rootMixer, 0);
            _rootMixer.SetInputWeight(0, stateMachineData.basicPipelineWeight);

            // 创建主线
            _mainPipeline = new StatePipeline(StatePipelineType.Main, _graph);
            _graph.Connect(_mainPipeline.GetMixer(), 0, _rootMixer, 1);
            _rootMixer.SetInputWeight(1, stateMachineData.mainPipelineWeight);

            // 创建Buff线
            _buffPipeline = new StatePipeline(StatePipelineType.Buff, _graph);
            _graph.Connect(_buffPipeline.GetMixer(), 0, _rootMixer, 2);
            _rootMixer.SetInputWeight(2, stateMachineData.buffPipelineWeight);
        }

        private void LoadClipTables()
        {
            // 使用覆盖的Clip表或默认Clip表
            _activeClipTable = runtimeClipTableOverride != null 
                ? runtimeClipTableOverride 
                : stateMachineData.defaultClipTable;

            if (_activeClipTable == null)
            {
                Debug.LogWarning("No clip table available!");
            }

            // 合并额外的Clip表
            if (stateMachineData.additionalClipTables != null)
            {
                foreach (var table in stateMachineData.additionalClipTables)
                {
                    if (table != null && _activeClipTable != null)
                    {
                        _activeClipTable.MergeFrom(table, false);
                    }
                }
            }
        }

        private void InitializeDefaultParameters()
        {
            // 加载默认Float参数
            if (stateMachineData.defaultFloatParameters != null)
            {
                foreach (var param in stateMachineData.defaultFloatParameters)
                {
                    _context.SetFloat(param.name, param.defaultValue);
                }
            }

            // 加载默认Int参数
            if (stateMachineData.defaultIntParameters != null)
            {
                foreach (var param in stateMachineData.defaultIntParameters)
                {
                    _context.SetInt(param.name, param.defaultValue);
                }
            }

            // 加载默认Bool参数
            if (stateMachineData.defaultBoolParameters != null)
            {
                foreach (var param in stateMachineData.defaultBoolParameters)
                {
                    _context.SetBool(param.name, param.defaultValue);
                }
            }
        }

        /// <summary>
        /// 启动状态机
        /// </summary>
        [Button("启动状态机", ButtonSizes.Large)]
        public void StartStateMachine()
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("State machine not initialized! Initializing now...");
                Initialize();
            }

            if (_isRunning)
            {
                Debug.LogWarning("State machine already running!");
                return;
            }

            // 播放Graph
            _graph.Play();

            // 进入初始状态
            EnterInitialStates();

            _isRunning = true;
            Log("State machine started");
        }

        private void EnterInitialStates()
        {
            // 进入基本线初始状态
            if (stateMachineData.basicInitialStateId >= 0)
            {
                TryEnterState(stateMachineData.basicInitialStateId, StatePipelineType.Basic, true);
            }

            // 进入主线初始状态
            if (stateMachineData.mainInitialStateId >= 0)
            {
                TryEnterState(stateMachineData.mainInitialStateId, StatePipelineType.Main, true);
            }
        }

        private void Update()
        {
            if (!_isRunning)
                return;

            float deltaTime = Time.deltaTime;
            _currentTime += deltaTime;

            // 更新上下文
            _context.Update();

            // 更新代价返还
            _costManager.UpdateCostReturns(_currentTime);

            // 更新三条流水线
            _basicPipeline?.Update(deltaTime, _currentTime);
            _mainPipeline?.Update(deltaTime, _currentTime);
            _buffPipeline?.Update(deltaTime, _currentTime);

            // 检查备忘状态是否需要刷新
            if (_memoSystem.IsDirty)
            {
                _memoSystem.Refresh(_currentTime);
            }
        }

        /// <summary>
        /// 尝试进入指定状态
        /// </summary>
        public bool TryEnterState(int stateId, StatePipelineType? pipelineType = null, bool forceEnter = false)
        {
            var stateDef = stateMachineData.GetStateDefinition(stateId);
            if (stateDef == null)
            {
                Debug.LogWarning($"State {stateId} not found!");
                return false;
            }

            // 确定流水线
            var targetPipeline = pipelineType ?? stateDef.pipelineType;
            var pipeline = GetPipeline(targetPipeline);
            if (pipeline == null)
            {
                Debug.LogError($"Pipeline {targetPipeline} not found!");
                return false;
            }

            // 检查备忘状态
            if (!forceEnter && _memoSystem.IsStateDenied(stateId, _currentTime))
            {
                LogVerbose($"State {stateId} is denied by memoization system");
                return false;
            }

            // 检查进入条件
            if (!forceEnter && !stateDef.CheckEnterConditions(_context))
            {
                _memoSystem.RecordDenial(stateId, DenialReason.ConditionNotMet, _currentTime);
                LogVerbose($"State {stateId} enter conditions not met");
                return false;
            }

            // 检查代价
            if (!forceEnter && !stateDef.ignoreInCostCalculation)
            {
                if (!_costManager.CanAffordCost(stateDef.cost, stateId, false))
                {
                    // 尝试打断测试
                    if (!TestAndInterrupt(stateDef, pipeline))
                    {
                        _memoSystem.RecordDenial(stateId, DenialReason.CostNotEnough, _currentTime);
                        LogVerbose($"State {stateId} cost not affordable and cannot interrupt");
                        return false;
                    }
                }
            }

            // 可以进入,执行进入逻辑
            EnterState(stateDef, pipeline);
            return true;
        }

        private bool TestAndInterrupt(StateDefinition newState, StatePipeline pipeline)
        {
            var currentState = pipeline.GetCurrentState();
            if (currentState == null)
                return true;

            // 检查优先级
            if (newState.priority <= currentState.Definition.priority)
                return false;

            // 检查是否可以打断当前状态
            if (!pipeline.CanEnterState(newState, _costManager))
                return false;

            // 检查同路退化
            if (currentState.Definition.samePathType != SamePathType.None &&
                currentState.Definition.samePathType == newState.samePathType)
            {
                // 同路状态,检查退化
                if (currentState.Definition.allowWeakInterrupt)
                {
                    // 执行退化而不是完全退出
                    return TryDegradeState(currentState.Definition, pipeline);
                }
            }

            // 可以打断
            return true;
        }

        private bool TryDegradeState(StateDefinition currentState, StatePipeline pipeline)
        {
            if (currentState.degradeTargetId < 0)
                return false;

            var degradeTarget = stateMachineData.GetStateDefinition(currentState.degradeTargetId);
            if (degradeTarget == null)
                return false;

            // 退化到低级状态
            EnterState(degradeTarget, pipeline);
            _memoSystem.MarkDirty(); // 标记备忘状态为脏
            return true;
        }

        private void EnterState(StateDefinition stateDef, StatePipeline pipeline)
        {
            // 获取当前状态
            var previousState = pipeline.GetCurrentState();
            int previousStateId = previousState?.Definition.stateId ?? -1;

            // 消耗代价
            if (!stateDef.ignoreInCostCalculation && stateDef.cost != null)
            {
                _costManager.ConsumeCost(stateDef.cost, stateDef.stateId);
            }

            // 进入新状态
            pipeline.EnterState(stateDef, _context, _graph, _currentTime);

            // 安排代价返还
            if (!stateDef.ignoreInCostCalculation && stateDef.cost != null && stateDef.duration > 0f)
            {
                float recoveryStart = _currentTime + stateDef.duration * stateDef.recoveryStartTime;
                _costManager.ScheduleCostReturn(stateDef.cost, stateDef.stateId, 
                    recoveryStart, stateDef.recoveryDuration);
            }

            // 标记备忘系统为脏
            _memoSystem.MarkDirty();

            // 触发事件
            OnStateEntered?.Invoke(stateDef.stateId, stateDef.pipelineType);
            if (previousStateId >= 0)
            {
                OnStateTransitioned?.Invoke(previousStateId, stateDef.stateId, stateDef.pipelineType);
            }

            LogTransition(previousStateId, stateDef.stateId, stateDef.pipelineType);
        }

        private StatePipeline GetPipeline(StatePipelineType type)
        {
            return type switch
            {
                StatePipelineType.Basic => _basicPipeline,
                StatePipelineType.Main => _mainPipeline,
                StatePipelineType.Buff => _buffPipeline,
                _ => null
            };
        }

        /// <summary>
        /// 设置上下文参数
        /// </summary>
        public void SetFloat(string name, float value) => _context?.SetFloat(name, value);
        public void SetInt(string name, int value) => _context?.SetInt(name, value);
        public void SetBool(string name, bool value) => _context?.SetBool(name, value);
        public void SetTrigger(string name) => _context?.SetTrigger(name);

        /// <summary>
        /// 获取上下文参数
        /// </summary>
        public float GetFloat(string name) => _context?.GetFloat(name) ?? 0f;
        public int GetInt(string name) => _context?.GetInt(name) ?? 0;
        public bool GetBool(string name) => _context?.GetBool(name) ?? false;

        /// <summary>
        /// 获取当前状态
        /// </summary>
        public StateInstance GetCurrentState(StatePipelineType pipeline)
        {
            return GetPipeline(pipeline)?.GetCurrentState();
        }

        /// <summary>
        /// 停止状态机
        /// </summary>
        [Button("停止状态机", ButtonSizes.Medium)]
        public void StopStateMachine()
        {
            if (!_isRunning)
                return;

            _graph.Stop();
            _isRunning = false;
            Log("State machine stopped");
        }

        private void OnDestroy()
        {
            if (_graph.IsValid())
            {
                _basicPipeline?.Cleanup();
                _mainPipeline?.Cleanup();
                _buffPipeline?.Cleanup();
                
                _graph.Destroy();
            }
        }

        // ============ 调试日志 ============
        private void Log(string message)
        {
            if (stateMachineData != null && stateMachineData.enableDebugLog)
            {
                Debug.Log($"[StateMachine] {message}");
            }
        }

        private void LogVerbose(string message)
        {
            if (stateMachineData != null && stateMachineData.enableDebugLog)
            {
                Debug.Log($"[StateMachine] {message}");
            }
        }

        private void LogTransition(int fromStateId, int toStateId, StatePipelineType pipeline)
        {
            if (stateMachineData != null && stateMachineData.logStateTransitions)
            {
                Debug.Log($"[StateMachine] Transition: [{pipeline}] {fromStateId} -> {toStateId}");
            }
        }

        // ============ 调试信息 ============
        [ShowInInspector]
        [ReadOnly]
        [PropertySpace(10)]
        [ShowIf("_isRunning")]
        private string DebugInfo
        {
            get
            {
                if (!_isRunning) return "Not running";
                
                var basic = _basicPipeline?.GetCurrentState()?.Definition.stateName ?? "None";
                var main = _mainPipeline?.GetCurrentState()?.Definition.stateName ?? "None";
                var buff = _buffPipeline?.GetCurrentState()?.Definition.stateName ?? "None";
                
                return $"Basic: {basic}\nMain: {main}\nBuff: {buff}\nTime: {_currentTime:F2}s";
            }
        }
    }
}
