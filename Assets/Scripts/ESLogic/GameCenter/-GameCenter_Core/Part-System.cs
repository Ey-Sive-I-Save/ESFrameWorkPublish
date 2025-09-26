using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public partial class GameCenter
    {
        #region 全局事件-GameCenterAwakeBefore
        protected override void OnBeforeAwakeRegister()
        {
            base.OnBeforeAwakeRegister();
            GlobalLinkPool.SendLink(new Link_GameCenterAwakeBefoe());
        }
        #endregion

        #region 游戏退出时机
        private void OnApplicationQuit()
        {
            ESSystem.IsQuitting = true;
        }

        #endregion
    }

    public struct Link_GameCenterAwakeBefoe
    {

    }

}
