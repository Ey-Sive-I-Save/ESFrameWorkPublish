using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    public abstract class ESGetGameObjectExpression : ESGetExpression<GameObject> { }

    [Serializable, TypeRegistryItem("游戏物体获取表达")]
    public class ESGetGameObjectExpression_DirectPrefabOrReference : ESGetGameObjectExpression
    {
        [LabelText("直接引用预制件")]
        public GameObject gameObject;
        public override GameObject Evaluate(ESRuntimeTarget target, IOpSupporter support)
        {
            return gameObject;
        }
    }

    


}