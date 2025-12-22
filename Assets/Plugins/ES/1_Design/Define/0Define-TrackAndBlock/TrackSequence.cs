using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public interface ITrackSequence  
    {
         public IEnumerable<ITrackItem> Tracks{get;}
         public List<Type> UseableTrackTypes{get;}

         void InitByEditor();//被初始化按钮点击

      // public IEnumerable<>
    }
    
}
