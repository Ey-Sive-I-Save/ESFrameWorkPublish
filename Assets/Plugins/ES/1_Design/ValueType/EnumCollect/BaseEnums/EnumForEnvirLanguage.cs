using System;
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
            [InspectorName("繁体中文")] ChineseTraditional = 8
        }
    }

    public static class ESLocalizationRuntime
    {
        private static EnumCollect.Envir_LanguageType currentLanguage =
            EnumCollect.Envir_LanguageType.ChineseSimplified;
        private static int generation = 1;

        public static EnumCollect.Envir_LanguageType CurrentLanguage => currentLanguage;
        public static int Generation => generation;

        public static event Action<EnumCollect.Envir_LanguageType,
            EnumCollect.Envir_LanguageType, int> CurrentLanguageChanged;

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
