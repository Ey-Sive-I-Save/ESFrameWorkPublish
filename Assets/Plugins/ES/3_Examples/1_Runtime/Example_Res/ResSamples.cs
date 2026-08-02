using ES;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
#if UNITY_EDITOR
#if UNITY_EDITOR
using UnityEditor;
#endif
#endif
using UnityEngine;
using UnityEngine.UI;

namespace ES.Samples{

    public class ResSamples : SerializedMonoBehaviour
    {
        [Title("资源加载示例")]
        [InfoBox("本示例通过新版 ESAssetRefer / TemporaryLease 加载预制件。Bundle 依赖、缓存和释放由新版 Provider 链路处理。")]

        [LabelText("新版预制件引用")]
        public ESAssetReferPrefab Asset;

        private ESAssetTemporaryLease<GameObject> m_Lease;
        private CancellationTokenSource m_LoadCancellation;
        private int m_LoadGeneration;
        private bool m_Destroyed;

        [Button("1. 异步加载预制件", ButtonSizes.Large)]
        private async void LoadPrefabAsync()
        {
            if (Asset == null || !Asset.IsValid)
            {
                Debug.LogError("请设置有效的新版 ESAssetReferPrefab。");
                return;
            }

            m_LoadCancellation?.Cancel();
            m_LoadCancellation?.Dispose();
            m_LoadCancellation = new CancellationTokenSource();
            int generation = ++m_LoadGeneration;
            m_Lease.Dispose();
            m_Lease = default;
            try
            {
                ESAssetTemporaryLease<GameObject> lease = await Asset.LoadAsyncLease(m_LoadCancellation.Token);
                if (m_Destroyed || generation != m_LoadGeneration)
                {
                    lease.Dispose();
                    return;
                }

                m_Lease = lease;
                GameObject prefab = lease.Asset;
                if (prefab != null)
                {
                    GameObject instance = Instantiate(prefab);
                    instance.name = $"{prefab.name}_Instance";
                    instance.transform.position = UnityEngine.Random.insideUnitSphere * 2;
                    Debug.Log($"[ResSamples] 新版资源加载成功并实例化: {instance.name}");
                }
                else
                    Debug.LogError("[ResSamples] 新版资源加载完成但资产为空。");
            }
            catch (OperationCanceledException)
            {
                // 新请求或对象销毁会取消旧请求；不记录为加载故障。
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [Button("2. 卸载资源", ButtonSizes.Medium)]
        private void UnloadResources()
        {
            m_Lease.Dispose();
            m_Lease = default;
            Debug.Log("[ResSamples] 已释放新版 TemporaryLease。");
        }

        private void OnDestroy()
        {
            m_Destroyed = true;
            ++m_LoadGeneration;
            m_LoadCancellation?.Cancel();
            m_LoadCancellation?.Dispose();
            m_LoadCancellation = null;
            m_Lease.Dispose();
            m_Lease = default;
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
}

}
