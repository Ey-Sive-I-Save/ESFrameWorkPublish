using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//接口--可更新
namespace ES
{
    //生命周期接口--纯Host和模块和Host且模块都有
    public interface IESWithLife
    {
        #region 生命周期接口
        bool Signal_IsActiveAndEnable { get; set; }
        bool CanUpdating { get; }
        public void TryDisableSelf();
        public void TryEnableSelf();
        public void TryUpdateSelf();
        public void TryDestroySelf();
        #endregion
    }
}
