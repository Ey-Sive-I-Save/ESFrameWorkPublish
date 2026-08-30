#if UNITY_EDITOR
using System;
using Cinemachine;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 不依赖 ESGameManager 的独立 Camera View。仅编辑器预览会创建它；它复用与正式
    /// SceneBinding 相同的 Director + CM2 Adapter 契约，却拥有自己的 ViewId、Epoch、
    /// Brain、Rig Registry 与全部 Lease，绝不触碰 PlayMode MainView。
    /// </summary>
    public sealed class ESCameraPreviewView : IDisposable
    {
        private readonly ESCameraDirector director = new ESCameraDirector();
        private readonly ESCameraViewId viewId;
        private readonly int sceneEpoch;
        private readonly CinemachineBrain brain;
        private ESCameraCinemachine2ViewAdapter adapter;
        private bool disposed;

        public ESCameraPreviewView(
            ESCameraViewId viewId,
            int sceneEpoch,
            Camera outputCamera,
            CinemachineBrain brain,
            ESCameraViewDefinitionCatalog definitionCatalog,
            ESCameraRigCatalog rigCatalog,
            Transform rigRoot,
            ESCameraGlobalPolicy globalPolicy = null)
        {
            this.viewId = viewId;
            this.sceneEpoch = sceneEpoch;
            this.brain = brain;
            if (!viewId.IsValid || sceneEpoch <= 0 || outputCamera == null || brain == null)
                return;
            if (globalPolicy != null && !globalPolicy.TryValidate(out _))
                return;

            ESCameraCinemachine2ViewAdapter candidate = null;
            bool registered = false;
            try
            {
                candidate = new ESCameraCinemachine2ViewAdapter(
                    outputCamera,
                    brain,
                    definitionCatalog,
                    rigCatalog,
                    rigRoot,
                    globalPolicy);
                if (candidate.IsReady && director.RegisterView(viewId, sceneEpoch, candidate))
                {
                    registered = true;
                    adapter = candidate;
                    candidate = null;
                    return;
                }
            }
            catch
            {
                if (registered)
                {
                    try { director.UnregisterView(viewId, sceneEpoch, candidate); }
                    catch (Exception cleanupException) { Debug.LogException(cleanupException); }
                }

                try { candidate?.Dispose(); }
                catch (Exception cleanupException) { Debug.LogException(cleanupException); }
                throw;
            }

            try { candidate?.Dispose(); }
            catch (Exception cleanupException) { Debug.LogException(cleanupException); }
            finally { adapter = null; }
        }

        public ESCameraViewId ViewId => viewId;
        public bool IsReady => !disposed && adapter != null && adapter.IsReady;

        public ESCameraLease Push(in ESCameraRequest request)
        {
            return IsReady ? director.Push(request) : ESCameraLease.Invalid;
        }

        public bool Update(ESCameraLease lease, in ESCameraRequest request)
        {
            return IsReady && director.Update(lease, request);
        }

        public bool Release(ESCameraLease lease)
        {
            return IsReady && director.Release(lease);
        }

        /// <summary>
        /// 编辑器采样的唯一提交点。这里允许 FlushNow，是因为它运行在离线时间采样中，
        /// 不属于游戏帧内的普通业务 Push/Release 路径。
        /// </summary>
        public void Sample()
        {
            if (!IsReady)
                return;

            director.FlushNow(viewId);
            brain.ManualUpdate();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            ESCameraCinemachine2ViewAdapter currentAdapter = adapter;
            adapter = null;
            if (currentAdapter != null)
            {
                try
                {
                    director.UnregisterView(viewId, sceneEpoch, currentAdapter);
                }
                catch (Exception exception)
                {
                    Debug.LogException(new InvalidOperationException(
                        "Camera Preview View 注销失败，继续执行资源释放。", exception));
                }

                try
                {
                    currentAdapter.Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogException(new InvalidOperationException(
                        "Camera Preview View Adapter 释放失败。", exception));
                }
            }

            try
            {
                director.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    "Camera Preview View Director 释放失败。", exception));
            }
        }
    }
}
#endif
