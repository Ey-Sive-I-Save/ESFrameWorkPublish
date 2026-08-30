# AAA级实时游戏相机制作方法（外部权威校准）

`KnowledgeId`: `es.external.aaa-camera-production-practice.v1`
`Authority`: `external-primary-source-calibrated`
`RouteKeys`: `camera, camera-production, third-person-camera, cinematography, camera-design, collision-avoidance, camera-occlusion, camera-transition, gameplay-camera, production-process`
`HashSchema`: `v2`
`ContentHash`: `72049c1c71fc20ab2b8d8ae5a9f8a5927f5d2f2968bdac33f65be9a411b5e799`
`SourceSetHash`: `72049c1c71fc20ab2b8d8ae5a9f8a5927f5d2f2968bdac33f65be9a411b5e799`
`EntryBodyHash`: `efcaa0bcc188afb56c0643ed49c3dc0819e9d3b480ee2dbca918b6f0c0b09ac8`
`EvidenceLevel`: `S2`
`StaleWhen`: `外部来源版本/语义、本地来源快照、相机制作假设或本条目合同变化时必须复核；本条目不随ES运行时代码自动刷新。`

## 权威边界

本条目只整理 Epic、Unity、GDC 与 Eurographics/SIGGRAPH 的外部资料，作为跨引擎的AAA制作校准。它不替代 ES AIWarnings、ES源码、用户授权，也不证明当前 Unity 场景、Prefab、PlayMode、设备舒适度、Profiler 或发布构建行为。

## 共识制作模型（ABCD）

- **A—架构（Architecture）**：设置一个相机导演/选择器，统一仲裁玩家跟随、载具、固定镜头、过场和临时修饰器；每种镜头以可复用 Profile/Rig 描述目标锚点、偏移、距离、视场角和优先级。
- **B—行为（Behavior）**：第三人称跟随采用目标枢轴与肩部高度；通过球扫/胶囊扫或等价探针处理墙体回缩，通过遮挡检测选择替代视点、透明化或最后已知位置回退；Follow/Fixed/None 等模式由状态机驱动，过渡使用确定性的阻尼和曲线。
- **C—成本与兼容（Cost/Compatibility）**：相机更新必须帧率无关；按距离、视锥和镜头模式预算碰撞查询、动画修饰、后处理、流送与LOD；对宽高比/FOV/安全构图做矩阵测试。Epic Gameplay Camera System 明确标为实验性，不能直接当作上线保证。
- **D—防御与证据（Defense/Evidence）**：每个 Profile 都要有可重放的失败用例（墙角、窄门、目标销毁、快速转身、分辨率变化、流送边界），记录当前模式、候选请求、探针命中、回退原因和耗时；静态资料只能形成S2外部证据，运行结论需另行取得。

## 推荐生产流程

1. 写镜头目标：玩家信息、情绪、可操作性、舒适性和禁止遮挡条件。
2. 在灰盒阶段先做相机原型，使用代表性坡度、墙角、门洞和战斗空间；不要等最终美术完成才发现视线问题。
3. 冻结目标锚点、跟随几何、碰撞探针、FOV/宽高比、安全框和优先级合同。
4. 逐模式验证回缩、遮挡恢复、目标丢失、快速输入与模式切换；再接入动画、特效、载具和过场。
5. 用设计/程序/美术/QA联合评审，加入调试叠加层、录制回放、舒适度检查和性能预算。
6. 发布前按分辨率、平台、输入设备、流送距离和低性能档位执行回归；任何未运行项标为`runtime-not-run`。

## 引擎无关配置模型

|字段|最低要求|
|---|---|
|Profile/Rig|稳定ID、模式、目标锚点、距离/偏移、肩部高度|
|Director|候选请求、优先级、确定性平局规则、进入/退出条件|
|Collision|探针形状、层过滤、最小距离、回缩速度、恢复速度|
|Occlusion|遮挡判定、替代视点、目标透明策略、最后已知位置|
|Framing|FOV、宽高比策略、安全框、兴趣点/前视偏移|
|Motion|旋转/位置阻尼、曲线、帧率无关参数、舒适性上限|
|Fail-safe|目标销毁、无可见点、流送缺失、查询超时的降级动作|
|Evidence|场景/输入种子、模式轨迹、命中记录、耗时、截图或回放ID|

## 失败面矩阵

|failureId|触发/症状|根因|预防检查|正确与恢复动作|证据现状|
|---|---|---|---|---|---|
|CAM-OCC-01|墙角穿模、目标被墙遮住|无扫掠或只做单点射线|角落/窄门/斜坡探针回归|回缩到最近可见点；无可见点时替代视点或最后已知位置|外部资料支持；无当前ES运行回放|
|CAM-TGT-02|目标销毁/传送后镜头甩飞|目标引用失效、无生命周期回退|销毁、换场景、流送延迟测试|冻结短时姿态并平滑到新锚点；无锚点则安全默认镜头|外部资料支持；无Unity运行证据|
|CAM-ARB-03|玩家输入与过场/载具镜头争抢|没有导演优先级和确定性仲裁|同时提交多个请求并重复回放|按优先级和状态条件选唯一赢家，退出时恢复玩家控制|GDC/Epic架构原则；无项目实现证据|
|CAM-COM-04|抖动、晕动、快速转身不适|阻尼随帧率变化、角速度无上限|不同刷新率、灵敏度、FOV矩阵|使用帧率无关阻尼、角速度/加速度上限和可关闭修饰器|GDC失败模式；无设备舒适度测试|
|CAM-FRM-05|超宽屏/窄屏构图丢失|FOV和安全框未约束|分辨率、宽高比、镜头距离矩阵|按平台约束FOV/安全框，必要时切换Profile|Epic/Unity资料；无当前Player构建证据|
|CAM-PERF-06|镜头查询尖峰、流送追不上|每帧高成本查询，未按镜头预算|Profiler、低端档、流送边界测试|降频/简化探针，预加载或降低LOD；记录降级原因|行业推导；无Profiler证据|
|CAM-PROD-07|后期才发现镜头破坏关卡|没有灰盒原型和跨职能评审|原型里程碑与相机验收清单|回退到灰盒重做构图，再恢复内容接入|GDC生产原则；无项目流程回执|

## 证据分层与非声明

本条目为`S2`：来源是可追溯的外部一手资料快照，结论是跨引擎制作建议。未声明任何ES类、Unity类或具体游戏的运行时通过；尚未证明实际碰撞体、输入延迟、摄像机舒适度、性能、网络同步、Player/IL2CPP或发布行为。

## SourceRefs

- `Documentation/AIKnowledge/ExternalSources/aaa-camera-production-provenance.v1.json` (`40c06bcb0ba2bcb83b987bce4d88049eb407228b99fede10387babdf26e07488`)

## RelatedSkills

`es-knowledge-creator`, `es-knowledge-validator`, `es-game-logic-system-development`, `es-editor-tooling`, `es-adversarial-review`
