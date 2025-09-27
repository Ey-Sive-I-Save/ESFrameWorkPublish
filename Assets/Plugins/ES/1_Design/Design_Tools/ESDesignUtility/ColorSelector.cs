using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public static partial class ESDesignUtility
    {
        //GUI Color 颜色库
        public static class ColorSelector
        {

            //使用方法↓
            //GUIColor("@ESDesignUtility.ColorSelector.Color_03")
            public static Color Color_01 = new Color(0.588f, 0.758f, 0.763f, 1);
            public static Color Color_02 = new Color(0.9988f, 0.958f, 0.163f, 1);
            public static Color Color_03 = new Color(0.9988f, 0.958f, 0f, 1);//黄色
            public static Color Color_04 = new Color(0.1588f, 0.958f, 0.9f, 1);//色
            public static Color Color_05 = new Color(0.7588f, 0.758f, 0.25f, 1);//色
            public static Color Color_06 = new Color(0.4588f, 0.758f, 0.45f, 1);//色

            public static Color ColorForDes = new Color(0.682f, 0.8392f, 0.945f);//备注信息  --偏白
            public static Color ColorForPlayerReadMe = new Color(0.49f, 0.2353f, 0.596f);//播放器注释信息  --偏白
            public static Color ColorForCaster = new Color(0.365f, 0.6784f, 0.886f);//投射器 --偏蓝
            public static Color ColorForCatcher = new Color(0.8314f, 0.6745f, 0.051f);//抓取器   --偏橙色
            public static Color ColorForESValue = new Color(0.153f, 0.682f, 0.376f);//ES值    --偏绿
            public static Color ColorForUpdating = new Color(0.804f, 0.67843f, 0);//更新中    --偏绿

            public static Color ColorForBinding = new Color(0, 0.97f, 1);//绑定色
            public static Color ColorForSearch = new Color(0.4f, 0.804f, 0.667f);//选择色
            public static Color ColorForApply = new Color(0, 0.804f, 0);//应用色

            public static readonly Dictionary<string, Color> normalColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
            {
        { "red", Color.red },
        { "green", Color.green },
        { "blue", Color.blue },
        { "yellow", Color.yellow },
        { "cyan", Color.cyan },
        { "magenta", Color.magenta },
        { "black", Color.black },
        { "white", Color.white },
        { "gray", Color.gray },
        { "grey", Color.grey },
        { "clear", Color.clear }
            };



            static void test()
            {


                Color c = ESDesignUtility.ColorSelector.Color_01;
            }
        }
    }
}

