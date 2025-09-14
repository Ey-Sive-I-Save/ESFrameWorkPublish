using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public static partial class EnumCollect
    {

        //支持本地化-多语言 ES全程支持
        public enum LanguageType
        {
            [InspectorName("未指定")] NotClear= Chinese | Japan| English,
            [InspectorName("中文")] Chinese=1,
            [InspectorName("日文")] Japan=2,

            [InspectorName("英文")] English=4
        }

        //有关委托集成式Link的发送类型 详见-》LinkUnityEvent
        public enum LinkEventType
        {
            [InspectorName("先发送Link再Invoke事件")] SendThenInvoke,
            [InspectorName("先Invoke事件再发送Link")] InvokeThenSend,
            [InspectorName("仅发送Link")] OnlySend,
            [InspectorName("仅发送Invoke")] OnlyInvoke
        }


        //原型支持的数据类型
        public enum ArchitectureValueType
        {
            [InspectorName("动态标签")] DynamicTag,//通常是某个技能解锁后会把自己放入
            [InspectorName("浮点值判据")] FloatValue,//浮点数值 通常是属性数值或者各种无法被整数确定的
            [InspectorName("整数值判据")] IntValue,//整数数值 和浮点类似，适用于精准情况
            [InspectorName("字符串值判据")] StringValue,//字符串值可用于高度自定义的各种情况，是一种加强版的标签
            [InspectorName("布尔值判据")] BoolValue,//布尔值非常简单，适用于流程控制
            [InspectorName("枚举值判据")] EnumValue//枚举值需要额外的支持，可以精确选定目标值
        }

        //缓存入 目标点
        public enum HandleCacheOption
        {
            [InspectorName("不处理")] None,
            [InspectorName("缓冲到Main主池")] ToMain,
            [InspectorName("缓冲到本体池")] ToSelf,//不一定技能专属
            [InspectorName("缓冲到自定义池")] ToSelfDefine
        }

        //输入时触发效果的条件
        [Flags]
        public enum InputHappenConditionOptions
        {
            [InspectorName("按下时")] Down = 1,
            [InspectorName("松开时")] Up = 2,
            [InspectorName("按住时")] Hold = 4,
            [InspectorName("辅助档位，不要单独选择")] DontBeUse_0 = 8,
            [InspectorName("按住足够时间")] HoldForTime = 12,
            [InspectorName("辅助档位，不要单独选择")] DontBeUse_1 = 16,
            [InspectorName("按住足够时间并松手")] HoldForTimeAndUp = 22
        }

        //Link被作为Task处理时，需要一个处理环境 
        // 1 未建立链接前，可以堆积到这里 等待完成处理
        public enum LinkTaskEnvironment
        {
            [InspectorName("任意")]Any=0,
            [InspectorName("会在游戏核心进行处理")]GameCenter=1,
            [InspectorName("在客户端处理")] Client=2,
            [InspectorName("在服务器处理")] Server=4,
            [InspectorName("在指定目标处理")] Target=8
        }
        //立刻发送消息-
        public enum LinkSendChannel
        {
            [InspectorName("无")]None=0,
            [InspectorName("任意")] Any = GameCenter| ClientToServer| ServerToClients| ServerToTarget,
            [InspectorName("会在游戏核心进行处理")] GameCenter = 1,
            [InspectorName("从客户端发到服务器")] ClientToServer = 2,
            [InspectorName("该事件将会在游戏核心进行处理")] ServerToClients = 4,
            [InspectorName("该事件将会在游戏核心进行处理")] ServerToTarget = 8
        }

        public enum Axis_XY
        {
            [InspectorName("X轴")] X, [InspectorName("Y轴")] Y
        }

        public enum Axis_XYZ
        {
            [InspectorName("X轴")] X, [InspectorName("Y轴")] Y, [InspectorName("Z轴")]Z
        }
    }
}

