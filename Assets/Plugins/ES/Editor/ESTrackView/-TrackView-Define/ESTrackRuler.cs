
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
                TopRuler.style.width = 800;
                TopRuler.style.minWidth = 800;
                TopRuler.style.maxWidth = 800;
                TopRuler.style.height = 30;
                Add(TopRuler);

            }
        }
        public void UpdateRuler()
        {
            
        }
    }

    public class ESRuler : VisualElement
    {
        public float pxPerSecond {get{ if(ESTrackViewWindow.window!=null) return ESTrackViewWindow.window.pixelPerSecond;return 100;}}
        
        
        public float showStart =>ESTrackViewWindow.window.startScale*totalTime;
        public float showEnd =>ESTrackViewWindow.window.endScale*totalTime;
        public float totalTime=>ESTrackViewWindow.TotalTime;
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
        private static readonly Dictionary<int, string> s_TimeLabelCache = new Dictionary<int, string>(512);

        public ESRuler()
        {
            //Paint 绘制
            generateVisualContent += DrawRulerOnlyPaint;
        }

        private void DrawRulerOnlyPaint(MeshGenerationContext context)
        {
            if(ESTrackViewWindow.window==null)return;
            //Debug.Log("DRAWING");
            var p = context.painter2D;
            
             AutoUpdateLevel();
            rec.Clear();
            for (int i = RulerLevel._500.GetHashCode(); i >= 0; i--)
            {
                DrawLevel(i, p, context);
            }
        }

        public void AutoUpdateLevel()
        {
            if (pxPerSecond >= 200)
            {
                LevelLow= RulerLevel._001;
            }
            else  if (pxPerSecond >= 100)
            {
                LevelLow= RulerLevel._005;
            }
           else  if (pxPerSecond >= 50)
            {
                LevelLow= RulerLevel._010;
            }else  if (pxPerSecond >= 20)
            {
                LevelLow= RulerLevel._030;
            }else  if (pxPerSecond >=10)
            {
                LevelLow= RulerLevel._100;
            }else 
            {
                LevelLow= RulerLevel._500;
            }
        }
        #region  颜色
        public static Color levelup_1 = new Color(0.72f, 0.76f, 0.82f, 0.4f);
        public static Color levelup_2 = new Color(0.9f, 0.92f, 0.96f, 0.68f);
        public static Color levelup_down1 = new Color(0.55f, 0.6f, 0.68f, 0.045f);
        public static Color levelup_down2 = new Color(0.62f, 0.67f, 0.74f, 0.11f);
        #endregion
        
        
        public HashSet<int> rec = new HashSet<int>(100);
        public void DrawLevel(int level, Painter2D painter, MeshGenerationContext context)
        {
            painter.fillColor = Color.white;

            int off = level - LevelLow.GetHashCode();
           
            float height = 9 + off * 5;
            float FontSize = Mathf.Clamp(7 + off * 2, 8, 11);

           
            if (off >= 0 && off <= MaxLevelGo)
            {
             
                var f = LevelsPX[(RulerLevel)level];
                if (off == 0) painter.strokeColor = new Color(0.5f, 0.55f, 0.62f, 0.28f);
                else painter.strokeColor = off > 1 ? levelup_2 : levelup_1;
                painter.lineWidth = off + 1;
                float firstTick = Mathf.Ceil(showStart / f) * f;
                float lastTick = Mathf.Floor(showEnd / f) * f;
                for (float realSecondPoint = firstTick; realSecondPoint <= lastTick + 0.0001f; realSecondPoint += f)
                {
                    if(realSecondPoint<showStart||realSecondPoint>showEnd)continue;
                    float secondsOffsetFromStart=realSecondPoint-showStart;
                    int pixel = Mathf.RoundToInt(secondsOffsetFromStart * pxPerSecond);
                    if (rec.Contains(pixel))
                    {

                    }
                    else
                    {
                      
                        Vector2 vv = new Vector2(pixel, 0);
                        painter.BeginPath();
                        painter.MoveTo(vv);//开始
                        vv.y = height;
                        painter.LineTo(vv);//滑下
                        painter.Stroke();
                        painter.ClosePath();
                        if (off > 0)
                        {
                            
                            rec.Add(pixel);
                            painter.BeginPath();
                            context.DrawText(FormatSecondsToMinuteSecondCached(realSecondPoint), vv, FontSize, new Color(0.86f, 0.89f, 0.94f, 0.92f));
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
                if (off > 0)
                    DrawEndBoundaryTick(painter, context, height, FontSize, off);
                
            }


        }

        private void DrawEndBoundaryTick(Painter2D painter, MeshGenerationContext context, float height, float fontSize, int off)
        {
            if (showEnd <= showStart)
                return;

            int pixel = Mathf.RoundToInt((showEnd - showStart) * pxPerSecond);
            if (pixel < 0)
                return;

            if (rec.Contains(pixel))
                return;

            float labelX = Mathf.Clamp(pixel - 30f, 0f, Mathf.Max(0f, contentRect.width - 36f));
            Vector2 tickTop = new Vector2(pixel, 0);
            Vector2 tickBottom = new Vector2(pixel, height);

            rec.Add(pixel);
            painter.BeginPath();
            painter.strokeColor = off > 1 ? levelup_down2 : levelup_down1;
            painter.lineWidth = off + 1;
            painter.MoveTo(tickTop);
            painter.LineTo(tickBottom);
            painter.Stroke();
            painter.ClosePath();

            context.DrawText(FormatSecondsToMinuteSecondCached(showEnd), new Vector2(labelX, height), fontSize, new Color(0.86f, 0.89f, 0.94f, 0.92f));
        }
        public static string FormatSecondsToMinuteSecondCached(float totalSeconds)
        {
            int key = Mathf.RoundToInt(Mathf.Max(0f, totalSeconds) * 60f);
            if (s_TimeLabelCache.TryGetValue(key, out string value))
                return value;

            value = FormatSecondsToMinuteSecond(key / 60f);
            if (s_TimeLabelCache.Count > 2048)
                s_TimeLabelCache.Clear();

            s_TimeLabelCache[key] = value;
            return value;
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
