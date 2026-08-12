using System;
using NUnit.Framework;

namespace ES.Tests
{
    [Parallelizable(ParallelScope.None)]
    public sealed class ESLocalizationLanguageTests
    {
        private EnumCollect.Envir_LanguageType originalLanguage;

        [SetUp]
        public void SaveCurrentLanguage()
        {
            originalLanguage = ESLocalizationRuntime.CurrentLanguage;
        }

        [TearDown]
        public void RestoreCurrentLanguage()
        {
            ESLocalizationRuntime.SetCurrentLanguageOrThrow(originalLanguage);
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
    }
}
