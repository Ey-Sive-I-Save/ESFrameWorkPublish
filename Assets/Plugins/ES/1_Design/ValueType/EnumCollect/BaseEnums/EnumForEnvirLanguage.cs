using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace ES
{
    public static partial class EnumCollect
    {
        /// <summary>
        /// ES 的严格语言身份。NotClear 仅用于查询时指向当前游戏语言，不能作为真实语言写入目录。
        /// </summary>
        public enum Envir_LanguageType : byte
        {
            [InspectorName("使用当前游戏语言")] NotClear = 0,
            [InspectorName("简体中文")] ChineseSimplified = 1,
            [InspectorName("日文")] Japanese = 2,
            [InspectorName("英文")] English = 4,
            [InspectorName("繁体中文")] ChineseTraditional = 8,
            [InspectorName("韩文")] Korean = 9,
            [InspectorName("法文")] French = 10,
            [InspectorName("德文")] German = 11,
            [InspectorName("西班牙文")] Spanish = 12,
            [InspectorName("巴西葡萄牙文")] PortugueseBrazil = 13,
            [InspectorName("俄文")] Russian = 14,
        }
    }

    /// <summary>ES 支持语言的稳定身份、中文显示名与 BCP-47 边界。</summary>
    public static class ESLocaleIdentity
    {
        private static readonly EnumCollect.Envir_LanguageType[] SupportedLanguages =
        {
            EnumCollect.Envir_LanguageType.ChineseSimplified,
            EnumCollect.Envir_LanguageType.ChineseTraditional,
            EnumCollect.Envir_LanguageType.English,
            EnumCollect.Envir_LanguageType.Japanese,
            EnumCollect.Envir_LanguageType.Korean,
            EnumCollect.Envir_LanguageType.French,
            EnumCollect.Envir_LanguageType.German,
            EnumCollect.Envir_LanguageType.Spanish,
            EnumCollect.Envir_LanguageType.PortugueseBrazil,
            EnumCollect.Envir_LanguageType.Russian,
        };

        public static int SupportedLanguageCount => SupportedLanguages.Length;

        public static EnumCollect.Envir_LanguageType GetSupportedLanguageAt(int index)
        {
            if ((uint)index >= (uint)SupportedLanguages.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return SupportedLanguages[index];
        }

        public static string GetCode(EnumCollect.Envir_LanguageType language)
        {
            switch (language)
            {
                case EnumCollect.Envir_LanguageType.ChineseSimplified: return "zh-CN";
                case EnumCollect.Envir_LanguageType.Japanese: return "ja-JP";
                case EnumCollect.Envir_LanguageType.English: return "en-US";
                case EnumCollect.Envir_LanguageType.ChineseTraditional: return "zh-TW";
                case EnumCollect.Envir_LanguageType.Korean: return "ko-KR";
                case EnumCollect.Envir_LanguageType.French: return "fr-FR";
                case EnumCollect.Envir_LanguageType.German: return "de-DE";
                case EnumCollect.Envir_LanguageType.Spanish: return "es-ES";
                case EnumCollect.Envir_LanguageType.PortugueseBrazil: return "pt-BR";
                case EnumCollect.Envir_LanguageType.Russian: return "ru-RU";
                default: return string.Empty;
            }
        }

        public static string GetDisplayName(EnumCollect.Envir_LanguageType language)
        {
            switch (language)
            {
                case EnumCollect.Envir_LanguageType.NotClear: return "使用当前游戏语言";
                case EnumCollect.Envir_LanguageType.ChineseSimplified: return "简体中文";
                case EnumCollect.Envir_LanguageType.ChineseTraditional: return "繁体中文";
                case EnumCollect.Envir_LanguageType.English: return "英文";
                case EnumCollect.Envir_LanguageType.Japanese: return "日文";
                case EnumCollect.Envir_LanguageType.Korean: return "韩文";
                case EnumCollect.Envir_LanguageType.French: return "法文";
                case EnumCollect.Envir_LanguageType.German: return "德文";
                case EnumCollect.Envir_LanguageType.Spanish: return "西班牙文";
                case EnumCollect.Envir_LanguageType.PortugueseBrazil: return "巴西葡萄牙文";
                case EnumCollect.Envir_LanguageType.Russian: return "俄文";
                default: return "无效语言";
            }
        }

        public static bool TryParse(string code, out EnumCollect.Envir_LanguageType language)
        {
            language = EnumCollect.Envir_LanguageType.NotClear;
            if (string.IsNullOrWhiteSpace(code))
                return false;

            string normalized = code.Trim().Replace('_', '-').ToLowerInvariant();
            switch (normalized)
            {
                case "zh-cn":
                case "zh-hans":
                case "zh-sg":
                    language = EnumCollect.Envir_LanguageType.ChineseSimplified;
                    return true;
                case "ja-jp":
                case "ja":
                    language = EnumCollect.Envir_LanguageType.Japanese;
                    return true;
                case "en-us":
                case "en":
                case "en-gb":
                    language = EnumCollect.Envir_LanguageType.English;
                    return true;
                case "zh-tw":
                case "zh-hant":
                case "zh-hk":
                    language = EnumCollect.Envir_LanguageType.ChineseTraditional;
                    return true;
                case "ko-kr":
                case "ko":
                    language = EnumCollect.Envir_LanguageType.Korean;
                    return true;
                case "fr-fr":
                case "fr":
                    language = EnumCollect.Envir_LanguageType.French;
                    return true;
                case "de-de":
                case "de":
                    language = EnumCollect.Envir_LanguageType.German;
                    return true;
                case "es-es":
                case "es":
                    language = EnumCollect.Envir_LanguageType.Spanish;
                    return true;
                case "pt-br":
                case "pt":
                    language = EnumCollect.Envir_LanguageType.PortugueseBrazil;
                    return true;
                case "ru-ru":
                case "ru":
                    language = EnumCollect.Envir_LanguageType.Russian;
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>Stable text identity. Literal UI text must not be used as a runtime key.</summary>
    public readonly struct ESTextKey : IEquatable<ESTextKey>
    {
        public readonly string Value;

        public ESTextKey(string value)
        {
            Value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public bool IsValid => !string.IsNullOrEmpty(Value);
        public static bool IsCanonical(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && string.Equals(value, value.Trim(), StringComparison.Ordinal);
        }

        public bool Equals(ESTextKey other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ESTextKey && Equals((ESTextKey)obj);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;
        public static implicit operator ESTextKey(string value) => new ESTextKey(value);
    }

    [Serializable]
    public struct ESLocalizedTextRef
    {
        public string textKey;
        public EnumCollect.Envir_LanguageType language;
        [TextArea(1, 4)] public string fallbackLiteral;

        public ESLocalizedTextRef(string key,
            EnumCollect.Envir_LanguageType language = EnumCollect.Envir_LanguageType.NotClear,
            string fallbackLiteral = null)
        {
            textKey = key == null ? string.Empty : key.Trim();
            this.language = language;
            this.fallbackLiteral = fallbackLiteral;
        }

        public ESTextKey Key => new ESTextKey(textKey);
    }

    /// <summary>Inspector-friendly named argument for a localized text template.</summary>
    [Serializable]
    public sealed class ESLocalizationArgument
    {
        public string name;
        public string value;
        public ESLocalizationArgumentContent content = ESLocalizationArgumentContent.PlainText;

        public ESLocalizationArgument() { }

        public ESLocalizationArgument(string name, string value,
            ESLocalizationArgumentContent content = ESLocalizationArgumentContent.PlainText)
        {
            this.name = name;
            this.value = value;
            this.content = content;
        }
    }

    public enum ESLocalizationArgumentContent : byte
    {
        [InspectorName("纯文本（安全）")] PlainText = 0,
        [InspectorName("受信富文本")] TrustedRichText = 1,
    }

    public enum ESLocalizationTemplateErrorCode : byte
    {
        None = 0,
        UnexpectedClosingBrace = 1,
        UnterminatedExpression = 2,
        NestedExpression = 3,
        EmptyExpression = 4,
        InvalidArgumentName = 5,
        InvalidSelectorKind = 6,
        InvalidBranch = 7,
        InvalidBranchKey = 8,
        DuplicateBranch = 9,
        MissingOtherBranch = 10,
        MissingArgument = 11,
        InvalidPluralValue = 12,
    }

    public readonly struct ESLocalizationTemplateError
    {
        public readonly ESLocalizationTemplateErrorCode Code;
        public readonly int Position;
        public readonly string ArgumentName;
        public readonly string Message;

        public bool HasError => Code != ESLocalizationTemplateErrorCode.None;

        public ESLocalizationTemplateError(ESLocalizationTemplateErrorCode code,
            int position, string argumentName, string message)
        {
            Code = code;
            Position = position;
            ArgumentName = argumentName ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public override string ToString() => HasError
            ? $"[{Code}] {Message}（位置 {Position}）"
            : string.Empty;
    }

    public enum ESLocalizationResolveStatus : byte
    {
        Resolved,
        ResolvedByFallbackLanguage,
        ResolvedByLiteralFallback,
        MissingKey,
        MissingProvider,
        InvalidKey,
        InvalidArguments,
        InvalidTemplate,
        InvalidLanguage,
    }

    public readonly struct ESLocalizationTextResult
    {
        public readonly string Value;
        public readonly EnumCollect.Envir_LanguageType RequestedLanguage;
        public readonly EnumCollect.Envir_LanguageType ResolvedLanguage;
        public readonly ESLocalizationResolveStatus Status;
        public readonly int Generation;

        public bool IsResolved => Status == ESLocalizationResolveStatus.Resolved
            || Status == ESLocalizationResolveStatus.ResolvedByFallbackLanguage
            || Status == ESLocalizationResolveStatus.ResolvedByLiteralFallback;

        public ESLocalizationTextResult(string value,
            EnumCollect.Envir_LanguageType requestedLanguage,
            EnumCollect.Envir_LanguageType resolvedLanguage,
            ESLocalizationResolveStatus status,
            int generation)
        {
            Value = value ?? string.Empty;
            RequestedLanguage = requestedLanguage;
            ResolvedLanguage = resolvedLanguage;
            Status = status;
            Generation = generation;
        }
    }

    /// <summary>Runtime text provider contract. Implementations belong to the actual consumer assembly.</summary>
    public interface IESLocalizationProvider
    {
        EnumCollect.Envir_LanguageType DefaultLanguage { get; }

        bool TryResolve(ESTextKey key,
            EnumCollect.Envir_LanguageType language,
            out string value);
    }

    /// <summary>Small deterministic provider useful for bootstrap, tests and local runtime injection.</summary>
    public sealed class ESInMemoryLocalizationProvider : IESLocalizationProvider
    {
        private readonly Dictionary<ESTextKey, Dictionary<EnumCollect.Envir_LanguageType, string>> values =
            new Dictionary<ESTextKey, Dictionary<EnumCollect.Envir_LanguageType, string>>();

        public EnumCollect.Envir_LanguageType DefaultLanguage { get; }

        public ESInMemoryLocalizationProvider(
            EnumCollect.Envir_LanguageType defaultLanguage = EnumCollect.Envir_LanguageType.ChineseSimplified)
        {
            if (!ESLocalizationRuntime.IsConcreteLanguage(defaultLanguage))
                throw new ArgumentOutOfRangeException(nameof(defaultLanguage), defaultLanguage,
                    "Provider 默认语言必须是已声明的具体语言。");
            DefaultLanguage = defaultLanguage;
        }

        public bool Set(ESTextKey key, EnumCollect.Envir_LanguageType language, string value)
        {
            if (!key.IsValid || !ESLocalizationRuntime.IsConcreteLanguage(language) || value == null)
                return false;
            if (!values.TryGetValue(key, out Dictionary<EnumCollect.Envir_LanguageType, string> byLanguage))
            {
                byLanguage = new Dictionary<EnumCollect.Envir_LanguageType, string>();
                values.Add(key, byLanguage);
            }
            byLanguage[language] = value;
            return true;
        }

        public bool TryResolve(ESTextKey key,
            EnumCollect.Envir_LanguageType language,
            out string value)
        {
            value = null;
            return values.TryGetValue(key, out Dictionary<EnumCollect.Envir_LanguageType, string> byLanguage)
                && byLanguage.TryGetValue(language, out value)
                && value != null;
        }
    }

    public static class ESLocalizationRuntime
    {
        private static EnumCollect.Envir_LanguageType currentLanguage =
            EnumCollect.Envir_LanguageType.ChineseSimplified;
        private static int generation = 1;
        private static IESLocalizationProvider provider;

        public static EnumCollect.Envir_LanguageType CurrentLanguage => currentLanguage;
        public static int Generation => generation;
        public static IESLocalizationProvider Provider => provider;

        public static event Action<EnumCollect.Envir_LanguageType,
            EnumCollect.Envir_LanguageType, int> CurrentLanguageChanged;

        /// <summary>Raised after the active provider changes so already-enabled views can refresh.</summary>
        public static event Action<int> ProviderChanged;

        public static bool RegisterProvider(IESLocalizationProvider value)
        {
            if (value == null || !IsConcreteLanguage(value.DefaultLanguage))
                return false;
            if (provider != null && !ReferenceEquals(provider, value))
                return false;
            if (ReferenceEquals(provider, value))
                return true;
            provider = value;
            AdvanceGeneration();
            RaiseProviderChanged();
            return true;
        }

        public static bool UnregisterProvider(IESLocalizationProvider value)
        {
            if (value == null || !ReferenceEquals(provider, value))
                return false;
            provider = null;
            AdvanceGeneration();
            RaiseProviderChanged();
            return true;
        }

        public static ESLocalizationTextResult ResolveText(ESLocalizedTextRef reference)
        {
            if (reference.language != EnumCollect.Envir_LanguageType.NotClear
                && !IsConcreteLanguage(reference.language))
            {
                return new ESLocalizationTextResult(reference.fallbackLiteral,
                    reference.language, EnumCollect.Envir_LanguageType.NotClear,
                    ESLocalizationResolveStatus.InvalidLanguage, generation);
            }

            ESTextKey key = reference.Key;
            EnumCollect.Envir_LanguageType requested = Resolve(reference.language);
            if (!key.IsValid)
                return new ESLocalizationTextResult(reference.fallbackLiteral,
                    requested, EnumCollect.Envir_LanguageType.NotClear,
                    string.IsNullOrEmpty(reference.fallbackLiteral)
                        ? ESLocalizationResolveStatus.InvalidKey
                        : ESLocalizationResolveStatus.ResolvedByLiteralFallback,
                    generation);

            if (provider != null && TryResolveProvider(key, requested, out string value,
                out EnumCollect.Envir_LanguageType resolvedLanguage))
            {
                return new ESLocalizationTextResult(value, requested, resolvedLanguage,
                    resolvedLanguage == requested
                        ? ESLocalizationResolveStatus.Resolved
                        : ESLocalizationResolveStatus.ResolvedByFallbackLanguage,
                    generation);
            }

            if (!string.IsNullOrEmpty(reference.fallbackLiteral))
                return new ESLocalizationTextResult(reference.fallbackLiteral, requested,
                    EnumCollect.Envir_LanguageType.NotClear,
                    ESLocalizationResolveStatus.ResolvedByLiteralFallback, generation);
            return new ESLocalizationTextResult(string.Empty, requested,
                EnumCollect.Envir_LanguageType.NotClear,
                provider == null ? ESLocalizationResolveStatus.MissingProvider : ESLocalizationResolveStatus.MissingKey,
                generation);
        }

        /// <summary>
        /// Resolves a TextKey and applies named or numeric placeholders explicitly supplied by the caller.
        /// Formatting is a separate contract so ordinary text lookup remains allocation-free for callers
        /// that do not need parameters.
        /// </summary>
        public static ESLocalizationTextResult ResolveFormattedText(
            ESLocalizedTextRef reference,
            IReadOnlyDictionary<string, string> arguments)
        {
            ESLocalizationTextResult resolved = ResolveText(reference);
            if (!resolved.IsResolved)
                return resolved;
            if (!TryFormatDetailed(resolved.Value, arguments, resolved.ResolvedLanguage,
                null, out string formatted, out ESLocalizationTemplateError templateError))
            {
                return new ESLocalizationTextResult(resolved.Value, resolved.RequestedLanguage,
                    resolved.ResolvedLanguage,
                    IsArgumentFailure(templateError.Code)
                        ? ESLocalizationResolveStatus.InvalidArguments
                        : ESLocalizationResolveStatus.InvalidTemplate,
                    resolved.Generation);
            }
            return new ESLocalizationTextResult(formatted, resolved.RequestedLanguage,
                resolved.ResolvedLanguage, resolved.Status, resolved.Generation);
        }

        /// <summary>
        /// Formats a serialized argument list. Duplicate or malformed names are rejected
        /// before lookup so authoring mistakes cannot silently choose one value.
        /// </summary>
        public static ESLocalizationTextResult ResolveFormattedTextArguments(
            ESLocalizedTextRef reference,
            IReadOnlyList<ESLocalizationArgument> arguments)
        {
            Dictionary<string, string> values = null;
            HashSet<string> trustedRichTextArguments = null;
            if (arguments != null && arguments.Count > 0)
            {
                values = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int index = 0; index < arguments.Count; index++)
                {
                    ESLocalizationArgument argument = arguments[index];
                    string name = argument?.name?.Trim();
                    if (argument == null || string.IsNullOrWhiteSpace(name)
                        || !IsValidArgumentName(name))
                    {
                        ESLocalizationTextResult invalid = ResolveText(reference);
                        return new ESLocalizationTextResult(invalid.Value, invalid.RequestedLanguage,
                            invalid.ResolvedLanguage, ESLocalizationResolveStatus.InvalidArguments, invalid.Generation);
                    }
                    if (!values.TryAdd(name, argument.value ?? string.Empty))
                    {
                        ESLocalizationTextResult invalid = ResolveText(reference);
                        return new ESLocalizationTextResult(invalid.Value, invalid.RequestedLanguage,
                            invalid.ResolvedLanguage, ESLocalizationResolveStatus.InvalidArguments, invalid.Generation);
                    }
                    if (argument.content == ESLocalizationArgumentContent.TrustedRichText)
                    {
                        if (trustedRichTextArguments == null)
                            trustedRichTextArguments = new HashSet<string>(StringComparer.Ordinal);
                        trustedRichTextArguments.Add(name);
                    }
                }
            }
            ESLocalizationTextResult resolved = ResolveText(reference);
            if (!resolved.IsResolved)
                return resolved;
            if (!TryFormatDetailed(resolved.Value, values, resolved.ResolvedLanguage,
                trustedRichTextArguments, out string formatted,
                out ESLocalizationTemplateError templateError))
            {
                return new ESLocalizationTextResult(resolved.Value, resolved.RequestedLanguage,
                    resolved.ResolvedLanguage,
                    IsArgumentFailure(templateError.Code)
                        ? ESLocalizationResolveStatus.InvalidArguments
                        : ESLocalizationResolveStatus.InvalidTemplate,
                    resolved.Generation);
            }
            return new ESLocalizationTextResult(formatted, resolved.RequestedLanguage,
                resolved.ResolvedLanguage, resolved.Status, resolved.Generation);
        }

        public static bool TryFormat(
            string template,
            IReadOnlyDictionary<string, string> arguments,
            out string formatted,
            out string error)
        {
            bool success = TryFormatDetailed(template, arguments,
                EnumCollect.Envir_LanguageType.ChineseSimplified, null,
                out formatted, out ESLocalizationTemplateError templateError);
            error = success ? null : templateError.ToString();
            return success;
        }

        public static bool TryAnalyzeTemplate(string template,
            out IReadOnlyList<string> argumentContracts,
            out ESLocalizationTemplateError error)
        {
            var contracts = new HashSet<string>(StringComparer.Ordinal);
            bool success = TryProcessTemplate(template, null,
                EnumCollect.Envir_LanguageType.ChineseSimplified, null,
                false, contracts, out _, out error);
            var ordered = new List<string>(contracts);
            ordered.Sort(StringComparer.Ordinal);
            argumentContracts = ordered;
            return success;
        }

        public static bool TryFormatDetailed(string template,
            IReadOnlyDictionary<string, string> arguments,
            EnumCollect.Envir_LanguageType language,
            IReadOnlyCollection<string> trustedRichTextArguments,
            out string formatted,
            out ESLocalizationTemplateError error)
        {
            return TryProcessTemplate(template, arguments, Resolve(language),
                trustedRichTextArguments, true, null, out formatted, out error);
        }

        public static bool IsValidArgumentName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            bool numeric = true;
            for (int index = 0; index < name.Length; index++)
            {
                char character = name[index];
                if (character < '0' || character > '9') { numeric = false; break; }
            }
            if (numeric) return true;
            char first = name[0];
            if (!IsAsciiLetter(first) && first != '_') return false;
            for (int index = 1; index < name.Length; index++)
            {
                char character = name[index];
                if (!IsAsciiLetter(character) && (character < '0' || character > '9') && character != '_')
                    return false;
            }
            return true;
        }

        private static bool TryProcessTemplate(string template,
            IReadOnlyDictionary<string, string> arguments,
            EnumCollect.Envir_LanguageType language,
            IReadOnlyCollection<string> trustedRichTextArguments,
            bool format,
            HashSet<string> contracts,
            out string output,
            out ESLocalizationTemplateError error)
        {
            template = template ?? string.Empty;
            var builder = format ? new StringBuilder(template.Length + 16) : null;
            error = default;
            for (int index = 0; index < template.Length; index++)
            {
                char character = template[index];
                if (character == '{')
                {
                    if (index + 1 < template.Length && template[index + 1] == '{')
                    {
                        if (format) builder.Append('{');
                        index++;
                        continue;
                    }
                    int closing = template.IndexOf('}', index + 1);
                    if (closing < 0)
                        return Fail(ESLocalizationTemplateErrorCode.UnterminatedExpression,
                            index, string.Empty, "模板表达式缺少右花括号。", builder, out output, out error);
                    int nested = template.IndexOf('{', index + 1, closing - index - 1);
                    if (nested >= 0)
                        return Fail(ESLocalizationTemplateErrorCode.NestedExpression,
                            nested, string.Empty, "表达式分支不允许嵌套花括号；复数数量请使用 #。", builder, out output, out error);
                    string expression = template.Substring(index + 1, closing - index - 1);
                    if (!TryProcessExpression(expression, index, arguments, language,
                        trustedRichTextArguments, format, contracts, builder, out error))
                    {
                        output = template;
                        return false;
                    }
                    index = closing;
                    continue;
                }
                if (character == '}')
                {
                    if (index + 1 < template.Length && template[index + 1] == '}')
                    {
                        if (format) builder.Append('}');
                        index++;
                        continue;
                    }
                    return Fail(ESLocalizationTemplateErrorCode.UnexpectedClosingBrace,
                        index, string.Empty, "模板包含未转义的右花括号；字面量请写成 }}。", builder, out output, out error);
                }
                if (format) builder.Append(character);
            }
            output = format ? builder.ToString() : template;
            return true;
        }

        private static bool TryProcessExpression(string expression,
            int position,
            IReadOnlyDictionary<string, string> arguments,
            EnumCollect.Envir_LanguageType language,
            IReadOnlyCollection<string> trustedRichTextArguments,
            bool format,
            HashSet<string> contracts,
            StringBuilder builder,
            out ESLocalizationTemplateError error)
        {
            error = default;
            if (string.IsNullOrEmpty(expression))
                return SetError(ESLocalizationTemplateErrorCode.EmptyExpression, position,
                    string.Empty, "模板表达式不能为空。", out error);

            int firstSeparator = expression.IndexOf('|');
            if (firstSeparator < 0)
            {
                string argumentName = expression.Trim();
                if (!IsValidArgumentName(argumentName))
                    return SetError(ESLocalizationTemplateErrorCode.InvalidArgumentName, position,
                        argumentName, "参数名必须是 ASCII 标识符或纯数字索引。", out error);
                contracts?.Add(argumentName + ":value");
                if (!format) return true;
                if (arguments == null || !arguments.TryGetValue(argumentName, out string value))
                    return SetError(ESLocalizationTemplateErrorCode.MissingArgument, position,
                        argumentName, "缺少本地化参数：" + argumentName, out error);
                AppendArgument(builder, value, IsTrusted(argumentName, trustedRichTextArguments));
                return true;
            }

            int secondSeparator = expression.IndexOf('|', firstSeparator + 1);
            if (secondSeparator < 0 || expression.IndexOf('|', secondSeparator + 1) >= 0)
                return SetError(ESLocalizationTemplateErrorCode.InvalidSelectorKind, position,
                    string.Empty, "选择表达式必须是 {参数|plural/select|分支}。", out error);
            string name = expression.Substring(0, firstSeparator).Trim();
            string kind = expression.Substring(firstSeparator + 1, secondSeparator - firstSeparator - 1).Trim();
            string branchText = expression.Substring(secondSeparator + 1);
            if (!IsValidArgumentName(name))
                return SetError(ESLocalizationTemplateErrorCode.InvalidArgumentName, position,
                    name, "参数名必须是 ASCII 标识符或纯数字索引。", out error);
            bool plural = string.Equals(kind, "plural", StringComparison.Ordinal);
            bool select = string.Equals(kind, "select", StringComparison.Ordinal);
            if (!plural && !select)
                return SetError(ESLocalizationTemplateErrorCode.InvalidSelectorKind, position,
                    name, "仅支持 plural 和 select 两种选择语义。", out error);

            var branches = new Dictionary<string, string>(StringComparer.Ordinal);
            string[] segments = branchText.Split(';');
            for (int index = 0; index < segments.Length; index++)
            {
                string segment = segments[index];
                int equals = segment.IndexOf('=');
                if (equals <= 0)
                    return SetError(ESLocalizationTemplateErrorCode.InvalidBranch, position,
                        name, "每个分支必须使用 key=value，分支之间用分号分隔。", out error);
                string branchKey = segment.Substring(0, equals).Trim();
                string branchValue = segment.Substring(equals + 1);
                if (plural ? !IsPluralBranchKey(branchKey) : !IsSelectBranchKey(branchKey))
                    return SetError(ESLocalizationTemplateErrorCode.InvalidBranchKey, position,
                        name, "无效分支键：" + branchKey, out error);
                if (!branches.TryAdd(branchKey, branchValue))
                    return SetError(ESLocalizationTemplateErrorCode.DuplicateBranch, position,
                        name, "重复分支：" + branchKey, out error);
            }
            if (!branches.ContainsKey("other"))
                return SetError(ESLocalizationTemplateErrorCode.MissingOtherBranch, position,
                    name, "plural/select 表达式必须包含 other 分支。", out error);
            contracts?.Add(name + ":" + kind);
            if (!format) return true;
            if (arguments == null || !arguments.TryGetValue(name, out string argumentValue))
                return SetError(ESLocalizationTemplateErrorCode.MissingArgument, position,
                    name, "缺少本地化参数：" + name, out error);

            string selectedKey;
            if (plural)
            {
                if (!decimal.TryParse(argumentValue, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out decimal number))
                {
                    return SetError(ESLocalizationTemplateErrorCode.InvalidPluralValue, position,
                        name, "复数参数必须是使用英文小数点的有限数字：" + name, out error);
                }
                if (number == 0m && branches.ContainsKey("zero"))
                {
                    selectedKey = "zero";
                }
                else
                {
                    string category = SelectPluralCategory(language, number);
                    selectedKey = branches.ContainsKey(category) ? category : "other";
                }
            }
            else
            {
                selectedKey = branches.ContainsKey(argumentValue ?? string.Empty)
                    ? argumentValue ?? string.Empty
                    : "other";
            }

            string selectedValue = branches[selectedKey];
            if (plural)
                builder.Append(selectedValue.Replace("#", argumentValue));
            else
                builder.Append(selectedValue);
            return true;
        }

        private static string SelectPluralCategory(
            EnumCollect.Envir_LanguageType language,
            decimal number)
        {
            decimal absolute = Math.Abs(number);
            bool integer = decimal.Truncate(absolute) == absolute && GetDecimalScale(absolute) == 0;
            decimal integerPart = decimal.Truncate(absolute);
            switch (language)
            {
                case EnumCollect.Envir_LanguageType.English:
                case EnumCollect.Envir_LanguageType.German:
                case EnumCollect.Envir_LanguageType.Spanish:
                    return integer && integerPart == 1m ? "one" : "other";
                case EnumCollect.Envir_LanguageType.French:
                case EnumCollect.Envir_LanguageType.PortugueseBrazil:
                    return integerPart == 0m || integerPart == 1m ? "one" : "other";
                case EnumCollect.Envir_LanguageType.Russian:
                    if (!integer) return "other";
                    int modulo10 = (int)(integerPart % 10m);
                    int modulo100 = (int)(integerPart % 100m);
                    if (modulo10 == 1 && modulo100 != 11) return "one";
                    if (modulo10 >= 2 && modulo10 <= 4
                        && (modulo100 < 12 || modulo100 > 14)) return "few";
                    if (modulo10 == 0 || modulo10 >= 5
                        || modulo100 >= 11 && modulo100 <= 14) return "many";
                    return "other";
                default:
                    return "other";
            }
        }

        private static int GetDecimalScale(decimal value)
        {
            return (decimal.GetBits(value)[3] >> 16) & 0x7F;
        }

        private static bool IsPluralBranchKey(string value)
        {
            return value == "zero" || value == "one" || value == "two"
                || value == "few" || value == "many" || value == "other";
        }

        private static bool IsSelectBranchKey(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!IsAsciiLetter(character) && (character < '0' || character > '9')
                    && character != '_' && character != '-' && character != '.')
                    return false;
            }
            return true;
        }

        private static bool IsAsciiLetter(char value)
        {
            return value >= 'A' && value <= 'Z' || value >= 'a' && value <= 'z';
        }

        private static bool IsTrusted(string name, IReadOnlyCollection<string> trustedArguments)
        {
            if (trustedArguments == null) return false;
            foreach (string trusted in trustedArguments)
                if (string.Equals(name, trusted, StringComparison.Ordinal)) return true;
            return false;
        }

        private static void AppendArgument(StringBuilder builder, string value, bool trustedRichText)
        {
            value = value ?? string.Empty;
            if (trustedRichText)
            {
                builder.Append(value);
                return;
            }
            for (int index = 0; index < value.Length; index++)
            {
                switch (value[index])
                {
                    case '&': builder.Append("&amp;"); break;
                    case '<': builder.Append("&lt;"); break;
                    case '>': builder.Append("&gt;"); break;
                    default: builder.Append(value[index]); break;
                }
            }
        }

        private static bool IsArgumentFailure(ESLocalizationTemplateErrorCode code)
        {
            return code == ESLocalizationTemplateErrorCode.MissingArgument
                || code == ESLocalizationTemplateErrorCode.InvalidPluralValue;
        }

        private static bool SetError(ESLocalizationTemplateErrorCode code, int position,
            string argumentName, string message, out ESLocalizationTemplateError error)
        {
            error = new ESLocalizationTemplateError(code, position, argumentName, message);
            return false;
        }

        private static bool Fail(ESLocalizationTemplateErrorCode code, int position,
            string argumentName, string message, StringBuilder builder,
            out string output, out ESLocalizationTemplateError error)
        {
            output = builder == null ? string.Empty : builder.ToString();
            error = new ESLocalizationTemplateError(code, position, argumentName, message);
            return false;
        }

        private static bool TryResolveProvider(
            ESTextKey key,
            EnumCollect.Envir_LanguageType requested,
            out string value,
            out EnumCollect.Envir_LanguageType resolvedLanguage)
        {
            value = null;
            resolvedLanguage = EnumCollect.Envir_LanguageType.NotClear;
            if (provider.TryResolve(key, requested, out value))
            {
                resolvedLanguage = requested;
                return true;
            }
            EnumCollect.Envir_LanguageType providerDefault = provider.DefaultLanguage;
            bool attemptedProviderDefault = providerDefault == requested;
            for (int index = 0; TryGetFallbackLanguage(requested, index, out EnumCollect.Envir_LanguageType fallback); index++)
            {
                attemptedProviderDefault |= fallback == providerDefault;
                if (provider.TryResolve(key, fallback, out value))
                {
                    resolvedLanguage = fallback;
                    return true;
                }
            }
            if (!attemptedProviderDefault && IsConcreteLanguage(providerDefault)
                && provider.TryResolve(key, providerDefault, out value))
            {
                resolvedLanguage = providerDefault;
                return true;
            }
            return false;
        }

        /// <summary>Returns the deterministic locale fallback chain without allocating.</summary>
        public static bool TryGetFallbackLanguage(
            EnumCollect.Envir_LanguageType requested,
            int index,
            out EnumCollect.Envir_LanguageType fallback)
        {
            fallback = EnumCollect.Envir_LanguageType.NotClear;
            if (index < 0)
                return false;
            switch (requested)
            {
                case EnumCollect.Envir_LanguageType.English:
                    if (index == 0) fallback = EnumCollect.Envir_LanguageType.ChineseSimplified;
                    else return false;
                    break;
                case EnumCollect.Envir_LanguageType.ChineseTraditional:
                    if (index == 0) fallback = EnumCollect.Envir_LanguageType.ChineseSimplified;
                    else if (index == 1) fallback = EnumCollect.Envir_LanguageType.English;
                    else return false;
                    break;
                case EnumCollect.Envir_LanguageType.Japanese:
                    if (index == 0) fallback = EnumCollect.Envir_LanguageType.English;
                    else if (index == 1) fallback = EnumCollect.Envir_LanguageType.ChineseSimplified;
                    else return false;
                    break;
                case EnumCollect.Envir_LanguageType.ChineseSimplified:
                    if (index == 0) fallback = EnumCollect.Envir_LanguageType.English;
                    else return false;
                    break;
                case EnumCollect.Envir_LanguageType.Korean:
                    if (index == 0) fallback = EnumCollect.Envir_LanguageType.English;
                    else if (index == 1) fallback = EnumCollect.Envir_LanguageType.ChineseSimplified;
                    else return false;
                    break;
                case EnumCollect.Envir_LanguageType.French:
                case EnumCollect.Envir_LanguageType.German:
                case EnumCollect.Envir_LanguageType.Spanish:
                case EnumCollect.Envir_LanguageType.PortugueseBrazil:
                case EnumCollect.Envir_LanguageType.Russian:
                    if (index == 0) fallback = EnumCollect.Envir_LanguageType.English;
                    else if (index == 1) fallback = EnumCollect.Envir_LanguageType.ChineseSimplified;
                    else return false;
                    break;
                default:
                    return false;
            }
            return fallback != requested;
        }

        public static bool IsConcreteLanguage(EnumCollect.Envir_LanguageType language)
        {
            return language != EnumCollect.Envir_LanguageType.NotClear
                && Enum.IsDefined(typeof(EnumCollect.Envir_LanguageType), language);
        }

        public static EnumCollect.Envir_LanguageType Resolve(
            EnumCollect.Envir_LanguageType language)
        {
            return language == EnumCollect.Envir_LanguageType.NotClear
                ? currentLanguage
                : language;
        }

        public static bool TrySetCurrentLanguage(EnumCollect.Envir_LanguageType language)
        {
            if (!IsConcreteLanguage(language))
                return false;
            if (language == currentLanguage)
                return true;

            EnumCollect.Envir_LanguageType previous = currentLanguage;
            currentLanguage = language;
            AdvanceGeneration();
            Action<EnumCollect.Envir_LanguageType,
                EnumCollect.Envir_LanguageType, int> handlers = CurrentLanguageChanged;
            if (handlers != null)
            {
                Delegate[] invocationList = handlers.GetInvocationList();
                for (int i = 0; i < invocationList.Length; i++)
                {
                    try
                    {
                        ((Action<EnumCollect.Envir_LanguageType,
                            EnumCollect.Envir_LanguageType, int>)invocationList[i])(
                            previous, language, generation);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }
            return true;
        }

        public static void SetCurrentLanguageOrThrow(EnumCollect.Envir_LanguageType language)
        {
            if (!TrySetCurrentLanguage(language))
                throw new ArgumentOutOfRangeException(nameof(language), language,
                    "当前游戏语言必须是已声明的具体语言，不能是 NotClear。");
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

        private static void RaiseProviderChanged()
        {
            Delegate[] invocationList = ProviderChanged?.GetInvocationList();
            if (invocationList == null) return;
            for (int index = 0; index < invocationList.Length; index++)
            {
                try { ((Action<int>)invocationList[index])(generation); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }
    }

    public static class EnvirLanguageClear
    {
        public static void ToClear(this ref EnumCollect.Envir_LanguageType envir_)
        {
            envir_ = ESLocalizationRuntime.Resolve(envir_);
        }

        public static void ToClear(this ref EnumCollect.Envir_LanguageType envir_, EnumCollect.Envir_LanguageType defaultValue)
        {
            if (envir_ != EnumCollect.Envir_LanguageType.NotClear)
                return;
            if (defaultValue == EnumCollect.Envir_LanguageType.NotClear)
            {
                envir_ = ESLocalizationRuntime.CurrentLanguage;
                return;
            }
            if (!ESLocalizationRuntime.IsConcreteLanguage(defaultValue))
                throw new ArgumentOutOfRangeException(nameof(defaultValue), defaultValue,
                    "默认语言必须是已声明的具体语言或 NotClear。");
            envir_ = defaultValue;
        }
    }
}
