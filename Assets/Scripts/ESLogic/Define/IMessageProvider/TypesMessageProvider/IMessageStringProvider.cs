using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    //String获得
    public interface IMessageStringProvider : IMessageProvider<string,MessageStringKey,int>
    {
          
    }

    #region 演示
    public class Example_IMessageStringProviderEasy : IMessageStringProvider
    {
        public string GetMessage(MessageStringKey k, EnumCollect.Envir_LanguageType language = EnumCollect.Envir_LanguageType.NotClear, int hepler = 0)
        {
            language.ToClear();//先清晰化

            return "";
        }
    }

    #endregion
}
