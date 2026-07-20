using UnityEngine;

namespace ES
{
    public enum ESGameTag : ushort
    {
        [InspectorName("无")]
        None = 0,

        [InspectorName("生命类/死亡")]
        生命类_死亡 = 1,

        [InspectorName("控制类/眩晕")]
        控制类_眩晕 = 2,

        [InspectorName("控制类/沉默")]
        控制类_沉默 = 3,

        [InspectorName("控制类/定身")]
        控制类_定身 = 4,

        [InspectorName("防御类/霸体")]
        防御类_霸体 = 5,

        [InspectorName("防御类/无敌")]
        防御类_无敌 = 6,

        [InspectorName("感知类/隐身")]
        感知类_隐身 = 7,

        [InspectorName("元素类/燃烧")]
        元素类_燃烧 = 8,

        [InspectorName("元素类/冰冻")]
        元素类_冰冻 = 9,

        [InspectorName("元素类/中毒")]
        元素类_中毒 = 10,

        [InspectorName("元素类/感电")]
        元素类_感电 = 11,

        [InspectorName("战斗类/战斗中")]
        战斗类_战斗中 = 12,

        [InspectorName("战斗类/瞄准中")]
        战斗类_瞄准中 = 13,

        [InspectorName("技能类/施法中")]
        技能类_施法中 = 14,

        [InspectorName("技能类/引导中")]
        技能类_引导中 = 15,

        [InspectorName("移动类/冲刺中")]
        移动类_冲刺中 = 16,

        [InspectorName("移动类/跳跃中")]
        移动类_跳跃中 = 17,

        [InspectorName("移动类/下落中")]
        移动类_下落中 = 18,

        [InspectorName("移动类/攀爬中")]
        移动类_攀爬中 = 19,

        [InspectorName("移动类/骑乘中")]
        移动类_骑乘中 = 20,

        [InspectorName("交互类/可锁定")]
        交互类_可锁定 = 21,

        [InspectorName("交互类/可交互")]
        交互类_可交互 = 22,

        [InspectorName("交互类/可受击")]
        交互类_可受击 = 23,

        [InspectorName("交互类/可被治疗")]
        交互类_可被治疗 = 24,

        [InspectorName("阵营类/友方")]
        阵营类_友方 = 25,

        [InspectorName("阵营类/敌方")]
        阵营类_敌方 = 26,

        [InspectorName("阵营类/中立")]
        阵营类_中立 = 27,

        [InspectorName("身份类/玩家")]
        身份类_玩家 = 28,

        [InspectorName("身份类/NPC")]
        身份类_NPC = 29,

        [InspectorName("身份类/召唤物")]
        身份类_召唤物 = 30,

        [InspectorName("身份类/投射物")]
        身份类_投射物 = 31,

        [InspectorName("保留/32")]
        Reserved32 = 32,
        [InspectorName("保留/33")]
        Reserved33 = 33,
        [InspectorName("保留/34")]
        Reserved34 = 34,
        [InspectorName("保留/35")]
        Reserved35 = 35,
        [InspectorName("保留/36")]
        Reserved36 = 36,
        [InspectorName("保留/37")]
        Reserved37 = 37,
        [InspectorName("保留/38")]
        Reserved38 = 38,
        [InspectorName("保留/39")]
        Reserved39 = 39,
        [InspectorName("保留/40")]
        Reserved40 = 40,
        [InspectorName("保留/41")]
        Reserved41 = 41,
        [InspectorName("保留/42")]
        Reserved42 = 42,
        [InspectorName("保留/43")]
        Reserved43 = 43,
        [InspectorName("保留/44")]
        Reserved44 = 44,
        [InspectorName("保留/45")]
        Reserved45 = 45,
        [InspectorName("保留/46")]
        Reserved46 = 46,
        [InspectorName("保留/47")]
        Reserved47 = 47,
        [InspectorName("保留/48")]
        Reserved48 = 48,
        [InspectorName("保留/49")]
        Reserved49 = 49,
        [InspectorName("保留/50")]
        Reserved50 = 50,
        [InspectorName("保留/51")]
        Reserved51 = 51,
        [InspectorName("保留/52")]
        Reserved52 = 52,
        [InspectorName("保留/53")]
        Reserved53 = 53,
        [InspectorName("保留/54")]
        Reserved54 = 54,
        [InspectorName("保留/55")]
        Reserved55 = 55,
        [InspectorName("保留/56")]
        Reserved56 = 56,
        [InspectorName("保留/57")]
        Reserved57 = 57,
        [InspectorName("保留/58")]
        Reserved58 = 58,
        [InspectorName("保留/59")]
        Reserved59 = 59,
        [InspectorName("保留/60")]
        Reserved60 = 60,
        [InspectorName("保留/61")]
        Reserved61 = 61,
        [InspectorName("保留/62")]
        Reserved62 = 62,
        [InspectorName("保留/63")]
        Reserved63 = 63
    }
}
