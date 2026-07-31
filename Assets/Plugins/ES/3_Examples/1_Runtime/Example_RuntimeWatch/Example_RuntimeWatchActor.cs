using Sirenix.OdinInspector;
using PrimeTween;
using UnityEngine;

namespace ES.Samples
{
    /// <summary>
    /// 最小可复制示例：展示业务对象如何用少量标记接入 ESRuntimeWatch。
    /// 完整案例请使用 RuntimeWatchCases。
    /// </summary>
    [AddComponentMenu("ES Samples/Runtime Watch Actor")]
    public class Example_RuntimeWatchActor : MonoBehaviour
    {
        [Header("RuntimeWatch 最小示例")]
        [SerializeField] private bool autoAnimate = true;
        [SerializeField] private string stateName = "待机";
        [SerializeField] private float hp = 100f;
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private Transform target;

        [ESRuntimeWatch("战斗/角色", "生命值", category: ESRuntimeWatchAttribute.CategoryCharacter)]
        public float Hp => hp;

        [ESRuntimeWatch("战斗/角色", "移动速度", category: ESRuntimeWatchAttribute.CategoryPerformance)]
        public float MoveSpeed => moveSpeed;

        [ESRuntimeWatch("战斗/角色", "是否存活", category: ESRuntimeWatchAttribute.CategoryCharacter)]
        public bool IsAlive => hp > 0.1f;

        [ESRuntimeWatch("战斗/角色", "目标对象", category: ESRuntimeWatchAttribute.CategoryScene)]
        public string TargetName => target != null ? target.name : "未设置";

        [ESRuntimeWatch("战斗/AI", "当前状态", category: ESRuntimeWatchAttribute.CategoryDebug)]
        public string CurrentState => stateName;

        [ESRuntimeWatch("战斗/AI", "状态摘要", category: ESRuntimeWatchAttribute.CategoryDebug)]
        public string StateSummary => $"{stateName} · HP {hp:0.0} · 速度 {moveSpeed:0.0}";

        [ESRuntimeWatch("战斗/技能", "技能冷却", category: ESRuntimeWatchAttribute.CategoryPerformance)]
        private float skillCooldown;

        [ESRuntimeWatch("战斗/技能", "连击数", category: ESRuntimeWatchAttribute.CategoryDebug)]
        private int comboCount;

        private float stateTimer;
        private Sequence pulseSequence;

        private void OnDisable()
        {
            // PrimeTween 动画必须跟随宿主生命周期停止，避免回池/禁用后继续写入 Transform。
            pulseSequence.Stop();
            pulseSequence = default;
        }

        private void Update()
        {
            if (!autoAnimate)
                return;

            float time = Time.time;
            hp = Mathf.Clamp(75f + Mathf.Sin(time) * 25f, 0f, 100f);
            moveSpeed = 4.5f + Mathf.Sin(time * 0.7f);
            skillCooldown = Mathf.PingPong(time, 3f);
            comboCount = Mathf.FloorToInt(Mathf.PingPong(time * 2f, 6f));

            stateTimer += Time.deltaTime;
            if (stateTimer > 1.25f)
            {
                stateTimer = 0f;
                stateName = stateName == "待机" ? "追击" : stateName == "追击" ? "攻击" : "待机";
            }
        }

        [ESRuntimeWatch("战斗/操作", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [Button("重置战斗状态")]
        public void ResetCombatState()
        {
            hp = 100f;
            moveSpeed = 4.5f;
            skillCooldown = 0f;
            comboCount = 0;
            stateName = "待机";
            stateTimer = 0f;
        }

        [ESRuntimeWatch("战斗/操作", category: ESRuntimeWatchAttribute.CategoryCharacter)]
        [Button("设置生命值")]
        public void SetField_SetHp(float value)
        {
            hp = Mathf.Clamp(value, 0f, 100f);
        }

        [ESRuntimeWatch("战斗/操作", category: ESRuntimeWatchAttribute.CategoryScene)]
        [Button("设置目标名称")]
        public void SetField_SetTargetName(string value)
        {
            if (target == null || string.IsNullOrWhiteSpace(value))
                return;

            target.name = value;
        }

        [ESRuntimeWatch("战斗/操作", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [Button("切换自动演示")]
        public void ToggleAutoAnimate()
        {
            autoAnimate = !autoAnimate;
        }

        [ESRuntimeWatch("战斗/操作", category: ESRuntimeWatchAttribute.CategoryScene)]
        [Button("PrimeTween 脉冲示例")]
        public void PlayPrimeTweenPulse()
        {
            if (transform == null)
                return;

            Vector3 baseScale = transform.localScale;
            pulseSequence.Stop();
            pulseSequence = Sequence.Create()
                .Chain(Tween.Scale(transform, baseScale * 1.08f, 0.12f, Ease.OutQuad))
                .Chain(Tween.Scale(transform, baseScale, 0.18f, Ease.InOutQuad));
        }

        [ESRuntimeWatch("战斗/AI", "战斗诊断文本", category: ESRuntimeWatchAttribute.CategoryDebug)]
        public string GetCombatDebugText()
        {
            return $"{stateName} · Alive={IsAlive} · HP={hp:0.0} · Combo={comboCount}";
        }
    }
}
