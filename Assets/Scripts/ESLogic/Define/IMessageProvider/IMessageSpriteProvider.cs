using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    public interface IMessageSpriteProvider : IMessageProvider<Sprite, MessageSpriteKey, int>
    {

    }
    #region 演示
    public class Example_IMessageSpriteProviderEasy : IMessageSpriteProvider
    {
        public Sprite GetMessage(MessageSpriteKey k, EnumCollect.LanguageType language = EnumCollect.LanguageType.NotClear, int hepler = 0)
        {
            if (language == EnumCollect.LanguageType.NotClear)
            {

            }
            return null;
        }
    }

    #endregion
}
