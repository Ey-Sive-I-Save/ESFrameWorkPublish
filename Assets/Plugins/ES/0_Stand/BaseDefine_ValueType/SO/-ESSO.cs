using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    /// <summary>
    /// 轻量级 ScriptableObject 基类，作为项目内所有编辑器友好 SO 的共同父类。
    /// </summary>
    /// <remarks>
    /// - 功能：在编辑器初始化时将实例注册到编辑器索引（<c>ESEditorSO.SOS</c>），并提供编辑器专用的生命周期钩子：
    ///   <see cref="OnEditorInitialized"/> 与 <see cref="OnEditorApply"/>。
    /// - 使用场景：适用于需要在编辑器中集中管理并展示的配置数据（可继承并扩展）。
    /// - 运行时注意：该类主要用于编辑器支持；运行时若使用请显式获取实例引用并避免依赖 Editor API。
    /// </remarks>
    public class ESSO : SerializedScriptableObject
    {
        /*
         * 性能与使用注意：
         * - 不要在大量实例的构造/Enable 时执行昂贵操作；OnEditorInitialized 仅在编辑器首次访问时触发一次。
         * - 本类将实例注册到静态索引（ESEditorSO.SOS），此索引为全局结构，频繁修改可能影响编辑器性能。
         * - 若需要大量 SO 批量初始化或扫描，建议在后台分帧处理或延迟；避免在主线程内做阻塞 I/O。
         */
        #region 编辑器支持
#if UNITY_EDITOR
        [NonSerialized]
        private bool init_Editor = false;
        [NonSerialized]
        private bool apply_Editor = false;
#endif
        public virtual void OnEditorInitialized()
        {
#if UNITY_EDITOR
            if (!init_Editor)
            {
                init_Editor = true;
                ESEditorSO.SOS.Add(this.GetType(), this);
            }
#endif
        }
        public virtual void OnEditorApply()
        {

        }

        public void OnEnable()
        {
#if UNITY_EDITOR
            if (!init_Editor)
            {
                OnEditorInitialized();
                init_Editor = true;
            }

            if (!apply_Editor)
            {
                OnEditorApply();
                apply_Editor = true;
            }
#endif
        }

        #endregion
    }
}
