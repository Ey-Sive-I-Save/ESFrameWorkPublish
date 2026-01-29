using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ES;

/// <summary>
/// 游戏启动示例 - Shader自动预热演示
/// 
/// 使用场景：
/// 1. 游戏启动时自动预热所有Shader变体，避免运行时卡顿
/// 2. 无需手动配置，系统自动发现所有ShaderVariantCollection
/// 3. 显示预加载进度和统计信息
/// 
/// 注意：
/// - Shader预热会在ESResMaster初始化完成后自动执行
/// - 本示例仅演示如何查看预热状态和统计信息
/// </summary>
public class GameBootstrapExample : MonoBehaviour
{
    [Header("UI引用")]
    public Text statusText;
    
    void Start()
    {
        StartCoroutine(BootstrapSequence());
    }
    
    IEnumerator BootstrapSequence()
    {
        // 1. 显示启动信息
        UpdateStatus("初始化游戏系统...");
        yield return new WaitForSeconds(0.5f);
        
        // 2. 等待Shader预热完成（自动执行，无需手动调用）
        UpdateStatus("等待Shader预热完成...");
        
        while (!ESResMaster.IsShadersWarmedUp())
        {
            yield return null;
        }
        
        // 3. 显示预热统计信息
        UpdateStatus("Shader预热完成！");
        Debug.Log(ESResMaster.GetShaderStatistics());
        yield return new WaitForSeconds(1f);
        
        // 4. 继续游戏初始化
        UpdateStatus("加载游戏资源...");
        yield return new WaitForSeconds(0.5f);
        
        // 5. 完成，进入主菜单
        UpdateStatus("完成！");
        yield return new WaitForSeconds(0.5f);
        
        // 加载主菜单场景
        // UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        Debug.Log("游戏启动完成！");
    }
    
    void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"[GameBootstrap] {message}");
    }
}
