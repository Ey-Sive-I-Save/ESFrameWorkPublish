using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    [DefaultExecutionOrder(-9)]
    public partial class GameCenter : SingletonAsCore<GameCenter>
    {
        #region 域
        [TabGroup("扩展域", "【全局】", TabLayouting = TabLayouting.MultiRow, TextColor = "@ESDesignUtility.ColorSelector.Color_04"), HideLabel]
        public GlobalDomain GlobalDomain=new GlobalDomain();


        [TabGroup("扩展域", "游戏运行", TabLayouting = TabLayouting.MultiRow, TextColor = "@ESDesignUtility.ColorSelector.Color_04"), HideLabel]
        //显性声明扩展域
        public GameRunDomain GameRunDomain=new GameRunDomain();


        protected override void OnAwakeRegisterOnly()
        {
            
            base.OnAwakeRegisterOnly();
            RegisterDomain(GlobalDomain);
            RegisterDomain(GameRunDomain);

        }
        #endregion

    }
    #region 声明域和模块
    [Serializable]
    public class GlobalDomain : Domain<GameCenter, GlobalModule>
    {
    }
    [Serializable]
    public abstract class GlobalModule : Module<GameCenter, GlobalDomain> { }

    [Serializable]
    public class GameRunDomain : Domain<GameCenter, GameRunModule> { }
    [Serializable]
    public abstract class GameRunModule : Module<GameCenter, GameRunDomain> { }
    #endregion
}
