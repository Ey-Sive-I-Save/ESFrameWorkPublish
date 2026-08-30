using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("设置激活", OperationTypeRegistryNames.GameObject)]
    public sealed class OpGameObject_SetActive : ESOutputOp
    {
        public GameObjectExpressionSource targetObject = new GameObjectExpressionSource();
        public BoolExpressionSource active = new BoolExpressionSource { directBool = true };

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            GameObject obj = targetObject != null ? targetObject.Evaluate(target, support) : null;
            if (obj != null)
                obj.SetActive(active == null || active.Evaluate(target, support));
        }
    }

    [Serializable, TypeRegistryItem("生成Prefab", OperationTypeRegistryNames.GameObject)]
    public sealed class OpGameObject_Instantiate : ESOutputOp
    {
        [LabelText("Prefab")]
        public GameObjectExpressionSource prefab = new GameObjectExpressionSource();

        [LabelText("父对象")]
        public GameObjectExpressionSource parent = new GameObjectExpressionSource();

        [LabelText("本地坐标")]
        public bool useLocalTransform = true;

        public Vector3ExpressionSource position = new Vector3ExpressionSource { directVector3 = Vector3.zero };
        public Vector3ExpressionSource euler = new Vector3ExpressionSource { directVector3 = Vector3.zero };

        [LabelText("生成物设为主目标")]
        public bool setCreatedAsMainTarget;

        [LabelText("生成物加入目标列表")]
        public bool addCreatedEntityToTargets;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            GameObject prefabObject = prefab != null ? prefab.Evaluate(target, support) : null;
            if (prefabObject == null)
                return;

            Transform parentTransform = null;
            GameObject parentObject = parent != null ? parent.Evaluate(target, support) : null;
            if (parentObject != null)
                parentTransform = parentObject.transform;

            GameObject created = UnityEngine.Object.Instantiate(prefabObject, parentTransform);
            Vector3 pos = position != null ? position.Evaluate(target, support) : Vector3.zero;
            Quaternion rot = Quaternion.Euler(euler != null ? euler.Evaluate(target, support) : Vector3.zero);

            if (useLocalTransform)
            {
                created.transform.localPosition = pos;
                created.transform.localRotation = rot;
            }
            else
            {
                created.transform.SetPositionAndRotation(pos, rot);
            }

            Entity entity = FindEntityInSelfOrParents(created);
            if (target != null && entity != null)
            {
                if (setCreatedAsMainTarget)
                    target.SetEntityMainTarget(entity);
                if (addCreatedEntityToTargets)
                    target.AddTarget(entity);
            }
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

    [Serializable, TypeRegistryItem("设置位置旋转", OperationTypeRegistryNames.Transform)]
    public sealed class OpTransform_SetPositionRotation : ESOutputOp
    {
        public GameObjectExpressionSource targetObject = new GameObjectExpressionSource();
        public bool useLocal;
        public Vector3ExpressionSource position = new Vector3ExpressionSource { directVector3 = Vector3.zero };
        public Vector3ExpressionSource euler = new Vector3ExpressionSource { directVector3 = Vector3.zero };

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            GameObject obj = targetObject != null ? targetObject.Evaluate(target, support) : null;
            if (obj == null)
                return;

            Vector3 pos = position != null ? position.Evaluate(target, support) : Vector3.zero;
            Quaternion rot = Quaternion.Euler(euler != null ? euler.Evaluate(target, support) : Vector3.zero);
            if (useLocal)
            {
                obj.transform.localPosition = pos;
                obj.transform.localRotation = rot;
            }
            else
            {
                obj.transform.SetPositionAndRotation(pos, rot);
            }
        }
    }

    [Serializable, TypeRegistryItem("播放粒子", OperationTypeRegistryNames.Vfx)]
    public sealed class OpVfx_PlayParticleSystem : ESOutputOp
    {
        public override bool NeedsStop => true;

        [InfoBox("兼容旁路：直接操作已有 ParticleSystem，不创建 ESVfxHandle，也不参与 ESVfxModule 预算；新内容请使用“播放 VFX 定义”。", InfoMessageType.Warning)]
        public GameObjectExpressionSource targetObject = new GameObjectExpressionSource();
        public bool withChildren = true;

        [LabelText("播放配置的 VFX 音频")]
        [InfoBox("优先使用目标根节点的 ESVfxAudioEmitterSet；若没有 Set，则只触发根节点单个 ESVfxAudioEmitter 的 OnVfxPlay。不扫描 AudioSource 子树。")]
        public bool playConfiguredAudio = true;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            GameObject obj = targetObject != null ? targetObject.Evaluate(target, support) : null;
            if (obj == null)
                return;

            ParticleSystem[] particles = withChildren
                ? obj.GetComponentsInChildren<ParticleSystem>(true)
                : obj.GetComponents<ParticleSystem>();

            for (int i = 0; i < particles.Length; i++)
                particles[i].Play(true);

            ESPooledGameObject pooled = obj.GetComponent<ESPooledGameObject>();
            int pooledVersion = pooled != null ? pooled.Version : 0;
            bool cleanupWithChildren = withChildren;
            bool cleanupAudio = playConfiguredAudio;
            support?.AddCleanup(() =>
            {
                if (pooled != null && (!pooled.IsSpawned || pooled.Version != pooledVersion))
                    return;
                StopObjectPresentation(obj, cleanupWithChildren, cleanupAudio);
            });

            if (playConfiguredAudio)
            {
                ESVfxAudioEmitterSet emitterSet = obj.GetComponent<ESVfxAudioEmitterSet>();
                if (emitterSet != null)
                    emitterSet.PlayConfiguredEmitters();
                else
                    obj.GetComponent<ESVfxAudioEmitter>()?.PlayFromVfx();
            }
        }

        protected override void StopOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            GameObject obj = targetObject != null ? targetObject.Evaluate(target, support) : null;
            if (obj == null)
                return;

            StopObjectPresentation(obj, withChildren, playConfiguredAudio);
        }

        private static void StopObjectPresentation(GameObject obj, bool stopChildren, bool stopAudio)
        {
            if (obj == null)
                return;

            ParticleSystem[] particles = stopChildren
                ? obj.GetComponentsInChildren<ParticleSystem>(true)
                : obj.GetComponents<ParticleSystem>();

            for (int i = 0; i < particles.Length; i++)
                particles[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);

            if (stopAudio)
            {
                ESVfxAudioEmitterSet emitterSet = obj.GetComponent<ESVfxAudioEmitterSet>();
                if (emitterSet != null)
                    emitterSet.StopConfiguredEmitters();
                else
                    obj.GetComponent<ESVfxAudioEmitter>()?.StopConfigured();
            }
        }
    }

    [Serializable, TypeRegistryItem("播放 VFX 定义", OperationTypeRegistryNames.Vfx)]
    public sealed class OpVfx_PlayDefinition : ESOutputOp
    {
        public ESVfxKey vfxKey = new ESVfxKey();
        public GameObjectExpressionSource ownerObject = new GameObjectExpressionSource();
        public Vector3ExpressionSource position = new Vector3ExpressionSource { directVector3 = Vector3.zero };
        public Vector3ExpressionSource euler = new Vector3ExpressionSource { directVector3 = Vector3.zero };
        public bool followOwner;

        public override bool NeedsStop => true;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            StopOwnedVfx(support);
            if (ESGameManager.Vfx == null)
                return;

            GameObject owner = ownerObject != null ? ownerObject.Evaluate(target, support) : null;
            Vector3 pos = position != null ? position.Evaluate(target, support) : Vector3.zero;
            Vector3 eulerAngles = euler != null ? euler.Evaluate(target, support) : Vector3.zero;
            ESVfxPlayRequest request = new ESVfxPlayRequest
            {
                position = pos,
                rotation = Quaternion.Euler(eulerAngles)
            };
            ESVfxHandle handle;
            if (followOwner)
            {
                // Use the same explicit attached-play contract as Audio; a missing Owner is a
                // rejected request, never a silently detached VFX instance.
                handle = owner != null
                    ? ESGameManager.Vfx.PlayAttached(vfxKey, owner.transform, request)
                    : ESGameManager.Vfx.PlayAttached(vfxKey, null, request);
            }
            else
            {
                handle = ESGameManager.Vfx.PlayAtPosition(vfxKey, pos, request);
            }
            if (handle.IsValid)
                support?.AddVfxHandle(this, handle);
        }

        protected override void StopOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            StopOwnedVfx(support);
        }

        private void StopOwnedVfx(ESOpSupport support)
        {
            support?.StopVfxHandles(this);
        }
    }
}
