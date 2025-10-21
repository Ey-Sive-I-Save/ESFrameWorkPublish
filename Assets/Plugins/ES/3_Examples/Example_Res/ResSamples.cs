using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{

    public class ResSamples : MonoBehaviour
    {
        [HideInInspector]
        public List<Vector222> v22s = new List<Vector222>();

        public PagedListDrawSolver<Vector222> drawer = new PagedListDrawSolver<Vector222>();

        [OnInspectorGUI]
        public void draw()
        {
            drawer.Init(v22s,5);
            drawer.Draw();
        }


        /* 
        public ESResRefer res蓝色;
        public ESResReferPrefab res黄色;
        public ESResRefer res黑色;
        [LabelText("路径")]
        public string path;

        public ESResLoader loader = new ESResLoader();

        [Button("加载测试·0")]
        private void StartLoad0()
        {
            //调用loader的AssetPath加载方式
            loader.AddAsset2LoadByPathSourcer(path, (b, o) =>
            {
                if (b)
                {
                    if (o.Asset is GameObject g)
                    {
                        Instantiate(g, Vector3.left, Quaternion.identity);
                    }
                }
            });
            loader.LoadAllAsync();
        }

        [Button("加载测试·1")]
        private void StartLoad()
        {
            //调用loader的GUID加载方式
            loader.AddAsset2LoadByGUIDSourcer(res蓝色.GUID, (b, o) =>
            {
                if (b)
                {
                    if (o.Asset is GameObject g)
                    {
                        Instantiate(g, Vector3.down, Quaternion.identity);
                    }
                }
            });
            loader.LoadAllAsync();
        }
        [Button("加载测试·2")]
        private void StartLoad2()
        {
            //Refer 异步加载
            res黄色.TryLoadByLoaderASync(loader, (prefab) => { Instantiate(prefab, default, Quaternion.identity); });
        }

        [Button("加载测试·3")]
        private void StartLoad3()
        {
            //Refer 同步加载
            if (res黑色.TryLoadByLoaderSync(loader, out var prefab))
            {
                Instantiate(prefab, Vector3.up, Quaternion.identity);
            }
        }*/
    }

    [Serializable]
    public class Vector222 : IDrawIMGUI
    {
        public float x;
        public float y;
        public void Editor_DrawIMGUI()
        {
            x = EditorGUILayout.FloatField("X", x);
            y = EditorGUILayout.FloatField("Y", y);
        }
    }
}

