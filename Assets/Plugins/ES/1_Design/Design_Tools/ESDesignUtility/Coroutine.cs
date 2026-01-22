using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace ES
{
    public static partial class ESDesignUtility
    {
        //
        public static class Coroutine
        {
            // 固定时间缓存，避免在高频场景中每次 new WaitForSeconds 分配。
            // 注意：这些是进程级别的静态缓存，生命周期与程序相同。
            private static readonly WaitForSeconds s_wait_1s = new WaitForSeconds(1f);
            private static readonly WaitForSeconds s_wait_0_5s = new WaitForSeconds(0.5f);
            private static readonly WaitForSeconds s_wait_0_1s = new WaitForSeconds(0.1f);
            private static readonly WaitForFixedUpdate s_waitFixedUpdate = new WaitForFixedUpdate();
            private static readonly WaitForEndOfFrame s_waitEndOfFrame = new WaitForEndOfFrame();

            
            #region Action协程包裹（通用Action延迟/重复/持续/帧控制/条件执行等）
            /// <summary>
            /// 重复执行指定的 <see cref="Action"/>。
            /// 支持首次延时、固定间隔、指定次数或无限循环，并可通过 <see cref="CancellationTokenSource"/> 取消。
            /// 典型场景：轮询、周期性任务、心跳或定时刷新。
            /// </summary>
            /// <param name="action">要执行的委托，允许为 null（会被安全忽略）。</param>
            /// <param name="startDelay">首次执行前的延迟，单位为秒。</param>
            /// <param name="internal_">每次执行之间的间隔，单位为秒。</param>
            /// <param name="times">执行次数；传入 -1 表示无限循环。</param>
            /// <param name="source">可选的取消令牌源，用于外部中止协程。</param>
            /// <returns>用于 <c>StartCoroutine</c> 的 <see cref="IEnumerator"/>；遇取消或委托异常时安全退出。</returns>
            public static IEnumerator ActionRepeat(Action action, float startDelay = 1, float internal_ = 1, int times = -1, CancellationTokenSource source = default)
            {

                yield return new WaitForSeconds(startDelay);
                for (int i = 0; i < times || times == -1; i++)
                {
                    try
                    {
                        if (source != null)
                        {
                            source.Token.ThrowIfCancellationRequested();
                        }
                    }
                    catch (OperationCanceledException e)
                    {
                        Debug.Log("令牌取消" + e);
                        yield break;
                    }
                    catch (Exception e2)
                    {
                        Debug.Log("其他原因错误" + e2);
                        yield break;
                    }

                    action?.Invoke();
                    yield return new WaitForSeconds(internal_);
                }
            }
            /// <summary>
            /// 延迟指定时间后执行一个 <see cref="Action"/>，使用 Unity 的时间缩放（受 <c>Time.timeScale</c> 影响）。
            /// 典型场景：延迟提示、冷却结束回调或需要等待一段游戏时间后执行的逻辑。
            /// </summary>
            /// <param name="action">要执行的委托，允许为 null（安全处理）。</param>
            /// <param name="delay">延迟时长，单位为秒。</param>
            /// <param name="source">可选的取消令牌源，在到达前可中止执行。</param>
            /// <returns>用于 <c>StartCoroutine</c> 的 <see cref="IEnumerator"/>；取消时协程静默结束。</returns>
            public static IEnumerator ActionDelay(Action action, float delay = 1, CancellationTokenSource source = default)
            {
                yield return new WaitForSeconds(delay);
                try
                {
                    if (source != null)
                    {
                        source.Token.ThrowIfCancellationRequested();
                    }
                }
                catch (OperationCanceledException e)
                {
                    Debug.Log("令牌取消" + e);
                    yield break;
                }
                catch (Exception e2)
                {
                    Debug.Log("其他原因错误" + e2);
                    yield break;
                }
                action?.Invoke();
            }
            /// <summary>
            /// 在下一帧执行指定的 <see cref="Action"/>。
            /// 用于等待当前帧的更新或渲染完成后再执行后续逻辑。
            /// </summary>
            /// <param name="action">将在下一帧执行的委托，允许为 null（安全忽略）。</param>
            /// <returns>用于 <c>StartCoroutine</c> 的 <see cref="IEnumerator"/>。</returns>
            public static IEnumerator ActionDelayOneFrame(Action action)
            {
                yield return null;
                action?.Invoke();
            }

            /// <summary>
            /// 使用缓存的 WaitForSeconds(1s) 在大多数情况下避免分配，等待 1 秒后执行 action。
            /// </summary>
            public static IEnumerator Delay1sCached(Action action)
            {
                yield return s_wait_1s;
                action?.Invoke();
            }

            /// <summary>
            /// 使用缓存的 WaitForSeconds(0.5s) 等待后执行 action。
            /// </summary>
            public static IEnumerator Delay0_5sCached(Action action)
            {
                yield return s_wait_0_5s;
                action?.Invoke();
            }

            /// <summary>
            /// 使用缓存的 WaitForSeconds(0.1s) 等待后执行 action。
            /// </summary>
            public static IEnumerator Delay0_1sCached(Action action)
            {
                yield return s_wait_0_1s;
                action?.Invoke();
            }

            /// <summary>
            /// 在下一次 FixedUpdate 使用缓存的 WaitForFixedUpdate 执行 action。
            /// </summary>
            public static IEnumerator DelayNextFixedCached(Action action)
            {
                yield return s_waitFixedUpdate;
                action?.Invoke();
            }

            /// <summary>
            /// 在当前帧末尾（EndOfFrame）使用缓存的 WaitForEndOfFrame 执行 action。
            /// </summary>
            public static IEnumerator DelayEndOfFrameCached(Action action)
            {
                yield return s_waitEndOfFrame;
                action?.Invoke();
            }
            /// <summary>
            /// 在指定的总时长内持续执行回调：先执行 <paramref name="start"/>，
            /// 然后在每次 FixedUpdate 调用时执行 <paramref name="running"/>，最后执行 <paramref name="end"/>。
            /// 支持通过取消令牌提前终止。
            /// </summary>
            /// <param name="start">开始时调用的一次性回调，允许为 null。</param>
            /// <param name="running">运行期间每个 FixedUpdate 调用的回调，允许为 null。</param>
            /// <param name="end">结束时调用的一次性回调，允许为 null。</param>
            /// <param name="time">总持续时间，单位为秒。</param>
            /// <param name="source">可选的取消令牌源，用于提前中止协程。</param>
            /// <returns>用于 <c>StartCoroutine</c> 的 <see cref="IEnumerator"/>；遇取消或异常时安全退出。</returns>
            public static IEnumerator ActionRunning(Action start, Action running, Action end, float time, CancellationTokenSource source = default)
            {
                start?.Invoke();
                while (time > 0)
                {
                    yield return new WaitForFixedUpdate();
                    time -= Time.deltaTime;
                    try
                    {
                        if (source != null)
                        {
                            source.Token.ThrowIfCancellationRequested();
                        }
                    }
                    catch (OperationCanceledException e)
                    {
                        Debug.Log("令牌取消" + e);
                        yield break;
                    }
                    catch (Exception e2)
                    {
                        Debug.Log("其他原因错误" + e2);
                        yield break;
                    }
                    running?.Invoke();

                }

                end?.Invoke();
                yield return null;
            }
            /// <summary>
            /// 以真实时间为基准等待指定时长（不受 <c>Time.timeScale</c> 影响），
            /// 使用 while 循环逐帧检查以支持在等待期间响应取消请求，等待结束后执行回调。
            /// </summary>
            /// <param name="time">真实时间等待时长，单位为秒。</param>
            /// <param name="action">等待结束后要执行的委托，允许为 null。</param>
            /// <param name="source">可选的取消令牌源，用于在等待期间中止。</param>
            /// <returns>用于 <c>StartCoroutine</c> 的 <see cref="IEnumerator"/>。</returns>
            public static IEnumerator ActionWaitForRealSecondsBaseWhile(float time, Action action, CancellationTokenSource source = default)
            {
                float start = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup < start + time)
                {
                    yield return null;
                    try
                    {
                        if (source != null)
                        {
                            source.Token.ThrowIfCancellationRequested();
                        }
                    }
                    catch (OperationCanceledException e)
                    {
                        Debug.Log("令牌取消" + e);
                        yield break;
                    }
                    catch (Exception e2)
                    {
                        Debug.Log("其他原因错误" + e2);
                        yield break;
                    }

                }
                action?.Invoke();
            }
            /// <summary>
            /// 使用 <see cref="WaitForSecondsRealtime"/> 在真实时间（不受 <c>Time.timeScale</c> 影响）下延迟执行回调。
            /// </summary>
            /// <param name="time">真实时间等待时长，单位为秒。</param>
            /// <param name="action">等待结束后要执行的委托，允许为 null。</param>
            /// <param name="source">可选的取消令牌源。</param>
            /// <returns>用于 <c>StartCoroutine</c> 的 <see cref="IEnumerator"/>。</returns>
            public static IEnumerator ActionWaitForRealSeconds(float time, Action action, CancellationTokenSource source = default)
            {
                yield return new WaitForSecondsRealtime(time);
                try
                {
                    if (source != null)
                    {
                        source.Token.ThrowIfCancellationRequested();
                    }
                }
                catch (OperationCanceledException e)
                {
                    Debug.Log("令牌取消" + e);
                    yield break;
                }
                catch (Exception e2)
                {
                    Debug.Log("其他原因错误" + e2);
                    yield break;
                }
                action?.Invoke();
            }
            /// <summary>
            /// 在下一个物理 FixedUpdate 后执行给定的 <see cref="Action"/>，适合与物理系统同步的逻辑。
            /// </summary>
            /// <param name="action">将在 FixedUpdate 后调用的委托，允许为 null。</param>
            /// <returns>用于 <c>StartCoroutine</c> 的 <see cref="IEnumerator"/>。</returns>
            public static IEnumerator ActionWaitForFixedUpdate(Action action)
            {
                yield return new WaitForFixedUpdate();
                action?.Invoke();
            }
            /// <summary>
            /// 在当前帧的末尾（渲染后）执行指定的 <see cref="Action"/>，常用来做帧级别的清理或统计。
            /// </summary>
            /// <param name="action">将在帧末调用的委托，允许为 null。</param>
            /// <returns>用于 <c>StartCoroutine</c> 的 <see cref="IEnumerator"/>。</returns>
            public static IEnumerator ActionWaitForWaitForEndOfFrame(Action action)
            {
                yield return new WaitForEndOfFrame();
                action?.Invoke();
            }
            /// <summary>
            /// 按固定间隔重复执行指定的 <see cref="Action"/>，直到提供的停止条件返回 true 为止。
            /// 用于轮询异步条件或等待外部状态满足的场景。
            /// </summary>
            /// <param name="action">每次迭代要执行的委托，允许为 null。</param>
            /// <param name="stopCondition">用于判断是否停止迭代的函数，返回 true 则停止。</param>
            /// <param name="interval">每次迭代之间的间隔，单位为秒。</param>
            /// <returns>用于 <c>StartCoroutine</c> 的 <see cref="IEnumerator"/>。</returns>
            public static IEnumerator ActionRepeatExecuteUntil(Action action, Func<bool> stopCondition, float interval = 1f)
            {
                while (!stopCondition())
                {
                    action?.Invoke();
                    yield return new WaitForSeconds(interval);
                }
            }

            #endregion

            #region 网络与资源异步回调（下载/加载/实例化/场景等）
            // 特殊回调：网络下载、资源加载、场景加载、网络请求、异步实例化等
            /// <summary>
            /// 根据指定 URL 异步下载图片并在完成后通过回调返回纹理。
            /// 使用 UnityWebRequestTexture 获取资源，支持超时设置。
            /// </summary>
            /// <param name="url">图片资源的完整 URL。</param>
            /// <param name="timeout">下载超时时长（秒），默认为 10 秒。</param>
            /// <param name="callback">下载完成后的回调，参数为 (成功?, Texture2D)。</param>
            /// <returns>用于 <c>StartCoroutine</c> 的 <see cref="IEnumerator"/>。</returns>
            public static IEnumerator CallBackTextureDownload(string url, float timeout = 10f, Action<bool, Texture2D> callback = null)
            {
                using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
                {
                    request.timeout = (int)timeout;

                    yield return request.SendWebRequest();

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        callback?.Invoke(false, null);
                        yield break;
                    }

                    // 从 DownloadHandler 中获取纹理
                    DownloadHandlerTexture downloadHandler = (DownloadHandlerTexture)request.downloadHandler;
                    callback?.Invoke(true, downloadHandler.texture);
                }
            }
            /// <summary>
            /// 根据指定 URL 异步下载 AssetBundle 并通过回调返回结果。
            /// 使用 UnityWebRequestAssetBundle 获取，支持超时与空包判定。
            /// </summary>
            /// <param name="url">AssetBundle 的 URL。</param>
            /// <param name="timeout">下载超时时长（秒），默认为 30 秒。</param>
            /// <param name="callback">下载完成后的回调，参数为 (成功?, AssetBundle)。</param>
            /// <returns>用于 <c>StartCoroutine</c> 的 <see cref="IEnumerator"/>。</returns>
            public static IEnumerator CallBackAssetBundleDownload(string url, float timeout = 30f, Action<bool, AssetBundle> callback = null)
            {
                using (UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(url))
                {
                    request.timeout = (int)timeout;

                    yield return request.SendWebRequest();

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        callback?.Invoke(false, null);
                        yield break;
                    }

                    // 从 DownloadHandler 中获取 AssetBundle
                    DownloadHandlerAssetBundle downloadHandler = (DownloadHandlerAssetBundle)request.downloadHandler;
                    AssetBundle bundle = downloadHandler.assetBundle;

                    if (bundle == null)
                    {
                        callback?.Invoke(false, null);
                    }
                    else
                    {
                        callback?.Invoke(true, bundle);
                    }
                }
            }
            /// <summary>
            /// 异步加载 Resources 路径下的资源（使用 <see cref="Resources.LoadAsync{T}(string)"/>）。
            /// 加载完成后通过回调返回指定类型的资源或 null（并在控制台输出错误）。
            /// </summary>
            /// <typeparam name="T">资源类型，必须继承自 <see cref="UnityEngine.Object"/>。</typeparam>
            /// <param name="path">Resources 下的相对路径（不包含扩展名）。</param>
            /// <param name="callback">加载完成后的回调，参数为加载到的资源或 null。</param>
            /// <returns>用于 <c>StartCoroutine</c> 的 <see cref="IEnumerator"/>。</returns>
            public static IEnumerator CallBackLoadResource<T>(string path, Action<T> callback) where T : UnityEngine.Object
            {
                ResourceRequest request = Resources.LoadAsync<T>(path);

                yield return request; // 等待加载完成

                if (request.asset is T loadedAsset)
                {
                    callback?.Invoke(loadedAsset);
                }
                else
                {
                    Debug.LogError($"Failed to load resource at path: {path}");
                    callback?.Invoke(null);
                }
            }
            /// <summary>
            /// 异步加载指定场景（Additive 或 Single 模式），在完成后通过回调通知结果。
            /// 使用 allowSceneActivation 控制激活时机，并在加载完成后触发回调。
            /// </summary>
            /// <param name="sceneName">要加载的场景名称（须在 Build Settings 中注册）。</param>
            /// <param name="isAdditive">是否以 Additive 模式加载，默认为 true。</param>
            /// <param name="onComplete">加载完成后的回调，参数为是否成功。</param>
            /// <returns>用于 <c>StartCoroutine</c> 的 <see cref="IEnumerator"/>。</returns>
            public static IEnumerator CallBackLoadScene(string sceneName, bool isAdditive = true, System.Action<bool> onComplete = null)
            {
                LoadSceneMode mode = isAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single;
                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, mode);

                if (loadOperation == null)
                {
                    Debug.LogError($"Scene '{sceneName}' not found. Make sure the scene is added in the Build Settings.");
                    onComplete?.Invoke(false);
                    yield break;
                }

                loadOperation.allowSceneActivation = false; // 防止加载完成后自动切换

                // 等待加载进度达到90%（Unity中异步加载场景时，0.9表示加载操作基本完成，但尚未激活）
                while (loadOperation.progress < 0.9f)
                {
                    // 这里你可以选择性地返回进度，如果需要的话
                    // 例如：onProgress?.Invoke(loadOperation.progress);
                    yield return null;
                }

                // 允许场景激活，Unity会完成剩余的加载工作并切换场景
                loadOperation.allowSceneActivation = true;

                // 等待场景加载和激活真正彻底完成
                yield return loadOperation;

                // 加载完成，调用回调函数，传递成功状态和场景名称
                onComplete?.Invoke(true);
            }
            /// <summary>
            /// 发送 HTTP GET 请求并读取响应文本，完成后通过回调返回结果。
            /// 兼容不同 Unity 版本的错误检测逻辑（条件编译）。
            /// </summary>
            /// <param name="url">请求的完整 URL。</param>
            /// <param name="onComplete">请求完成后的回调，参数为 (成功?, string 内容)。</param>
            /// <returns>用于 <c>StartCoroutine</c> 的 <see cref="IEnumerator"/>。</returns>
            public static IEnumerator CallBackHttpGetRequestText(string url, Action<bool, string> onComplete = null)
            {
                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
                    if (request.result == UnityWebRequest.Result.Success)
#else
        if (!request.isNetworkError && !request.isHttpError)
#endif
                    {
                        onComplete?.Invoke(true, request.downloadHandler.text);
                    }
                    else
                    {
                        Debug.LogError($"Request failed: {request.error}");
                        onComplete?.Invoke(false, null);
                    }
                }
            }
            /// <summary>
            /// 从 Resources 异步加载并实例化一个预制体，完成后通过回调返回实例。
            /// 若加载失败则通过回调返回 null。
            /// </summary>
            /// <param name="path">Resources 下的预制体路径（不带扩展名）。</param>
            /// <param name="onComplete">实例化完成后的回调，参数为生成的 GameObject 或 null。</param>
            /// <returns>用于 <c>StartCoroutine</c> 的 <see cref="IEnumerator"/>。</returns>
            public static IEnumerator CallBackInstantiateResourcePrefab(string path, Action<GameObject> onComplete = null)
            {
                // 先异步加载预制体
                yield return CallBackLoadResource<GameObject>(path, (prefab) =>
                {
                    if (prefab != null)
                    {
                        // 加载成功后，实例化并返回实例
                        GameObject instance = GameObject.Instantiate(prefab);
                        onComplete?.Invoke(instance);
                    }
                    else
                    {
                        onComplete?.Invoke(null);
                    }
                });
            }

            #endregion

        }
    }
}

