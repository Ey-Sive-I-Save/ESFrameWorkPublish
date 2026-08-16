using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
namespace ES
{
    [System.Serializable]
    public sealed class ESLocalizationCatalogEntry
    {
        public string textKey;
        public EnumCollect.Envir_LanguageType language = EnumCollect.Envir_LanguageType.ChineseSimplified;
        [TextArea(1, 6)] public string value;
    }

    /// <summary>Runtime-owned localization table. Editor builds it; GameManager registers it explicitly.</summary>
    [CreateAssetMenu(menuName = "【ES】/资源与发布/多语言/本地化目录", fileName = "ESLocalizationCatalog")]
    public sealed class ESLocalizationCatalog : ScriptableObject, IESLocalizationProvider
    {
        public const int CurrentFormatVersion = 2;
        public string catalogId = "default";
        public int formatVersion = CurrentFormatVersion;
        public EnumCollect.Envir_LanguageType defaultLanguage = EnumCollect.Envir_LanguageType.ChineseSimplified;
        [Tooltip("Editor 生成来源的稳定标识；运行时不读取源文件。")]
        public string sourceId;
        [Tooltip("Editor 生成来源的 SHA-256；用于发布前确认目录没有脱离源表。")]
        public string sourceHash;
        public List<ESLocalizationCatalogEntry> entries = new List<ESLocalizationCatalogEntry>();

        [System.NonSerialized] private Dictionary<LocalizationLookupKey, string> index;

        private readonly struct LocalizationLookupKey : System.IEquatable<LocalizationLookupKey>
        {
            private readonly ESTextKey textKey;
            private readonly EnumCollect.Envir_LanguageType language;

            public LocalizationLookupKey(ESTextKey textKey,
                EnumCollect.Envir_LanguageType language)
            {
                this.textKey = textKey;
                this.language = language;
            }

            public bool Equals(LocalizationLookupKey other)
                => textKey.Equals(other.textKey) && language == other.language;

            public override bool Equals(object obj)
                => obj is LocalizationLookupKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (textKey.GetHashCode() * 397) ^ (int)language;
                }
            }
        }

        public EnumCollect.Envir_LanguageType DefaultLanguage => defaultLanguage;

        public bool TryResolve(ESTextKey key, EnumCollect.Envir_LanguageType language, out string value)
        {
            EnsureIndex();
            return index.TryGetValue(new LocalizationLookupKey(key, language), out value)
                && value != null;
        }

        public IReadOnlyList<string> Validate()
        {
            index = null;
            var errors = new List<string>();
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            var keys = new HashSet<ESTextKey>();
            var defaultKeys = new HashSet<ESTextKey>();
            var templateContracts = new Dictionary<ESTextKey, HashSet<string>>();
            if (string.IsNullOrWhiteSpace(catalogId)) errors.Add("本地化目录缺少稳定 catalogId。");
            if (!string.IsNullOrEmpty(catalogId)
                && !string.Equals(catalogId, catalogId.Trim(), System.StringComparison.Ordinal))
                errors.Add("本地化目录 catalogId 不能包含首尾空白。");
            if (!string.IsNullOrEmpty(catalogId) && catalogId.IndexOfAny(new[] { '/', '\\' }) >= 0) errors.Add("本地化目录 catalogId 不能包含路径分隔符。");
            if (formatVersion != CurrentFormatVersion) errors.Add("本地化目录 formatVersion 不受支持：" + formatVersion + "，当前要求 " + CurrentFormatVersion + "。");
            if (!string.IsNullOrEmpty(sourceHash) && (sourceHash.Length != 64 || !IsSha256(sourceHash))) errors.Add("本地化目录 sourceHash 不是有效 SHA-256。");
            if (!ESLocalizationRuntime.IsConcreteLanguage(defaultLanguage)) errors.Add("本地化目录默认语言无效：" + defaultLanguage);
            if (entries == null || entries.Count == 0) errors.Add("本地化目录不能为空。");
            foreach (ESLocalizationCatalogEntry entry in entries ?? new List<ESLocalizationCatalogEntry>())
            {
                if (entry == null) { errors.Add("本地化目录包含空条目。"); continue; }
                ESTextKey key = new ESTextKey(entry.textKey);
                if (!key.IsValid) { errors.Add("本地化条目缺少 textKey。"); continue; }
                if (!ESTextKey.IsCanonical(entry.textKey))
                    errors.Add("本地化条目 textKey 不能包含首尾空白：" + entry.textKey);
                if (!ESLocalizationRuntime.IsConcreteLanguage(entry.language)) errors.Add("本地化条目语言无效：" + entry.language);
                if (string.IsNullOrWhiteSpace(entry.value)) errors.Add("本地化条目文本为空：" + key);
                if (!seen.Add(key + "|" + entry.language)) errors.Add("本地化条目重复：" + key + "/" + entry.language);
                keys.Add(key);
                if (entry.language == defaultLanguage) defaultKeys.Add(key);
                if (!ESLocalizationRuntime.TryAnalyzeTemplate(entry.value,
                    out IReadOnlyList<string> analyzedContracts,
                    out ESLocalizationTemplateError templateError))
                {
                    errors.Add("本地化模板无效：" + key + "/" + entry.language + " " + templateError);
                    continue;
                }
                var contracts = new HashSet<string>(analyzedContracts, System.StringComparer.Ordinal);
                if (!templateContracts.TryGetValue(key, out HashSet<string> expected))
                    templateContracts.Add(key, contracts);
                else if (!expected.SetEquals(contracts))
                    errors.Add("本地化参数合同不一致：" + key + "/" + entry.language);
            }
            foreach (ESTextKey key in keys)
                if (!defaultKeys.Contains(key)) errors.Add("本地化条目缺少默认语言：" + key + "/" + defaultLanguage);
            if (errors.Count == 0)
                EnsureIndex();
            return errors;
        }

        public void InvalidateIndex() => index = null;

        private void OnEnable() => index = null;

        private static bool IsSha256(string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool hex = character >= '0' && character <= '9'
                    || character >= 'a' && character <= 'f'
                    || character >= 'A' && character <= 'F';
                if (!hex) return false;
            }
            return true;
        }

        private void EnsureIndex()
        {
            if (index != null) return;
            index = new Dictionary<LocalizationLookupKey, string>(entries?.Count ?? 0);
            foreach (ESLocalizationCatalogEntry entry in entries ?? new List<ESLocalizationCatalogEntry>())
            {
                if (entry == null) continue;
                ESTextKey key = new ESTextKey(entry.textKey);
                if (!key.IsValid || !ESLocalizationRuntime.IsConcreteLanguage(entry.language) || entry.value == null) continue;
                var lookupKey = new LocalizationLookupKey(key, entry.language);
                if (!index.ContainsKey(lookupKey)) index.Add(lookupKey, entry.value);
            }
        }
    }

    /// <summary>Reusable TMP binding. It reacts only to explicit language changes; it never scans the scene.</summary>
    [DisallowMultipleComponent]
    public sealed class ESLocalizedTMPText : MonoBehaviour
    {
        [SerializeField] private TMP_Text target;
        [SerializeField] private ESLocalizedTextRef textReference;
        [SerializeField] private List<ESLocalizationArgument> formatArguments = new List<ESLocalizationArgument>();
        [SerializeField] private ESRuntimeFontRole fontRole = ESRuntimeFontRole.Body;
        [System.NonSerialized] private bool runtimeEventsSubscribed;

        public ESLocalizedTextRef TextReference
        {
            get => textReference;
            set { textReference = value; Refresh(); }
        }

        public ESRuntimeFontRole FontRole
        {
            get => fontRole;
            set { fontRole = value; Refresh(); }
        }

        public List<ESLocalizationArgument> FormatArguments => formatArguments;
        public ESLocalizationTextResult LastTextResult { get; private set; }
        public bool LastFontResolved { get; private set; }

        private void Awake()
        {
            if (target == null) target = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || runtimeEventsSubscribed)
                return;
            ESLocalizationRuntime.CurrentLanguageChanged += HandleLanguageChanged;
            ESLocalizationRuntime.ProviderChanged += HandleProviderChanged;
            ESFontRuntime.CatalogChanged += HandleFontCatalogChanged;
            runtimeEventsSubscribed = true;
            Refresh();
        }

        private void OnDisable()
        {
            if (!runtimeEventsSubscribed)
                return;
            ESLocalizationRuntime.CurrentLanguageChanged -= HandleLanguageChanged;
            ESLocalizationRuntime.ProviderChanged -= HandleProviderChanged;
            ESFontRuntime.CatalogChanged -= HandleFontCatalogChanged;
            runtimeEventsSubscribed = false;
        }

        private void OnValidate()
        {
            if (target == null) target = GetComponent<TMP_Text>();
            if (Application.isPlaying && isActiveAndEnabled) Refresh();
        }

        public void Refresh()
        {
            if (target == null) return;
            ESLocalizationTextResult result = ESLocalizationRuntime.ResolveFormattedTextArguments(textReference, formatArguments);
            LastTextResult = result;
            target.text = result.Value;
            LastFontResolved = ESFontRuntime.Apply(target, fontRole, result.RequestedLanguage);
        }

        private void HandleLanguageChanged(
            EnumCollect.Envir_LanguageType previous,
            EnumCollect.Envir_LanguageType current,
            int currentGeneration)
        {
            Refresh();
        }

        private void HandleProviderChanged(int currentGeneration) => Refresh();

        private void HandleFontCatalogChanged(int currentGeneration) => Refresh();
    }

    public enum ESRuntimeFontRole : byte
    {
        [InspectorName("正文")] Body,
        [InspectorName("标题")] Title,
        [InspectorName("数字")] Number,
        [InspectorName("图标")] Icon,
        [InspectorName("自定义")] Custom,
    }

    [System.Serializable]
    public sealed class ESRuntimeFontBinding
    {
        public EnumCollect.Envir_LanguageType language = EnumCollect.Envir_LanguageType.ChineseSimplified;
        public ESRuntimeFontRole role = ESRuntimeFontRole.Body;
        public TMP_FontAsset font;
    }

    /// <summary>
    /// Runtime-owned font directory. The Editor font builder writes this asset; runtime UI
    /// resolves it by the same stable language identity used by localization.
    /// </summary>
    [CreateAssetMenu(menuName = "【ES】/资源与发布/字体/运行时字体目录", fileName = "ESRuntimeFontCatalog")]
    public sealed class ESRuntimeFontCatalog : ScriptableObject
    {
        public const int CurrentFormatVersion = 1;
        public string catalogId = "default";
        public int formatVersion = CurrentFormatVersion;
        public List<ESRuntimeFontBinding> bindings = new List<ESRuntimeFontBinding>();

        [System.NonSerialized]
        private Dictionary<int, TMP_FontAsset> index;

        public bool TryResolve(
            EnumCollect.Envir_LanguageType language,
            ESRuntimeFontRole role,
            out TMP_FontAsset font)
        {
            EnsureIndex();
            if (index.TryGetValue(MakeKey(language, role), out font) && font != null)
                return true;
            for (int i = 0; ESLocalizationRuntime.TryGetFallbackLanguage(language, i, out EnumCollect.Envir_LanguageType fallback); i++)
            {
                if (index.TryGetValue(MakeKey(fallback, role), out font) && font != null)
                    return true;
            }
            font = null;
            return false;
        }

        public IReadOnlyList<string> Validate()
        {
            index = null;
            var errors = new List<string>();
            var seen = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(catalogId)) errors.Add("字体目录缺少稳定 catalogId。");
            if (!string.IsNullOrEmpty(catalogId)
                && !string.Equals(catalogId, catalogId.Trim(), System.StringComparison.Ordinal))
                errors.Add("字体目录 catalogId 不能包含首尾空白。");
            if (!string.IsNullOrEmpty(catalogId) && catalogId.IndexOfAny(new[] { '/', '\\' }) >= 0)
                errors.Add("字体目录 catalogId 不能包含路径分隔符。");
            if (formatVersion != CurrentFormatVersion)
                errors.Add("字体目录 formatVersion 不受支持：" + formatVersion + "，当前要求 " + CurrentFormatVersion + "。");
            if (bindings == null || bindings.Count == 0) errors.Add("字体目录不能为空。");
            foreach (ESRuntimeFontBinding binding in bindings ?? new List<ESRuntimeFontBinding>())
            {
                if (binding == null)
                {
                    errors.Add("字体目录包含空绑定。");
                    continue;
                }
                if (!ESLocalizationRuntime.IsConcreteLanguage(binding.language))
                {
                    errors.Add("字体绑定使用了无效语言：" + binding.language);
                    continue;
                }
                if (!System.Enum.IsDefined(typeof(ESRuntimeFontRole), binding.role))
                {
                    errors.Add("字体绑定使用了无效角色：" + (int)binding.role);
                    continue;
                }
                int key = MakeKey(binding.language, binding.role);
                if (!seen.Add(key))
                    errors.Add("字体目录存在重复绑定：" + binding.language + "/" + binding.role);
                if (binding.font == null)
                    errors.Add("字体绑定缺少 TMP_FontAsset：" + binding.language + "/" + binding.role);
            }
            if (errors.Count == 0)
                EnsureIndex();
            return errors;
        }

        public void InvalidateIndex() => index = null;

        private void EnsureIndex()
        {
            if (index != null)
                return;
            index = new Dictionary<int, TMP_FontAsset>();
            foreach (ESRuntimeFontBinding binding in bindings ?? new List<ESRuntimeFontBinding>())
            {
                if (binding == null || binding.font == null || !ESLocalizationRuntime.IsConcreteLanguage(binding.language))
                    continue;
                int key = MakeKey(binding.language, binding.role);
                if (!index.ContainsKey(key))
                    index.Add(key, binding.font);
            }
        }

        private void OnEnable() => index = null;
        private void OnValidate() => index = null;

        private static int MakeKey(EnumCollect.Envir_LanguageType language, ESRuntimeFontRole role)
            => ((int)language << 8) | (int)role;
    }

    public static class ESFontRuntime
    {
        private static ESRuntimeFontCatalog catalog;
        private static int generation = 1;

        public static event System.Action<int> CatalogChanged;

        public static ESRuntimeFontCatalog Catalog => catalog;
        public static int Generation => generation;

        public static bool RegisterCatalog(ESRuntimeFontCatalog value)
        {
            if (value == null)
                return false;
            if (ReferenceEquals(catalog, value))
                return true;
            if (value.Validate().Count > 0)
                return false;
            if (catalog != null && !ReferenceEquals(catalog, value))
                return false;
            catalog = value;
            AdvanceGeneration();
            RaiseCatalogChanged();
            return true;
        }

        public static bool UnregisterCatalog(ESRuntimeFontCatalog value)
        {
            if (value == null || !ReferenceEquals(catalog, value))
                return false;
            catalog = null;
            AdvanceGeneration();
            RaiseCatalogChanged();
            return true;
        }

        private static void AdvanceGeneration()
        {
            unchecked
            {
                generation++;
                if (generation == 0)
                    generation++;
            }
        }

        private static void RaiseCatalogChanged()
        {
            System.Delegate[] invocationList = CatalogChanged?.GetInvocationList();
            if (invocationList == null) return;
            for (int index = 0; index < invocationList.Length; index++)
            {
                try { ((System.Action<int>)invocationList[index])(generation); }
                catch (System.Exception exception) { Debug.LogException(exception); }
            }
        }

        public static bool TryResolve(
            ESRuntimeFontRole role,
            out TMP_FontAsset font,
            EnumCollect.Envir_LanguageType language = EnumCollect.Envir_LanguageType.NotClear)
        {
            font = null;
            if (catalog == null)
                return false;
            EnumCollect.Envir_LanguageType requested = ESLocalizationRuntime.Resolve(language);
            return catalog.TryResolve(requested, role, out font);
        }

        public static bool Apply(
            TMP_Text text,
            ESRuntimeFontRole role,
            EnumCollect.Envir_LanguageType language = EnumCollect.Envir_LanguageType.NotClear)
        {
            if (text == null || !TryResolve(role, out TMP_FontAsset font, language))
                return false;
            text.font = font;
            return true;
        }
    }

    public partial class ESGameManager
    {
        public static EnumCollect.Envir_LanguageType Envir_Language
        {
            get => ESLocalizationRuntime.CurrentLanguage;
            set => ESLocalizationRuntime.SetCurrentLanguageOrThrow(value);
        }
        #region 全局事件-GameCenterAwakeBefore
        protected override void OnBeforeAwakeRegister()
        {
            base.OnBeforeAwakeRegister();
            GlobalLinkPool.SendLink(new Link_GameCenterAwakeBefoe());
        }
        #endregion

        #region 游戏退出时机
        private void OnApplicationQuit()
        {
            ESSystem.IsQuitting = true;
        }

        #endregion
    }

    public struct Link_GameCenterAwakeBefoe
    {

    }

}
