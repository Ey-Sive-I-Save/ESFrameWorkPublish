using UnityEngine;
using ES;
using System.Reflection;

namespace ES.Examples
{
    /// <summary>
    /// Reflection API 演示 - 反射工具
    /// 提供字段/属性/方法的读取、设置、调用等反射操作
    /// </summary>
    public class Example_Reflection : MonoBehaviour
    {
        public class TestClass
        {
            public string publicField = "公开字段";
            private int privateField = 42;
            public string PublicProperty { get; set; } = "公开属性";
            private string PrivateProperty { get; set; } = "私有属性";

            public void PublicMethod()
            {
                Debug.Log("公开方法被调用");
            }

            private void PrivateMethod(string message)
            {
                Debug.Log($"私有方法被调用: {message}");
            }

            public int Add(int a, int b)
            {
                return a + b;
            }

            public string GetInfo()
            {
                return $"Field={privateField}, Prop={PrivateProperty}";
            }
        }

        private void Start()
        {
            Debug.Log("=== Reflection API 演示 ===");

            TestClass obj = new TestClass();

            // ========== 字段操作 ==========
            Debug.Log("--- 字段操作 ---");

            // 1. 获取公开字段
            object publicValue = ESDesignUtility.Reflection.GetField(obj, "publicField");
            Debug.Log($"公开字段值: {publicValue}");

            // 2. 获取私有字段
            object privateValue = ESDesignUtility.Reflection.GetField(obj, "privateField");
            Debug.Log($"私有字段值: {privateValue}");

            // 3. 设置公开字段
            ESDesignUtility.Reflection.SetField(obj, "publicField", "新的公开字段值");
            Debug.Log($"修改后: {obj.publicField}");

            // 4. 设置私有字段
            ESDesignUtility.Reflection.SetField(obj, "privateField", 999);
            object newPrivateValue = ESDesignUtility.Reflection.GetField(obj, "privateField");
            Debug.Log($"修改后的私有字段: {newPrivateValue}");

            // 5. 泛型方式获取字段
            string fieldValue = ESDesignUtility.Reflection.GetField<string>(obj, "publicField");
            Debug.Log($"泛型获取字段: {fieldValue}");

            // 6. 泛型方式设置字段
            ESDesignUtility.Reflection.SetField<string>(obj, "publicField", "泛型设置");
            Debug.Log($"泛型设置后: {obj.publicField}");

            // 7. 尝试获取字段（不打日志）
            if (ESDesignUtility.Reflection.TryGetField(obj, "privateField", out object tryValue))
            {
                Debug.Log($"TryGet成功: {tryValue}");
            }

            // ========== 属性操作 ==========
            Debug.Log("--- 属性操作 ---");

            // 8. 获取公开属性
            object publicProp = ESDesignUtility.Reflection.GetProperty(obj, "PublicProperty");
            Debug.Log($"公开属性值: {publicProp}");

            // 9. 获取私有属性
            object privateProp = ESDesignUtility.Reflection.GetProperty(obj, "PrivateProperty");
            Debug.Log($"私有属性值: {privateProp}");

            // 10. 设置属性
            ESDesignUtility.Reflection.SetProperty(obj, "PublicProperty", "新属性值");
            Debug.Log($"修改后的属性: {obj.PublicProperty}");

            // 11. 泛型方式操作属性
            string propValue = ESDesignUtility.Reflection.GetProperty<string>(obj, "PublicProperty");
            Debug.Log($"泛型获取属性: {propValue}");

            ESDesignUtility.Reflection.SetProperty<string>(obj, "PrivateProperty", "泛型设置私有属性");
            string newPropValue = ESDesignUtility.Reflection.GetProperty<string>(obj, "PrivateProperty");
            Debug.Log($"泛型设置私有属性后: {newPropValue}");

            // ========== 方法调用 ==========
            Debug.Log("--- 方法调用 ---");

            // 12. 调用无参方法
            ESDesignUtility.Reflection.InvokeMethod(obj, "PublicMethod");

            // 13. 调用带参数的私有方法
            ESDesignUtility.Reflection.InvokeMethod(obj, "PrivateMethod", "Hello from reflection!");

            // 14. 调用有返回值的方法
            object result = ESDesignUtility.Reflection.InvokeMethod(obj, "Add", 10, 20);
            if (result is int addResult)
            {
                Debug.Log($"Add(10, 20) = {addResult}");
            }

            // 15. 调用返回字符串的方法
            object infoResult = ESDesignUtility.Reflection.InvokeMethod(obj, "GetInfo");
            if (infoResult is string info)
            {
                Debug.Log($"GetInfo(): {info}");
            }

            // 16. 使用TryInvokeMethodReturn安全调用
            object[] parameters = new object[] { 5, 15 };
            if (ESDesignUtility.Reflection.TryInvokeMethodReturn(obj, "Add", out object tryResult, parameters))
            {
                Debug.Log($"TryInvoke成功: {tryResult}");
            }

            // ========== 类型信息获取 ==========
            Debug.Log("--- 类型信息 ---");

            // 17. 获取所有字段（使用反射API）
            var type = typeof(TestClass);
            var fields = type.GetFields(
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance
            );
            Debug.Log($"类有 {fields.Length} 个字段");

            // 18. 获取所有属性
            var properties = type.GetProperties(
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance
            );
            Debug.Log($"类有 {properties.Length} 个属性");

            // 19. 获取所有方法
            var methods = type.GetMethods(
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance | 
                System.Reflection.BindingFlags.DeclaredOnly
            );
            Debug.Log($"类有 {methods.Length} 个方法");
        }
    }
}
