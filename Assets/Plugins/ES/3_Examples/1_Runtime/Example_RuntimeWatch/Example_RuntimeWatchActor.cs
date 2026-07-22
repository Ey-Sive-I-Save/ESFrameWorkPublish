using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES.Samples{
    public class Example_RuntimeWatchActor : MonoBehaviour
    {
        [Header("Runtime Watch Demo")]
        [SerializeField] private bool autoAnimate = true;
        [SerializeField] private string stateName = "Idle";
        [SerializeField] private float hp = 100;
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private Transform target;

        [ESRuntimeWatch("鎴樻枟/瑙掕壊", "鐢熷懡鍊?, showIf: "@this.autoAnimate")]
        public float Hp => hp;

        [ESRuntimeWatch("鎴樻枟/瑙掕壊", "绉诲姩閫熷害", showIf: "@this.autoAnimate")]
        public float MoveSpeed => moveSpeed;

        [ESRuntimeWatch("鎴樻枟/瑙掕壊", "鏄惁瀛樻椿", showIf: "@this.autoAnimate")]
        public bool IsAlive => hp > 0.1f;

        [ESRuntimeWatch("鎴樻枟/瑙掕壊", "鐩爣瀵硅薄", showIf: "@this.autoAnimate")]
        public string TargetName => target != null ? target.name : "null";

        [ESRuntimeWatch("鎴樻枟/AI", "褰撳墠鐘舵€?, showIf: "@this.autoAnimate")]
        public string CurrentState => stateName;

        [ESRuntimeWatch("鎴樻枟/AI", "鐘舵€佹憳瑕?, showIf: "@this.autoAnimate")]
        public string StateSummary => $"{stateName} | HP:{hp:0.0} | Speed:{moveSpeed:0.0}";

        [ESRuntimeWatch("鎴樻枟/鎶€鑳?, "鎶€鑳藉喎鍗?, showIf: "@this.autoAnimate")]
        private float skillCooldown;

        [ESRuntimeWatch("鎴樻枟/鎶€鑳?, "杩炲嚮鏁?, showIf: "@this.autoAnimate")]
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

        [ESRuntimeWatch("鎴樻枟/鎶€鑳?, "閲嶇疆鎴樻枟鐘舵€?, showIf: "@this.autoAnimate")]
        [Button("閲嶇疆鎴樻枟鐘舵€?)]
        public void SetField_ResetCombatState()
        {
            hp = 100f;
            moveSpeed = 4.5f;
            skillCooldown = 0f;
            comboCount = 0;
            stateName = "Idle";
            stateTimer = 0f;
        }

        [ESRuntimeWatch("鎴樻枟/瑙掕壊", "璁剧疆鐢熷懡鍊?, showIf: "@this.autoAnimate")]
        [Button("璁剧疆鐢熷懡鍊?)]
        public void SetField_SetHp(float value)
        {
            hp = Mathf.Clamp(value, 0f, 100f);
        }

        [ESRuntimeWatch("鎴樻枟/瑙掕壊", "璁剧疆鐩爣", showIf: "@this.autoAnimate")]
        [Button("璁剧疆鐩爣鍚?)]
        public void SetField_SetTargetName(string value)
        {
            if (target == null || string.IsNullOrWhiteSpace(value))
                return;

            target.name = value;
        }

        [ESRuntimeWatch("鎴樻枟/瑙掕壊", "鍒囨崲鑷姩婕旂ず", showIf: "@this.autoAnimate")]
        [Button("鍒囨崲鑷姩婕旂ず")]
        public void ToggleAutoAnimate()
        {
            autoAnimate = !autoAnimate;
        }

        [ESRuntimeWatch("鎴樻枟/AI", "鎴樻枟璇婃柇鏂囨湰", showIf: "@this.autoAnimate")]
        public string GetCombatDebugText()
        {
            return $"{stateName} | Alive:{IsAlive} | Hp:{hp:0.0} | Combo:{comboCount}";
        }
    }
}

