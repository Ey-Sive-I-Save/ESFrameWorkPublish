using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ES
{
    public static partial class ESDesignUtility
    {
        //
        public class Couroutine
        {
            #region 运行

            #endregion
            #region 制造Couroutine
            public static IEnumerator RepeatConroutine(Action action, float startDelay = 1, float internal_ = 1, int times = -1, CancellationTokenSource source = default)
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
            public static IEnumerator DelayCoroutine(Action action, float delay = 1, CancellationTokenSource source = default)
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
            public static IEnumerator DelayOneFrameCoroutine(Action action)
            {
                yield return null;
                action?.Invoke();
            }
            public static IEnumerator RunningConroutine(Action start, Action running, Action end, float time, CancellationTokenSource source = default)
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
            public static IEnumerator WaitForRealSecondsCoroutine(float time)
            {
                float start = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup < start + time)
                {
                    yield return null;
                }
            }

            #endregion
        }
    }
}

