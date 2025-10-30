using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES
{
    public static class EnvirClear
    {
        public static void ToClear(this ref EnumCollect.Envir_LanguageType envir_)
        {
            if (envir_ == EnumCollect.Envir_LanguageType.NotClear) envir_ = GameManager.Envir_Language;
        }
    }
}
