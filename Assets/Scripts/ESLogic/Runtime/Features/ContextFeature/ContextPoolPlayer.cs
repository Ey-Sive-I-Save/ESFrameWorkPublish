using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [AddComponentMenu("ES/Context/Context Pool Player")]
    public class ContextPoolPlayer : MonoBehaviour
    {
        [TabGroup("上下文池"), HideLabel]
        public ContextPool Pool = new ContextPool();

        [TabGroup("生命周期设置"), LabelText("自动初始化")]
        public bool AutoInit = true;

        [TabGroup("生命周期设置"), LabelText("自动随组件启用状态切换")]
        public bool AutoEnable = true;

        #region Unity生命周期
        private void Awake()
        {
            if (AutoInit) InitPool();
        }

        private void OnEnable()
        {
            if (AutoEnable) EnablePool();
        }

        private void OnDisable()
        {
            if (AutoEnable) DisablePool();
        }

        public void InitPool()
        {
            Pool ??= new ContextPool();
            Pool.Init();
        }

        public void EnablePool()
        {
            Pool ??= new ContextPool();
            Pool.Enable();
        }

        public void DisablePool()
        {
            Pool?.Disable();
        }
        #endregion
    }
}
