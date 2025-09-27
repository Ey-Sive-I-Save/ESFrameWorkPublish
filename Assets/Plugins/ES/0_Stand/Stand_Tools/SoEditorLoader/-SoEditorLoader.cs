using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static UnityEditor.PlayerSettings;

namespace ES
{
    public class ESEditorSO
    {
        public static TypeKeyGroup<ESSO> SOS = new TypeKeyGroup<ESSO>();
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
                    ESEditorSO.SOS.TryAdd(soAsset.GetType(), soAsset);
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
#endif
    }

}