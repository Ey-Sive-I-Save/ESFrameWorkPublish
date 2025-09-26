using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
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
