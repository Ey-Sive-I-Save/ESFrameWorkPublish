using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES
{
    /*此处针对碰撞事件制造Link 声明通用的Link*/
    public enum Channel_ColEvent
    {
        [InspectorName("进入")]Enter,
        [InspectorName("停留")] Stay,
        [InspectorName("退出")] Exit,
    }
    public struct Link_ColEvent_2D 
    {
        public Collider2D collider;
        public Vector3 posAT;
    }
    public struct Link_ColEvent_3D 
    {
        public Collider collider;
        public Vector3 posAT;
    }
}
