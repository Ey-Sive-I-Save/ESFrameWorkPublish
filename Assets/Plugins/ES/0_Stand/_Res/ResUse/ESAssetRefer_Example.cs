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
        if (!iconSprite.TryApplyToImage(image))
            await iconSprite.ApplyToImageAsync(image, this);

        AudioSource source = GetComponent<AudioSource>();
        if (source != null)
            await bgm.PlayAsync(source, this);
    }
}

[CreateAssetMenu(fileName = "EnemyData", menuName = "Game/Enemy Data")]
public sealed class EnemyDataConfig : ScriptableObject
{
    public ESAssetReferPrefab prefab;
    public ESAssetReferSprite icon;
    public ESAssetReferAudioClip spawnSound;
    public int health = 100;
    public float speed = 5f;

    public async UniTask PreloadAsync()
    {
        await prefab.PreloadAsync();
        await icon.PreloadAsync();
        await spawnSound.PreloadAsync();
    }
}
