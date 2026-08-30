# Web 知识库架构聚合报告（2026-08-29）

## 结论

当前 WebPageStudio 知识已形成三个互补维度：排版/布局、动效展示、交互状态。新增 `WebKnowledgeKnowledgeGraph.yaml` 将三维条目与生成、验证、发布阶段连接为可路由图谱；它是派生导航层，不替代源码、合同、验证器或 KnowledgeIndex。

## 三维能力边界

| 维度 | 生成重点 | 必须验证 | 发布约束 |
|---|---|---|---|
| 排版 | 内容意图、流体字号、网格、CJK/RTL fallback | Quality、Accessibility、溢出、焦点顺序 | 保持语义 DOM 与静态资源清单 |
| 动效 | hero/reveal/focus/micro 叙事、有限 3D、声明式滚动 | reduced-motion、属性白名单、静态首帧、降级 | 不支持 API 时仍可用，避免无限循环 |
| 交互 | intent→trigger→feedback→success/error/cancel/recovery 状态机 | 键盘等价、焦点、ARIA、重复提交与错误夹具 | 动态请求服从 Backend Contract，静态阶段不宣称真实请求 |

## 闭环使用方式

1. 从 `AIBRAIN_ENTRY.md` 和 `KnowledgeIndex.yaml` 选择 1～3 个最小条目。
2. 将三维知识合并到 Page IR 与设计 Token，生成 HTML/CSS/JS/Manifest。
3. 依次运行 Quality、Accessibility、Contract、UTF-8 静态门禁；运行时回执单独记录，不能由静态结果代替。
4. 只有带哈希、来源和非声明的证据集合才能进入发布暂存；任何 stale 或来源漂移都要求重路由。

## 开源能力对齐（静态校准）

现有外部校准快照与 `web-open-source-calibration` 条目提供标准/API 级参考。可借鉴的能力被拆为：声明式响应式布局（CSS Grid/Container Query）、滚动驱动动画与 View Transition、渐进增强、可访问状态机、PWA/CSP 与测试观测。Astro/Next/Nuxt/Vite 等框架的具体版本兼容、构建性能和生产部署仍需版本锁定的官方快照及运行证据；本报告不将其当作已集成事实。

## 证据与未证实项

- `EvidenceLevel: S1`，来源为项目源码、合同、现有三维条目和项目内官方校准快照。
- 未执行浏览器、Node、网络、Unity、视觉像素回归、性能 Profiler 或生产部署；这些均为 `runtime-not-run`/未证实。
- 图谱不修改 `KnowledgeIndex.yaml`，因此不会自动成为 AIBrain 路由；注册绑定需另行维护并重新计算索引哈希。

## 失败面

图谱明确记录三类高风险：来源/身份漂移、增强层失败导致不可用、静态证据越权为运行或发布结论。对应 prevention、recovery 与缺失证据字段位于 YAML `failureSurface`。

## SourceRefs

详见 `Documentation/AIKnowledge/WebKnowledgeKnowledgeGraph.yaml` 的 `sourceRefs`；所有来源均为项目内路径并带 SHA-256。外部 URL 仅保留在既有校准条目中，不作为本报告的事实来源。
