using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES
{
    public interface IOOP_GGGloat : IOutputOperation<GameObject, GameObject, float>
    {
        public string Name { get; }
    }
  
    [CreateNodeRunnerSoMenu(NodeEnvironment.Test,"组","动态-GGFloat-操作")]
    public class NodeTestDY : NodeRunnerDynamicSO<IOOP_GGGloat>
    {
        public override Type DefaultValueType()
        {
            return typeof(OOP_GGFloat_);
        }

        public override string GetNameForT()
        {
            return Value?.Name??"未定义";
        }
    }
    #region 演示
    [Serializable,TypeRegistryItem("GGF_活动设置")]
    public class OOP_GGFloat_ : IOOP_GGGloat
    {
        public string aaa;
        public bool bbb;
        public string Name => "GGF_活动设置";

        public void TryCancel(GameObject on, GameObject from, float with)
        {
            on.SetActive(false);
        }

        public void TryOperation(GameObject on, GameObject from, float with)
        {
            on.SetActive(true);
        }
    }

    [Serializable, TypeRegistryItem("GGF_对象添加")]
    public class OOP_GGFloat2_ : IOOP_GGGloat
    {
        public float sda;
        public Vector2 bbdadb;
        public string Name => "GGF_对象添加";

        public void TryCancel(GameObject on, GameObject from, float with)
        {
            on.SetActive(false);
        }

        public void TryOperation(GameObject on, GameObject from, float with)
        {
            on.SetActive(true);
        }
    }

    [Serializable, TypeRegistryItem("GGF_标记")]
    public class OOP_GGFloat3_ : IOOP_GGGloat
    {
        public string Name => "GGF_标记";

        public void TryCancel(GameObject on, GameObject from, float with)
        {
            on.SetActive(false);
        }

        public void TryOperation(GameObject on, GameObject from, float with)
        {
            on.SetActive(true);
        }
    }
    #endregion
}
