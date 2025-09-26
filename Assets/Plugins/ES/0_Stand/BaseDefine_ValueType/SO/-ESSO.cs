using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public class ESSO : SerializedScriptableObject
    {
        #region 编辑器支持
#if UNITY_EDITOR
        private bool init_Editor = false;
        private bool apply_Editor = false;
#endif
        public virtual void OnEditorInitialized()
        {

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
