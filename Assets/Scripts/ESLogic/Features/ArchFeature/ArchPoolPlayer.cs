using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    public class ContextPoolPlayer : MonoBehaviour
    {
        [TabGroup("原型池本体"),HideLabel]
        public ContextPool Pool = new ContextPool();

        [TabGroup("初始化设置"),LabelText("自动  初始化")]
        public bool AutoInit = true;
        [TabGroup("初始化设置"), LabelText("自动  控制启用禁用")]
        public bool AutoEnable = true;

        #region 无聊的初始化生命周期
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
            Pool.Init();
        }
        public void EnablePool()
        {
            Pool.Enable();
        }
        public void DisablePool()
        {
            Pool.Disable();
        }
        #endregion
    }
}
