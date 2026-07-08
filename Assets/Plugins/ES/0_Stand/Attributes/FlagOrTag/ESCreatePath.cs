using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public class ESCreatePathAttribute : Attribute
    {
        public string GroupName;
        public string MyName;
        public ESCreatePathAttribute(string groupName,string myName)
        {
            GroupName = groupName;
            MyName = myName;
        }
    }
}
