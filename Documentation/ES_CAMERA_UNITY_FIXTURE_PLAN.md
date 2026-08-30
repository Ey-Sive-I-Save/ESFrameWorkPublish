# ES 相机 Unity Fixture 设计

## 目标

为相机内核提供可重复的 EditMode/PlayMode 输入边界。Fixture 只负责构造测试世界和采集证据，不向正式角色、Rig 或相机资产写入测试逻辑。

## 责任分工

| 角色 | 输入 | 不负责 |
|---|---|---|
| 程序 | Fixture Builder、固定输入、诊断回执、Profiler 标记 | 以截图主观判断镜头美感 |
| 策划 | 镜头模式、优先级、切换时序、舒适度阈值 | 直接修改 VCam 或运行时后端 |
| 美术 | Rig Prefab、VCam/Offset/Collider 结构、遮挡材质 | 在场景实例上绕过 Catalog 修组件 |
| QA | 运行矩阵、回放、差异归档、失败复现 | 修改基线以掩盖失败 |

## Fixture 结构

每个场景只包含：`FixtureRoot`、测试目标、障碍体、独立 `RigRoot`、输出 Camera/Brain、`ESCameraSceneBinding` 和只读诊断采集器。正式资产通过 Catalog 引用，禁止复制一份“测试版相机逻辑”。

## 场景与断言

| ID | 主要断言 | 失败即阻断 |
|---|---|---|
| F-01 Occlusion | 墙角/窄门中目标可见，镜头不穿模，阻尼收敛 | 穿模、目标丢失、持续振荡 |
| F-02 Arbitration | 角色/载具请求按稳定优先级选唯一 Winner | 双写后端、旧 Lease 生效 |
| F-03 Cancellation | Shot 取消后恢复 Base，Modifier 不残留 | 取消后黑屏、残留偏移 |
| F-04 Aspect | 16:9/21:9/4:3 与安全区构图一致 | 关键目标出安全区 |
| F-05 Lifetime | 目标销毁/换场景后旧 ViewId、Epoch、Lease 全失效 | 旧请求影响新场景 |
| F-06 Performance | 预热后固定 60 秒采样，LateUpdate/GC/查询低于预算 | 超预算或持续分配 |

## 回放与证据

每次运行固定 `branch/HEAD`、Unity/Cinemachine 版本、平台、分辨率、输入脚本版本、Catalog/Prefab/Policy 哈希。每帧或采样点记录 `sceneEpoch`、`activeRequestCount`、Winner、Owner、场景路径、BuildId；视觉用截图哈希，性能保留原始 Profiler 数据。

## 失败恢复

Fixture 失败时保留原始回执和场景快照，使用新 `runId` 重跑；不得覆盖基线、修改正式资产或把 `runtime-not-run` 改写成通过。只有同一输入、同一版本和同一资产哈希下重复通过，才可更新验收状态。
