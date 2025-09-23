using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[SpecialClass("常见类","依薇尔工具")]
public class AAA3
{
   
}
[SpecialClass("常见类", "依薇尔2工具")]
public class AAA4
{
    
}

public class SpecialClassAttribute : Attribute
{
    public string SelectType;
    public string GroupName;
    public SpecialClassAttribute(string SelectType, string GroupName)
    {
        this.SelectType = SelectType;
        this.GroupName = GroupName;
    }
}

public class ER_SpecialClassAttribute : EditorRegister_FOR_ClassAttribute<SpecialClassAttribute>
{
    public override void Handle(SpecialClassAttribute attribute, Type type)
    {
        Debug.Log("通过分组" + attribute.SelectType + "/" + attribute.GroupName + ",可以创建一个" + type);
    }
}
