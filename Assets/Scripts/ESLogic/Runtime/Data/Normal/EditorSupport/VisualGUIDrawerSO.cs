
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    [ESCreatePath("常规SO编辑器支持", "虚拟GUI绘制SO")]
    [ESOnlyEditorSO("虚拟GUI绘制 SO 只服务编辑器显示和调试，不应进入运行时构建或AB资源包。")]
    public class VisualGUIDrawerSO : ESSO
    {    
        [SerializeReference,HideReferenceObjectPicker,HideLabel]
        public object drawerData;

        //public string s="";
    }
}

//ES已修正
