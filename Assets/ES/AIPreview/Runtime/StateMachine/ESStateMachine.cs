using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES.AIPreview.StateMachine
{
    /// <summary>
    /// 轻量级状态机原型：
    /// - 状态本身可以是 ScriptableObject（ESStateAsset）或纯 C# 对象；
    /// - 此处只实现最小可用的运行时状态机逻辑，不依赖现有框架代码；
    /// - 供后续参考，未来如需可与 IESWithLife / IESModule 整合。
    /// </summary>
    public class ESStateMachine
    {
        private readonly Dictionary<string, IESState> _states = new Dictionary<string, IESState>();
        private IESState _current;

        public string CurrentStateId => _current?.Id;

        public void Register(IESState state)
        {
            if (state == null || string.IsNullOrEmpty(state.Id))
                throw new ArgumentException("state 或 state.Id 不能为空");
            _states[state.Id] = state;
        }

        public void ChangeState(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!_states.TryGetValue(id, out var next)) return;

            _current?.OnExit();
            _current = next;
            _current?.OnEnter();
        }

        public void Tick(float deltaTime)
        {
            _current?.OnUpdate(deltaTime);
        }
    }

    /// <summary>
    /// 状态接口：
    /// - 为了保持示例简单，只暴露 Id/OnEnter/OnExit/OnUpdate；
    /// - 未来可在此基础上扩展条件、过渡等能力。
    /// </summary>
    public interface IESState
    {
        string Id { get; }
        void OnEnter();
        void OnExit();
        void OnUpdate(float deltaTime);
    }

    /// <summary>
    /// ScriptableObject 版状态资产示例：
    /// - 可在 Inspector 中配置 Id 与附加参数；
    /// - 运行时通过构造状态机时实例化对应的运行时对象。
    /// </summary>
    public abstract class ESStateAsset : ScriptableObject, IESState
    {
        [SerializeField]
        private string id;

        public string Id => id;

        public abstract void OnEnter();
        public abstract void OnExit();
        public abstract void OnUpdate(float deltaTime);
    }
}
