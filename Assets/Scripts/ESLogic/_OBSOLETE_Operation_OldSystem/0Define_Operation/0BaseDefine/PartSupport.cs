/*
using DG.Tweening;
using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {

    /// <summary>
    /// 浮点数缓冲数据配置类
    /// 
    /// 【核心作用�?
    /// 为缓冲操作提供时间和曲线相关的配置参数，用于实现平滑的数值过渡效�?
    /// 
    /// 【典型应用场景�?
    /// �?血�?蓝条的平滑变化动�?
    /// �?UI数值的渐变显示效果
    /// �?游戏对象属性的缓动过渡
    /// �?技能冷却时间的可视化进�?
    /// 
    /// 【工作原理�?
    /// 通过AnimationCurve定义数值变化的时间函数，结合乘数和时间参数�?
    /// 实现从起始值到目标值的平滑过渡，而不是瞬间跳�?
    /// </summary>
    [Serializable, TypeRegistryItem("缓冲参数")]
    public class BufferDataFloat
    {
        /// <summary>
        /// 缓冲变化曲线
        /// 
        /// 【曲线定义�?
        /// �?X轴：标准化时间进�?(0.0 = 开�? 1.0 = 结束)
        /// �?Y轴：数值变化进�?(通常0.0-1.0，表示从起始值到目标值的插值比�?
        /// 
        /// 【默认设置�?
        /// AnimationCurve.Constant(0, 1, 1) 创建一个常量曲线，Y值始终为1
        /// 这意味着没有渐变过程，直接跳到目标�?
        /// 
        /// 【常用曲线类型�?
        /// �?线性过渡：AnimationCurve.Linear(0,0,1,1) - 匀速变�?
        /// �?缓入效果：开始慢，后面快 
        /// �?缓出效果：开始快，后面慢
        /// �?缓入缓出：两端慢，中间快，S形曲�?
        /// �?弹性效果：带有超调和回弹的曲线
        /// 
        /// 【编辑器使用�?
        /// 在Inspector中可以可视化编辑曲线，实时预览动画效�?
        /// </summary>
        [LabelText("缓冲曲线")] 
        public AnimationCurve curve = AnimationCurve.Constant(0, 1, 1);
        
        private void SelectCurve(){
            
        }

        /// <summary>
        /// 缓冲效果强度乘数
        /// 
        /// 【作用机制�?
        /// 对曲线计算出的结果值进行缩放，用于控制缓冲效果的强�?
        /// 最终效果�?= curve.Evaluate(time) * mutipler
        /// 
        /// 【数值含义�?
        /// �?1.0：标准强度，按曲线原样执�?
        /// �?>1.0：放大效果，变化更剧�?
        /// �?0.0-1.0：减弱效果，变化更温�?
        /// �?0.0：无效果，相当于禁用缓冲
        /// �?负值：反向效果，用于特殊的反向动画
        /// 
        /// 【使用场景�?
        /// �?同一个曲线配置，通过调整乘数实现不同强度的效�?
        /// �?动态调整缓冲强度，如根据游戏难度调整动画速度
        /// �?实现淡入淡出效果的强度控�?
        /// </summary>
        [LabelText("乘数")] 
        public float mutipler = 1;
        
        /// <summary>
        /// 缓冲效果总持续时间（单位：秒�?
        /// 
        /// 【时间控制�?
        /// 定义整个缓冲动画从开始到结束的总时间长�?
        /// 
        /// 【数值说明�?
        /// �?>0：正常的缓冲时间，数值越大动画越�?
        /// �?1.0：默�?秒完成整个缓冲过�?
        /// �?0.0：瞬时完成，没有缓冲效果
        /// �?极小�?接近0)：非常快的缓冲，几乎瞬间完成
        /// 
        /// 【与曲线的关系�?
        /// curve的X�?0-1)会被映射到实际的时间区间(0-needTime)
        /// 例如：needTime=2秒时，curve�?.5对应实际�?秒时�?
        /// 
        /// 【性能考虑�?
        /// �?时间过短可能导致动画不流�?
        /// �?时间过长可能影响游戏的响应�?
        /// �?建议根据60fps来设计，确保有足够的帧数表现动画
        /// </summary>
        [LabelText("总时�?)] 
        public float needTime = 1;
    }
    
}

*/