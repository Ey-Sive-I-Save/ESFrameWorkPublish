using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    [CreateAssetMenu(fileName = "全局编辑器流程基本配置",menuName = "全局SO/全局编辑器流程基本配置")]
    public class ESGlobalEditorDefaultConfi : ESEditorGlobalSo<ESGlobalEditorDefaultConfi>
    {
        
        [FolderPath,LabelText("默认的SOInfo脚本父文件夹")]
        public string Path_SoInfoParent;
        [FolderPath,LabelText("默认的DataPack包父文件夹")]
        public string Path_PackParent;
        [FolderPath, LabelText("默认的DataGroup组父文件夹")]
        public string Path_GroupParent;
    }
}
