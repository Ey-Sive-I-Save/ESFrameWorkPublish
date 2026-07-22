using UnityEngine;
using ES;
using System.Threading;
using System;

namespace ES.Samples{
    /// <summary>
    /// Coroutine API 婕旂ず - 鍗忕▼宸ュ叿
    /// 鎻愪緵Action寤惰繜鎵ц銆侀噸澶嶆墽琛屻€佺綉缁滀笅杞界瓑鍗忕▼灏佽
    /// </summary>
    public class Example_Coroutine : MonoBehaviour
    {
        private CancellationTokenSource cancellationToken;

        private void Start()
        {
            Debug.Log("=== Coroutine API 婕旂ず ===");

            // 1. 寤惰繜鎵цAction锛堜娇鐢ㄧ紦瀛樼殑WaitForSeconds锛?
            StartCoroutine(ESDesignUtility.Coroutine.Delay1sCached(() =>
            {
                Debug.Log("1绉掑悗鎵ц锛堜娇鐢ㄧ紦瀛橈級");
            }));

            // 2. 鑷畾涔夊欢杩熸墽琛?
            StartCoroutine(ESDesignUtility.Coroutine.ActionDelay(() =>
            {
                Debug.Log("2.5绉掑悗鎵ц");
            }, delay: 2.5f));

            // 3. 涓嬩竴甯ф墽琛?
            StartCoroutine(ESDesignUtility.Coroutine.ActionDelayOneFrame(() =>
            {
                Debug.Log("涓嬩竴甯ф墽琛?);
            }));

            // 4. 閲嶅鎵ц锛堟瘡闅?绉掓墽琛屼竴娆★紝鍏?娆★級
            StartCoroutine(ESDesignUtility.Coroutine.ActionRepeat(
                action: () => Debug.Log($"閲嶅鎵ц: {Time.time}"),
                startDelay: 0.5f,
                internal_: 1f,
                times: 3
            ));

            // 5. 鍙彇娑堢殑閲嶅鎵ц
            cancellationToken = new CancellationTokenSource();
            StartCoroutine(ESDesignUtility.Coroutine.ActionRepeat(
                action: () => Debug.Log("鍙彇娑堢殑閲嶅鎵ц"),
                startDelay: 0f,
                internal_: 0.5f,
                times: -1, // 鏃犻檺寰幆
                source: cancellationToken
            ));

            // 6绉掑悗鍙栨秷
            StartCoroutine(ESDesignUtility.Coroutine.ActionDelay(() =>
            {
                cancellationToken?.Cancel();
                Debug.Log("宸插彇娑堥噸澶嶆墽琛?);
            }, delay: 6f));

            // 6. 绛夊緟鏉′欢婊¤冻锛堜娇鐢ˋctionRepeat閰嶅悎鏉′欢鍒ゆ柇瀹炵幇锛?
            float startTime = Time.time;
            CancellationTokenSource conditionToken = new CancellationTokenSource();
            StartCoroutine(ESDesignUtility.Coroutine.ActionRepeat(
                action: () => {
                    Debug.Log($"鎸佺画鎵ц涓?.. 宸茶繃 {Time.time - startTime:F1}绉?);
                    if (Time.time - startTime > 3f) conditionToken?.Cancel();
                },
                startDelay: 0f,
                internal_: 0.5f,
                times: -1,
                source: conditionToken
            ));

            // 7. FixedUpdate 寤惰繜
            StartCoroutine(ESDesignUtility.Coroutine.DelayNextFixedCached(() =>
            {
                Debug.Log("鍦ㄤ笅涓€娆ixedUpdate鎵ц");
            }));

            // 8. 甯ф湯灏炬墽琛?
            StartCoroutine(ESDesignUtility.Coroutine.DelayEndOfFrameCached(() =>
            {
                Debug.Log("鍦ㄥ綋鍓嶅抚鏈熬鎵ц");
            }));
        }

        private void OnDestroy()
        {
            // 娓呯悊鍙栨秷浠ょ墝
            cancellationToken?.Dispose();
        }
    }
}

