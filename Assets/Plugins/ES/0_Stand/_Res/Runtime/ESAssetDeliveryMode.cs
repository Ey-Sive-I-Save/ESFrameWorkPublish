using UnityEngine;

namespace ES
{
    /// <summary>
    /// Library 的交付方式。它描述资源从哪里获得，不描述资源加载时机。
    /// </summary>
    public enum ESAssetDeliveryMode
    {
        /// <summary>随应用首包提供，不要求网络。</summary>
        [InspectorName("随包")]
        BuiltIn = 0,

        /// <summary>首包提供，同时允许远端版本更新。</summary>
        [InspectorName("更新")]
        Updateable = 1,

        /// <summary>首包不提供，必须从远端获取。</summary>
        [InspectorName("远端")]
        Remote = 2
    }
}
