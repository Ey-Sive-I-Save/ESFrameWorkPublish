using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public class ESEditorSO
    {
        public static TypeKeyGroup<ESSO> SOS = new TypeKeyGroup<ESSO>();
        public static BidirectionalDictionary<string,Type> AllSoNames=new BidirectionalDictionary<string, Type>();
    }
#if UNITY_EDITOR
    public class SoEditorIniter : EditorInvoker_Level0
    {
        public override void InitInvoke()
        {

            Debug.Log("此处注册ES-SO");
            ESEditorSO.SOS.Clear();
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(ESSO)}");

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                ESSO soAsset = AssetDatabase.LoadAssetAtPath<ESSO>(assetPath);
                if (soAsset != null)
                {
                    soAsset.OnEditorInitialized();
                }
            }

        }
    }
    
    public class SoEditorApplier : EditorInvoker_SoApply
    {

        public override void InitInvoke()
        {
            Debug.Log("此处应用ES-SO");
            var keys = ESEditorSO.SOS.Groups.Keys;
            foreach (var i in keys)
            {
                var group = ESEditorSO.SOS.GetGroup(i);
                foreach (var g in group)
                {
                    g.OnEditorApply();
                }
            }
        }
    }


    public class ER_So_SubClass : EditorRegister_FOR_AsSubclass<ScriptableObject>
    {
        public override int Order => 10;

        public override void Handle(Type SubClassType)
        {

            var Disname=SubClassType._GetTypeDisplayName();
                       
            ESEditorSO.AllSoNames.Add(Disname,SubClassType);
        }
    }
    
#endif

}