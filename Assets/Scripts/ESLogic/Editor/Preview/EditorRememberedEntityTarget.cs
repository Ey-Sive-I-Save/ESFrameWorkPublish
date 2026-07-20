#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary> 
    /// Entity 专用编辑器目标记忆。
    /// 基础逻辑在 ES_Stand；这里只负责 Entity 类型和业务用途的固定 Key。
    /// </summary>
    public sealed class EditorRememberedEntityTarget : ESEditorRememberedObjectTarget<Entity>
    {
        public static readonly EditorRememberedEntityTarget TrackPreview = new EditorRememberedEntityTarget("ES.TrackView.LastPreviewEntity");
        public static readonly EditorRememberedEntityTarget StatePreview = new EditorRememberedEntityTarget("ES.EntityState.LastPreviewEntity");

        private EditorRememberedEntityTarget(string editorPrefsKey) : base(editorPrefsKey)
        {
        }

        public Entity ResolveOrNull()
        {
            return TryResolve(out Entity entity) ? entity : null;
        }

        public Entity ResolveOrSceneFallback()
        {
            Entity entity = ResolveOrNull();
            if (entity != null)
                return entity;

            entity = ResolveSingleActiveSceneEntity();
            if (entity != null)
                Remember(entity);

            return entity;
        }

        public Entity ResolveFromSelectionOrMemory()
        {
            Entity selected = ResolveSelectionEntity();
            if (selected != null)
            {
                Remember(selected);
                return selected;
            }

            return ResolveOrSceneFallback();
        }

        public ESRuntimeTargetPack FillPreviewTarget(ESRuntimeTargetPack target, Entity preferredEntity = null)
        {
            if (target == null)
                return null;

            Entity entity = preferredEntity != null ? preferredEntity : ResolveFromSelectionOrMemory();
            if (entity != null)
                Remember(entity);

            target.SetUser(entity);
            target.SetEntityMainTarget(entity);
            target.ClearTargets();
            target.AddTarget(entity);
            return target;
        }

        private static Entity ResolveSelectionEntity()
        {
            if (Selection.activeGameObject != null)
                return FindEntityInSelfOrParents(Selection.activeGameObject);

            if (Selection.activeObject is UnityEngine.Component component)
                return FindEntityInSelfOrParents(component.gameObject);

            return null;
        }

        private static Entity FindEntityInSelfOrParents(UnityEngine.GameObject gameObject)
        {
            UnityEngine.Transform current = gameObject != null ? gameObject.transform : null;
            while (current != null)
            {
                Entity entity = current.GetComponent<Entity>();
                if (entity != null)
                    return entity;

                current = current.parent;
            }

            return null;
        }

        private static Entity ResolveSingleActiveSceneEntity()
        {
            Entity[] entities = Object.FindObjectsOfType<Entity>();
            Entity result = null;
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (entity == null || entity.gameObject == null)
                    continue;

                if (!entity.gameObject.scene.IsValid() || string.IsNullOrEmpty(entity.gameObject.scene.path))
                    continue;

                if (!entity.gameObject.activeInHierarchy)
                    continue;

                if (entity.name.EndsWith("_ESPreview", System.StringComparison.Ordinal))
                    continue;

                if (result != null)
                    return null;

                result = entity;
            }

            return result;
        }
    }
}
#endif
