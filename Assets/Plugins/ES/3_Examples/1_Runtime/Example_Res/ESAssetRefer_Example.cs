using Cysharp.Threading.Tasks;
using ES;
using UnityEngine;
using UnityEngine.UI;

/// <summary>ESAssetRefer 新版统一用法：同步只查 Ready 缓存，未就绪时使用 UniTask。</summary>
public sealed class ESAssetReferExample : MonoBehaviour
{
    [Header("@ ES资源引用")]
    public ESAssetReferPrefab enemyPrefab;
    public ESAssetReferSprite iconSprite;
    public ESAssetReferAudioClip bgm;

    private async UniTaskVoid Start()
    {
        GameObject prefab = await enemyPrefab.LoadAsync(this);
        Instantiate(prefab, transform);

        Image image = GetComponent<Image>();
        if (!iconSprite.TryApplyToImage(image, this))
            await iconSprite.ApplyToImageAsync(image, this);

        AudioSource source = GetComponent<AudioSource>();
        if (source != null)
            await bgm.PlayAsync(source, this);
    }
}

[CreateAssetMenu(fileName = "EnemyData", menuName = "【ES】/示例/资源/Enemy Data")]
public sealed class EnemyDataConfig : ScriptableObject
{
    public ESAssetReferPrefab prefab;
    public ESAssetReferSprite icon;
    public ESAssetReferAudioClip spawnSound;
    public int health = 100;
    public float speed = 5f;

    /// <summary>把这组敌人资源预热并绑定到明确的运行时 Owner，而不是放入全局 Resident。</summary>
    public async UniTask PreloadForOwnerAsync(Component owner)
    {
        if (owner == null) throw new System.ArgumentNullException(nameof(owner));
        await prefab.LoadAsync(owner);
        await icon.LoadAsync(owner);
        await spawnSound.LoadAsync(owner);
    }
}
