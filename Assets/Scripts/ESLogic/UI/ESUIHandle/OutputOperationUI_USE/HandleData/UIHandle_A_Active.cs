using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES
{
    /*A  Active  系列 -》*/
    [Serializable, TypeRegistryItem("A活动-【GameObject】活动状态-(On/From)")]
    public class OutputOperationUI_Handle_ActiveUIGameObject : IOperationUI
    {
        [ESBoolOption("作用自己(om)", "作用于来源(from)")] public bool OnOrFrom = false;
        [LabelText("活动")] public bool active;
        public void TryOperation(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            var applyOn = (OnOrFrom ? from : on) ?? on ?? from;
            applyOn.gameObject.SetActive(active);
        }
        public void TryCancel(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {

        }
    }

    [Serializable, TypeRegistryItem("A活动-【GameObject】活动状态(手动引用)")]
    public class OutputOperationUI_Handle_ActiveReferGameObject : IOperationUI
    {
        [LabelText("作用游戏对象")] public GameObject gameObject;
        [LabelText("活动")] public bool active;
        [LabelText("可NotFlag")] public bool NotGlag = false;
        public void TryOperation(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            if (gameObject != null)
            {
                if (NotGlag)
                {
                    if (with is Link_UI_NotFlag flag)
                    {
                        gameObject.SetActive(!active);
                    }
                    else
                    {
                        gameObject.SetActive(active);
                    }
                }
                else gameObject.SetActive(active);
            }
        }
        public void TryCancel(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {

        }
    }

    [Serializable, TypeRegistryItem("A活动-【GameObject】活动状态-关闭(查名/Tag)")]
    public class OutputOperationUI_Handle_ActiveNamedOrTagGameObject : IOperationUI
    {
        [InfoBox("查询时，只能查询到正在活动的物体！！所以想从不活动到活动是不支持的-", InfoMessageType.Warning)]
        [ESBoolOption("查询名称", "查询标签")] public bool FalseNameOrTrueTag = false;
        [ShowIf("@FalseNameOrTrueTag")]
        [ESBoolOption("只作用一个对象", "全部的对象")] public bool FalseOneTrueAll = false;
        [LabelText("名字"), ValueDropdown("@ESDesignUtility.SafeEditor.GetAllTags()", AppendNextDrawer = true)] public string Name;
        public void TryOperation(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {

            if (FalseNameOrTrueTag)
            {
                if (FalseOneTrueAll)
                {
                    var gs = GameObject.FindGameObjectsWithTag(Name);
                    int count = gs.Length;
                    for (int i = 0; i < count; i++)
                    {
                        gs[i].SetActive(false);
                    }
                }
                else
                {
                    var g = GameObject.FindGameObjectWithTag(Name);
                    if (g != null)
                    {
                        g.SetActive(false);
                    }
                }

            }
            else
            {
                var g = GameObject.Find(Name);
                if (g != null)
                {
                    g.SetActive(false);
                }
            }
        }
        public void TryCancel(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {

        }
    }
}
