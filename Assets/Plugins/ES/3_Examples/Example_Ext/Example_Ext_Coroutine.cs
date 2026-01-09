using System;
using System.Collections;
using UnityEngine;

// 示例：演示 ExtForCouroutine.cs 中常用用法
// 来源：Assets/Plugins/ES/1_Design/Extension/EX_SimpleExtension/ExtForCouroutine.cs
namespace ES
{
    public class Example_Ext_Coroutine : MonoBehaviour
    {
        void Start()
        {
            // 简单例子：启动并等待
            IEnumerator ExampleRoutine()
            {
                Debug.Log("ExampleRoutine start");
                yield return new WaitForSeconds(0.5f);
                Debug.Log("ExampleRoutine after 0.5s");
            }

            // 1) 传统方式启动
            this.StartCoroutine(ExampleRoutine());

            // 2) 扩展启动：在当前 runner（this）上启动
            ExampleRoutine()._StartAt(this);

            // 3) 延迟启动
            ExampleRoutine()._StartAtDelayed(1.0f, this);

            // 4) 重复执行：传入工厂函数。示例：间隔 0.7s，执行 4 次
            System.Func<IEnumerator> factory = () => RepeatOnce();
            factory._StartRepeating(0.7f, count: 4, behaviour: this);

            // 5) 停止协程：演示中先启动一个协程并在 2 秒后停止它
            Coroutine c = ExampleRoutine()._StartAt(this);
            StartCoroutine(StopLater(c, 2f));

            // 6) 在主线程延时执行任意 Action（扩展提供）
            Action act = () => Debug.Log("Action executed on main thread after delay");
            act._RunDelayOnMainThread(0.3f);
        }

        IEnumerator RepeatOnce()
        {
            Debug.Log("RepeatOnce tick");
            yield return null;
        }

        IEnumerator StopLater(Coroutine c, float delay)
        {
            yield return new WaitForSeconds(delay);
            c._StopAt(this);
            Debug.Log("Stopped coroutine via _StopAt");
        }
    }
}
