using UnityEngine;

namespace ES.Examples
{
    public class Example_RuntimeWatchActor : MonoBehaviour
    {
        [Header("Runtime Watch Demo")]
        [ESRuntimeWatch("战斗/角色", "生命值")]
        public float hp = 100;

        [ESRuntimeWatch("战斗/角色", "移动速度")]
        public float moveSpeed = 4.5f;

        [ESRuntimeWatch("战斗/角色", "是否存活")]
        public bool isAlive = true;

        [ESRuntimeWatch("战斗/AI", "当前状态")]
        public string currentState = "Idle";

        [ESRuntimeWatch("战斗/AI", "目标对象")]
        public Transform target;

        [ESRuntimeWatch("战斗/技能", "技能冷却")]
        private float skillCooldown;

        [ESRuntimeWatch("战斗/技能", "连击数")]
        private int comboCount;

        private float stateTimer;

        private void Update()
        {
            float time = Time.time;
            hp = Mathf.Clamp(75f + Mathf.Sin(time) * 25f, 0f, 100f);
            moveSpeed = 4.5f + Mathf.Sin(time * 0.7f);
            isAlive = hp > 0.1f;
            skillCooldown = Mathf.PingPong(time, 3f);
            comboCount = Mathf.FloorToInt(Mathf.PingPong(time * 2f, 6f));

            stateTimer += Time.deltaTime;
            if (stateTimer > 1.25f)
            {
                stateTimer = 0f;
                currentState = currentState == "Idle" ? "Chase" : currentState == "Chase" ? "Attack" : "Idle";
            }
        }
    }
}
