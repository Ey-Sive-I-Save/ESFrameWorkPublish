using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES.Examples
{
    public class Example_RuntimeWatchActor : MonoBehaviour
    {
        [Header("Runtime Watch Demo")]
        [SerializeField] private bool autoAnimate = true;
        [SerializeField] private string stateName = "Idle";
        [SerializeField] private float hp = 100;
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private Transform target;

        [ESRuntimeWatch("战斗/角色", "生命值", showIf: "@this.autoAnimate")]
        public float Hp => hp;

        [ESRuntimeWatch("战斗/角色", "移动速度", showIf: "@this.autoAnimate")]
        public float MoveSpeed => moveSpeed;

        [ESRuntimeWatch("战斗/角色", "是否存活", showIf: "@this.autoAnimate")]
        public bool IsAlive => hp > 0.1f;

        [ESRuntimeWatch("战斗/角色", "目标对象", showIf: "@this.autoAnimate")]
        public string TargetName => target != null ? target.name : "null";

        [ESRuntimeWatch("战斗/AI", "当前状态", showIf: "@this.autoAnimate")]
        public string CurrentState => stateName;

        [ESRuntimeWatch("战斗/AI", "状态摘要", showIf: "@this.autoAnimate")]
        public string StateSummary => $"{stateName} | HP:{hp:0.0} | Speed:{moveSpeed:0.0}";

        [ESRuntimeWatch("战斗/技能", "技能冷却", showIf: "@this.autoAnimate")]
        private float skillCooldown;

        [ESRuntimeWatch("战斗/技能", "连击数", showIf: "@this.autoAnimate")]
        private int comboCount;

        private float stateTimer;

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
                stateName = stateName == "Idle" ? "Chase" : stateName == "Chase" ? "Attack" : "Idle";
            }
        }

        [ESRuntimeWatch("战斗/技能", "重置战斗状态", showIf: "@this.autoAnimate")]
        [Button("重置战斗状态")]
        public void SetField_ResetCombatState()
        {
            hp = 100f;
            moveSpeed = 4.5f;
            skillCooldown = 0f;
            comboCount = 0;
            stateName = "Idle";
            stateTimer = 0f;
        }

        [ESRuntimeWatch("战斗/角色", "设置生命值", showIf: "@this.autoAnimate")]
        [Button("设置生命值")]
        public void SetField_SetHp(float value)
        {
            hp = Mathf.Clamp(value, 0f, 100f);
        }

        [ESRuntimeWatch("战斗/角色", "设置目标", showIf: "@this.autoAnimate")]
        [Button("设置目标名")]
        public void SetField_SetTargetName(string value)
        {
            if (target == null || string.IsNullOrWhiteSpace(value))
                return;

            target.name = value;
        }

        [ESRuntimeWatch("战斗/角色", "切换自动演示", showIf: "@this.autoAnimate")]
        [Button("切换自动演示")]
        public void ToggleAutoAnimate()
        {
            autoAnimate = !autoAnimate;
        }

        [ESRuntimeWatch("战斗/AI", "战斗诊断文本", showIf: "@this.autoAnimate")]
        public string GetCombatDebugText()
        {
            return $"{stateName} | Alive:{IsAlive} | Hp:{hp:0.0} | Combo:{comboCount}";
        }
    }
}
