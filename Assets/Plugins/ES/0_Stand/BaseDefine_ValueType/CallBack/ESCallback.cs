using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 异步任务回调类：用于处理异步任务的进度、成功、失败等回调
    /// 适用于下载、加载等需要进度反馈的异步操作
    /// </summary>
    /// <typeparam name="T">回调结果的类型</typeparam>
    public class ESCallback<T> : IPoolableAuto
    {
        /// <summary>
        /// 进度回调：参数为进度值(0-1)和进度描述
        /// </summary>
        public Action<float, string> OnProgress;
        public static ESSimplePool<ESCallback<T>> Pool = new ESSimplePool<ESCallback<T>>(() => new ESCallback<T>(), null, 20, 100);
        /// <summary>
        /// 成功回调：参数为结果数据
        /// </summary>
        public Action<T> OnSuccess;

        /// <summary>
        /// 失败回调：参数为错误信息
        /// </summary>
        public Action<string> OnError;

        /// <summary>
        /// 完成回调：无论成功或失败都会调用
        /// </summary>
        public Action OnComplete;

        /// <summary>
        /// 当前进度值(0-1)
        /// </summary>
        public float Progress { get; private set; }

        /// <summary>
        /// 是否已完成
        /// </summary>
        public bool IsDone { get; private set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; private set; }

        /// <summary>
        /// 结果数据
        /// </summary>
        public T Result { get; private set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// 是否已被回收到对象池
        /// </summary>
        public bool IsRecycled { get; set; }

        public ESCallback()
        {
            Progress = 0f;
            IsDone = false;
            IsSuccess = false;
        }

        /// <summary>
        /// 更新进度
        /// </summary>
        /// <param name="progress">进度值(0-1)</param>
        /// <param name="message">进度描述信息</param>
        public void UpdateProgress(float progress, string message = "")
        {
            Progress = Mathf.Clamp01(progress);
            OnProgress?.Invoke(Progress, message);
        }

        /// <summary>
        /// 标记为成功并返回结果
        /// </summary>
        /// <param name="result">结果数据</param>
        public void Success(T result)
        {
            if (IsDone) return;

            IsDone = true;
            IsSuccess = true;
            Result = result;
            Progress = 1f;

            OnSuccess?.Invoke(result);
            OnComplete?.Invoke();
        }

        /// <summary>
        /// 标记为失败并返回错误信息
        /// </summary>
        /// <param name="errorMessage">错误信息</param>
        public void Error(string errorMessage)
        {
            if (IsDone) return;

            IsDone = true;
            IsSuccess = false;
            ErrorMessage = errorMessage;

            OnError?.Invoke(errorMessage);
            OnComplete?.Invoke();
        }

        /// <summary>
        /// 重置回调状态
        /// </summary>
        public void Reset()
        {
            Progress = 0f;
            IsDone = false;
            IsSuccess = false;
            Result = default;
            ErrorMessage = null;
        }

        /// <summary>
        /// 清空所有回调委托
        /// </summary>
        public void ClearCallbacks()
        {
            OnProgress = null;
            OnSuccess = null;
            OnError = null;
            OnComplete = null;
        }

        public void TryAutoPushedToPool()
        {
            ESCallback<T>.Pool.PushToPool(this);
        }

        public void OnResetAsPoolable()
        {
            Reset();
            ClearCallbacks();
        }
    }

    /// <summary>
    /// 无返回值的异步任务回调类
    /// </summary>
    public class ESCallback : ESCallback<object>
    {
        /// <summary>
        /// 标记为成功（无返回值）
        /// </summary>
        public void Success()
        {
            Success(null);
        }
    }

    /// <summary>
    /// 下载任务专用回调类：带下载速度和剩余时间计算
    /// </summary>
    /// <typeparam name="T">下载结果类型</typeparam>
    public class ESDownloadCallback<T> : ESCallback<T>
    {
        /// <summary>
        /// 总大小（字节）
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// 已下载大小（字节）
        /// </summary>
        public long DownloadedSize { get; private set; }

        /// <summary>
        /// 下载速度（字节/秒）
        /// </summary>
        public float DownloadSpeed { get; private set; }

        /// <summary>
        /// 预计剩余时间（秒）
        /// </summary>
        public float EstimatedTimeRemaining { get; private set; }

        private float lastUpdateTime;
        private long lastDownloadedSize;

        public ESDownloadCallback()
        {
            lastUpdateTime = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// 更新下载进度（自动计算速度和剩余时间）
        /// </summary>
        /// <param name="downloadedSize">已下载大小（字节）</param>
        /// <param name="message">进度描述</param>
        public void UpdateDownloadProgress(long downloadedSize, string message = "")
        {
            DownloadedSize = downloadedSize;

            // 计算下载速度
            float currentTime = Time.realtimeSinceStartup;
            float deltaTime = currentTime - lastUpdateTime;
            if (deltaTime > 0.1f) // 每0.1秒更新一次速度计算
            {
                long deltaSize = downloadedSize - lastDownloadedSize;
                DownloadSpeed = deltaSize / deltaTime;
                
                // 计算剩余时间
                if (DownloadSpeed > 0 && TotalSize > 0)
                {
                    long remainingSize = TotalSize - downloadedSize;
                    EstimatedTimeRemaining = remainingSize / DownloadSpeed;
                }

                lastUpdateTime = currentTime;
                lastDownloadedSize = downloadedSize;
            }

            // 计算进度
            float progress = TotalSize > 0 ? (float)downloadedSize / TotalSize : 0f;
            UpdateProgress(progress, message);
        }

        /// <summary>
        /// 获取格式化的下载信息
        /// </summary>
        public string GetFormattedDownloadInfo()
        {
            string downloadedStr = FormatBytes(DownloadedSize);
            string totalStr = FormatBytes(TotalSize);
            string speedStr = FormatBytes((long)DownloadSpeed) + "/s";
            int remainingSeconds = Mathf.CeilToInt(EstimatedTimeRemaining);

            return $"{downloadedStr}/{totalStr} - {speedStr} - 剩余 {remainingSeconds}秒";
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024f).ToString("F2") + " KB";
            if (bytes < 1024 * 1024 * 1024) return (bytes / 1024f / 1024f).ToString("F2") + " MB";
            return (bytes / 1024f / 1024f / 1024f).ToString("F2") + " GB";
        }
    }
}
