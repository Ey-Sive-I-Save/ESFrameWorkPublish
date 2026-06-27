using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES
{
    /// <summary>
    /// 用于在 Inspector 面板头部添加自定义扩展的基类。
    /// </summary>
    public abstract class ESEditorInspectorUser
    {
        /// <summary>
        /// 执行顺序（数值越小越先执行）。
        /// </summary>
        public virtual int Order { get; }

        /// <summary>
        /// 核心逻辑绘制入口。
        /// </summary>
        /// <param name="ob">当前选中的物体</param>
        /// <returns>true 会拦截后续扩展，false 则允许继续执行下一个扩展。</returns>
        public abstract bool Apply(UnityEngine.Object ob);
    }
}
