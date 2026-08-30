using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    /// <summary>
    /// 受控的相机诊断采集入口。调用方显式请求时才读取当前 View，不订阅 Update、
    /// 不写文件、不缓存运行时对象；实际落盘由上层证据通道负责。
    /// </summary>
    public static class ESCameraDiagnosticCollector
    {
        public static bool TryCapture(ESCameraViewId viewId, out ESCameraDiagnosticReceipt receipt)
        {
            receipt = default;
            ESCameraModule camera = ESGameManager.Camera;
            if (camera == null || !camera.TryGetDiagnosticReceipt(viewId, Time.frameCount, out receipt))
                return false;

            receipt.scenePath = SceneManager.GetActiveScene().path ?? string.Empty;
            receipt.platform = Application.platform.ToString();
            receipt.buildId = Application.buildGUID ?? string.Empty;
            return true;
        }
    }
}
