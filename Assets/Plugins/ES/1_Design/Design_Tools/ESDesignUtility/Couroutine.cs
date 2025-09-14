using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace ES
{
    public static partial class ESDesignUtility
    {
        //
        public static class Couroutine
        {

            #region Action协程包裹
            /// <summary>
            /// 协程重复执行
            /// </summary>
            /// <param name="action">执行</param>
            /// <param name="startDelay">开始延迟</param>
            /// <param name="internal_">间隔</param>
            /// <param name="times">次数</param>
            /// <param name="source">取消令牌</param>
            /// <returns></returns>
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
            /// 协程延迟执行
            /// </summary>
            /// <param name="action">执行</param>
            /// <param name="delay">延迟</param>
            /// <param name="source">取消令牌</param>
            /// <returns></returns>
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
            /// 协程延迟一帧执行
            /// </summary>
            /// <param name="action">执行</param>
            /// <returns></returns>
            public static IEnumerator ActionDelayOneFrame(Action action)
            {
                yield return null;
                action?.Invoke();
            }
            /// <summary>
            /// 协程持续执行
            /// </summary>
            /// <param name="start">开始执行</param>
            /// <param name="running">持续执行</param>
            /// <param name="end">结束执行</param>
            /// <param name="time">总时间</param>
            /// <param name="source">取消令牌</param>
            /// <returns></returns>
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
            /// 延迟真实时间执行<While原理>(TimeScale!=0)
            /// </summary>
            /// <param name="time">等待真实时间</param>
            /// <param name="action">执行内容</param>
            /// <param name="source">取消源</param>
            /// <returns></returns>
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
            /// 延迟真实时间执行<通用>
            /// </summary>
            /// <param name="time">等待真实时间</param>
            /// <param name="action">执行内容</param>
            /// <param name="source">取消源</param>
            /// <returns></returns>
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
            /// 等待Fixed更新执行
            /// </summary>
            /// <param name="action">执行内容</param>
            /// <returns></returns>
            public static IEnumerator ActionWaitForFixedUpdate(Action action)
            {
                yield return new WaitForFixedUpdate();
                action?.Invoke();
            }
            /// <summary>
            /// 等待帧末执行
            /// </summary>
            /// <param name="action">执行内容</param>
            /// <returns></returns>
            public static IEnumerator ActionWaitForWaitForEndOfFrame(Action action)
            {
                yield return new WaitForEndOfFrame();
                action?.Invoke();
            }
            /// <summary>
            /// 重复持续执行直到--Func<bool>
            /// </summary>
            /// <param name="action">持续执行</param>
            /// <param name="stopCondition">直到-true停止</param>
            /// <param name="interval">间隔</param>
            /// <returns></returns>
            public static IEnumerator ActionRepeatExecuteUntil(Action action, Func<bool> stopCondition, float interval = 1f)
            {
                while (!stopCondition())
                {
                    action?.Invoke();
                    yield return new WaitForSeconds(interval);
                }
            }
            #endregion

            #region 特殊回调
            /// <summary>
            /// 依赖URL下载图片
            /// </summary>
            /// <param name="url">URL链接</param>
            /// <param name="timeout">超时</param>
            /// <param name="callback">回调<成功，资源></param>
            /// <returns></returns>
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
            /// 依赖URl下载AssetBundle
            /// </summary>
            /// <param name="url">URL</param>
            /// <param name="timeout">超时</param>
            /// <param name="callback">AssetBundle回调<成功？,AB包></param>
            /// <returns></returns>
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
            /// 加载Resource资源
            /// </summary>
            /// <typeparam name="T">资源类型</typeparam>
            /// <param name="path">路径(Resource的)</param>
            /// <param name="callback">资源回调(可能为NULL)</param>
            /// <returns></returns>
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
            /// 加载场景
            /// </summary>
            /// <param name="sceneName">场景名字</param>
            /// <param name="isAdditive">是叠加的？</param>
            /// <param name="onComplete">结束时<成功？></param>
            /// <returns></returns>
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
            /// 从URL获得网络申请文本
            /// </summary>
            /// <param name="url">URL</param>
            /// <param name="onComplete">结束回调<成功?，下载文本></param>
            /// <returns></returns>
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
            /// 实例化Resource预制件
            /// </summary>
            /// <param name="path">路径(Resource的)</param>
            /// <param name="onComplete">生成完成回调(可能为NULL)</param>
            /// <returns></returns>
            public static IEnumerator CallBackInstantiateResourcePrefab(string path, Action<GameObject> onComplete = null)
            {
                // 先异步加载预制体
                yield return CallBackLoadResource<GameObject>(path, (prefab) => {
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

