using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [DisallowMultipleComponent]
    public sealed class ESStoryInteractable : ESInteractable
    {
        [Title("任务与剧情")]
        [LabelText("定义")]
        public ESStoryDefinitionDataInfo definition;

        [ShowInInspector, ReadOnly, LabelText("活动实例")]
        private string activeInstanceId;
        private ESInteractionBinding activeBinding;

        public override bool UsesExternalCompletion => true;

        public override void OnInteractStarted(Entity entity)
        {
            base.OnInteractStarted(entity);
            if (!TryGetInteractionModule(entity, out EntityBasicInteractionModule interaction)
                || !interaction.TryGetActiveBinding(this, out activeBinding))
            {
                Debug.LogError("[Story] 无法取得当前 InteractionBinding。", this);
                return;
            }

            ESStoryModule module = ESGameManager.GetOrCreateModule<ESStoryModule>();
            string error = module == null ? "无法创建 ESStoryModule。" : null;
            if (module == null || !module.TryStartFromInteraction(definition?.definitionId, entity, activeBinding, out activeInstanceId, out error))
            {
                Debug.LogError("[Story] 启动失败：" + error, this);
                interaction.TryEndExternalInteraction(activeBinding, false, ESInteractionEndReason.BeginRejected);
            }
        }

        public override void OnInteractEnded(Entity entity, bool success, ESInteractionEndReason reason)
        {
            base.OnInteractEnded(entity, success, reason);
            if (!string.IsNullOrEmpty(activeInstanceId)
                && ESGameManager.TryGetModule(out ESStoryModule module))
                module.NotifyInteractionEnded(activeBinding, reason);
            activeInstanceId = null;
            activeBinding = default;
        }

        private static bool TryGetInteractionModule(Entity entity, out EntityBasicInteractionModule result)
        {
            result = null;
            if (entity?.basicDomain?.MyModules?.ValuesNow == null) return false;
            List<EntityBasicModuleBase> modules = entity.basicDomain.MyModules.ValuesNow;
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i] is EntityBasicInteractionModule interaction)
                {
                    result = interaction;
                    return true;
                }
            }
            return false;
        }
    }
}
