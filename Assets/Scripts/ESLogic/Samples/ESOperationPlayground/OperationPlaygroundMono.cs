using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Scene Mono sample for assembling regular ESOutputOp chains by hand.
    /// Use this to inspect common operation categories and ContextPool interactions.
    /// </summary>
    public class OperationPlaygroundMono : MonoBehaviour
    {
        [Title("运行目标")]
        [LabelText("使用者 Entity")]
        public Entity userEntity;

        [LabelText("主目标物体")]
        public GameObject mainTargetObject;

        [LabelText("生成用 Prefab")]
        public GameObject prefab;

        [LabelText("音效片段")]
        public AudioClip audioClip;

        [Title("操作配置")]
        [SerializeReference, LabelText("单个 Operation"), ESCompactEdit("单个 Operation")]
        public ESOutputOp singleOperation;

        [SerializeReference, LabelText("Operation 列表"), ESCompactEdit("Operation 列表")]
        public List<ESOutputOp> operations = new List<ESOutputOp>();

        [ShowInInspector, ReadOnly, LabelText("Context 摘要")]
        public string LastContextSummary { get; private set; }

        private ESOpSupport runtimeSupport;

        [Button("填充常规 Op 链", ButtonSizes.Medium)]
        [ContextMenu("ES Sample/Operation/填充常规 Op 链")]
        public void FillCommonOpChain()
        {
            singleOperation = new OpCommon_Sequence
            {
                operations = new List<ESOutputOp>
                {
                    MakeSetFloat("damage", 100f),
                    MakeAddFloat("damage", 25f),
                    MakeSetInt("combo", 3),
                    MakeAddInt("combo", 1),
                    MakeSetBool("can_cast", true),
                    new OpContext_ToggleBool { key = "can_cast" },
                    MakeSetString("phase", "ready_to_cast"),
                    new OpContext_ReplaceString { key = "phase", from = "ready", to = "released" },
                    MakeSetVector3("hit_offset", new Vector3(0f, 1f, 0f)),
                    new OpExpression_WriteFloatToContext
                    {
                        key = "scaled_damage",
                        expression = ESExpressionBuilder.Multiply(
                            ESExpressionBuilder.Constant(100f),
                            ESExpressionBuilder.Constant(1.5f))
                    },
                    new OpExpression_WriteBoolToRuntimeTarget
                    {
                        expression = ESExpressionBuilder.Compare(
                            ESExpressionBuilder.Constant(10f),
                            ESExpressionBuilder.Constant(3f),
                            ESCompareFloatExpression.CompareType.Greater)
                    },
                    MakeEnableTag("casting", 1.5f),
                    new OpContext_EnableLink { key = "combo_window" },
                    new OpContext_ReadFloatToRuntimeTarget { key = "damage", defaultValue = 0f },
                    new OpContext_ReadBoolToRuntimeTarget { key = "can_cast", defaultValue = false },
                    new OpDebug_LogContextValue { key = "damage", prefix = "CommonOpChain" }
                }
            };

            operations = new List<ESOutputOp>
            {
                singleOperation,
                new OpCondition_IfContextBool
                {
                    key = "can_cast",
                    defaultValue = true,
                    onTrue = new OpDebug_LogContextValue { key = "phase", prefix = "CanCast" },
                    onFalse = new OpDebug_LogContextValue { key = "phase", prefix = "CannotCast" }
                }
            };
        }

        [Button("填充目标与表现 Op", ButtonSizes.Medium)]
        [ContextMenu("ES Sample/Operation/填充目标与表现 Op")]
        public void FillTargetAndPresentationOps()
        {
            operations = new List<ESOutputOp>
            {
                new OpTarget_SetUserAsMainTarget { addToTargets = true },
                new OpTarget_SetMainTargetByExpression
                {
                    expression = new ESGetGameObjectExpression_DirectPrefabOrReference { gameObject = mainTargetObject },
                    addToTargets = true
                },
                new OpTarget_AddMainTargetToList(),
                new OpMovement_LookAtMainTarget { rotateUser = true, keepY = true },
                new OpMovement_Translate
                {
                    useMainTarget = false,
                    delta = MakeVector3Source(Vector3.forward),
                    space = Space.Self
                },
                new OpTransform_SetPositionRotation
                {
                    targetObject = MakeGameObjectSource(mainTargetObject),
                    useLocal = false,
                    position = MakeVector3Source(transform.position + Vector3.up),
                    euler = MakeVector3Source(Vector3.zero)
                },
                new OpGameObject_SetActive
                {
                    targetObject = MakeGameObjectSource(mainTargetObject),
                    active = MakeBoolSource(true)
                },
                new OpGameObject_Instantiate
                {
                    prefab = MakeGameObjectSource(prefab),
                    parent = MakeGameObjectSource(gameObject),
                    useLocalTransform = true,
                    position = MakeVector3Source(Vector3.zero),
                    euler = MakeVector3Source(Vector3.zero),
                    setCreatedAsMainTarget = false,
                    addCreatedEntityToTargets = false
                },
                new OpVfx_PlayParticleSystem
                {
                    targetObject = MakeGameObjectSource(mainTargetObject),
                    withChildren = true
                },
                new OpAudio_PlayOneShot
                {
                    clip = MakeAudioClipSource(audioClip),
                    audioSourceObject = MakeGameObjectSource(gameObject),
                    volume = MakeFloatSource(0.75f)
                }
            };
        }

        [Button("填充 Animator Op", ButtonSizes.Medium)]
        [ContextMenu("ES Sample/Operation/填充 Animator Op")]
        public void FillAnimatorOps()
        {
            operations = new List<ESOutputOp>
            {
                new OpAnimator_SetTrigger { triggerName = "Attack", useMainTargetAnimator = false },
                new OpAnimator_SetBool { parameterName = "IsCasting", value = MakeBoolSource(true), useMainTargetAnimator = false },
                new OpAnimator_SetFloat { parameterName = "MoveSpeed", value = MakeFloatSource(1f), useMainTargetAnimator = false },
                new OpAnimator_PlayState { stateName = "Attack01", layer = -1, normalizedTime = 0f, useMainTargetAnimator = false }
            };
        }

        [Button("启动单个 Operation", ButtonSizes.Medium)]
        [ContextMenu("ES Sample/Operation/启动单个 Operation")]
        public void StartSingleOperation()
        {
            ESRuntimeTargetPack target = CreateTargetPack();
            try
            {
                ESOpSupport support = GetSupport();
                singleOperation?._TryStartOp(target, support, support);
                RefreshContextSummary(target);
            }
            finally
            {
                RecycleTarget(target);
            }
        }

        [Button("停止单个 Operation", ButtonSizes.Medium)]
        [ContextMenu("ES Sample/Operation/停止单个 Operation")]
        public void StopSingleOperation()
        {
            ESRuntimeTargetPack target = CreateTargetPack();
            try
            {
                ESOpSupport support = GetSupport();
                singleOperation?._TryStopOp(target, support, support);
                RefreshContextSummary(target);
            }
            finally
            {
                RecycleTarget(target);
            }
        }

        [Button("启动 Operation 列表", ButtonSizes.Medium)]
        [ContextMenu("ES Sample/Operation/启动 Operation 列表")]
        public void StartOperationList()
        {
            ESRuntimeTargetPack target = CreateTargetPack();
            try
            {
                if (operations != null)
                {
                    ESOpSupport support = GetSupport();
                    for (int i = 0; i < operations.Count; i++)
                        operations[i]?._TryStartOp(target, support, support);
                }

                RefreshContextSummary(target);
            }
            finally
            {
                RecycleTarget(target);
            }
        }

        [Button("倒序停止 Operation 列表", ButtonSizes.Medium)]
        [ContextMenu("ES Sample/Operation/倒序停止 Operation 列表")]
        public void StopOperationListReverse()
        {
            ESRuntimeTargetPack target = CreateTargetPack();
            try
            {
                if (operations != null)
                {
                    ESOpSupport support = GetSupport();
                    for (int i = operations.Count - 1; i >= 0; i--)
                        operations[i]?._TryStopOp(target, support, support);
                }

                RefreshContextSummary(target);
            }
            finally
            {
                RecycleTarget(target);
            }
        }

        [Button("重置运行 Context", ButtonSizes.Medium)]
        [ContextMenu("ES Sample/Operation/重置运行 Context")]
        public void ResetRuntimeContext()
        {
            if (runtimeSupport != null && !runtimeSupport.IsRecycled)
                runtimeSupport.TryAutoPushedToPool();

            runtimeSupport = null;
            LastContextSummary = string.Empty;
        }

        private ESOpSupport GetSupport()
        {
            if (runtimeSupport == null || runtimeSupport.IsRecycled)
            {
                runtimeSupport = ESOpSupport.Pool.GetInPool();
                runtimeSupport.BindCustom(this, userEntity, GetInstanceID(), null);
            }

            return runtimeSupport;
        }

        private ESRuntimeTargetPack CreateTargetPack()
        {
            ESRuntimeTargetPack target = ESRuntimeTargetPack.Pool.GetInPool();
            target.SetUser(userEntity);
            target.SetEntity(userEntity);

            Entity mainTarget = FindEntityInSelfOrParents(mainTargetObject);
            target.SetEntityMainTarget(mainTarget != null ? mainTarget : userEntity);
            if (mainTarget != null)
                target.AddTarget(mainTarget);

            return target;
        }

        private void RefreshContextSummary(ESRuntimeTargetPack target)
        {
            ESOpSupport support = GetSupport();
            LastContextSummary =
                $"damage={support.Context.GetFloat("damage", 0f)}, " +
                $"combo={support.Context.GetInt("combo", 0)}, " +
                $"can_cast={support.Context.GetBool("can_cast", false)}, " +
                $"phase={support.Context.GetValue("phase")}, " +
                $"runtimeFloat={(target != null ? target.runtimeFloat : 0f)}, " +
                $"runtimeBool={(target != null && target.runtimeBool)}";

            Debug.Log($"[OperationPlayground] {LastContextSummary}", this);
        }

        private static FloatExpressionSource MakeFloatSource(float value)
        {
            FloatExpressionSource source = new FloatExpressionSource();
            source.SetDirect(value);
            return source;
        }

        private static BoolExpressionSource MakeBoolSource(bool value)
        {
            BoolExpressionSource source = new BoolExpressionSource();
            source.SetDirect(value);
            return source;
        }

        private static IntExpressionSource MakeIntSource(int value)
        {
            IntExpressionSource source = new IntExpressionSource();
            source.SetDirect(value);
            return source;
        }

        private static StringExpressionSource MakeStringSource(string value)
        {
            StringExpressionSource source = new StringExpressionSource();
            source.SetDirect(value);
            return source;
        }

        private static Vector3ExpressionSource MakeVector3Source(Vector3 value)
        {
            Vector3ExpressionSource source = new Vector3ExpressionSource();
            source.SetDirect(value);
            return source;
        }

        private static GameObjectExpressionSource MakeGameObjectSource(GameObject value)
        {
            GameObjectExpressionSource source = new GameObjectExpressionSource();
            source.SetDirect(value);
            return source;
        }

        private static AudioClipExpressionSource MakeAudioClipSource(AudioClip value)
        {
            AudioClipExpressionSource source = new AudioClipExpressionSource();
            source.SetDirect(value);
            return source;
        }

        private static OpContext_SetFloat MakeSetFloat(string key, float value)
        {
            return new OpContext_SetFloat { key = key, value = MakeFloatSource(value) };
        }

        private static OpContext_AddFloat MakeAddFloat(string key, float value)
        {
            return new OpContext_AddFloat { key = key, delta = MakeFloatSource(value) };
        }

        private static OpContext_SetInt MakeSetInt(string key, int value)
        {
            return new OpContext_SetInt { key = key, value = MakeIntSource(value) };
        }

        private static OpContext_AddInt MakeAddInt(string key, int value)
        {
            return new OpContext_AddInt { key = key, delta = MakeIntSource(value) };
        }

        private static OpContext_SetBool MakeSetBool(string key, bool value)
        {
            return new OpContext_SetBool { key = key, value = MakeBoolSource(value) };
        }

        private static OpContext_SetString MakeSetString(string key, string value)
        {
            return new OpContext_SetString { key = key, value = MakeStringSource(value) };
        }

        private static OpContext_SetVector3 MakeSetVector3(string key, Vector3 value)
        {
            return new OpContext_SetVector3 { key = key, value = MakeVector3Source(value) };
        }

        private static OpContext_EnableTag MakeEnableTag(string key, float duration)
        {
            return new OpContext_EnableTag { key = key, duration = MakeFloatSource(duration) };
        }

        private static void RecycleTarget(ESRuntimeTargetPack target)
        {
            if (target != null && !target.IsRecycled)
                target.ForcePushToPool();
        }

        private void OnDestroy()
        {
            if (runtimeSupport != null && !runtimeSupport.IsRecycled)
                runtimeSupport.TryAutoPushedToPool();

            runtimeSupport = null;
        }

        private static Entity FindEntityInSelfOrParents(GameObject gameObject)
        {
            Transform current = gameObject != null ? gameObject.transform : null;
            while (current != null)
            {
                Entity entity = current.GetComponent<Entity>();
                if (entity != null)
                    return entity;

                current = current.parent;
            }

            return null;
        }
    }
}
