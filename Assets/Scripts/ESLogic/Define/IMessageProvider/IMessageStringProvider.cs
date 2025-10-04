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
        public string GetMessage(MessageStringKey k, EnumCollect.LanguageType language = EnumCollect.LanguageType.NotClear, int hepler = 0)
        {
            if(language== EnumCollect.LanguageType.NotClear)
            {
                
            }
            return "";
        }
    }

    #endregion
}
