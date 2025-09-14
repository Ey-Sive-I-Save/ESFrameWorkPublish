using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("攻击模块")]
    public class ExampleNormal_Attack : ExampleNormalModule
    {
        public override Type TableKeyType => typeof(ExampleNormal_Attack);
        protected override void Awake()
        {
            
            Debug.Log("Awake"+this);
            base.Awake();
        }
        protected override void OnEnable()
        {
            Debug.Log("OnEnable" + this);
            base.OnEnable();
        }
        public override void Start()
        {
            Debug.Log("Start" + this);
            base.Start();
        }
        protected override void Update()    
        {
            Debug.Log("Update" + this);
            base.Update();
        }
        protected override void OnDisable()
        {
            Debug.Log("OnDisable" + this);
            base.OnDisable();
        }
        public override void OnDestroy()
        {
            Debug.Log("OnDestroy" + this);
            base.OnDestroy();
        }
        
    }

    [Serializable, TypeRegistryItem("攻击特效扩展模块")]
    public class ExampleNormal_ExpandAttack : ExampleNormalModule
    {
        public override Type TableKeyType => typeof(ExampleNormal_ExpandAttack);
        [NonSerialized]
        public ExampleNormal_Attack refer_attack;
        protected override void Awake()
        {
            Debug.Log("Awake" + this);
            base.Awake();
        }
        protected override void OnEnable()
        {
            Debug.Log("OnEnable" + this);
            base.OnEnable();
        }
        public override void Start()
        {
            Debug.Log("Start" + this);
            refer_attack = MyCore.GetMoudle<ExampleNormal_Attack>();
            base.Start();
        }
        protected override void Update()
        {
            Debug.Log("Update" + this+" and "+refer_attack);
            base.Update();
        }
        protected override void OnDisable()
        {
            Debug.Log("OnDisable" + this);
            base.OnDisable();
        }
        public override void OnDestroy()
        {
            Debug.Log("OnDestroy" + this);
            base.OnDestroy();
        }

    }

    [Serializable,TypeRegistryItem("束缚攻击支持")]
    public class ExampleNormal_BanAttack : ExampleNormalModule
    {
        public override Type TableKeyType => null;
        [NonSerialized]
        public ExampleNormal_Attack refer_attack;

        public override void Start()
        {
            Debug.Log("Start" + this);
            refer_attack = MyCore.GetMoudle<ExampleNormal_Attack>();
            base.Start();
        }

        protected override void Update()
        {
            base.Update();
            if(Input.GetKeyDown(KeyCode.O))
            {
                refer_attack.EnabledSelf = !refer_attack.EnabledSelf;
            }
            else if(Input.GetKeyDown(KeyCode.P))
            {
                refer_attack.TryDestroySelf();
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            var c = MyCore;
            var d = MyDomain;
            
        }


    }
}