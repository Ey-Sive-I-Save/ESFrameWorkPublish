using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 当前 Inspector 绘制回调的显式上下文。
    /// 路由负责生成该上下文，扩展不应再用全局窗口状态自行推断宿主。
    /// </summary>
    public enum ESEditorInspectorContextKind
    {
        Other,
        GameObjectMainHeader,
        ComponentInGameObjectInspector,
        StandaloneComponentInspector
    }

    public readonly struct ESEditorInspectorContext
    {
        public UnityEditor.Editor Editor { get; }

        public UnityEngine.Object Target { get; }

        public IReadOnlyList<UnityEngine.Object> Targets { get; }

        public EventType EventType { get; }

        /// <summary>当前权威绘制宿主标识。来自当前 GUIView 对应的 EditorWindow，避免依赖焦点/悬停窗口。</summary>
        public int HostId { get; }

        public ESEditorInspectorContextKind Kind { get; }

        public ESEditorInspectorContext(
            UnityEditor.Editor editor,
            IReadOnlyList<UnityEngine.Object> targets,
            EventType eventType,
            int hostId,
            ESEditorInspectorContextKind kind)
        {
            Editor = editor;
            Targets = targets ?? new UnityEngine.Object[0];
            Target = Targets.Count > 0 ? Targets[0] : null;
            EventType = eventType;
            HostId = hostId;
            Kind = kind;
        }
    }
}
