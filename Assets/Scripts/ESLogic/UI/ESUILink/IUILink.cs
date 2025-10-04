using ES;
 
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    public interface ILink_UI
    {

    }
    //有各种Flag 和 携带信息 的变体，支持的行为自己去拆
    public interface ILink_UI_OperationOptions : ILink_UI
    {

    }
    public struct Link_UI_Default : ILink_UI_OperationOptions
    {

    }
    #region Flag支持
    public struct Link_UI_NotFlag : ILink_UI_OperationOptions
    {

    }


    #endregion
}

