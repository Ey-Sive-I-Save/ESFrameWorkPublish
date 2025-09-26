using ES;
using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ESTree_CreateMenu : ESTreeCollector
{
    #region 基本构造
    public override ESTreeCollectorName GlobalTreeName => ESTreeCollectorName.ESCreate;
    
    public override void InitTree()
    {
        base.InitTree();

        #region 复制粘贴剪切
        AddToolBarItem(SdfIconType.StickiesFill, () => {
            Debug.Log("AAA");
        });

        AddToolBarItem(SdfIconType.TrashFill, () => {
            Debug.Log("AAA");
        });


        AddToolBarItem(SdfIconType.Scissors, () => {
            Debug.Log("AAA");
        },true);
        #endregion

        #region 定位标记打包
        AddToolBarItem(SdfIconType.GeoAltFill, () => {
            AssetDatabase.ExportPackage(AssetDatabase.GetAssetPath(Selection.activeObject), "", ExportPackageOptions.Default | ExportPackageOptions.Recurse);

        });

        AddToolBarItem(SdfIconType.ShareFill, () => {
            Debug.Log("AAA");
        });


        AddToolBarItem(SdfIconType.Link45deg, () => {
            Debug.Log("AAA");
        },true);
        #endregion
    }

    #endregion

    [ESItemDefine(ESTreeCollectorName.ESCreate, "测试1")]
    public class ESCreateItem__Test1 : ESItem
    {
        public override void Click()
        {

        }
        [ESItemDefine(ESTreeCollectorName.ESCreate, "创建SO1", "测试1")]
        public class c11 : ESItem
        {
            public override void Click()
            {

            }
        }
    }
    [ESItemDefine(ESTreeCollectorName.ESCreate, "测试2")]
    public class ESCreateItem__Test2 : ESItem
    {
        public override void Click()
        {

        }
    }
    [ESItemDefine(ESTreeCollectorName.ESCreate, "测试3")]
    public class ESCreateItem__Test3 : ESItem
    {
        public override void Click()
        {

        }
    }

    [ESItemDefine(ESTreeCollectorName.ESCreate, "创建SO1", "测试3")]
    public class c11 : ESItem
    {
        public override void Click()
        {
            Debug.Log("AAAA");
        }
    }
}

