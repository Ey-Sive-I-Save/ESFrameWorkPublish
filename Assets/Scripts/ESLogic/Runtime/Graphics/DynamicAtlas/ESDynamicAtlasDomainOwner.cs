using UnityEngine;

namespace ES
{
    /// <summary>
    /// 可选的场景/UI Root 聚合器。多个 Owner 使用同一 Key 时共享 Domain；最后一个 Owner
    /// Disable/Destroy 后关闭 Domain，旧 Entry Lease 会立即失效并由 Graphic 回退到占位内容。
    /// </summary>
    [AddComponentMenu("【ES】/场景与对象/动态图集 Domain Owner")]
    [DisallowMultipleComponent]
    public sealed class ESDynamicAtlasDomainOwner : MonoBehaviour
    {
        [SerializeField] private string domainKey = "ui.runtime";
        [SerializeField, InspectorName("按运行平台使用默认策略"), Tooltip("开启时在实际运行平台选择默认页大小和预算；关闭后使用手动策略。")]
        private bool usePlatformDefaultPolicy = true;
        [SerializeField, InspectorName("手动策略（关闭上项后生效）")]
        [Tooltip("仅在关闭“按运行平台使用默认策略”后使用。")]
        private ESDynamicAtlasDomainPolicy policy = ESDynamicAtlasDomainPolicy.CreatePlatformDefault();

        private ESDynamicAtlasDomainLease domainLease;

        private void OnEnable()
        {
            TryOpen();
        }

        private void Start()
        {
            TryOpen();
        }

        private void Update()
        {
            // GameManager may register after this component's OnEnable/Start
            // (for example when the owner lives in a bootstrap scene). Keep the
            // retry cheap and stop checking permanently once the Domain Lease is
            // acquired.
            if (!domainLease.IsValid)
                TryOpen();
        }

        private void OnDisable()
        {
            domainLease.Dispose();
            domainLease = default;
        }

        private void TryOpen()
        {
            if (!Application.isPlaying || domainLease.IsValid || !ESGameManager.IsReady || string.IsNullOrWhiteSpace(domainKey))
                return;

            domainLease = ESDynamicAtlas.OpenDomain(new ESDynamicAtlasDomainKey(domainKey),
                usePlatformDefaultPolicy ? null : policy);
        }
    }
}
