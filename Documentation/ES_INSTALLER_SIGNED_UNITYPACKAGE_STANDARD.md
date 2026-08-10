# ES Installer：已签名 `.unitypackage` 交付标准

状态：现行供应链门禁规范。开发主线已建立，生产信任根、事务恢复和真实安装矩阵仍在验证。

最后验证：2026-08-10。

适用源码入口：

- `Assets/Plugins/ES/Editor/Installer/ESInstaller.cs`
- `Assets/Plugins/ES/Editor/Installer/ESInstallerPackageTrust.cs`
- `Assets/Plugins/ES/Editor/Installer/Downloads/Main/package.json`
- `Assets/Plugins/ES/Editor/ESMenuTreeWindow/SimpleToolsWindow/AssetsTools/Simple_AssetTool_Page_UnityPackageTool.cs`
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/GlobalEditorData/ESGlobalEditorDefaultConfi.cs`

## AI 快速升级备注

后续 AI 要把可安装主包升级到当前源码状态时，只走以下唯一主链：

```text
显式资产白名单
  -> 导出 .unitypackage
  -> 生成签名清单
  -> Assets/Plugins/ES/Editor/Installer/Downloads/Main
  -> 旧 ESInstaller 预览和验签
  -> AssetDatabase.ImportPackage
  -> Assets/Plugins/ES
```

职责固定如下：

- UnityPackage 工具只负责白名单预览、导出、签名和把发布物提升到 `Downloads/Main`。
- 旧 `ESInstaller` 是唯一安装入口，负责依赖检查、影响预览、验签、暂存、导入和回执。
- `Assets/Plugins/ES` 是框架正式安装根；不得把 UPM、`Packages/com.esframework.*` 或新的 FrameworkPackage 服务恢复为主路径。
- 未经用户明确要求“生成或替换正式安装包”，AI 只能更新源码、依赖配置和发布配置，不得执行发布按钮或替换 `Downloads/Main` 现有发布物。

### 最短升级步骤

1. 先审查工作树，只处理本次明确相关文件；禁止 `reset`、`checkout`、`clean`、擅自提交或整理其他窗口改动。
2. 更新 `Downloads/Main/package.json`：提升 `version`，同步 Unity、Git、手动商业依赖和 `checkClass`。Git 依赖必须固定完整 40 位 commit；商业资产只声明检查，不打进主包。
3. 更新 `ESGlobalEditorDefaultConfi.PackagePublishAssetPaths`：只加入确实属于框架且目标工程必须拥有的显式 `Assets/` 根。当前基线为 `Assets/Plugins/ES`、`Assets/Scripts/ESLogic`、`Assets/KinematicCharacterController`。
4. 保持固定排除：`Assets/Plugins/ES/Obsolete` 和 `Assets/Plugins/ES/Editor/Installer/Downloads`。预览与实际导出必须消费同一白名单和同一排除规则。
5. 先验证 `ESInstaller.csproj`、`ES_Editor.csproj`、`ES_Stand.csproj`，再让当前 Unity 工程完成脚本导入和 Domain Reload。生成工程编译不能替代 Unity 编译。
6. 只有获得发布授权后，才在 UnityPackage 工具中执行正式发布。发布工具应先输出到 `ES/Output/UnityPackages`，再调用旧 Installer 的固定发布入口生成清单并原子提升到 `Downloads/Main`。
7. 在旧安装管理器中刷新状态，核对包版本、签名 keyId、文件 Size/SHA-256 和影响预览，再发起导入。只有 `ImportPackageCompleted` 回执和随后成功的 Domain Reload 才能证明导入完成。
8. 保存发布物路径、清单 Hash、签名 keyId、导入回执和 Unity 日志。没有真实导入、ReloadDomain、自检与恢复证据时，状态保持 `Implemented / Verifying`。

### 配置入口

- 主包版本和依赖：`Assets/Plugins/ES/Editor/Installer/Downloads/Main/package.json`，或安装器内“编辑主包依赖清单”。保存后 JSON 必须继续保持扁平 Main schema。
- 正式发布白名单：`ESGlobalEditorDefaultConfi.PackagePublishAssetPaths`，可从 UnityPackage 工具的“定位发布配置”进入。
- 普通导出排除项：各 `UnityPackageConfig.ExcludeFolders`；它不替代正式发布的固定排除规则。
- 安装状态刷新：`【ES】/安装与集成/安装管理器` 内显式点击“刷新状态”。首开和默认启动路径不得扫描项目或访问外部依赖源。

### 禁止绕路

- 禁止另建第二套安装状态机、第二个安装窗口或第二套 Ownership/Journal 权威。
- 禁止把 UPM 探针、v5 file fixture、资源下载测试或旧 FrameworkPackage 测试当作 Assets 安装证据。
- 禁止把 `Installer/Downloads`、`Obsolete`、私钥、商业插件或生成缓存打入 `.unitypackage`。
- 禁止使用浮动 Git 分支、短 commit、未固定 tag 或依赖本机 scoped registry 的隐式版本。
- 禁止在首开、Domain Reload、`InitializeOnLoad` 或普通 `OnGUI` 中执行 `AssetDatabase.FindAssets`、PackageManager 列表请求或磁盘全量扫描。
- 禁止仅凭 `ES.ESResMaster` 存在就宣称安装闭环。它目前只是粗安装标记；正式结论还要包含包版本、签名清单、文件 Hash、导入回执和 ReloadDomain 证据。
- 禁止把 `.csproj` 编译、清单生成或开发签名升级为生产发布结论。

### 当前成熟度口径

```text
Development Mainline Established / Implemented / Verifying
Development Signing and Import Path Verifying
Production Trust Root and Recovery Pending
```

在事务备份、Ownership、Operation Journal、安全回滚、生产公钥和真实目标工程安装矩阵完成前，不得标记 `Stable`、正式发布闭环或商业级交付。

## 可信根

安装器通过 `ESInstallerPackageTrust.TryGetTrustedRsaPublicKey()` 解析受信公钥。生产公钥必须以 `keyId -> RSA 公钥 XML` 形式编译进 `ProductionPublicKeys`；不得从 Downloads、`package.json`、清单或网络响应动态增加生产公钥。私钥只能保存在发布责任人的受控签名环境，禁止写入 Unity 工程、安装包或日志。

本机开发签名使用 `es-local-dev`：私钥位于 `%LOCALAPPDATA%/ESFramework/InstallerSigning`，项目侧开发公钥位于 `Library/ESInstaller/TrustRoots`。它只用于本机开发验证，不能作为生产信任根。生产签名通过 `ES_INSTALLER_SIGNING_KEY_ID` 和 `ES_INSTALLER_SIGNING_PRIVATE_KEY_PATH` 显式配置；仓库不得包含生产私钥。

当前仓库未配置生产公钥。未知 keyId、未签名包、Hash 不匹配或清单外 `.unitypackage` 必须 fail-closed 拒绝。

## 清单位置和格式

每个安装包目录都必须包含唯一的：

```text
es-unitypackage.manifest.json
```

同一目录内的全部、且仅有的 `.unitypackage` 必须逐一出现在 `artifacts`。不允许子目录、绝对路径、`..`、重复名称或未声明包。

```json
{
  "schemaVersion": 1,
  "keyId": "es-release-2026",
  "packageId": "es_main",
  "packageVersion": "1.4.0",
  "source": "https://releases.example.com/es/1.4.0",
  "artifacts": [
    {
      "relativePath": "ESFramework-1.4.0.unitypackage",
      "size": 123456,
      "sha256": "64 位小写十六进制 SHA-256"
    }
  ],
  "signature": "RSA/SHA-256 签名的 Base64"
}
```

`packageId` 与 `packageVersion` 必须与安装器读取的包配置完全一致；`source` 是可追溯发布来源，不能为空。

## 签名载荷（canonical UTF-8）

签名不包含 `signature` 字段。`artifacts` 按 `relativePath` 的 Ordinal 顺序排序；每一行结尾为 LF（`\n`）：

```text
ESInstaller.UnityPackageManifest
schemaVersion=1
keyId=<keyId>
packageId=<packageId>
packageVersion=<packageVersion>
source=<source>
artifactCount=<count>
artifact[0]
relativePath=<relativePath>
size=<size>
sha256=<lowercase sha256>
```

多 artifact 按 `artifact[0]`、`artifact[1]` 继续。字段不得含控制字符、前后空白或换行。算法为 `RSA PKCS#1 v1.5 + SHA-256`，公钥格式为 .NET `RSAKeyValue` XML；这是为 Unity 2022 Editor 的跨平台兼容性选择，后续算法迁移必须增加新 schemaVersion，不能静默改变 v1 含义。

## 安装时强制流程

```text
安全目录和重解析点检查
  -> 清单 schema/包集合/公钥 ID 校验
  -> 受信公钥验签
  -> 每个包 Size + SHA-256 复核
  -> 复制到 Library/ESInstaller/VerifiedImports/<unique batch>
  -> 再次 Size + SHA-256 复核
  -> Unity ImportPackage
  -> ImportRequested / Completed / Cancelled / Failed 收据
```

收据写入：

```text
Library/ESInstaller/ImportReceipts/<receiptId>.json
```

Installer 窗口会显示最近收据，并提供“定位”和“复制路径”。暂存副本只在该批次不再被引用时删除；清理失败会保留现场并记录错误。

## 发布责任人接入检查

1. 在离线或受控签名环境生成/保管私钥，并由第二渠道核验公钥指纹。
2. 经过代码审查，把公钥及唯一 `keyId` 加入 `ESInstallerPackageTrust.ProductionPublicKeys`。
3. 计算每个 `.unitypackage` 的 Size 与 SHA-256，生成 canonical payload 并签名。
4. 先在临时 Downloads 沙盒验证有效清单、错误 Hash、未知 keyId、被替换包和多包冲突均按预期拒绝。
5. 仅在 Unity Import 完成事件后把收据标记为 `ImportCompleted`；取消、失败或暂存身份漂移都不是成功安装。
