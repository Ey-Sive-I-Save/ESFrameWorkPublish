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

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            GameObject obj = targetObject != null ? targetObject.Evaluate(target, logic) : null;
            if (obj != null)
                obj.SetActive(active == null || active.Evaluate(target, logic));
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

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            GameObject prefabObject = prefab != null ? prefab.Evaluate(target, logic) : null;
            if (prefabObject == null)
                return;

            Transform parentTransform = null;
            GameObject parentObject = parent != null ? parent.Evaluate(target, logic) : null;
            if (parentObject != null)
                parentTransform = parentObject.transform;

            GameObject created = UnityEngine.Object.Instantiate(prefabObject, parentTransform);
            Vector3 pos = position != null ? position.Evaluate(target, logic) : Vector3.zero;
            Quaternion rot = Quaternion.Euler(euler != null ? euler.Evaluate(target, logic) : Vector3.zero);

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

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            GameObject obj = targetObject != null ? targetObject.Evaluate(target, logic) : null;
            if (obj == null)
                return;

            Vector3 pos = position != null ? position.Evaluate(target, logic) : Vector3.zero;
            Quaternion rot = Quaternion.Euler(euler != null ? euler.Evaluate(target, logic) : Vector3.zero);
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
        public GameObjectExpressionSource targetObject = new GameObjectExpressionSource();
        public bool withChildren = true;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            GameObject obj = targetObject != null ? targetObject.Evaluate(target, logic) : null;
            if (obj == null)
                return;

            ParticleSystem[] particles = withChildren
                ? obj.GetComponentsInChildren<ParticleSystem>(true)
                : obj.GetComponents<ParticleSystem>();

            for (int i = 0; i < particles.Length; i++)
                particles[i].Play(true);
        }

        protected override void StopOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            GameObject obj = targetObject != null ? targetObject.Evaluate(target, logic) : null;
            if (obj == null)
                return;

            ParticleSystem[] particles = withChildren
                ? obj.GetComponentsInChildren<ParticleSystem>(true)
                : obj.GetComponents<ParticleSystem>();

            for (int i = 0; i < particles.Length; i++)
                particles[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
