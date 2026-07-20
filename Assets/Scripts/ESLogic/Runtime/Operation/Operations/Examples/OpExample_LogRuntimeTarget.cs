using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 最小 Operation 示例：用于验证 Skill -> TrackTarget -> ClipTarget -> Operation 的运行链路。
    /// </summary>
    [Serializable, TypeRegistryItem(OperationTypeRegistryNames.LogRuntimeTargetName, OperationTypeRegistryNames.DebugExamples)]
    public sealed class OpExample_LogRuntimeTarget : ESOutputOp
    {
        [LabelText("日志前缀")]
        public string logPrefix = "Operation链路测试";

        [LabelText("写入 runtimeFloat")]
        public bool setRuntimeFloat = true;

        [ShowIf(nameof(setRuntimeFloat))]
        [LabelText("runtimeFloat")]
        public float runtimeFloat = 1f;

        [LabelText("打印 Stop 日志")]
        public bool logOnStop;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            if (target == null)
            {
                Debug.LogWarning($"[{logPrefix}] Start | Target=<空>");
                return;
            }

            if (setRuntimeFloat)
                target.runtimeFloat = runtimeFloat;

            Debug.Log(
                $"[{logPrefix}] Start | User={GetName(target.userEntity)} | Main={GetName(target.entityMainTarget)} | Targets={target.targetEntities.Count} | runtimeFloat={target.runtimeFloat}");
        }

        protected override void StopOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            if (!logOnStop)
                return;

            Debug.Log(
                $"[{logPrefix}] Stop | User={GetName(target != null ? target.userEntity : null)} | Main={GetName(target != null ? target.entityMainTarget : null)}");
        }

        private static string GetName(Entity entity)
        {
            return entity != null ? entity.name : "<无>";
        }
    }
}
