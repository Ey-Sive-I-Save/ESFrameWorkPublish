using System;
using UnityEngine;

// 示例：演示 ExtForEnum.cs 中常用方法
// 来源：Assets/Plugins/ES/1_Design/Extension/EX_SimpleExtension/ExtForEnum.cs
namespace ES
{
    [Flags]
    public enum ExampleFlags { A = 1, B = 2, C = 4 }

    public class Example_Ext_Enum : MonoBehaviour
    {
        void Start()
        {
            // 1) 基础 Flag 操作
            ExampleFlags f = ExampleFlags.A;
            Debug.Log($"initial: {f}");

            f = f._AddFlag(ExampleFlags.B);
            Debug.Log($"after _AddFlag(B): {f}");

            f = f._RemoveFlag(ExampleFlags.A);
            Debug.Log($"after _RemoveFlag(A): {f}");

            f = f._ToggleFlag(ExampleFlags.C);
            Debug.Log($"after _ToggleFlag(C): {f}");

            // 2) 标志检查
            bool hasAll = f._HasAllFlags(ExampleFlags.B, ExampleFlags.C);
            bool hasAny = f._HasAnyFlags(ExampleFlags.A, ExampleFlags.C);
            Debug.Log($"_HasAllFlags -> {hasAll}, _HasAnyFlags -> {hasAny}");

            // 3) Next / Previous（循环）
            var current = ExampleFlags.A;
            Debug.Log($"Current: {current}, Next: {current._Next()}, Prev: {current._Previous()}");

            // 4) 随机枚举与描述
            var random = ExtForEnum._Random<ExampleFlags>();
            Debug.Log($"Random enum: {random}");

            // 5) 描述（需 Description attribute 示例）
            var desc = ExampleFlags.A._GetDescription();
            Debug.Log($"_GetDescription(A): {desc}");
        }
    }
}
