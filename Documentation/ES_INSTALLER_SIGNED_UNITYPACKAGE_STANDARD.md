# ES Installer：已签名 `.unitypackage` 交付标准

状态：现行供应链门禁规范；首次接入前需要由发布责任人提供受信公钥，未配置时安装器必须拒绝导入。

## 可信根

安装器只信任编译进 `ESInstaller.cs` 内 `ESInstallerTrustedKeys` 的 `keyId -> RSA 公钥 XML` 映射。不得从 Downloads、`package.json`、清单或网络响应动态增加公钥。私钥只能保存在发布责任人的受控签名环境，禁止写入 Unity 工程、安装包或日志。

当前仓库不包含发布私钥或受信公钥；在发布责任人提供经过独立确认的公钥前，旧的未签名 `.unitypackage` 会被 fail-closed 拒绝。

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
2. 经过代码审查，把公钥及唯一 `keyId` 加入 `ESInstaller.cs` 内的 `ESInstallerTrustedKeys`。
3. 计算每个 `.unitypackage` 的 Size 与 SHA-256，生成 canonical payload 并签名。
4. 先在临时 Downloads 沙盒验证有效清单、错误 Hash、未知 keyId、被替换包和多包冲突均按预期拒绝。
5. 仅在 Unity Import 完成事件后把收据标记为 `ImportCompleted`；取消、失败或暂存身份漂移都不是成功安装。
