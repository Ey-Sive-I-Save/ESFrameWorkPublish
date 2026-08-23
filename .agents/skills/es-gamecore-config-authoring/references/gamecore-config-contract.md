# GameCore Config Contract

字段：ConfigId、RootAuthority、RuntimeData、ConfigKey、Consumer、Serialization、TransactionId、ReinjectStep、Rollback、EvidenceRef、Owner、StaleWhen。

稳定身份 Manifest 只保存 Scope、稳定 ID、身份类型、序列化稳定值和 SchemaHash。`RuntimeKey`/`RuntimeId` 仅在当前进程和当前 Catalog 生命周期内有效，禁止写入 Manifest、配置、存档或跨进程证据。
