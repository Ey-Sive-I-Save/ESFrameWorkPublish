using UnityEngine;

namespace ES
{
    public enum ESRuntimeMode
    {
        [InspectorName("游戏中")]
        Gameplay = 0,

        [InspectorName("主菜单")]
        MainMenu = 10,

        [InspectorName("暂停菜单")]
        PauseMenu = 20,

        [InspectorName("加载中")]
        Loading = 30,

        [InspectorName("场景切换")]
        SceneTransition = 40,

        [InspectorName("过场")]
        Cutscene = 50,

        [InspectorName("对话")]
        Dialogue = 60,

        [InspectorName("背包")]
        Inventory = 70,

        [InspectorName("地图")]
        Map = 80,

        [InspectorName("设置")]
        Settings = 90,

        [InspectorName("改键中")]
        RebindInput = 100,

        [InspectorName("确认弹窗")]
        ConfirmDialog = 110,

        [InspectorName("拍照模式")]
        PhotoMode = 120,

        [InspectorName("观战")]
        Spectator = 130
    }

    public enum ESRuntimeModeTag
    {
        [InspectorName("战斗")]
        Combat = 0,

        [InspectorName("瞄准")]
        Aiming = 1,

        [InspectorName("骑乘")]
        Mounted = 2,

        [InspectorName("攀爬")]
        Climbing = 3,

        [InspectorName("死亡")]
        Dead = 4,

        [InspectorName("眩晕")]
        Stunned = 5,

        [InspectorName("网络繁忙")]
        NetworkBusy = 6
    }
}
