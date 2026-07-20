using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Scene Mono sample for editing and manually evaluating ExpressionSource nodes.
    /// This file is intentionally a playground, not a production skill runner.
    /// </summary>
    public class ExpressionSourcePlaygroundMono : MonoBehaviour
    {
        [Title("运行目标")]
        [LabelText("使用者 Entity")]
        public Entity userEntity;

        [LabelText("直接目标物体")]
        public GameObject directTargetObject;

        [LabelText("直接音效片段")]
        public AudioClip directAudioClip;

        [LabelText("直接动画片段")]
        public AnimationClip directAnimationClip;

        [Title("Expression Source")]
        [LabelText("Float 来源")]
        public FloatExpressionSource damageMultiplier = new FloatExpressionSource();

        [LabelText("Bool 来源")]
        public BoolExpressionSource canCast = new BoolExpressionSource();

        [LabelText("Int 来源")]
        public IntExpressionSource comboIndex = new IntExpressionSource();

        [LabelText("String 来源")]
        public StringExpressionSource debugText = new StringExpressionSource();

        [LabelText("Vector3 来源")]
        public Vector3ExpressionSource sampleVector = new Vector3ExpressionSource();

        [LabelText("Entity 来源")]
        public EntityExpressionSource targetEntity = new EntityExpressionSource();

        [LabelText("GameObject 来源")]
        public GameObjectExpressionSource targetObject = new GameObjectExpressionSource();

        [LabelText("AudioClip 来源")]
        public AudioClipExpressionSource audioClip = new AudioClipExpressionSource();

        [LabelText("AnimationClip 来源")]
        public AnimationClipExpressionSource animationClip = new AnimationClipExpressionSource();

        [Title("原始表达式")]
        [SerializeReference, LabelText("Float 表达式"), ESCompactEdit("Float 表达式")]
        public ESGetFloatExpression multiplierExpression;

        [SerializeReference, LabelText("Bool 表达式"), ESCompactEdit("Bool 表达式")]
        public ESGetBoolExpression castConditionExpression;

        [SerializeReference, LabelText("Int 表达式"), ESCompactEdit("Int 表达式")]
        public ESGetIntExpression intExpression;

        [SerializeReference, LabelText("String 表达式"), ESCompactEdit("String 表达式")]
        public ESGetStringExpression stringExpression;

        [SerializeReference, LabelText("Vector3 表达式"), ESCompactEdit("Vector3 表达式")]
        public ESGetVector3Expression vectorExpression;

        [SerializeReference, LabelText("Entity 表达式"), ESCompactEdit("Entity 表达式")]
        public ESGetEntityExpression entityExpression;

        [SerializeReference, LabelText("GameObject 表达式"), ESCompactEdit("GameObject 表达式")]
        public ESGetGameObjectExpression targetExpression;

        [SerializeReference, LabelText("AudioClip 表达式"), ESCompactEdit("AudioClip 表达式")]
        public ESGetAudioClipExpression audioClipExpression;

        [SerializeReference, LabelText("AnimationClip 表达式"), ESCompactEdit("AnimationClip 表达式")]
        public ESGetAnimationClipExpression animationClipExpression;

        [Title("读取结果")]
        [ShowInInspector, ReadOnly, LabelText("Float")]
        public float LastFloat { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("Bool")]
        public bool LastBool { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("Int")]
        public int LastInt { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("String")]
        public string LastString { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("Vector3")]
        public Vector3 LastVector { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("Entity")]
        public Entity LastEntity { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("GameObject")]
        public GameObject LastGameObject { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("AudioClip")]
        public AudioClip LastAudioClip { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("AnimationClip")]
        public AnimationClip LastAnimationClip { get; private set; }

        private ESOpSupport runtimeSupport;

        [Button("填充直接值 Source", ButtonSizes.Medium)]
        [ContextMenu("ES Sample/ExpressionSource/填充直接值 Source")]
        public void FillDirectSources()
        {
            Entity directEntity = FindEntityInSelfOrParents(directTargetObject);

            damageMultiplier.SetDirect(1.25f);
            canCast.SetDirect(true);
            comboIndex.SetDirect(3);
            debugText.SetDirect("Direct source value");
            sampleVector.SetDirect(new Vector3(0f, 1f, 2f));
            targetEntity.SetDirect(directEntity != null ? directEntity : userEntity);
            targetObject.SetDirect(directTargetObject);
            audioClip.SetDirect(directAudioClip);
            animationClip.SetDirect(directAnimationClip);

            multiplierExpression = ESExpressionBuilder.Constant(1.25f);
            castConditionExpression = ESExpressionBuilder.Constant(true);
            intExpression = new ESConstantIntExpression { value = 3 };
            stringExpression = new ESConstantStringExpression { value = "Direct raw expression" };
            vectorExpression = new ESConstantVector3Expression { value = new Vector3(0f, 1f, 2f) };
            entityExpression = new ESConstantEntityExpression { value = directEntity != null ? directEntity : userEntity };
            targetExpression = new ESGetGameObjectExpression_DirectPrefabOrReference { gameObject = directTargetObject };
            audioClipExpression = new ESConstantAudioClipExpression { value = directAudioClip };
            animationClipExpression = new ESConstantAnimationClipExpression { value = directAnimationClip };
        }

        [Button("填充嵌套表达式", ButtonSizes.Medium)]
        [ContextMenu("ES Sample/ExpressionSource/填充嵌套表达式")]
        public void FillNestedExpressions()
        {
            Entity directEntity = FindEntityInSelfOrParents(directTargetObject);

            damageMultiplier.useDirectFloat = false;
            damageMultiplier.expression = ESExpressionBuilder.Multiply(
                ESExpressionBuilder.Constant(1.2f),
                ESExpressionBuilder.Constant(1.5f));

            canCast.useDirectBool = false;
            canCast.expression = ESExpressionBuilder.Compare(
                ESExpressionBuilder.Constant(10f),
                ESExpressionBuilder.Constant(3f),
                ESCompareFloatExpression.CompareType.GreaterEqual);

            comboIndex.useDirectInt = false;
            comboIndex.expression = new ESConstantIntExpression { value = 5 };

            debugText.useDirectString = false;
            debugText.expression = new ESConstantStringExpression { value = "Nested expression value" };

            sampleVector.useDirectVector3 = false;
            sampleVector.expression = new ESConstantVector3Expression { value = new Vector3(1f, 2f, 3f) };

            targetEntity.useDirectEntity = false;
            targetEntity.expression = new ESConstantEntityExpression { value = directEntity != null ? directEntity : userEntity };

            targetObject.useDirectGameObject = false;
            targetObject.expression = new ESGetGameObjectExpression_ChildPath
            {
                parentExpression = new ESGetGameObjectExpression_RuntimeTargetEntity(),
                childPath = "Model/Weapon",
                includeInactive = true
            };

            audioClip.useDirectAudioClip = false;
            audioClip.expression = new ESConstantAudioClipExpression { value = directAudioClip };

            animationClip.useDirectAnimationClip = false;
            animationClip.expression = new ESConstantAnimationClipExpression { value = directAnimationClip };

            multiplierExpression = ESExpressionBuilder.Add(
                ESExpressionBuilder.Constant(1f),
                ESExpressionBuilder.Constant(0.35f));
            castConditionExpression = ESExpressionBuilder.And(ESExpressionBuilder.Constant(true), canCast);
            intExpression = comboIndex;
            stringExpression = debugText;
            vectorExpression = sampleVector;
            entityExpression = targetEntity;
            targetExpression = targetObject;
            audioClipExpression = audioClip;
            animationClipExpression = animationClip;
        }

        [Button("读取 Source", ButtonSizes.Medium)]
        [ContextMenu("ES Sample/ExpressionSource/读取 Source")]
        public void EvaluateSources()
        {
            ESRuntimeTargetPack target = CreateTargetPack();
            try
            {
                ESOpSupport support = GetSupport();
                LastFloat = damageMultiplier != null ? damageMultiplier.Evaluate(target, support) : 0f;
                LastBool = canCast == null || canCast.Evaluate(target, support);
                LastInt = comboIndex != null ? comboIndex.Evaluate(target, support) : 0;
                LastString = debugText != null ? debugText.Evaluate(target, support) : string.Empty;
                LastVector = sampleVector != null ? sampleVector.Evaluate(target, support) : Vector3.zero;
                LastEntity = targetEntity != null ? targetEntity.Evaluate(target, support) : null;
                LastGameObject = targetObject != null ? targetObject.Evaluate(target, support) : null;
                LastAudioClip = audioClip != null ? audioClip.Evaluate(target, support) : null;
                LastAnimationClip = animationClip != null ? animationClip.Evaluate(target, support) : null;
                LogResult("Source");
            }
            finally
            {
                RecycleTarget(target);
            }
        }

        [Button("读取原始表达式", ButtonSizes.Medium)]
        [ContextMenu("ES Sample/ExpressionSource/读取原始表达式")]
        public void EvaluateRawExpressions()
        {
            ESRuntimeTargetPack target = CreateTargetPack();
            try
            {
                ESOpSupport support = GetSupport();
                LastFloat = multiplierExpression != null ? multiplierExpression.Evaluate(target, support) : 0f;
                LastBool = castConditionExpression == null || castConditionExpression.Evaluate(target, support);
                LastInt = intExpression != null ? intExpression.Evaluate(target, support) : 0;
                LastString = stringExpression != null ? stringExpression.Evaluate(target, support) : string.Empty;
                LastVector = vectorExpression != null ? vectorExpression.Evaluate(target, support) : Vector3.zero;
                LastEntity = entityExpression != null ? entityExpression.Evaluate(target, support) : null;
                LastGameObject = targetExpression != null ? targetExpression.Evaluate(target, support) : null;
                LastAudioClip = audioClipExpression != null ? audioClipExpression.Evaluate(target, support) : null;
                LastAnimationClip = animationClipExpression != null ? animationClipExpression.Evaluate(target, support) : null;
                LogResult("Raw");
            }
            finally
            {
                RecycleTarget(target);
            }
        }

        private ESRuntimeTargetPack CreateTargetPack()
        {
            ESRuntimeTargetPack target = ESRuntimeTargetPack.Pool.GetInPool();
            target.SetUser(userEntity);
            target.SetEntity(userEntity);

            Entity mainTarget = FindEntityInSelfOrParents(directTargetObject);
            target.SetEntityMainTarget(mainTarget != null ? mainTarget : userEntity);
            if (mainTarget != null)
                target.AddTarget(mainTarget);

            return target;
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

        private void LogResult(string sourceName)
        {
            Debug.Log(
                $"[ExpressionSourcePlayground] {sourceName} => float={LastFloat}, bool={LastBool}, int={LastInt}, string={LastString}, vector={LastVector}, entity={GetObjectName(LastEntity)}, go={GetObjectName(LastGameObject)}, audio={GetObjectName(LastAudioClip)}, anim={GetObjectName(LastAnimationClip)}",
                this);
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

        private static string GetObjectName(Object obj)
        {
            return obj != null ? obj.name : "null";
        }
    }
}
