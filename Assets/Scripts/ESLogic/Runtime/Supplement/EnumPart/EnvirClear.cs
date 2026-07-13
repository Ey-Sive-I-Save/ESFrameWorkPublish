using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES
{
    public static class EnvirClear
    {
        /// <summary>
        /// 将未指定的语言参数解析为当前全局语言。
        /// 用于“调用方不传就使用默认语言，调用方传入就覆盖默认语言”的场景。
        /// </summary>
        public static void ResolveDefault(this ref EnumCollect.Envir_LanguageType envir_)
        {
            if (envir_ == EnumCollect.Envir_LanguageType.NotClear) envir_ = GameManager.Envir_Language;
        }

        /// <summary>
        /// 兼容旧命名。Clear 在这里表示把 NotClear 的泛态明确化为具体语言。
        /// </summary>
        public static void ToClear(this ref EnumCollect.Envir_LanguageType envir_)
        {
            envir_.ResolveDefault();
        }
    }
}
