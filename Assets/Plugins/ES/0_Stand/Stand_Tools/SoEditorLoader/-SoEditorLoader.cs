using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static UnityEditor.PlayerSettings;

namespace ES
{
    public class ESEditorSo
    {
        public static KeyGroup<Type, ESSO> SOS = new KeyGroup<Type, ESSO>();
    }
#if UNITY_EDITOR
    public class SoEditorIniter : EditorInvoker_Level0
    {
        public override void InitInvoke()
        {

            Debug.Log("此处注册ES-SO");
            ESEditorSo.SOS.Clear();
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(ESSO)}");

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                ESSO soAsset = AssetDatabase.LoadAssetAtPath<ESSO>(assetPath);
                if (soAsset != null)
                {
                    ESEditorSo.SOS.TryAdd(soAsset.GetType(), soAsset);
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
            var keys = ESEditorSo.SOS.Groups.Keys;
            foreach (var i in keys)
            {
                var group = ESEditorSo.SOS.GetGroup(i);
                foreach (var g in group)
                {
                    g.OnEditorApply();
                }
            }
        }
#endif
    }

}