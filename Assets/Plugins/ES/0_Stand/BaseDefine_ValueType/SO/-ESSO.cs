using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public class ESSO : SerializedScriptableObject
    {
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
                ESEditorSO.SOS.TryAdd(this.GetType(), this);
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
