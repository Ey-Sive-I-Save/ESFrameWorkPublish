using UnityEngine;
using ES;
using System.Threading;
using System;

namespace ES.Examples
{
    /// <summary>
    /// Coroutine API 演示 - 协程工具
    /// 提供Action延迟执行、重复执行、网络下载等协程封装
    /// </summary>
    public class Example_Coroutine : MonoBehaviour
    {
        private CancellationTokenSource cancellationToken;

        private void Start()
        {
            Debug.Log("=== Coroutine API 演示 ===");

            // 1. 延迟执行Action（使用缓存的WaitForSeconds）
            StartCoroutine(ESDesignUtility.Coroutine.Delay1sCached(() =>
            {
                Debug.Log("1秒后执行（使用缓存）");
            }));

            // 2. 自定义延迟执行
            StartCoroutine(ESDesignUtility.Coroutine.ActionDelay(() =>
            {
                Debug.Log("2.5秒后执行");
            }, delay: 2.5f));

            // 3. 下一帧执行
            StartCoroutine(ESDesignUtility.Coroutine.ActionDelayOneFrame(() =>
            {
                Debug.Log("下一帧执行");
            }));

            // 4. 重复执行（每隔1秒执行一次，共3次）
            StartCoroutine(ESDesignUtility.Coroutine.ActionRepeat(
                action: () => Debug.Log($"重复执行: {Time.time}"),
                startDelay: 0.5f,
                internal_: 1f,
                times: 3
            ));

            // 5. 可取消的重复执行
            cancellationToken = new CancellationTokenSource();
            StartCoroutine(ESDesignUtility.Coroutine.ActionRepeat(
                action: () => Debug.Log("可取消的重复执行"),
                startDelay: 0f,
                internal_: 0.5f,
                times: -1, // 无限循环
                source: cancellationToken
            ));

            // 6秒后取消
            StartCoroutine(ESDesignUtility.Coroutine.ActionDelay(() =>
            {
                cancellationToken?.Cancel();
                Debug.Log("已取消重复执行");
            }, delay: 6f));

            // 6. 等待条件满足（使用ActionRepeat配合条件判断实现）
            float startTime = Time.time;
            CancellationTokenSource conditionToken = new CancellationTokenSource();
            StartCoroutine(ESDesignUtility.Coroutine.ActionRepeat(
                action: () => {
                    Debug.Log($"持续执行中... 已过 {Time.time - startTime:F1}秒");
                    if (Time.time - startTime > 3f) conditionToken?.Cancel();
                },
                startDelay: 0f,
                internal_: 0.5f,
                times: -1,
                source: conditionToken
            ));

            // 7. FixedUpdate 延迟
            StartCoroutine(ESDesignUtility.Coroutine.DelayNextFixedCached(() =>
            {
                Debug.Log("在下一次FixedUpdate执行");
            }));

            // 8. 帧末尾执行
            StartCoroutine(ESDesignUtility.Coroutine.DelayEndOfFrameCached(() =>
            {
                Debug.Log("在当前帧末尾执行");
            }));
        }

        private void OnDestroy()
        {
            // 清理取消令牌
            cancellationToken?.Dispose();
        }
    }
}
