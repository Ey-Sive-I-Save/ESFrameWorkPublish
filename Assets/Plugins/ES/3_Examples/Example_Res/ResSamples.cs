using ES;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ES
{

    public class ResSamples : SerializedMonoBehaviour
    {
        [Title("资源加载示例")]
        [InfoBox("本示例演示加载一个依赖其他AB包(如材质)的预制件。\n无需手动加载依赖，ESResLoader会自动处理。")]

        [LabelText("资源路径 (Assets/...)")]
        public string AssetPath = "Assets/Gaskellgames/Audio Controller/Resources/Audio Controller/Audio Controller.prefab";

        // 独立的Loader实例，用于管理本次加载任务的生命周期
        private ESResLoader m_Loader = new ESResLoader();

        [Button("1. 异步加载预制件", ButtonSizes.Large)]
        private void LoadPrefabAsync()
        {
            if (string.IsNullOrEmpty(AssetPath))
            {
                Debug.LogError("请设置资源路径");
                return;
            }

            // 确保没有残留任务
            m_Loader.ReleaseAll();

            Debug.Log($"[ResSamples] 开始加载: {AssetPath}");

            // 添加加载任务
            // 参数1: 资源路径 (Project视图中的完整路径)
            // 参数2: 单个资源加载完成的回调
            m_Loader.AddAsset2LoadByPathSourcer(AssetPath, OnResLoaded);

            // 开始处理加载队列
            // 参数: 所有任务完成后的回调
            m_Loader.LoadAllAsync(() =>
            {
                // 演示：在所有加载完成后，通过路径获取资源并实例化
                if (ESResMaster.GlobalAssetKeys.TryGetByKey1(AssetPath, out var key))
                {
                    if (m_Loader.TryGetLoadedAsset(key, out UnityEngine.Object asset) && asset is GameObject prefab)
                    {
                        var go = Instantiate(prefab);
                        go.name = prefab.name + "_AllDone";
                        go.transform.position = Vector3.right * 2; // 错开位置
                        Debug.Log($"[ResSamples] 全部加载完毕，实例化演示: {go.name}");
                    }else
                    {
                        Debug.LogError("[ResSamples] 全部加载完毕，但未能通过路径获取资源");
                    }
                }
                else
                {
                    Debug.LogError("[ResSamples] 全部加载完毕，但未能通过路径获取资源键");
                }
                Debug.Log("[ResSamples] 队列中所有资源加载完毕");
            });
        }

        private void OnResLoaded(bool success, ESResSource source)
        {
            if (success && source.Asset != null)
            {
                Debug.Log($"[ResSamples] 资源加载成功: {source.ResName}");

                // 实例化预制件
                if (source.Asset is GameObject original)
                {
                    GameObject instance = Instantiate(original);
                    instance.name = $"{source.ResName}_Instance";
                    // 随机位置避免重叠
                    instance.transform.position = UnityEngine.Random.insideUnitSphere * 2;
                }
            }
            else
            {
                Debug.LogError($"[ResSamples] 资源加载失败: {source?.ResName ?? "Unknown"}");
            }
        }

        [Button("2. 卸载资源", ButtonSizes.Medium)]
        private void UnloadResources()
        {
            // 释放Loader持有的引用
            // 如果引用计数降为0，底层会自动卸载AssetBundle
            m_Loader.ReleaseAll();
            Debug.Log("[ResSamples] 已释放Loader资源引用");
        }

        private void OnDestroy()
        {
            // 销毁时确保释放资源，防止内存泄漏
            if (m_Loader != null)
            {
                m_Loader.ReleaseAll();
                m_Loader = null;
            }
        }
    }

    [Serializable]
    public class Vector222 : IDrawIMGUI
    {
        public float x;
        public float y;
        public void Editor_DrawIMGUI()
        {
#if UNITY_EDITOR
            x = EditorGUILayout.FloatField("X", x);
            y = EditorGUILayout.FloatField("Y", y);
#endif
        }
    }
}
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
            BindingPool.SetIntDirect(it.Key, 0, EnableSendLinkIfCreateNew: true);
            BindingPool.LinkRCL_Int.AddReceiver(it.Key, this);//开始监听
        }

    }
    public void CancelOrCompleteQuest(bool complete)
    {
        // complete 是否完成
        foreach (var it in Items)
        {
            //可以移除参数--或者置空
            BindingPool.LinkRCL_Int.RemoveReceiver(it.Key, this);//开始监听
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
            }
            ;
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

