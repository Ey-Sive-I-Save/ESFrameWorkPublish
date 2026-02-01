using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
   public enum StateBaseStatus
    {
        [InspectorName("从未启动")]
        Never = 0,
        [InspectorName("运行中")]
        Running = 1,
        [InspectorName("退出")]
        Exited = 2,
    }

    
}
