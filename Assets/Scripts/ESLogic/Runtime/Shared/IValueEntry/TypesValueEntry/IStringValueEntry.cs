using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    //String获得
    public interface IStringValueEntry : IValueEntry<string,ValueEntryStringKey>
    {
          
    }

    #region 演示
    public class Example_IMessageStringvalueEntryEasy : IStringValueEntry
    {

        public void HandleValueEntry(ref string back, ValueEntryStringKey key, object help = null, EnumCollect.Envir_LanguageType lan = EnumCollect.Envir_LanguageType.NotClear, EnumCollect.ValueEntryGetOrSet getOrSet = EnumCollect.ValueEntryGetOrSet.NotClear)
        {
            var a=getOrSet;
            //读还是 写？？
            //1 不需要额外提示,类型本身就已经完成
            //2 可读可写 ，一般传入确定的一种情况
            if(a== EnumCollect.ValueEntryGetOrSet.Get)
            {
            
            }
            else if(a== EnumCollect.ValueEntryGetOrSet.Set)
            {
               
            }
            var lan_= lan;
            lan_.ToClear();
            //哪种语言??
            //1 不支持三语言，直接忽略
            //2 支持三语言，但是不想每次使用时都传入，可以在方法内获得当前语言
            //3 直接写入准确的语言
            
        }
    }

    #endregion
}
