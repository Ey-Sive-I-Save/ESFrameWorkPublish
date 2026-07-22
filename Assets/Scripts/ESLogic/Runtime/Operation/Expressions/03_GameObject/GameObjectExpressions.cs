using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    public abstract class ESGetGameObjectExpression : ESGetExpression<GameObject> { }

    [Serializable, TypeRegistryItem(ExpressionTypeRegistryNames.DirectGameObject)]
    public class ESGetGameObjectExpression_DirectPrefabOrReference : ESGetGameObjectExpression
    {
        [LabelText("GameObject")]
        public GameObject gameObject;

        public override GameObject Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return gameObject;
        }
    }

    [Serializable, TypeRegistryItem(ExpressionTypeRegistryNames.RuntimeTargetEntityGameObject)]
    public class ESGetGameObjectExpression_RuntimeTargetEntity : ESGetGameObjectExpression
    {
        [InfoBox("返回 ESRuntimeTargetPack.userEntity 所在的 GameObject。")]
        [ShowInInspector, ReadOnly, LabelText("来源")]
        private string Source => "target.userEntity.gameObject";

        public override GameObject Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return target != null && target.userEntity != null
                ? target.userEntity.gameObject
                : null;
        }
    }

    [Serializable, TypeRegistryItem(ExpressionTypeRegistryNames.RuntimeTargetEntityAnimator)]
    public class ESGetGameObjectExpression_RuntimeTargetEntityAnimator : ESGetGameObjectExpression
    {
        [InfoBox("返回 ESRuntimeTargetPack.userEntity.animator 所在的 GameObject。")]
        [ShowInInspector, ReadOnly, LabelText("来源")]
        private string Source => "target.userEntity.animator.gameObject";

        public override GameObject Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            var animator = target != null && target.userEntity != null
                ? target.userEntity.animator
                : null;

            return animator != null ? animator.gameObject : null;
        }
    }

    [Serializable, TypeRegistryItem(ExpressionTypeRegistryNames.RuntimeTargetGameObjectReferPath)]
    public class ESGetGameObjectExpression_RuntimeTargetGameObjectReferPath : ESGetGameObjectExpression
    {
        [LabelText("路径")]
        [InfoBox("从 target.userEntity 上的 GameObjectRefer 查询路径。运行时使用 GetGameObject，编辑器下使用 GetGameObjectEditor。")]
        public string path;

#if UNITY_EDITOR
        [NonSerialized]
        private string lastEditorDebugMessage;
#endif

        public override GameObject Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            if (target == null)
            {
#if UNITY_EDITOR
                LogEditorDebug("target=null");
#endif
                return null;
            }

            if (target.userEntity == null)
            {
#if UNITY_EDITOR
                LogEditorDebug("target.userEntity=null");
#endif
                return null;
            }

            if (string.IsNullOrEmpty(path))
            {
#if UNITY_EDITOR
                LogEditorDebug($"path is null or empty | Entity={target.userEntity.name}");
#endif
                return null;
            }

            var refer = target.userEntity.GetComponent<GameObjectRefer>();
            if (refer == null)
            {
#if UNITY_EDITOR
                LogEditorDebug($"GameObjectRefer missing | Entity={target.userEntity.name} | Path={path}");
#endif
                return null;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {

               
                GameObject editorResult = refer.GetGameObjectEditor(path);
                if (editorResult == null)
                    LogEditorDebug($"Editor path result=null | Entity={target.userEntity.name} | Path={path}");

                return editorResult;
            }
#endif
            return refer.GetGameObject(path);
        }

#if UNITY_EDITOR
        private void LogEditorDebug(string message)
        {
            if (Application.isPlaying || message == lastEditorDebugMessage)
                return;

            lastEditorDebugMessage = message;
            Debug.LogWarning($"[ESGetGameObjectExpression_RuntimeTargetGameObjectReferPath] {message}");
        }
#endif
    }

    [Serializable, TypeRegistryItem(ExpressionTypeRegistryNames.ChildPath)]
    public class ESGetGameObjectExpression_ChildPath : ESGetGameObjectExpression
    {
        [LabelText("父对象表达式")]
        [ESCompactEdit("父对象表达式")]
        [SerializeReference, InlineProperty]
        public ESGetGameObjectExpression parentExpression;

        [LabelText("子节点路径")]
        public string childPath;

        [LabelText("包含非激活对象")]
        public bool includeInactive = true;

        public override GameObject Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            GameObject parent = parentExpression != null
                ? parentExpression.Evaluate(target, support)
                : null;

            if (parent == null || string.IsNullOrEmpty(childPath))
                return null;

            Transform found = FindChildByPath(parent.transform, childPath, includeInactive);
            return found != null ? found.gameObject : null;
        }

        private static Transform FindChildByPath(Transform root, string path, bool includeInactive)
        {
            if (root == null || string.IsNullOrEmpty(path))
                return null;

            string[] segments = path.Split('/');
            Transform current = root;
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];
                if (string.IsNullOrEmpty(segment))
                    return null;

                current = FindDirectChild(current, segment, includeInactive);
                if (current == null)
                    return null;
            }

            return current;
        }

        private static Transform FindDirectChild(Transform parent, string childName, bool includeInactive)
        {
            int childCount = parent.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (!includeInactive && !child.gameObject.activeInHierarchy)
                    continue;

                if (child.name == childName)
                    return child;
            }

            return null;
        }
    }

    [Serializable, TypeRegistryItem(ExpressionTypeRegistryNames.Parent)]
    public class ESGetGameObjectExpression_Parent : ESGetGameObjectExpression
    {
        [LabelText("子对象表达式")]
        [ESCompactEdit("子对象表达式")]
        [SerializeReference, InlineProperty]
        public ESGetGameObjectExpression childExpression;

        public override GameObject Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            GameObject child = childExpression != null
                ? childExpression.Evaluate(target, support)
                : null;

            return child != null && child.transform.parent != null
                ? child.transform.parent.gameObject
                : null;
        }
    }

    [Serializable, TypeRegistryItem("Expression/对象/运行目标/使用者对象")]
    public class ESGetGameObjectExpression_RuntimeTargetUserObject : ESGetGameObjectExpression
    {
        [ShowInInspector, ReadOnly, LabelText("来源")]
        private string Source => "target.userEntity 或 target.userItem";

        public override GameObject Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return target != null ? target.GetGameObject() : null;
        }
    }

    [Serializable, TypeRegistryItem("Expression/对象/运行目标/主目标对象")]
    public class ESGetGameObjectExpression_RuntimeTargetMainObject : ESGetGameObjectExpression
    {
        [ShowInInspector, ReadOnly, LabelText("来源")]
        private string Source => "target.entityMainTarget 或 target.itemMainTarget";

        public override GameObject Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return target != null ? target.GetMainTargetGameObject() : null;
        }
    }
}
