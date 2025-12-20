using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    /*M Message 系列 -》*/
    [Serializable, TypeRegistryItem("M信息-【Message】提交自己的Message到父Panel")]
    public class OutputOperationUI_Handle_MessageSubmitToPanel : IOutputOperationUI
    {
        
        public void TryOperation(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            on.MyPanel.MessagevalueEntryDomain.SetMainMessagevalueEntry(on.MessagevalueEntryDomain.GetMainMessagevalueEntry());
        }
        public void TryCancel(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {

        }

    }
    [Serializable, TypeRegistryItem("M 信息-【Message】提交自己的Message到From")]
    public class OutputOperationUI_Handle_SubmitMessageToFrom : IOutputOperationUI
    {

        public void TryOperation(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            on.MyPanel.MessagevalueEntryDomain.SetMainMessagevalueEntry(on.MessagevalueEntryDomain.GetMainMessagevalueEntry());
        }
        public void TryCancel(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {

        }

    }
}
