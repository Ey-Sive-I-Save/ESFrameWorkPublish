using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    public interface ISpriteValueEntry : IValueEntry<Sprite, ValueEntrySpriteKey>
    {

    }
    #region 演示
    public class Example_IMessageSpritevalueEntryEasy : ISpriteValueEntry
    {

        public void HandleValueEntry(ref Sprite back, ValueEntrySpriteKey key, object help = null, EnumCollect.Envir_LanguageType lan = EnumCollect.Envir_LanguageType.NotClear, EnumCollect.ValueEntryGetOrSet getOrSet = EnumCollect.ValueEntryGetOrSet.NotClear)
        {
            if (lan == EnumCollect.Envir_LanguageType.NotClear)
            {
                 back=null;

            }
        }
    }

    #endregion
}
