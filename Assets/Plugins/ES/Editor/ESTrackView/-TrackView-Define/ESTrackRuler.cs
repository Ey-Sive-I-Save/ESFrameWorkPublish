using log4net.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    public class ESTrackRuler : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<ESTrackRuler, UxmlTraits> { }

        public ESRuler TopRuler = new ESRuler();
        public ESTrackRuler()
        {

            {
                TopRuler.style.left = 0;
                TopRuler.style.top = 0;
                TopRuler.style.width = 1000;
                TopRuler.style.height = 30;
                Add(TopRuler);

            }
        }
    }

    public class ESRuler : VisualElement
    {
        public int pxPerSecond = 100;
        public float showStart = 0;
        public float showEnd = 5;
        public static Dictionary<RulerLevel, float> LevelsPX = new Dictionary<RulerLevel, float>() {

            { RulerLevel._001, 1f/60f },
            { RulerLevel._005, 1f/12f },
            { RulerLevel._010, 1f/6f },
            { RulerLevel._030, 0.5f },
            { RulerLevel._100, 1f },
            { RulerLevel._500, 5f },

        };
        public RulerLevel LevelLow = RulerLevel._005;
        public int MaxLevelGo = 2;

        public ESRuler()
        {
            generateVisualContent += DrawRuler;
        }

        private void DrawRuler(MeshGenerationContext context)
        {
            var p = context.painter2D;
            rec.Clear();
            for (int i = RulerLevel._500.GetHashCode(); i >= 0; i--)
            {
                DrawLevel(i, p, context);
            }

        }
        public static Color levelup_1 = new Color(1, 1, 1, 0.7f);
        public static Color levelup_2 = new Color(1, 1, 1, 1f);

        public static Color levelup_down1 = new Color(1,1,1,0.1f);

        public static Color levelup_down2 = new Color(1, 1, 1, 0.25f);
        public HashSet<int> rec = new HashSet<int>(100);
        public void DrawLevel(int level, Painter2D painter, MeshGenerationContext context)
        {
            painter.fillColor = Color.white;

            int off = level - LevelLow.GetHashCode();
           
            float height = 10 + off * 5;
            float FontSize = 3 + off * 2;

           
            if (off >= 0 && off <= MaxLevelGo)
            {
             
                var f = LevelsPX[(RulerLevel)level];
                if (off == 0) painter.strokeColor = Color.gray;
                else painter.strokeColor = off > 1 ? levelup_2 : levelup_1;
                painter.lineWidth = off + 1;
                for (float i = showStart; i <= showEnd; i += f)
                {
                    int pixel = Mathf.RoundToInt((i * pxPerSecond));
                    if (rec.Contains(pixel))
                    {

                    }
                    else
                    {
                      
                        Vector2 vv = new Vector2(pixel, 0);
                        painter.BeginPath();
                        painter.MoveTo(vv);
                        vv.y = height;
                        painter.LineTo(vv);
                        painter.Stroke();
                        painter.ClosePath();
                        if (off > 0)
                        {
                            
                            rec.Add(pixel);
                            painter.BeginPath();
                            context.DrawText(FormatSecondsToMinuteSecond(i), vv, FontSize, Color.white);
                            painter.lineWidth = 1;
                            painter.strokeColor = off > 1 ? levelup_down2 : levelup_down1;

                            painter.MoveTo(vv);
                            vv.y = 1000;
                            painter.LineTo(vv);
                            painter.Stroke();
                            painter.ClosePath();
                            if (off == 0) painter.strokeColor = Color.gray;
                            else painter.strokeColor = off > 1 ? levelup_2 : levelup_1;
                            painter.lineWidth = off + 1;
                        }
                    }
                }
                
            }


        }
        public static string FormatSecondsToMinuteSecond(float totalSeconds)
        {
            // 处理负数，假设时间非负
            if (totalSeconds < 0)
            {
                totalSeconds = 0;
            }

            // 计算总秒数对应的分钟数（取整）

            float mmss = Mathf.RoundToInt(((totalSeconds % 1) * 60));
            // 计算剩余的秒数（浮点数，包含小数部分）
            float remainingSeconds = (int)totalSeconds;
            // 将剩余秒数格式化为至少两位整数，默认保留1位小数
            // 例如：remainingSeconds=9.5, 格式化为"09.5"
            // 使用 F1 格式表示固定小数点后1位（会根据需要四舍五入）


            // 组合最终字符串
            return $"{remainingSeconds}:{mmss.ToString("00")}";
        }

    }
    public enum RulerLevel
    {
        _001,
        _005,
        _010,
        _030,
        _100,
        _500,
    }


}
