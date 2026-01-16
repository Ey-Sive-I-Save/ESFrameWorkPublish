using System;
using System.Collections.Generic;

namespace ES.AIPreview.StateMachine
{
    /// <summary>
    /// 分层状态机原型：
    /// - 支持父子状态（Layer/Group + StateId）；
    /// - 设计为高性能：内部只做字典查找与委托调用，不分配 GC。
    /// </summary>
    public class ESHieraStateMachine
    {
        private readonly Dictionary<string, Dictionary<string, IESState>> _layers =
            new Dictionary<string, Dictionary<string, IESState>>();

        private readonly Dictionary<string, IESState> _currentPerLayer = new Dictionary<string, IESState>();

        public void Register(string layer, IESState state)
        {
            if (string.IsNullOrEmpty(layer)) throw new ArgumentException("layer 不能为空");
            if (state == null || string.IsNullOrEmpty(state.Id)) throw new ArgumentException("state 或 state.Id 不能为空");

            if (!_layers.TryGetValue(layer, out var map))
            {
                map = new Dictionary<string, IESState>();
                _layers[layer] = map;
            }

            map[state.Id] = state;
        }

        public void ChangeState(string layer, string stateId)
        {
            if (string.IsNullOrEmpty(layer) || string.IsNullOrEmpty(stateId)) return;
            if (!_layers.TryGetValue(layer, out var map)) return;
            if (!map.TryGetValue(stateId, out var next)) return;

            if (_currentPerLayer.TryGetValue(layer, out var current) && current == next)
                return;

            current?.OnExit();
            _currentPerLayer[layer] = next;
            next.OnEnter();
        }

        public void Tick(float deltaTime)
        {
            foreach (var kv in _currentPerLayer)
            {
                kv.Value?.OnUpdate(deltaTime);
            }
        }
    }
}
