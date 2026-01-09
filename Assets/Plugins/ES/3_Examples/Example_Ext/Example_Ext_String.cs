using System;
using UnityEngine;

// 示例：演示 ExtForString_Main.cs 中部分字符串扩展
namespace ES
{
    public class Example_Ext_String : MonoBehaviour
    {
        void Start()
        {
            string path = "Assets/Scripts/Test.cs";
            Debug.Log(path._KeepBeforeByLast("/"));

            string email = "user@example.com";
            Debug.Log(email._IsValidEmail());

            string number = "123.45";
            float v = number._AsFloat(0f);
            Debug.Log(v);

            string hash = "password"._ToMD5Hash();
            Debug.Log(hash);
        }
    }
}
