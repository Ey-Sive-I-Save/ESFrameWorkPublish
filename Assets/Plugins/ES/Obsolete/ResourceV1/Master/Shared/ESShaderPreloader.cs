using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Shader自动预热器
    /// 
    /// 核心理念：
    /// 1. 从GlobalAssetKeys自动发现所有ShaderVariantCollection
    /// 2. 直接加载AB包（不走ESResSource引用计数系统）
    /// 3. 调用WarmUp()预热，避免运行时卡顿（200-500ms）
    /// 4. Shader AB包保持常驻内存（通过不卸载AB实现）
    /// 
    /// 使用方式：
    /// - 在ESResMaster初始化时自动调用
    /// - 或手动调用 ESResMaster.WarmUpAllShaders()
    /// 
    /// ShaderVariantCollection创建流程：
    /// 1. Unity编辑器：Create > Shader Variant Collection
    /// 2. 运行游戏收集所有使用的Shader变体
    /// 3. 添加到ResLibrary的ShaderBook中
    /// 4. 构建AB包时自动包含
    /// 5. 启动时自动发现并预热
    /// </summary>
    public class ESShaderPreloader
    {
        private static List<AssetBundle> _loadedShaderBundles = new List<AssetBundle>();
        private static List<ShaderVariantCollection> _loadedCollections = new List<ShaderVariantCollection>();
        private static bool _isWarmedUp = false;

        /// <summary>
        /// 是否已完成预热
        /// </summary>
        public static bool IsWarmedUp => _isWarmedUp;

        /// <summary>
        /// 自动发现并预热所有ShaderVariantCollection
        /// 从GlobalAssetKeys中查找类型为ShaderVariantCollection的资源
        /// </summary>
        public static IEnumerator AutoWarmUpAllShaders(Action onComplete = null)
        {
            if (_isWarmedUp)
            {
                Debug.Log("[ESShaderPreloader] Shader已预热，跳过");
                onComplete?.Invoke();
                yield break;
            }

            Debug.Log("[ESShaderPreloader] 开始自动发现并预热Shader...");

            // 1. 从GlobalAssetKeys中查找所有ShaderVariantCollection
            var shaderKeys = FindAllShaderVariantCollectionKeys();
            
            if (shaderKeys.Count == 0)
            {
                Debug.LogWarning("[ESShaderPreloader] 未找到任何ShaderVariantCollection，请检查ResLibrary配置");
                onComplete?.Invoke();
                yield break;
            }

            Debug.Log($"[ESShaderPreloader] 找到 {shaderKeys.Count} 个ShaderVariantCollection");

            // 2. 直接加载AB包（不走ESResSource系统）
            int successCount = 0;
            foreach (var key in shaderKeys)
            {
                // 构建AB包路径
                string abPath = System.IO.Path.Combine(
                    ESResMaster.DefaultPaths.GetLocalABBasePath(key.LibFolderName),
                    key.ABPreName
                );

                if (!System.IO.File.Exists(abPath))
                {
                    Debug.LogWarning($"[ESShaderPreloader] AB包不存在: {abPath}");
                    continue;
                }

                // 加载AB包
                var abRequest = AssetBundle.LoadFromFileAsync(abPath);
                yield return abRequest;

                AssetBundle ab = abRequest.assetBundle;
                if (ab == null)
                {
                    Debug.LogError($"[ESShaderPreloader] 加载AB包失败: {abPath}");
                    continue;
                }

                // 从AB包中加载ShaderVariantCollection
                var assetRequest = ab.LoadAssetAsync<ShaderVariantCollection>(key.ResName);
                yield return assetRequest;

                ShaderVariantCollection collection = assetRequest.asset as ShaderVariantCollection;
                if (collection != null)
                {
                    // 预热Shader
                    Debug.Log($"[ESShaderPreloader] 预热: {collection.name} (Shaders: {collection.shaderCount}, Variants: {collection.variantCount})");
                    collection.WarmUp();

                    _loadedShaderBundles.Add(ab);
                    _loadedCollections.Add(collection);
                    successCount++;
                }
                else
                {
                    Debug.LogError($"[ESShaderPreloader] 从AB包加载资源失败: {key.ResName}");
                    ab.Unload(false);
                }
            }

            _isWarmedUp = true;
            Debug.Log($"[ESShaderPreloader] Shader预热完成: {successCount}/{shaderKeys.Count}");
            Debug.Log(GetStatistics());

            onComplete?.Invoke();
        }

        /// <summary>
        /// 从GlobalAssetKeys查找所有ShaderVariantCollection类型的资源
        /// </summary>
        private static List<ESResKey> FindAllShaderVariantCollectionKeys()
        {
            var result = new List<ESResKey>();

            if (ESResMaster.GlobalAssetKeys == null)
            {
                Debug.LogError("[ESShaderPreloader] GlobalAssetKeys未初始化");
                return result;
            }

            // 遍历所有资源键，查找ShaderVariantCollection
            foreach (var key in ESResMaster.GlobalAssetKeys.Values)
            {
                // 检查类型是否为ShaderVariantCollection
                if (key.TargetType == typeof(ShaderVariantCollection))
                {
                    result.Add(key);
                    Debug.Log($"[ESShaderPreloader] 发现ShaderVariantCollection: {key.ResName} (AB: {key.ABPreName})");
                }
            }

            return result;
        }

        /// <summary>
        /// 获取预热统计信息
        /// </summary>
        public static string GetStatistics()
        {
            int variantCount = 0;
            int shaderCount = 0;

            foreach (var collection in _loadedCollections)
            {
                if (collection != null)
                {
                    variantCount += collection.variantCount;
                    shaderCount += collection.shaderCount;
                }
            }

            return $"[ESShaderPreloader] 统计信息:\n" +
                   $"- 加载的AB包: {_loadedShaderBundles.Count}\n" +
                   $"- ShaderVariantCollection: {_loadedCollections.Count}\n" +
                   $"- Shader数量: {shaderCount}\n" +
                   $"- 变体总数: {variantCount}";
        }

        /// <summary>
        /// 清理资源（谨慎使用，会卸载Shader AB包）
        /// </summary>
        public static void Cleanup()
        {
            Debug.LogWarning("[ESShaderPreloader] 清理Shader AB包，可能导致材质变粉红色！");

            foreach (var ab in _loadedShaderBundles)
            {
                if (ab != null)
                {
                    ab.Unload(false);
                }
            }

            _loadedShaderBundles.Clear();
            _loadedCollections.Clear();
            _isWarmedUp = false;
        }
    }
}
