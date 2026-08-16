using System;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// One reusable window authoring definition. It contains stable aliases and presentation
    /// policy only; it never stores a live instance, lease, RuntimeKey, or provider handle.
    /// </summary>
    [CreateAssetMenu(menuName = "【ES】/配置/UI/窗口 Definition", fileName = "ESUIWindowDefinition")]
    public sealed class ESUIWindowDefinition : ScriptableObject
    {
        [Header("稳定身份")]
        [SerializeField] private ESUIWindowId builtInId;
        [SerializeField] private string stringKey;

        [Header("窗口行为")]
        [SerializeField] private ESUIWindowLayer layer = ESUIWindowLayer.Page;
        [SerializeField] private ESUIWindowClosePolicy closePolicy = ESUIWindowClosePolicy.DestroyOnClose;
        [SerializeField] private bool allowMultipleInstances;

        [Header("资源")]
        [SerializeField] private ESAssetReferPrefab prefab = new ESAssetReferPrefab();

        [Header("流程影响")]
        [SerializeField] private bool acquireRuntimeMode;
        [SerializeField] private ESRuntimeMode runtimeMode = ESRuntimeMode.Gameplay;

        public ESUIWindowId BuiltInId => builtInId;
        public string StringKey => stringKey;
        public ESUIWindowLayer Layer => layer;
        public ESUIWindowClosePolicy ClosePolicy => closePolicy;
        public bool AllowMultipleInstances => allowMultipleInstances;
        public ESAssetReferPrefab Prefab => prefab;
        public bool AcquireRuntimeMode => acquireRuntimeMode;
        public ESRuntimeMode RuntimeMode => runtimeMode;

        public ESUIWindowIdentity Identity => new ESUIWindowIdentity(builtInId, stringKey);

        public bool TryValidate(out string error)
        {
            if (!IsValidWindowKey(stringKey))
            {
                error = "窗口 StringKey 必须是稳定的小写 ui:* 标识，例如 ui:inventory。";
                return false;
            }

            if (prefab == null || !prefab.IsValid)
            {
                error = "窗口 Definition 缺少有效的 Prefab 资源引用。";
                return false;
            }

            if (!Enum.IsDefined(typeof(ESUIWindowLayer), layer))
            {
                error = "窗口 Layer 无效。";
                return false;
            }

            if (!Enum.IsDefined(typeof(ESUIWindowClosePolicy), closePolicy))
            {
                error = "窗口关闭策略无效。";
                return false;
            }

            if (allowMultipleInstances && closePolicy == ESUIWindowClosePolicy.KeepInactive)
            {
                error = "允许多实例的窗口不能使用 KeepInactive。请改用 DestroyOnClose 或 PoolOnClose。";
                return false;
            }

            error = null;
            return true;
        }

        internal static bool IsValidWindowKey(string key)
        {
            if (string.IsNullOrEmpty(key) || !string.Equals(key, key.Trim(), StringComparison.Ordinal))
                return false;

            if (!key.StartsWith("ui:", StringComparison.Ordinal) || key.Length <= 3)
                return false;

            for (int i = 0; i < key.Length; i++)
            {
                char character = key[i];
                if (char.IsWhiteSpace(character) || char.IsUpper(character))
                    return false;
            }

            return true;
        }
    }
}
