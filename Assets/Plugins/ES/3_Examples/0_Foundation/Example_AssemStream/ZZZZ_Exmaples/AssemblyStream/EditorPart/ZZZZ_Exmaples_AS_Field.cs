/*using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class AAA
{
    [SpecialField("你好")]
    public string name;
    [SpecialField("哈哈哈")]
    public float f; 
}

public class AAA2
{
    [SpecialField("222da")]
    public string name222;
    [SpecialField("22复健科")]
    public float f22;
}

public class SpecialFieldAttribute : Attribute
{
    public string FlagName;
    public SpecialFieldAttribute(string FlagName)
    {
        this.FlagName = FlagName;
    }
}

public class ER_SpecialAttribue : EditorRegister_FOR_FieldAttribute<SpecialFieldAttribute>
{
    public override void Handle(SpecialFieldAttribute attribute, FieldInfo fieldInfo)
    {
        Debug.Log(fieldInfo.DeclaringType + "标记了一个字段" + fieldInfo + "这里标记为"+attribute.FlagName);
    }
}
*/