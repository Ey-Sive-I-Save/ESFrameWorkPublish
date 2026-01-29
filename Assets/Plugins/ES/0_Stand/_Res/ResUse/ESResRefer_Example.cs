using ES;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ESResRefer 使用示例 - 简化版
/// ES 资源系统的便捷引用工具，依赖 ESResLoader 和 ESResSource 完成加载
/// </summary>
public class ESResReferExample : MonoBehaviour
{
    [Header("@ ES资源引用 - 在Inspector中拖拽资源")]
    public ESResReferPrefab enemyPrefab;
    public ESResReferSprite iconSprite;
    public ESResReferAudioClip bgm;
    public ESResReferMat material;

    // 如果需要批量加载，使用共享的 Loader
    private ESResLoader sharedLoader;

    void Start()
    {
        // 初始化共享 Loader（可选，不传则使用全局 Loader）
        sharedLoader = new ESResLoader();

        // 示例：异步加载并实例化
        Example_AsyncLoad();
    }

    /// <summary>
    /// 示例1：基础异步加载（使用全局 Loader）
    /// </summary>
    void Example_AsyncLoad()
    {
        enemyPrefab.LoadAsync((success, prefab) =>
        {
            if (success)
            {
                Instantiate(prefab, transform);
                Debug.Log("加载并实例化成功");
            }
        });
    }

    /// <summary>
    /// 示例2：使用指定 Loader 加载（推荐）
    /// </summary>
    void Example_LoadWithSharedLoader()
    {
        iconSprite.LoadAsync(sharedLoader, (success, sprite) =>
        {
            if (success)
            {
                GetComponent<SpriteRenderer>().sprite = sprite;
            }
        });
    }

    /// <summary>
    /// 示例3：批量加载多个资源（推荐方式）
    /// </summary>
    void Example_BatchLoad()
    {
        var loader = new ESResLoader();
        
        // 使用同一个 Loader 加载多个资源，设置 autoStartLoading=false 避免立即触发
        enemyPrefab.LoadAsync(loader, (s, p) => Debug.Log("Enemy loaded"), autoStartLoading: false);
        iconSprite.LoadAsync(loader, (s, i) => Debug.Log("Icon loaded"), autoStartLoading: false);
        bgm.LoadAsync(loader, (s, a) => Debug.Log("Audio loaded"), autoStartLoading: false);
        
        // 最后统一触发加载
        loader.LoadAllAsync(() =>
        {
            Debug.Log("所有资源加载完成！");
        });
    }

    /// <summary>
    /// 示例4：便捷方法 - 直接实例化
    /// </summary>
    void Example_InstantiateHelper()
    {
        enemyPrefab.InstantiateAsync((go) =>
        {
            if (go != null)
            {
                go.transform.position = Vector3.zero;
            }
        }, parent: transform, loader: sharedLoader);
    }

    /// <summary>
    /// 示例5：Sprite 便捷方法 - 应用到 Image
    /// </summary>
    void Example_SpriteHelper()
    {
        var image = GetComponent<Image>();
        iconSprite.ApplyToImage(image, sharedLoader, () =>
        {
            Debug.Log("Sprite已应用到Image");
        });
    }

    /// <summary>
    /// 示例6：音频便捷方法
    /// </summary>
    void Example_AudioHelper()
    {
        var audioSource = GetComponent<AudioSource>();
        bgm.Play(audioSource, sharedLoader, () =>
        {
            Debug.Log("音频开始播放");
        });
    }

    /// <summary>
    /// 示例7：async/await 方式
    /// </summary>
    async void Example_AsyncAwait()
    {
        var prefab = await enemyPrefab.LoadAsyncTask(sharedLoader);
        if (prefab != null)
        {
            Instantiate(prefab);
        }
    }

    /// <summary>
    /// 示例8：同步加载（仅必要时使用，会卡顿）
    /// </summary>
    void Example_SyncLoad()
    {
        if (enemyPrefab.LoadSync(sharedLoader, out var prefab))
        {
            Instantiate(prefab);
        }
    }

    /// <summary>
    /// 示例9：获取已加载的资源（从ES系统查询）
    /// </summary>
    void Example_GetLoadedAsset()
    {
        var prefab = enemyPrefab.GetLoadedAsset();
        if (prefab != null)
        {
            // 资源已加载，可以直接使用
            Instantiate(prefab);
        }
    }

    /// <summary>
    /// 示例10：验证资源
    /// </summary>
    void Example_Validate()
    {
        if (enemyPrefab.Validate())
        {
            Debug.Log("资源引用有效");
        }
        else
        {
            Debug.LogError("资源引用无效，可能资源已被删除");
        }
    }

    void OnDestroy()
    {
        // ESResRefer 不需要手动释放，ES 资源系统会自动管理引用计数
        // 如果使用了共享 Loader，可以选择性地清理
        sharedLoader?.ReleaseAll(resumePooling: false);
    }
}

/// <summary>
/// 最佳实践：在 ScriptableObject 中使用 ESResRefer
/// </summary>
[CreateAssetMenu(fileName = "EnemyData", menuName = "Game/Enemy Data")]
public class EnemyDataConfig : ScriptableObject
{
    [Header("@ ES资源引用")]
    public ESResReferPrefab prefab;
    public ESResReferSprite icon;
    public ESResReferAudioClip spawnSound;
    
    [Header("属性")]
    public int health = 100;
    public float speed = 5f;

    /// <summary>
    /// 加载所有资源（优化版）
    /// </summary>
    public void LoadAll(ESResLoader loader, System.Action onComplete)
    {
        // 批量加载，避免立即触发
        prefab.LoadAsync(loader, (s, p) => { }, autoStartLoading: false);
        icon.LoadAsync(loader, (s, i) => { }, autoStartLoading: false);
        spawnSound.LoadAsync(loader, (s, a) => { }, autoStartLoading: false);
        
        // 统一触发加载
        loader.LoadAllAsync(onComplete);
    }
}
