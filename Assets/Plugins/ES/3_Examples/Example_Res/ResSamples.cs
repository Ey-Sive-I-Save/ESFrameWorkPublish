using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ES
{
    [Serializable]
    public class QuestCore : IReceiveChannelLink_Context_Int
    {
        public ContextPool BindingPool;
        public List<QuestItem> Items = new List<QuestItem>() {
         new QuestItem(){ Key="击杀怪物数量",target=10 },
          new QuestItem(){ Key="拾取木头",target=100 },
           new QuestItem(){ Key="通关第一关",target=1 }
        };
        public List<Text> TargetText;


        public void StartQuest(ContextPool pool)
        {
            if (pool != null) BindingPool = pool;
            foreach (var it in Items)
            {
                //如果没有会创建
                BindingPool.SetIntDirect(it.Key, 0, EnableSendLinkIfCreateNew : true);
                BindingPool.LinkRCL_Int.AddReceive(it.Key, this);//开始监听
            }

        }
        public void CancelOrCompleteQuest(bool complete)
        {
            // complete 是否完成
            foreach (var it in Items)
            {
                //可以移除参数--或者置空
                BindingPool.LinkRCL_Int.RemoveReceive(it.Key, this);//开始监听
            }
        }
        //接受监听
        public void OnLink(string channel, Link_ContextEvent_IntChange link)
        {
            string keyName = channel;//哪一个发生变更
            int preValue = link.Value_Pre;//过去的值
            int newValue = link.Value_Now;//新的值
            Refresh();
        }

        public void Refresh()
        {
            /*
             可以进行UI等刷新
             */
            bool com = true;
            int index = 0;
            foreach (var it in Items)
            {
                var text = TargetText[index];

                text.text = it.Key + "：" + BindingPool.GetInt(it.Key) + "/" + it.target;

                if (BindingPool.GetInt(it.Key) < it.target)
                {
                    //有的没达到目标 -- 还没完成
                    com = false;
                };
                index++;
            }
            //完成
            if (com) CancelOrCompleteQuest(true);
        }
    }
    [Serializable]
    public class QuestItem
    {
        public string Key;
        public int target = 1;
    }

    public class ResSamples : SerializedMonoBehaviour
    {
        public ContextPool pool;
        public QuestCore QuestCore;
        public List<Text> texts = new List<Text>();
        private void Awake()
        {
            pool.Init();
        }
        private void OnEnable()
        {
            pool.Enable();
            QuestCore.TargetText = texts;
            QuestCore.StartQuest(pool);
            QuestCore.Refresh();
        }
        private void OnDisable()
        {
            pool.Disable();
        }

        /* [HideInInspector]
         public List<Vector222> v22s = new List<Vector222>();

         public PagedListDrawSolver<Vector222> drawer = new PagedListDrawSolver<Vector222>();

         [OnInspectorGUI]
         public void draw()
         {
             drawer.Init(v22s,5);
             drawer.Draw();
         }*/


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

