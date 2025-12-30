// using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.Reflection;
// using UnityEngine;
// namespace ES
// {
//     //类特性
//     public class QuickInvokeAttribute : Attribute
//     {
//         public Type target;
//         public string EventName;
//         public QuickInvokeAttribute(string eventName)
//         {
//             EventName = eventName;
//         }
//     }

//     //实例类
//     public class 策略
//     {
//         public static Dictionary<string,Type> Stras=new Dictionary<string, Type>();

//         public static Dictionary<string,MethodInfo> QuickInvoke=new Dictionary<string, MethodInfo>();
//         public virtual void DoWhat()
//         {
//            //做了什么事情
//         }
//     }

//     public abstract class EventHandler<T> : 策略
//     {
//         public override void DoWhat()
//         {
//             base.DoWhat();
//         }
//     }
//     //事件注册器

//     public class EventRegister : EditorRegister_FOR_MethodAttribute<QuickInvokeAttribute>
//     {
//         public override int Order => EditorRegisterOrder.Level3.GetHashCode();


//         public override void Handle(QuickInvokeAttribute attribute, MethodInfo methodInfo)
//         {
//             策略.QuickInvoke.Add(attribute.EventName, methodInfo);
//         }
//     }

    

//     //使用
//     public class 策略1 : 策略
//     {
//         [QuickInvoke("实例，1",target =typeof(策略))]
//         public override void DoWhat()
//         {
//             Debug.Log("执行策略1");
//             base.DoWhat();
//         }
//     }

//      public class 策略2 
//     { [QuickInvoke("实例，AAA")]
//        public virtual void AAAA()
//         {
//             {
//             Debug.Log("执行策略AAA");
//         }
//         }

//          [QuickInvoke("静态")]
//        public static void AAAA2()
//         {
//             {
//             Debug.Log("执行静态策略AAA");
//         }
//     }}
   
//      public class 策略3 : 策略
//     {
//         public override void DoWhat()
//         {
//             Debug.Log("执行策略3");
//               base.DoWhat();
//         }
//     }

//     // public class EventHandler6 : EditorInvoker_Level2
//     // {

//     //     public override void InitInvoke()
//     //     {  Debug.Log("查询可用的策略");
//     //         foreach(var kv in 策略.Stras)
//     //         {
//     //             Debug.Log("可用的策略"+kv.Key);
//     //         }
//     //     } 
//     // }

//       public class EventHandler64 : EditorInvoker_Level50
//     {

//         public override void InitInvoke()
//         {  Debug.Log("查询可用的调用 Level50");
//             foreach(var kv in 策略.QuickInvoke)
//             {
//                 Debug.Log("可用的调用 Level50"+kv.Key);
                
                
//                 if(kv.Value.IsStatic) kv.Value.Invoke(null,null);
//             }
//         } 
//     }
// } 
