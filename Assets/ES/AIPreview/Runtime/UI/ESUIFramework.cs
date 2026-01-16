using System.Collections.Generic;
using UnityEngine;

namespace ES.AIPreview.UI
{
    /// <summary>
    /// 运行时 UI 视图基类：
    /// - 每个视图代表一个独立的界面（Screen/Panel）；
    /// - 提供简单的 Show/Hide 生命周期；
    /// - 仅为示例，不依赖具体 UI 系统实现。
    /// </summary>
    public abstract class ESUIView : MonoBehaviour
    {
        public string ViewId;

        public virtual void Show(object args = null)
        {
            gameObject.SetActive(true);
        }

        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 简单 UI 路由器：
    /// - 维护一组 ESUIView；
    /// - 通过 ViewId 进行切换；
    /// - 可扩展为栈式导航等高级功能。
    /// </summary>
    public class ESUIRouter : MonoBehaviour
    {
        [SerializeField]
        private List<ESUIView> views = new List<ESUIView>();

        private readonly Dictionary<string, ESUIView> _map = new Dictionary<string, ESUIView>();
        private ESUIView _current;

        private void Awake()
        {
            _map.Clear();
            foreach (var v in views)
            {
                if (v == null || string.IsNullOrEmpty(v.ViewId)) continue;
                _map[v.ViewId] = v;
                v.gameObject.SetActive(false);
            }
        }

        public void Show(string viewId, object args = null)
        {
            if (string.IsNullOrEmpty(viewId)) return;
            if (!_map.TryGetValue(viewId, out var next)) return;

            if (_current == next)
            {
                _current.Show(args);
                return;
            }

            _current?.Hide();
            _current = next;
            _current.Show(args);
        }
    }
}
