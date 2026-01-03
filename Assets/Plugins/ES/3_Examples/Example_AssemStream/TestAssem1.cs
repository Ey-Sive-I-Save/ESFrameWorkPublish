using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ES
{
//    public class Mm_AutoAttribute : Attribute
//     {
//         public string Name; 
//         public Mm_AutoAttribute(string name_)
//         {
//             Name=name_;
//         }
//     }

//     public class Register_Mm_AutoAttribute : RuntimeRegister_FOR_ClassAttribute<Mm_AutoAttribute>
//     {
//         public override int LoadTiming => 2; 

//         public override void Handle(Mm_AutoAttribute attribute, Type type)
//         {
//             string ss=attribute.Name;
//             Type t=type;
//         }
//     }

}
// {
// namespace ES
// {
//     //类特性
//     public class QuickInvoke2Attribute : Attribute
//     {
//         public Type target;
//         public string EventName;
//         public QuickInvoke2Attribute(string eventName)
//         {
//             EventName = eventName;
//         }
//     }

//     //实例类
//     public class 策略22
//     {
//         public static Dictionary<string,Type> Stras=new Dictionary<string, Type>();

//         public static Dictionary<string,MethodInfo> QuickInvoke=new Dictionary<string, MethodInfo>();
//         public virtual void DoWhat()
//         {
//            //做了什么事情
//         }
//     }

//     public abstract class EventHandler2<T> : 策略
//     {
//         public override void DoWhat()
//         {
//             base.DoWhat();
//         }
//     }
//     //事件注册器

//     public class EventRegister22: RuntimeRegister_FOR_MethodAttribute<QuickInvoke2Attribute>
//     {

//         public override int LoadTiming => EditorRegisterOrder.Level3.GetHashCode();

//         public override void Handle(QuickInvoke2Attribute attribute, MethodInfo methodInfo)
//         {
//             策略22.QuickInvoke.Add(attribute.EventName, methodInfo);
//         }
//     }

    

//     //使用
//     public class 策略12 : 策略
//     {
//         [QuickInvoke2("实例，1",target =typeof(策略))]
//         public override void DoWhat()
//         {
//             Debug.Log("执行策略1");
//             base.DoWhat();
//         }
//     }

//      public class 策略222
//     { [QuickInvoke2("实例，AAA")]
//        public virtual void AAAA()
//         {
//             {
//             Debug.Log("执行策略AAA");
//         }
//         }

//          [QuickInvoke2("静态")]
//        public static void AAAA2()
//         {
//             {
//             Debug.Log("执行静态策略AAA");
//         }
//     }}
   
//      public class 策略32 : 策略
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

//       public class EventHandler642 : RuntimeGlobalLinker
//     {


//         public override void Level0()
//         {
//              Debug.Log("查询可用的调用 Level0");
//             foreach(var kv in 策略22.QuickInvoke)
//             {
//                 Debug.Log("可用的调用 Level0"+kv.Key);
                
                
//                 if(kv.Value.IsStatic) kv.Value.Invoke(null,null);
//             }
//         }

//         public override void Level1()
//         {
//               Debug.Log("查询可用的调用 Level1");
//             foreach(var kv in 策略22.QuickInvoke)
//             {
//                 Debug.Log("可用的调用 Level1"+kv.Key);
                
                
//                 if(kv.Value.IsStatic) kv.Value.Invoke(null,null);
//             }
//         }

//         public override void Level2_ApplyGlobalLinker()
//         {
//             Debug.Log("查询可用的调用 Level50");
//             foreach(var kv in 策略22.QuickInvoke)
//             {
//                 Debug.Log("可用的调用 Level50"+kv.Key);
                
                
//                 if(kv.Value.IsStatic) kv.Value.Invoke(null,null);
//             }
//         }
//     }
// }
