/*using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;


public class AAA5
{ 
    [EditorInitInvoke]
    public static void InvokeInit()
    {
        Debug.Log("初始化");
    }

    [EditorInitInvoke]
    public static void InvokeInit2()
    {
        Debug.Log("初始化22");
    }
}

public class AAA6
{
    [EditorInitInvoke]
    public static void InvokeInit2()
    {
        Debug.Log("初始化444");
    }
}

public class EditorInitInvokeAttribute : Attribute
{
    public EditorInitInvokeAttribute()
    {
    }
}

public class ER_SpecialMethodAttribute : EditorRegister_FOR_MethodAttribute<EditorInitInvokeAttribute>
{


    public override void Handle(EditorInitInvokeAttribute attribute, MethodInfo methodInfo)
    {
        methodInfo.Invoke(null, new object[] { });
    }
}
*/