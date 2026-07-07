using System.Collections.Generic;
using UnityEngine;

namespace ES.AIPreview.Input
{
    /// <summary>
    /// 高层输入系统原型：
    /// - 将按键/轴映射为逻辑动作（ActionId）；
    /// - 提供查询接口，供角色/相机/技能等系统使用；
    /// - 仅为轻量示例，不依赖 Unity 新输入系统。
    /// </summary>
    public class ESInputSystem : MonoBehaviour
    {
        [System.Serializable]
        public class InputAction
        {
            public string Id;
            public KeyCode PositiveKey = KeyCode.None;
            public KeyCode NegativeKey = KeyCode.None;
        }

        [SerializeField]
        private List<InputAction> actions = new List<InputAction>();

        private readonly Dictionary<string, float> _values = new Dictionary<string, float>();

        private void Update()
        {
            foreach (var a in actions)
            {
                if (string.IsNullOrEmpty(a.Id)) continue;

                float v = 0f;
                if (a.PositiveKey != KeyCode.None && UnityEngine.Input.GetKey(a.PositiveKey))
                    v += 1f;
                if (a.NegativeKey != KeyCode.None && UnityEngine.Input.GetKey(a.NegativeKey))
                    v -= 1f;

                _values[a.Id] = Mathf.Clamp(v, -1f, 1f);
            }
        }

        /// <summary>
        /// 获取某逻辑动作当前的输入值：
        /// -1 ~ 1 之间，0 表示无输入。
        /// </summary>
        public float GetValue(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return 0f;
            return _values.TryGetValue(actionId, out var v) ? v : 0f;
        }
    }
}
