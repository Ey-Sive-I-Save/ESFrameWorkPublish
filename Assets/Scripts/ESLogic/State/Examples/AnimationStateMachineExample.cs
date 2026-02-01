using UnityEngine;
using ES;

/// <summary>
/// 动画状态机使用示例
/// 演示如何在实际项目中使用基于Playable的多流水线动画系统
/// </summary>
public class AnimationStateMachineExample : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private PlayableStateMachineController stateMachine;
    
    [Header("状态ID配置")]
    [SerializeField] private int idleStateId = 0;
    [SerializeField] private int walkStateId = 1;
    [SerializeField] private int runStateId = 2;
    [SerializeField] private int jumpStateId = 10;
    [SerializeField] private int attackStateId = 20;
    [SerializeField] private int buffStateId = 100;

    [Header("运行时参数")]
    [SerializeField] private float moveSpeed = 0f;
    [SerializeField] private bool isGrounded = true;

    private void Start()
    {
        // 如果没有自动初始化,手动初始化
        if (stateMachine != null && !stateMachine.enabled)
        {
            stateMachine.Initialize();
            stateMachine.StartStateMachine();
        }

        // 订阅状态转换事件
        if (stateMachine != null)
        {
            stateMachine.OnStateEntered += OnStateEntered;
            stateMachine.OnStateTransitioned += OnStateTransitioned;
        }
    }

    private void Update()
    {
        if (stateMachine == null) return;

        // 示例1: 根据输入控制移动状态
        HandleMovementInput();

        // 示例2: 处理跳跃输入
        HandleJumpInput();

        // 示例3: 处理攻击输入
        HandleAttackInput();

        // 示例4: 更新参数到状态机
        UpdateStateMachineParameters();
    }

    /// <summary>
    /// 处理移动输入
    /// </summary>
    private void HandleMovementInput()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector2 input = new Vector2(horizontal, vertical);
        
        moveSpeed = input.magnitude;

        // 根据速度切换状态 (同路状态: Idle -> Walk -> Run)
        if (moveSpeed < 0.1f)
        {
            stateMachine.TryEnterState(idleStateId, StatePipelineType.Basic);
        }
        else if (moveSpeed < 0.5f)
        {
            stateMachine.TryEnterState(walkStateId, StatePipelineType.Basic);
        }
        else
        {
            stateMachine.TryEnterState(runStateId, StatePipelineType.Basic);
        }
    }

    /// <summary>
    /// 处理跳跃输入
    /// </summary>
    private void HandleJumpInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            // 使用触发器
            stateMachine.SetTrigger("Jump");
            
            // 尝试进入跳跃状态
            stateMachine.TryEnterState(jumpStateId, StatePipelineType.Basic);
        }
    }

    /// <summary>
    /// 处理攻击输入
    /// </summary>
    private void HandleAttackInput()
    {
        if (Input.GetMouseButtonDown(0)) // 左键攻击
        {
            // 设置攻击类型
            stateMachine.SetInt("AttackType", 1);
            
            // 尝试进入攻击状态(主线)
            bool success = stateMachine.TryEnterState(attackStateId, StatePipelineType.Main);
            
            if (success)
            {
                Debug.Log("进入攻击状态");
            }
            else
            {
                Debug.Log("无法进入攻击状态(可能被备忘系统拦截或代价不足)");
            }
        }

        if (Input.GetMouseButtonDown(1)) // 右键技能
        {
            stateMachine.SetInt("AttackType", 2);
            stateMachine.SetTrigger("Skill");
        }
    }

    /// <summary>
    /// 更新参数到状态机
    /// </summary>
    private void UpdateStateMachineParameters()
    {
        // 更新速度参数
        stateMachine.SetFloat("Speed", moveSpeed);
        
        // 更新接地状态
        stateMachine.SetBool("IsGrounded", isGrounded);
        
        // 更新生命值(示例)
        float health = 100f; // 从实际的生命系统获取
        stateMachine.SetFloat("Health", health);
    }

    /// <summary>
    /// 激活Buff示例
    /// </summary>
    public void ActivateBuff(int buffId)
    {
        // Buff线状态,不影响主要动画
        stateMachine.TryEnterState(buffId, StatePipelineType.Buff);
        
        Debug.Log($"激活Buff: {buffId}");
    }

    /// <summary>
    /// 强制进入状态(调试用)
    /// </summary>
    [ContextMenu("Force Enter Idle")]
    public void ForceEnterIdle()
    {
        stateMachine.TryEnterState(idleStateId, forceEnter: true);
    }

    [ContextMenu("Force Enter Attack")]
    public void ForceEnterAttack()
    {
        stateMachine.TryEnterState(attackStateId, StatePipelineType.Main, forceEnter: true);
    }

    // ============ 事件回调 ============

    private void OnStateEntered(int stateId, StatePipelineType pipeline)
    {
        Debug.Log($"[{pipeline}] 进入状态: {stateId}");
        
        // 根据状态ID执行特定逻辑
        switch (stateId)
        {
            case 10: // Jump
                Debug.Log("执行跳跃!");
                break;
            case 20: // Attack
                Debug.Log("执行攻击!");
                break;
        }
    }

    private void OnStateTransitioned(int fromStateId, int toStateId, StatePipelineType pipeline)
    {
        Debug.Log($"[{pipeline}] 状态转换: {fromStateId} -> {toStateId}");
    }

    private void OnDestroy()
    {
        // 取消订阅
        if (stateMachine != null)
        {
            stateMachine.OnStateEntered -= OnStateEntered;
            stateMachine.OnStateTransitioned -= OnStateTransitioned;
        }
    }

    // ============ 高级示例 ============

    /// <summary>
    /// 示例: 检查代价系统
    /// </summary>
    public void CheckCostAvailability()
    {
        // 可以通过获取当前状态来检查代价
        var currentState = stateMachine.GetCurrentState(StatePipelineType.Main);
        if (currentState != null)
        {
            Debug.Log($"当前主线状态: {currentState.Definition.stateName}");
            Debug.Log($"状态时间: {currentState.GetStateTime():F2}秒");
            Debug.Log($"归一化时间: {currentState.GetNormalizedTime():F2}");
            Debug.Log($"是否在后摇: {currentState.IsInRecovery()}");
        }
    }

    /// <summary>
    /// 示例: 使用临时代价
    /// </summary>
    public void UseTempCost()
    {
        // 添加临时代价(例如受伤时增加疲劳值)
        stateMachine.SetFloat("Fatigue", 0.3f);
        
        // 临时代价会影响状态进入判断
    }

    /// <summary>
    /// 示例: 设置IK曲线
    /// </summary>
    public void SetIKCurve()
    {
        // 创建IK权重曲线
        AnimationCurve ikCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        // 将曲线设置到上下文(需要扩展StateContext API)
        // context.SetCurve("RightHandIK", ikCurve);
    }
}

/// <summary>
/// 高级示例: 自定义状态动作
/// </summary>
public class PlaySoundAction : StateAction
{
    public AudioClip soundClip;
    public float volume = 1f;

    public override void Execute(StateRuntime runtime)
    {
        if (soundClip != null)
        {
            // 播放音效
            AudioSource.PlayClipAtPoint(soundClip, Camera.main.transform.position, volume);
            Debug.Log($"播放音效: {soundClip.name}");
        }
    }
}

/// <summary>
/// 高级示例: 自定义状态组件
/// </summary>
public class ParticleEffectComponent : StateComponent
{
    public GameObject particlePrefab;
    public Vector3 spawnOffset;

    private GameObject _spawnedEffect;

    public override void OnStateEnter(StateRuntime runtime)
    {
        base.OnStateEnter(runtime);
        
        if (particlePrefab != null)
        {
            // 实例化粒子特效
            // _spawnedEffect = Instantiate(particlePrefab, position, rotation);
            Debug.Log("生成粒子特效");
        }
    }

    public override void OnStateExit(StateRuntime runtime)
    {
        base.OnStateExit(runtime);
        
        if (_spawnedEffect != null)
        {
            Object.Destroy(_spawnedEffect);
            _spawnedEffect = null;
        }
    }
}
