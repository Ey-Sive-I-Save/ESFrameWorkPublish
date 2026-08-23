using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("【ES】/相机与表现/Composite Global Wind")]
    public sealed class ESCompositeGlobalWind : MonoBehaviour
    {
        private static readonly List<ESCompositeGlobalWind> ActiveWinds = new List<ESCompositeGlobalWind>();

        [SerializeField] private int priority;
        [SerializeField] private Vector2 direction = Vector2.right;
        [SerializeField, Min(0f)] private float strength = 1f;
        [SerializeField, Min(0f)] private float speed = 1f;

        public static ESCompositeGlobalWind ActiveWind { get; private set; }
        public int Priority => priority;
        public Vector2 Direction => direction;
        public float Strength => strength;
        public float Speed => speed;

        private void OnEnable()
        {
            if (!ActiveWinds.Contains(this)) ActiveWinds.Add(this);
            ResolveAndPublish();
        }

        private void OnDisable()
        {
            ActiveWinds.Remove(this);
            ResolveAndPublish();
        }

        private void Start()
        {
            // Re-register when Enter Play Mode disables both domain and scene reload.
            if (!ActiveWinds.Contains(this)) ActiveWinds.Add(this);
            ResolveAndPublish();
        }

        private void OnValidate()
        {
            strength = Mathf.Max(0f, strength);
            speed = Mathf.Max(0f, speed);
            if (isActiveAndEnabled) ResolveAndPublish();
        }

        public void Configure(Vector2 windDirection, float windStrength, float windSpeed, int windPriority = 0)
        {
            direction = windDirection.sqrMagnitude > 0.000001f ? windDirection.normalized : Vector2.right;
            strength = Mathf.Max(0f, windStrength);
            speed = Mathf.Max(0f, windSpeed);
            priority = windPriority;
            if (isActiveAndEnabled) ResolveAndPublish();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveWinds.Clear();
            ActiveWind = null;
            ESCompositeURPProperties.ClearGlobalWind();
        }

        private static void ResolveAndPublish()
        {
            ESCompositeGlobalWind selected = null;
            for (int i = ActiveWinds.Count - 1; i >= 0; i--)
            {
                ESCompositeGlobalWind candidate = ActiveWinds[i];
                if (candidate == null)
                {
                    ActiveWinds.RemoveAt(i);
                    continue;
                }
                if (!candidate.isActiveAndEnabled) continue;
                if (selected == null
                    || candidate.priority > selected.priority
                    || (candidate.priority == selected.priority
                        && candidate.GetInstanceID() > selected.GetInstanceID()))
                    selected = candidate;
            }

            ActiveWind = selected;
            if (selected == null)
            {
                ESCompositeURPProperties.ClearGlobalWind();
                return;
            }
            ESCompositeURPProperties.SetGlobalWind(selected.direction, selected.strength, selected.speed);
        }
    }
}
