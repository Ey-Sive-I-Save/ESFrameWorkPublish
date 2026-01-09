using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// 协程工具扩展（弱依赖）：提供在指定 MonoBehaviour 上启动协程、延迟执行、重复执行、停止协程以及一个简单的全局 Runner 备选。
/// </summary>
public static class ExtForCouroutine
{
    // 简单的全局 Runner，用于在没有 MonoBehaviour 引用时启动协程；首次使用时创建一个隐藏的 GameObject。
    private class DefaultCoroutineRunner : MonoBehaviour { }

    private static DefaultCoroutineRunner _runnerInstance;

    private static DefaultCoroutineRunner Runner
    {
        get
        {
            if (_runnerInstance == null)
            {
                GameObject go = new GameObject("__ExtForCouroutine_Runner");
                go.hideFlags = HideFlags.HideAndDontSave;
                _runnerInstance = go.AddComponent<DefaultCoroutineRunner>();
                Object.DontDestroyOnLoad(go);
            }
            return _runnerInstance;
        }
    }

    /// <summary>
    /// 在指定的 MonoBehaviour 上启动协程；若 behaviour 为 null，则使用全局 Runner。
    /// </summary>
    /// <param name="enumerator">要运行的协程枚举器。</param>
    /// <param name="behaviour">目标 MonoBehaviour；可为 null。</param>
    /// <returns>返回 Coroutine 对象（在 global runner 上启动时有效）。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Coroutine _StartAt(this IEnumerator enumerator, MonoBehaviour behaviour = null)
    {
        if (enumerator == null) return null;
        if (behaviour != null) return behaviour.StartCoroutine(enumerator);
        return Runner.StartCoroutine(enumerator);
    }

    /// <summary>
    /// 在指定 MonoBehaviour 上延迟启动一个协程（通过包装一个延迟协程实现）。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Coroutine _StartAtDelayed(this IEnumerator enumerator, float delaySeconds, MonoBehaviour behaviour = null)
    {
        if (enumerator == null) return null;
        IEnumerator Wrapper()
        {
            if (delaySeconds > 0f)
                yield return new WaitForSeconds(delaySeconds);
            yield return behaviour != null ? behaviour.StartCoroutine(enumerator) : Runner.StartCoroutine(enumerator);
        }
        return (behaviour != null ? behaviour : (MonoBehaviour)Runner).StartCoroutine(Wrapper());
    }

    /// <summary>
    /// 每隔 intervalSeconds 执行一次 action 协程，重复 count 次（count <= 0 表示无限重复）。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Coroutine _StartRepeating(this System.Func<IEnumerator> enumeratorFactory, float intervalSeconds, int count = 0, MonoBehaviour behaviour = null)
    {
        if (enumeratorFactory == null) return null;

        IEnumerator Repeater()
        {
            int executed = 0;
            while (count <= 0 || executed < count)
            {
                IEnumerator it = enumeratorFactory();
                if (it != null)
                {
                    yield return (behaviour != null ? behaviour.StartCoroutine(it) : Runner.StartCoroutine(it));
                }
                executed++;
                if (intervalSeconds > 0f)
                    yield return new WaitForSeconds(intervalSeconds);
                else
                    yield return null; // yield one frame
            }
        }

        return (behaviour != null ? behaviour : (MonoBehaviour)Runner).StartCoroutine(Repeater());
    }

    /// <summary>
    /// 停止在指定 MonoBehaviour 或全局 Runner 上运行的协程。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void _StopAt(this Coroutine coroutine, MonoBehaviour behaviour = null)
    {
        if (coroutine == null) return;
        if (behaviour != null)
        {
            behaviour.StopCoroutine(coroutine);
        }
        else if (_runnerInstance != null)
        {
            Runner.StopCoroutine(coroutine);
        }
    }

    /// <summary>
    /// 在主线程上（通过全局 Runner）延迟执行一个简单的回调（不需要协程）。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void _RunDelayOnMainThread(this System.Action action, float delaySeconds = 0f)
    {
        if (action == null) return;
        IEnumerator Wrapper()
        {
            if (delaySeconds > 0f) yield return new WaitForSeconds(delaySeconds);
            action();
            yield break;
        }
        Runner.StartCoroutine(Wrapper());
    }
}
