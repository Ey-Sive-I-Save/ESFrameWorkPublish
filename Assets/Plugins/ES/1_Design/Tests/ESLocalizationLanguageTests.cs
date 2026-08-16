using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ES.Tests
{
    [Parallelizable(ParallelScope.None)]
    public sealed class ESLocalizationLanguageTests
    {
        private EnumCollect.Envir_LanguageType originalLanguage;
        private IESLocalizationProvider originalProvider;
        private ESRuntimeFontCatalog originalFontCatalog;

        [SetUp]
        public void SaveCurrentLanguage()
        {
            originalLanguage = ESLocalizationRuntime.CurrentLanguage;
            originalProvider = ESLocalizationRuntime.Provider;
            originalFontCatalog = ESFontRuntime.Catalog;
        }

        [TearDown]
        public void RestoreCurrentLanguage()
        {
            ESLocalizationRuntime.SetCurrentLanguageOrThrow(originalLanguage);
            if (ESLocalizationRuntime.Provider != null)
                ESLocalizationRuntime.UnregisterProvider(ESLocalizationRuntime.Provider);
            if (originalProvider != null)
                ESLocalizationRuntime.RegisterProvider(originalProvider);
            if (ESFontRuntime.Catalog != null)
                ESFontRuntime.UnregisterCatalog(ESFontRuntime.Catalog);
            if (originalFontCatalog != null)
                ESFontRuntime.RegisterCatalog(originalFontCatalog);
        }

        [Test]
        public void ConcreteLanguageValues_PreserveExistingNumericIdentities()
        {
            Assert.That((byte)EnumCollect.Envir_LanguageType.NotClear, Is.Zero);
            Assert.That((byte)EnumCollect.Envir_LanguageType.ChineseSimplified, Is.EqualTo(1));
            Assert.That((byte)EnumCollect.Envir_LanguageType.Japanese, Is.EqualTo(2));
            Assert.That((byte)EnumCollect.Envir_LanguageType.English, Is.EqualTo(4));
            Assert.That((byte)EnumCollect.Envir_LanguageType.ChineseTraditional, Is.EqualTo(8));
        }

        [Test]
        public void LocaleIdentity_ExposesExactlyTenStableConcreteLanguages()
        {
            Assert.That(ESLocaleIdentity.SupportedLanguageCount, Is.EqualTo(10));
            var languages = new HashSet<EnumCollect.Envir_LanguageType>();
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < ESLocaleIdentity.SupportedLanguageCount; index++)
            {
                EnumCollect.Envir_LanguageType language = ESLocaleIdentity.GetSupportedLanguageAt(index);
                string code = ESLocaleIdentity.GetCode(language);
                Assert.That(ESLocalizationRuntime.IsConcreteLanguage(language), Is.True);
                Assert.That(languages.Add(language), Is.True);
                Assert.That(codes.Add(code), Is.True);
                Assert.That(ESLocaleIdentity.GetDisplayName(language), Is.Not.Empty);
                Assert.That(ESLocaleIdentity.TryParse(code, out EnumCollect.Envir_LanguageType parsed), Is.True);
                Assert.That(parsed, Is.EqualTo(language));
            }
        }

        [Test]
        public void NotClear_AlwaysResolvesToCurrentGameLanguage()
        {
            ESLocalizationRuntime.SetCurrentLanguageOrThrow(
                EnumCollect.Envir_LanguageType.Japanese);

            Assert.That(ESLocalizationRuntime.Resolve(
                    EnumCollect.Envir_LanguageType.NotClear),
                Is.EqualTo(EnumCollect.Envir_LanguageType.Japanese));

            EnumCollect.Envir_LanguageType value = EnumCollect.Envir_LanguageType.NotClear;
            value.ToClear();
            Assert.That(value, Is.EqualTo(EnumCollect.Envir_LanguageType.Japanese));
        }

        [Test]
        public void ChangingCurrentLanguage_AdvancesGenerationAndPublishesExactTransition()
        {
            ESLocalizationRuntime.SetCurrentLanguageOrThrow(
                EnumCollect.Envir_LanguageType.ChineseSimplified);
            int generation = ESLocalizationRuntime.Generation;
            EnumCollect.Envir_LanguageType observedPrevious = default;
            EnumCollect.Envir_LanguageType observedCurrent = default;
            int observedGeneration = 0;
            Action<EnumCollect.Envir_LanguageType,
                EnumCollect.Envir_LanguageType, int> handler = (previous, current, currentGeneration) =>
            {
                observedPrevious = previous;
                observedCurrent = current;
                observedGeneration = currentGeneration;
            };
            ESLocalizationRuntime.CurrentLanguageChanged += handler;
            try
            {
                Assert.That(ESLocalizationRuntime.TrySetCurrentLanguage(
                    EnumCollect.Envir_LanguageType.English), Is.True);
                Assert.That(ESLocalizationRuntime.Generation, Is.GreaterThan(generation));
                Assert.That(observedPrevious,
                    Is.EqualTo(EnumCollect.Envir_LanguageType.ChineseSimplified));
                Assert.That(observedCurrent,
                    Is.EqualTo(EnumCollect.Envir_LanguageType.English));
                Assert.That(observedGeneration, Is.EqualTo(ESLocalizationRuntime.Generation));
            }
            finally
            {
                ESLocalizationRuntime.CurrentLanguageChanged -= handler;
            }
        }

        [Test]
        public void NotClearAndUnknownValues_CannotBecomeConcreteCurrentLanguage()
        {
            var unknown = (EnumCollect.Envir_LanguageType)255;

            Assert.That(ESLocalizationRuntime.TrySetCurrentLanguage(
                EnumCollect.Envir_LanguageType.NotClear), Is.False);
            Assert.That(ESLocalizationRuntime.TrySetCurrentLanguage(unknown), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ESLocalizationRuntime.SetCurrentLanguageOrThrow(unknown));

            EnumCollect.Envir_LanguageType value = EnumCollect.Envir_LanguageType.NotClear;
            Assert.Throws<ArgumentOutOfRangeException>(() => value.ToClear(unknown));
        }

        [Test]
        public void ResolveText_InvalidSerializedLanguageIsNotHiddenByLiteralFallback()
        {
            var reference = new ESLocalizedTextRef("ui.invalid-language",
                EnumCollect.Envir_LanguageType.ChineseSimplified, "安全回退");
            reference.language = (EnumCollect.Envir_LanguageType)255;

            ESLocalizationTextResult result = ESLocalizationRuntime.ResolveText(reference);

            Assert.That(result.Value, Is.EqualTo("安全回退"));
            Assert.That(result.Status, Is.EqualTo(ESLocalizationResolveStatus.InvalidLanguage));
            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.RequestedLanguage, Is.EqualTo((EnumCollect.Envir_LanguageType)255));
        }

        [Test]
        public void LocaleIdentity_ParsesCanonicalAndLegacyAliases()
        {
            Assert.That(ESLocaleIdentity.GetCode(EnumCollect.Envir_LanguageType.ChineseSimplified), Is.EqualTo("zh-CN"));
            Assert.That(ESLocaleIdentity.TryParse("zh-Hans", out var simplified), Is.True);
            Assert.That(simplified, Is.EqualTo(EnumCollect.Envir_LanguageType.ChineseSimplified));
            Assert.That(ESLocaleIdentity.TryParse("en", out var english), Is.True);
            Assert.That(english, Is.EqualTo(EnumCollect.Envir_LanguageType.English));
            Assert.That(ESLocaleIdentity.TryParse("not-a-locale", out _), Is.False);
        }

        [Test]
        public void Providers_DefaultToSimplifiedChineseAndRejectAmbiguousDefault()
        {
            var inMemory = new ESInMemoryLocalizationProvider();
            var catalog = ScriptableObject.CreateInstance<ESLocalizationCatalog>();
            try
            {
                Assert.That(inMemory.DefaultLanguage,
                    Is.EqualTo(EnumCollect.Envir_LanguageType.ChineseSimplified));
                Assert.That(catalog.DefaultLanguage,
                    Is.EqualTo(EnumCollect.Envir_LanguageType.ChineseSimplified));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    new ESInMemoryLocalizationProvider(EnumCollect.Envir_LanguageType.NotClear));

                catalog.defaultLanguage = EnumCollect.Envir_LanguageType.NotClear;
                Assert.That(ESLocalizationRuntime.RegisterProvider(catalog), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Provider_ResolvesRequestedLanguageThenDeterministicFallbackAndLiteral()
        {
            var provider = new ESInMemoryLocalizationProvider();
            Assert.That(provider.Set("ui.greeting", EnumCollect.Envir_LanguageType.ChineseSimplified, "你好"), Is.True);
            Assert.That(ESLocalizationRuntime.RegisterProvider(provider), Is.True);
            Assert.That(ESLocalizationRuntime.RegisterProvider(new ESInMemoryLocalizationProvider()), Is.False);

            ESLocalizationRuntime.SetCurrentLanguageOrThrow(EnumCollect.Envir_LanguageType.Japanese);
            ESLocalizationTextResult fallback = ESLocalizationRuntime.ResolveText(
                new ESLocalizedTextRef("ui.greeting"));
            Assert.That(fallback.Value, Is.EqualTo("你好"));
            Assert.That(fallback.Status, Is.EqualTo(ESLocalizationResolveStatus.ResolvedByFallbackLanguage));

            ESLocalizationTextResult literal = ESLocalizationRuntime.ResolveText(
                new ESLocalizedTextRef("ui.missing", fallbackLiteral: "默认文本"));
            Assert.That(literal.Value, Is.EqualTo("默认文本"));
            Assert.That(literal.Status, Is.EqualTo(ESLocalizationResolveStatus.ResolvedByLiteralFallback));
        }

        [Test]
        public void FallbackChain_IsDeterministicAndDoesNotReturnNotClear()
        {
            Assert.That(ESLocalizationRuntime.TryGetFallbackLanguage(
                EnumCollect.Envir_LanguageType.Japanese, 0, out var first), Is.True);
            Assert.That(first, Is.EqualTo(EnumCollect.Envir_LanguageType.English));
            Assert.That(ESLocalizationRuntime.TryGetFallbackLanguage(
                EnumCollect.Envir_LanguageType.Japanese, 1, out var second), Is.True);
            Assert.That(second, Is.EqualTo(EnumCollect.Envir_LanguageType.ChineseSimplified));
            Assert.That(ESLocalizationRuntime.TryGetFallbackLanguage(
                EnumCollect.Envir_LanguageType.Japanese, 2, out _), Is.False);
            Assert.That(ESLocalizationRuntime.TryGetFallbackLanguage(
                EnumCollect.Envir_LanguageType.NotClear, 0, out _), Is.False);
        }

        [Test]
        public void TenLanguageFallbackChains_AreConcreteUniqueAndBounded()
        {
            for (int languageIndex = 0; languageIndex < ESLocaleIdentity.SupportedLanguageCount; languageIndex++)
            {
                EnumCollect.Envir_LanguageType language = ESLocaleIdentity.GetSupportedLanguageAt(languageIndex);
                var seen = new HashSet<EnumCollect.Envir_LanguageType>();
                int fallbackIndex = 0;
                while (ESLocalizationRuntime.TryGetFallbackLanguage(language, fallbackIndex,
                    out EnumCollect.Envir_LanguageType fallback))
                {
                    Assert.That(ESLocalizationRuntime.IsConcreteLanguage(fallback), Is.True);
                    Assert.That(fallback, Is.Not.EqualTo(language));
                    Assert.That(seen.Add(fallback), Is.True);
                    Assert.That(++fallbackIndex, Is.LessThanOrEqualTo(2));
                }
            }
        }

        [Test]
        public void Catalog_RejectsMissingDefaultLanguageAndPlaceholderDrift()
        {
            var catalog = ScriptableObject.CreateInstance<ESLocalizationCatalog>();
            try
            {
                catalog.entries.Add(new ESLocalizationCatalogEntry
                {
                    textKey = "ui.score",
                    language = EnumCollect.Envir_LanguageType.English,
                    value = "Score: {value}"
                });
                catalog.entries.Add(new ESLocalizationCatalogEntry
                {
                    textKey = "ui.score",
                    language = EnumCollect.Envir_LanguageType.ChineseSimplified,
                    value = "分数"
                });

                var errors = catalog.Validate();
                Assert.That(errors.Any(error => error.Contains("参数合同不一致")), Is.True);
                Assert.That(errors.Any(error => error.Contains("缺少默认语言")), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Catalog_RejectsKeyWithoutDefaultLanguage()
        {
            var catalog = ScriptableObject.CreateInstance<ESLocalizationCatalog>();
            try
            {
                catalog.entries.Add(new ESLocalizationCatalogEntry
                {
                    textKey = "ui.onlyEnglish",
                    language = EnumCollect.Envir_LanguageType.English,
                    value = "Only English"
                });

                IReadOnlyList<string> errors = catalog.Validate();
                Assert.That(errors.Any(error => error.Contains("缺少默认语言")), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Catalog_RejectsNonCanonicalStableIdentities()
        {
            var catalog = ScriptableObject.CreateInstance<ESLocalizationCatalog>();
            try
            {
                catalog.catalogId = " catalog ";
                catalog.entries.Add(new ESLocalizationCatalogEntry
                {
                    textKey = " ui.title ",
                    language = EnumCollect.Envir_LanguageType.ChineseSimplified,
                    value = "标题"
                });

                IReadOnlyList<string> errors = catalog.Validate();
                Assert.That(errors.Any(error => error.Contains("catalogId 不能包含首尾空白")), Is.True);
                Assert.That(errors.Any(error => error.Contains("textKey 不能包含首尾空白")), Is.True);
                Assert.That(ESTextKey.IsCanonical("ui.title"), Is.True);
                Assert.That(ESTextKey.IsCanonical(" ui.title "), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Catalog_RejectsWhitespaceOnlyTranslation()
        {
            var catalog = ScriptableObject.CreateInstance<ESLocalizationCatalog>();
            try
            {
                catalog.entries.Add(new ESLocalizationCatalogEntry
                {
                    textKey = "ui.blank",
                    language = EnumCollect.Envir_LanguageType.ChineseSimplified,
                    value = "   "
                });

                Assert.That(catalog.Validate().Any(error => error.Contains("文本为空")), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void CatalogValidation_PrewarmsSingleFlatRuntimeIndex()
        {
            ESLocalizationCatalog catalog = CreateValidCatalog("flat-index");
            try
            {
                Assert.That(catalog.Validate(), Is.Empty);
                FieldInfo indexField = typeof(ESLocalizationCatalog).GetField(
                    "index", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(indexField, Is.Not.Null);
                object index = indexField.GetValue(catalog);
                Assert.That(index, Is.Not.Null);
                Assert.That(index.GetType().GetGenericArguments()[1], Is.EqualTo(typeof(string)));
                Assert.That((int)index.GetType().GetProperty("Count").GetValue(index), Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void CatalogDefaultLanguage_IsTerminalRuntimeFallback()
        {
            var catalog = ScriptableObject.CreateInstance<ESLocalizationCatalog>();
            try
            {
                catalog.catalogId = "ja-default";
                catalog.defaultLanguage = EnumCollect.Envir_LanguageType.Japanese;
                catalog.entries.Add(new ESLocalizationCatalogEntry
                {
                    textKey = "ui.onlyJapanese",
                    language = EnumCollect.Envir_LanguageType.Japanese,
                    value = "日本語"
                });
                Assert.That(catalog.Validate(), Is.Empty);
                Assert.That(ESLocalizationRuntime.RegisterProvider(catalog), Is.True);

                ESLocalizationTextResult result = ESLocalizationRuntime.ResolveText(
                    new ESLocalizedTextRef("ui.onlyJapanese", EnumCollect.Envir_LanguageType.English));

                Assert.That(result.Value, Is.EqualTo("日本語"));
                Assert.That(result.ResolvedLanguage, Is.EqualTo(EnumCollect.Envir_LanguageType.Japanese));
                Assert.That(result.Status, Is.EqualTo(ESLocalizationResolveStatus.ResolvedByFallbackLanguage));
            }
            finally
            {
                if (ReferenceEquals(ESLocalizationRuntime.Provider, catalog))
                    ESLocalizationRuntime.UnregisterProvider(catalog);
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void ResolveFormattedText_RequiresAndAppliesNamedArguments()
        {
            var provider = new ESInMemoryLocalizationProvider();
            provider.Set("ui.greeting", EnumCollect.Envir_LanguageType.ChineseSimplified, "你好，{name}");
            Assert.That(ESLocalizationRuntime.RegisterProvider(provider), Is.True);
            try
            {
                ESLocalizationTextResult missing = ESLocalizationRuntime.ResolveFormattedText(
                    new ESLocalizedTextRef("ui.greeting"), null);
                Assert.That(missing.Status, Is.EqualTo(ESLocalizationResolveStatus.InvalidArguments));

                var arguments = new Dictionary<string, string> { ["name"] = "玩家" };
                ESLocalizationTextResult resolved = ESLocalizationRuntime.ResolveFormattedText(
                    new ESLocalizedTextRef("ui.greeting"), arguments);
                Assert.That(resolved.Value, Is.EqualTo("你好，玩家"));
                Assert.That(resolved.IsResolved, Is.True);
            }
            finally
            {
                ESLocalizationRuntime.UnregisterProvider(provider);
            }
        }

        [Test]
        public void SerializedArguments_RejectDuplicateNamesAndFormatValidValues()
        {
            var provider = new ESInMemoryLocalizationProvider();
            provider.Set("ui.greeting", EnumCollect.Envir_LanguageType.ChineseSimplified, "你好，{name}");
            Assert.That(ESLocalizationRuntime.RegisterProvider(provider), Is.True);
            try
            {
                ESLocalizationTextResult valid = ESLocalizationRuntime.ResolveFormattedTextArguments(
                    new ESLocalizedTextRef("ui.greeting"),
                    new List<ESLocalizationArgument> { new ESLocalizationArgument("name", "玩家") });
                Assert.That(valid.IsResolved, Is.True);
                Assert.That(valid.Value, Is.EqualTo("你好，玩家"));

                ESLocalizationTextResult duplicate = ESLocalizationRuntime.ResolveFormattedTextArguments(
                    new ESLocalizedTextRef("ui.greeting"),
                    new List<ESLocalizationArgument>
                    {
                        new ESLocalizationArgument("name", "玩家"),
                        new ESLocalizationArgument("name", "重复")
                    });
                Assert.That(duplicate.Status, Is.EqualTo(ESLocalizationResolveStatus.InvalidArguments));
            }
            finally
            {
                ESLocalizationRuntime.UnregisterProvider(provider);
            }
        }

        [Test]
        public void TemplateFormatting_SupportsEscapesPluralAndSelectDeterministically()
        {
            var arguments = new Dictionary<string, string>
            {
                ["count"] = "1",
                ["kind"] = "hero",
                ["name"] = "<b>玩家</b>"
            };
            string template = "{{状态}} {name}：{count|plural|zero=无;one=# item;other=# items}，{kind|select|hero=英雄;other=访客}";

            Assert.That(ESLocalizationRuntime.TryFormatDetailed(template, arguments,
                EnumCollect.Envir_LanguageType.English, null,
                out string english, out ESLocalizationTemplateError englishError), Is.True, englishError.ToString());
            Assert.That(english, Is.EqualTo("{状态} &lt;b&gt;玩家&lt;/b&gt;：1 item，英雄"));

            Assert.That(ESLocalizationRuntime.TryFormatDetailed(template, arguments,
                EnumCollect.Envir_LanguageType.ChineseSimplified, null,
                out string chinese, out ESLocalizationTemplateError chineseError), Is.True, chineseError.ToString());
            Assert.That(chinese, Is.EqualTo("{状态} &lt;b&gt;玩家&lt;/b&gt;：1 items，英雄"));

            arguments["count"] = "0";
            arguments["kind"] = "unknown";
            Assert.That(ESLocalizationRuntime.TryFormatDetailed(template, arguments,
                EnumCollect.Envir_LanguageType.ChineseSimplified, null,
                out string fallback, out ESLocalizationTemplateError fallbackError), Is.True, fallbackError.ToString());
            Assert.That(fallback, Does.Contain("无，访客"));
        }

        [Test]
        public void TemplateFormatting_UsesFrenchAndRussianPluralCategories()
        {
            var arguments = new Dictionary<string, string> { ["count"] = "0" };
            const string template = "{count|plural|one=one;few=few;many=many;other=other}";

            Assert.That(ESLocalizationRuntime.TryFormatDetailed(template, arguments,
                EnumCollect.Envir_LanguageType.French, null,
                out string frenchZero, out ESLocalizationTemplateError frenchError), Is.True, frenchError.ToString());
            Assert.That(frenchZero, Is.EqualTo("one"));

            arguments["count"] = "2";
            Assert.That(ESLocalizationRuntime.TryFormatDetailed(template, arguments,
                EnumCollect.Envir_LanguageType.Russian, null,
                out string russianFew, out ESLocalizationTemplateError russianFewError), Is.True, russianFewError.ToString());
            Assert.That(russianFew, Is.EqualTo("few"));

            arguments["count"] = "5";
            Assert.That(ESLocalizationRuntime.TryFormatDetailed(template, arguments,
                EnumCollect.Envir_LanguageType.Russian, null,
                out string russianMany, out ESLocalizationTemplateError russianManyError), Is.True, russianManyError.ToString());
            Assert.That(russianMany, Is.EqualTo("many"));

            arguments["count"] = "1.5";
            Assert.That(ESLocalizationRuntime.TryFormatDetailed(template, arguments,
                EnumCollect.Envir_LanguageType.Russian, null,
                out string russianFraction, out ESLocalizationTemplateError russianFractionError), Is.True, russianFractionError.ToString());
            Assert.That(russianFraction, Is.EqualTo("other"));
        }

        [Test]
        public void SerializedArguments_RequireExplicitTrustForRichText()
        {
            var provider = new ESInMemoryLocalizationProvider();
            provider.Set("ui.rich", EnumCollect.Envir_LanguageType.ChineseSimplified, "你好，{name}");
            Assert.That(ESLocalizationRuntime.RegisterProvider(provider), Is.True);
            try
            {
                ESLocalizationTextResult safe = ESLocalizationRuntime.ResolveFormattedTextArguments(
                    new ESLocalizedTextRef("ui.rich"),
                    new List<ESLocalizationArgument>
                    {
                        new ESLocalizationArgument("name", "<color=red>玩家</color>")
                    });
                Assert.That(safe.Value, Is.EqualTo("你好，&lt;color=red&gt;玩家&lt;/color&gt;"));

                ESLocalizationTextResult trusted = ESLocalizationRuntime.ResolveFormattedTextArguments(
                    new ESLocalizedTextRef("ui.rich"),
                    new List<ESLocalizationArgument>
                    {
                        new ESLocalizationArgument("name", "<color=red>玩家</color>",
                            ESLocalizationArgumentContent.TrustedRichText)
                    });
                Assert.That(trusted.Value, Is.EqualTo("你好，<color=red>玩家</color>"));
            }
            finally
            {
                ESLocalizationRuntime.UnregisterProvider(provider);
            }
        }

        [Test]
        public void TemplateAnalysis_ReturnsStableCodesForMalformedContracts()
        {
            Assert.That(ESLocalizationRuntime.TryAnalyzeTemplate("错误 }",
                out _, out ESLocalizationTemplateError closingError), Is.False);
            Assert.That(closingError.Code, Is.EqualTo(ESLocalizationTemplateErrorCode.UnexpectedClosingBrace));

            Assert.That(ESLocalizationRuntime.TryAnalyzeTemplate(
                "{count|plural|one=一个}", out _, out ESLocalizationTemplateError branchError), Is.False);
            Assert.That(branchError.Code, Is.EqualTo(ESLocalizationTemplateErrorCode.MissingOtherBranch));

            Assert.That(ESLocalizationRuntime.TryAnalyzeTemplate(
                "{count|plural|one={count};other=#}", out _, out ESLocalizationTemplateError nestedError), Is.False);
            Assert.That(nestedError.Code, Is.EqualTo(ESLocalizationTemplateErrorCode.NestedExpression));
        }

        [Test]
        public void Catalog_RejectsTemplateKindDriftAndInvalidSyntax()
        {
            var catalog = ScriptableObject.CreateInstance<ESLocalizationCatalog>();
            try
            {
                catalog.entries.Add(new ESLocalizationCatalogEntry
                {
                    textKey = "ui.count",
                    language = EnumCollect.Envir_LanguageType.ChineseSimplified,
                    value = "{count|plural|other=# 个}"
                });
                catalog.entries.Add(new ESLocalizationCatalogEntry
                {
                    textKey = "ui.count",
                    language = EnumCollect.Envir_LanguageType.English,
                    value = "{count} items"
                });
                catalog.entries.Add(new ESLocalizationCatalogEntry
                {
                    textKey = "ui.bad",
                    language = EnumCollect.Envir_LanguageType.ChineseSimplified,
                    value = "未闭合 {name"
                });

                IReadOnlyList<string> errors = catalog.Validate();
                Assert.That(errors.Any(error => error.Contains("参数合同不一致")), Is.True);
                Assert.That(errors.Any(error => error.Contains("UnterminatedExpression")), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void ProviderChanged_FiresForRegisterAndUnregister()
        {
            var provider = new ESInMemoryLocalizationProvider();
            int changed = 0;
            Action<int> handler = _ => changed++;
            ESLocalizationRuntime.ProviderChanged += handler;
            try
            {
                Assert.That(ESLocalizationRuntime.RegisterProvider(provider), Is.True);
                Assert.That(ESLocalizationRuntime.UnregisterProvider(provider), Is.True);
                Assert.That(changed, Is.EqualTo(2));
            }
            finally
            {
                ESLocalizationRuntime.ProviderChanged -= handler;
                if (ReferenceEquals(ESLocalizationRuntime.Provider, provider))
                    ESLocalizationRuntime.UnregisterProvider(provider);
            }
        }

        [Test]
        public void RuntimeDataPresentationRegistration_OwnsAndReleasesLocalizationCatalog()
        {
            ESLocalizationCatalog first = CreateValidCatalog("first");
            ESLocalizationCatalog second = CreateValidCatalog("second");
            var module = new ESRuntimeDataModule();
            MethodInfo register = typeof(ESRuntimeDataModule).GetMethod(
                "TryRegisterPresentationCatalog", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo dispose = typeof(ESRuntimeDataModule).GetMethod(
                "DisposeConsumerResidentAssets", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(register, Is.Not.Null);
            Assert.That(dispose, Is.Not.Null);
            try
            {
                Assert.That(register.Invoke(module, new object[] { first }), Is.EqualTo(true));
                Assert.That(ESLocalizationRuntime.Provider, Is.SameAs(first));

                TargetInvocationException duplicate = Assert.Throws<TargetInvocationException>(() =>
                    register.Invoke(module, new object[] { second }));
                Assert.That(duplicate.InnerException, Is.TypeOf<InvalidOperationException>());

                dispose.Invoke(module, null);
                Assert.That(ESLocalizationRuntime.Provider, Is.Null);
            }
            finally
            {
                if (ReferenceEquals(ESLocalizationRuntime.Provider, first))
                    ESLocalizationRuntime.UnregisterProvider(first);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void RuntimeFontCatalog_UsesLocaleFallbackAndRejectsDuplicateBindings()
        {
            UnityEngine.Object bodyFont = CreateTmpFontAsset();
            var catalog = ScriptableObject.CreateInstance<ESRuntimeFontCatalog>();
            try
            {
                catalog.catalogId = "font-test";
                var firstBinding = new ESRuntimeFontBinding
                {
                    language = EnumCollect.Envir_LanguageType.ChineseSimplified,
                    role = ESRuntimeFontRole.Body
                };
                SetBindingFont(firstBinding, bodyFont);
                catalog.bindings.Add(firstBinding);
                Assert.That(catalog.Validate(), Is.Empty);
                MethodInfo resolve = typeof(ESRuntimeFontCatalog).GetMethod("TryResolve");
                object[] resolveArguments =
                {
                    EnumCollect.Envir_LanguageType.English,
                    ESRuntimeFontRole.Body,
                    null
                };
                Assert.That(resolve.Invoke(catalog, resolveArguments), Is.EqualTo(true));
                Assert.That(resolveArguments[2], Is.SameAs(bodyFont));

                var duplicateBinding = new ESRuntimeFontBinding
                {
                    language = EnumCollect.Envir_LanguageType.ChineseSimplified,
                    role = ESRuntimeFontRole.Body
                };
                SetBindingFont(duplicateBinding, bodyFont);
                catalog.bindings.Add(duplicateBinding);
                Assert.That(catalog.Validate().Any(error => error.Contains("重复绑定")), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(bodyFont);
            }
        }

        [Test]
        public void RuntimeFontCatalog_RejectsInvalidIdentityFormatAndRole()
        {
            UnityEngine.Object font = CreateTmpFontAsset();
            ESRuntimeFontCatalog catalog = CreateValidFontCatalog("font-contract", font);
            try
            {
                catalog.catalogId = " font-contract ";
                Assert.That(catalog.Validate().Any(error => error.Contains("首尾空白")), Is.True);

                catalog.catalogId = "font/contract";
                Assert.That(catalog.Validate().Any(error => error.Contains("路径分隔符")), Is.True);

                catalog.catalogId = "font-contract";
                FieldInfo currentFormatVersion = typeof(ESRuntimeFontCatalog).GetField(
                    "CurrentFormatVersion", BindingFlags.Public | BindingFlags.Static);
                Assert.That(currentFormatVersion, Is.Not.Null);
                int currentVersion = (int)currentFormatVersion.GetRawConstantValue();
                catalog.formatVersion = currentVersion + 1;
                Assert.That(catalog.Validate().Any(error => error.Contains("formatVersion 不受支持")), Is.True);

                catalog.formatVersion = currentVersion;
                catalog.bindings[0].role = (ESRuntimeFontRole)byte.MaxValue;
                Assert.That(catalog.Validate().Any(error => error.Contains("无效角色")), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(font);
            }
        }

        [Test]
        public void RuntimeFontCatalog_InvalidateIndexReflectsBindingChanges()
        {
            UnityEngine.Object firstFont = CreateTmpFontAsset();
            UnityEngine.Object secondFont = CreateTmpFontAsset();
            ESRuntimeFontCatalog catalog = CreateValidFontCatalog("font-index", firstFont);
            MethodInfo resolve = typeof(ESRuntimeFontCatalog).GetMethod("TryResolve");
            try
            {
                Assert.That(catalog.Validate(), Is.Empty);
                object[] firstArguments =
                {
                    EnumCollect.Envir_LanguageType.ChineseSimplified,
                    ESRuntimeFontRole.Body,
                    null
                };
                Assert.That(resolve.Invoke(catalog, firstArguments), Is.EqualTo(true));
                Assert.That(firstArguments[2], Is.SameAs(firstFont));

                SetBindingFont(catalog.bindings[0], secondFont);
                MethodInfo invalidate = typeof(ESRuntimeFontCatalog).GetMethod("InvalidateIndex");
                Assert.That(invalidate, Is.Not.Null);
                invalidate.Invoke(catalog, null);
                object[] secondArguments =
                {
                    EnumCollect.Envir_LanguageType.ChineseSimplified,
                    ESRuntimeFontRole.Body,
                    null
                };
                Assert.That(resolve.Invoke(catalog, secondArguments), Is.EqualTo(true));
                Assert.That(secondArguments[2], Is.SameAs(secondFont));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(firstFont);
                UnityEngine.Object.DestroyImmediate(secondFont);
            }
        }

        [Test]
        public void FontCatalogGeneration_IsIndependentAndIdempotent()
        {
            UnityEngine.Object font = CreateTmpFontAsset();
            ESRuntimeFontCatalog catalog = CreateValidFontCatalog("font-generation", font);
            int localizationGeneration = ESLocalizationRuntime.Generation;
            int before = ESFontRuntime.Generation;
            int eventCount = 0;
            int observedGeneration = 0;
            Action<int> handler = value =>
            {
                eventCount++;
                observedGeneration = value;
            };
            ESFontRuntime.CatalogChanged += handler;
            try
            {
                Assert.That(ESFontRuntime.RegisterCatalog(catalog), Is.True);
                Assert.That(ESFontRuntime.Generation, Is.GreaterThan(before));
                Assert.That(observedGeneration, Is.EqualTo(ESFontRuntime.Generation));
                Assert.That(eventCount, Is.EqualTo(1));
                Assert.That(ESLocalizationRuntime.Generation, Is.EqualTo(localizationGeneration));

                Assert.That(ESFontRuntime.RegisterCatalog(catalog), Is.True);
                Assert.That(eventCount, Is.EqualTo(1));
            }
            finally
            {
                ESFontRuntime.CatalogChanged -= handler;
                if (ReferenceEquals(ESFontRuntime.Catalog, catalog))
                    ESFontRuntime.UnregisterCatalog(catalog);
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(font);
            }
        }

        [Test]
        public void RuntimeDataPresentationRegistration_OwnsAndReleasesFontCatalog()
        {
            UnityEngine.Object firstFont = CreateTmpFontAsset();
            UnityEngine.Object secondFont = CreateTmpFontAsset();
            ESRuntimeFontCatalog first = CreateValidFontCatalog("first-font", firstFont);
            ESRuntimeFontCatalog second = CreateValidFontCatalog("second-font", secondFont);
            var module = new ESRuntimeDataModule();
            MethodInfo register = typeof(ESRuntimeDataModule).GetMethod(
                "TryRegisterPresentationCatalog", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo dispose = typeof(ESRuntimeDataModule).GetMethod(
                "DisposeConsumerResidentAssets", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(register, Is.Not.Null);
            Assert.That(dispose, Is.Not.Null);
            try
            {
                Assert.That(register.Invoke(module, new object[] { first }), Is.EqualTo(true));
                Assert.That(ESFontRuntime.Catalog, Is.SameAs(first));

                TargetInvocationException duplicate = Assert.Throws<TargetInvocationException>(() =>
                    register.Invoke(module, new object[] { second }));
                Assert.That(duplicate.InnerException, Is.TypeOf<InvalidOperationException>());

                dispose.Invoke(module, null);
                Assert.That(ESFontRuntime.Catalog, Is.Null);
            }
            finally
            {
                if (ReferenceEquals(ESFontRuntime.Catalog, first))
                    ESFontRuntime.UnregisterCatalog(first);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(firstFont);
                UnityEngine.Object.DestroyImmediate(secondFont);
            }
        }

        [Test]
        public void FontUnicodeScalarCollection_PreservesSupplementaryCharactersAndRejectsBrokenUtf16()
        {
            string emoji = char.ConvertFromUtf32(0x1F600);
            string extensionB = char.ConvertFromUtf32(0x20000);
            var scalars = new SortedSet<uint>();

            ESFontBuildProfileEditor.AddUnicodeScalars(scalars, "A" + emoji + extensionB + "A");

            Assert.That(scalars.Count, Is.EqualTo(3));
            Assert.That(ESFontBuildProfileEditor.BuildUnicodeString(scalars),
                Is.EqualTo("A" + emoji + extensionB));
            Assert.That(ESFontBuildProfileEditor.CountUnicodeScalars(
                "A" + emoji + extensionB + "A"), Is.EqualTo(3));
            Assert.Throws<InvalidOperationException>(() =>
                ESFontBuildProfileEditor.AddUnicodeScalars(new HashSet<uint>(), "\uD800"));
        }

        [Test]
        public void FontCharacterCollection_ConsumesMatchingESLocalizationCatalogLanguage()
        {
            ESFontBuildProfile profile = ScriptableObject.CreateInstance<ESFontBuildProfile>();
            ESLocalizationCatalog catalog = ScriptableObject.CreateInstance<ESLocalizationCatalog>();
            var entry = new ESFontLanguageBuildEntry
            {
                language = EnumCollect.Envir_LanguageType.ChineseSimplified,
                usage = ESFontUsage.Body,
                additionalCharacters = "A"
            };
            string emoji = char.ConvertFromUtf32(0x1F600);
            catalog.entries.Add(new ESLocalizationCatalogEntry
            {
                textKey = "story.zh",
                language = EnumCollect.Envir_LanguageType.ChineseSimplified,
                value = "汉" + emoji
            });
            catalog.entries.Add(new ESLocalizationCatalogEntry
            {
                textKey = "story.en",
                language = EnumCollect.Envir_LanguageType.English,
                value = "EnglishOnly"
            });
            profile.localizationCatalogs.Add(catalog);

            try
            {
                string characters = ESFontBuildProfileEditor.CollectCharacters(profile, entry);
                var scalars = new HashSet<uint>();
                ESFontBuildProfileEditor.AddUnicodeScalars(scalars, characters);
                Assert.That(scalars, Does.Contain((uint)'A'));
                Assert.That(scalars, Does.Contain((uint)'汉'));
                Assert.That(scalars, Does.Contain(0x1F600u));
                Assert.That(scalars.Contains((uint)'E'), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void FontStandardTemplate_CreatesTenStableEsLanguageEntries()
        {
            ESFontBuildProfile profile = ScriptableObject.CreateInstance<ESFontBuildProfile>();
            try
            {
                profile.enabledUsages = new List<ESFontUsage> { ESFontUsage.Body };
                ESFontBuildProfileEditor.ApplyStandardTenLanguageTemplate(profile);

                Assert.That(profile.enabledLanguages.Count, Is.EqualTo(10));
                Assert.That(profile.languages.Count, Is.EqualTo(10));
                Assert.That(profile.languages.Select(entry => entry.language).Distinct().Count(), Is.EqualTo(10));
                Assert.That(profile.languages.All(entry => entry.usage == ESFontUsage.Body), Is.True);
                Assert.That(profile.languages.All(entry => string.IsNullOrEmpty(entry.legacyLanguageCode)), Is.True);
                Assert.That(profile.fontFamily.sources.Select(source => source.scriptGroup).Distinct().Count(),
                    Is.EqualTo(Enum.GetValues(typeof(ESFontScriptGroup)).Length));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void FontBuildPlan_RejectsEntryIdentityDriftBeforeGeneration()
        {
            ESFontBuildProfile profile = ScriptableObject.CreateInstance<ESFontBuildProfile>();
            try
            {
                ESFontBuildProfileEditor.ApplyStandardTenLanguageTemplate(profile);
                profile.languages[0].language = EnumCollect.Envir_LanguageType.English;

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => ESFontBuildProfileEditor.CreateBuildPlan(profile));
                Assert.That(exception.Message, Does.Contain("身份已经漂移"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        private static ESLocalizationCatalog CreateValidCatalog(string id)
        {
            var catalog = ScriptableObject.CreateInstance<ESLocalizationCatalog>();
            catalog.catalogId = id;
            catalog.entries.Add(new ESLocalizationCatalogEntry
            {
                textKey = "ui.test",
                language = EnumCollect.Envir_LanguageType.ChineseSimplified,
                value = "测试"
            });
            return catalog;
        }

        private static ESRuntimeFontCatalog CreateValidFontCatalog(string id, UnityEngine.Object font)
        {
            var catalog = ScriptableObject.CreateInstance<ESRuntimeFontCatalog>();
            catalog.catalogId = id;
            var binding = new ESRuntimeFontBinding
            {
                language = EnumCollect.Envir_LanguageType.ChineseSimplified,
                role = ESRuntimeFontRole.Body
            };
            SetBindingFont(binding, font);
            catalog.bindings.Add(binding);
            return catalog;
        }

        private static UnityEngine.Object CreateTmpFontAsset()
        {
            Type fontType = Type.GetType("TMPro.TMP_FontAsset, Unity.TextMeshPro", true);
            return ScriptableObject.CreateInstance(fontType);
        }

        private static void SetBindingFont(ESRuntimeFontBinding binding, UnityEngine.Object font)
        {
            FieldInfo fontField = typeof(ESRuntimeFontBinding).GetField("font");
            Assert.That(fontField, Is.Not.Null);
            fontField.SetValue(binding, font);
        }

    }
}
