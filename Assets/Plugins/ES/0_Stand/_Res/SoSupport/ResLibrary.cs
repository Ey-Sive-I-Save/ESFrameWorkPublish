using ES;
using Sirenix.OdinInspector;
using Sirenix.Utilities;

#if UNITY_EDITOR
using Sirenix.Utilities.Editor;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ResLibrary
    /// 
    /// ScriptableObject 形式的资源库：
    /// - 继承 SoLibrary&lt;ResBook&gt;，一份 Library 可以包含多本 ResBook；
    /// - 通过 Inspector 决定是否参与构建、是否走远程下载；
    /// - 是上层“资源分组 / 逻辑分类”的载体，真正的 AB / 路径信息由 ResPage 决定。
    /// </summary>
    public class ResLibrary : LibrarySoBase<ResBook>
    {
        
        [LabelText("参与构建")]
        public bool ContainsBuild = true;
        [ESBoolOption("通过远程下载", "是本体库")]
        public bool IsNet = true;
        public override void OnEditorApply()
        {
            base.OnEditorApply();
            Refresh();
        }
        public override void Refresh()
        {
            if(LibFolderName.IsNullOrWhitespace())
            {
                LibFolderName = IESLibrary.DefaultLibFolderName;
            }
            //验证
            ESResMaster.TrySetResLibFolderName(this, LibFolderName,0);
            base.Refresh();
        }

    }

}

