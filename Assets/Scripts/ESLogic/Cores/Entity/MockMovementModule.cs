using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Obsolete("示例模块，建议放入具体域模块脚本中", false)]
    [Serializable, TypeRegistryItem("模拟移动模块")]
    public class EntityMockMovementModule : EntityBasicModuleBase
    {
        public float speed = 1f;
        public bool applyToTransform = false;
        public Vector3 simulatedPosition;

        public override void Start()
        {
            if (MyCore != null)
            {
                simulatedPosition = MyCore.transform.position;
            }
        }

        protected override void Update()
        {
            simulatedPosition += Vector3.forward * (speed * Time.deltaTime);

            if (applyToTransform && MyCore != null)
            {
                MyCore.transform.position = simulatedPosition;
            }
        }
    }
}
