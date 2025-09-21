using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ExtensionForCouroutine 
{
    /// <summary>
    /// 启动协程于
    /// </summary>
    /// <param name="enumerator"></param>
    /// <param name="behaviour"></param>
    public static void _StartAt(this IEnumerator enumerator,MonoBehaviour behaviour=null)
    {
        //此处可以接全局管理器
        if (behaviour != null)
        {
            behaviour.StartCoroutine(enumerator);
        }
    }
}
