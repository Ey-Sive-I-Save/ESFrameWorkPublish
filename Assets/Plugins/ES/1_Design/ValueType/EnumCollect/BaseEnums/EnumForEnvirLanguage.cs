using UnityEngine;

namespace ES
{
    public static partial class EnumCollect
    {
        //支持本地化-多语言 ES全程支持
        public enum Envir_LanguageType
        {
            [InspectorName("未指定")] NotClear = Chinese | Japan | English,
            [InspectorName("中文")] Chinese = 1,
            [InspectorName("日文")] Japan = 2,
            [InspectorName("英文")] English = 4
        }
    }

    public static class EnvirLanguageClear
    {
        public static void ToClear(this ref EnumCollect.Envir_LanguageType envir_)
        {
            envir_.ToClear(EnumCollect.Envir_LanguageType.NotClear, EnumCollect.Envir_LanguageType.Chinese);
        }

        public static void ToClear(this ref EnumCollect.Envir_LanguageType envir_, EnumCollect.Envir_LanguageType defaultValue)
        {
            envir_.ToClear(EnumCollect.Envir_LanguageType.NotClear, defaultValue);
        }
    }
}
