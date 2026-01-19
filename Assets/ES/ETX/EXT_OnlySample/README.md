# ES Framework Sample Package (OnlySamples)

这是一个完整的示例包，用于演示ES框架安装器的各种功能和配置选项。

## 包内容

### Scripts (脚本)
- `SampleScript.cs`: 基础MonoBehaviour示例，展示移动、跳跃和碰撞检测
- `SampleUIScript.cs`: UI交互示例，使用Unity UI和TextMesh Pro

### Prefabs (预制件)
- `SampleCube.prefab`: 包含SampleScript的立方体预制件

### Textures (纹理)
- `SampleTexture.png`: 示例纹理文件

### Scenes (场景)
- `SampleScene.unity`: 演示所有功能的完整场景

## 依赖项

### Unity官方包
- **TextMesh Pro**: 高级文本渲染系统
- **Unity UI**: 用户界面基础系统

### Git包
- **DOTween**: 动画引擎
- **NaughtyAttributes**: Inspector属性扩展

### 用户包
- **Custom User Package**: 需要手动下载的自定义包
- **Third Party Plugin**: 需要从Asset Store购买的插件

### 资产文件依赖
- **Sample Texture**: 示例纹理文件
- **Sample Script**: 示例脚本文件

## 安装说明

1. 通过ES安装器安装此包
2. 安装器会自动检查和安装所有必需的依赖
3. 安装完成后，查看Assets/OnlySamples文件夹
4. 打开SampleScene.unity测试功能

## 使用示例

```csharp
// 使用SampleScript
SampleScript sample = GetComponent<SampleScript>();
sample.ChangeColor(Color.red);
StartCoroutine(sample.DelayedAction(2.0f));

// 使用SampleUIScript
SampleUIScript uiScript = GetComponent<SampleUIScript>();
uiScript.ButtonClickedEvent += OnButtonClicked;
StartCoroutine(uiScript.BlinkText(5.0f));
```

## 注意事项

- 这个包仅用于演示目的
- 所有依赖项都可以设置为可选或必需
- 可以根据需要修改package.json配置
- 示例代码包含详细注释，便于学习

## 作者信息

- **作者**: ES Framework Team
- **网站**: https://esframework.com
- **许可证**: MIT