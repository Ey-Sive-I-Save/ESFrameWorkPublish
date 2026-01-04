
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    [ESCreatePath("常规SO编辑器支持", "虚拟GUI绘制SO")]
    public class VisualGUIDrawerSO : ESSO
    {    
        [SerializeReference,HideReferenceObjectPicker,HideLabel]
        public object drawerData;

        //public string s="";
    }
}

//ES已修正