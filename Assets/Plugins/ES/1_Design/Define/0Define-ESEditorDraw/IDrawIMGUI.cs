using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    //非常简单的接口，用来辅助实现自定义IMGUI绘制的
    public interface IDrawIMGUI 
    {
        public void Editor_DrawIMGUI();
    }

    //处理一类绘制需求
    public class DrawIMGUISolver
    {
        public virtual void Draw() { }
    }
}

