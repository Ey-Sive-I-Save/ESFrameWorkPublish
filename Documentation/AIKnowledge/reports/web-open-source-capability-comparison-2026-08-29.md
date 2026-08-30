# 开源网页框架能力对比（2026-08-29 快照）

## 结论

ES WebPageStudio 已具备静态生成、动态边界、主题令牌、动效/交互合同和分层验证入口。与主流开源框架相比，下一阶段应把“框架能力”作为可声明的 Page IR 策略，而不是复制某个框架运行时：路由级渲染策略、组件级 hydration/island 边界、容器响应式、缓存失效、适配器和证据分轴是共同核心。

## 对比摘要

| 框架 | 强项 | ES 可借鉴机制 | 关键未证实 |
|---|---|---|---|
| Next.js | 静态导出与 SSR/缓存组件组合、流式/客户端边界 | static shell + dynamic island；显式 cache/dynamic boundary | 精确版本、浏览器、生产缓存与性能 |
| Astro | static-first、island/partial hydration、adapter | 组件 hydration policy；默认静态 Page IR | adapter 差异与岛运行时 |
| Nuxt | Route Rules 可按路由选择 prerender/SWR/ISR/SSR/CSR | route-level `renderPolicy` 与 cache evidence | 主机运行时与实际缓存 |
| SvelteKit | 每路由 prerender/SSR/CSR、渐进增强表单 | route prerender 与表单状态机 | adapter/浏览器行为 |
| Remix | loader/action 数据流、嵌套路由、HTTP 缓存 | route data contract、mutation state machine | 静态适配器与生产缓存 |
| Qwik | resumability、事件延迟加载、低 hydration 成本 | interaction-resumability hint、hydration budget | 运行时恢复与适配器 |
| Eleventy | 简洁多模板静态输出、构建可控 | static-first、显式 client island、UTF-8/link gate | 动态能力依赖外部服务 |

## 设计维度对齐

- 排版/主题：所有框架都依赖 CSS；ES 应以 design tokens、`responsiveBasis=container`、`forced-colors` 与 `prefers-reduced-motion` 为跨框架基线。
- 高动态展示：动效本身不是框架特性。ES 需要把 CSS/Web Animations/WebGL 能力声明、降级和 reduced-motion 证据分开，避免“有动画代码”被当作视觉通过。
- 交互：Astro/Qwik 的局部客户端与 Remix/Nuxt 的数据边界可统一为 Page IR 的 client boundary + state/event contract；静态 HTML 仍需可用。
- 性能：Next 的缓存组件、Nuxt Route Rules、Qwik resumability 提供不同优化路径；ES 只采纳可测量预算（LCP/INP/CLS）和证据字段，不搬运框架实现。
- 发布：Vite/各 adapter 的 `basePath`、资源哈希、边缘/主机缓存和失效策略必须成为 delivery contract；静态检查不能证明部署成功。

## 来源与边界

本报告只使用项目内 `Documentation/AIKnowledge/external/web/web-official-calibration-2026-08-29-v2.md` 及 `WebKnowledgeExternalSourcePlan.yaml` 的官方资料快照；网络保持关闭，未重新抓取。快照记录了 MDN、W3C、web.dev、Google Search Central、Nuxt、Next.js、Vite 的来源 URL 与许可证说明。Astro、SvelteKit、Remix、Qwik、Eleventy 在本矩阵中仅作能力分类信号，未有项目内本轮官方快照，故精确版本、API 兼容、许可证、浏览器行为、真实性能和生产部署均标记为未证实。

## 采用门槛

1. 先固定外部框架 tag/commit、许可证和 adapter，再进入 `es-open-source-migration` 的 mapping-approved 流程；禁止直接 vendoring。
2. 每个能力必须映射到 ES canonical 合同（Page IR、renderPolicy、theme tokens、interaction state machine、evidence layers）。
3. 任何 runtime、浏览器、网络、视觉回归、Web Vitals 或发布结论，必须由对应运行时证据单独证明；本报告不提供这些证据。

对应机器可读矩阵：`Documentation/AIKnowledge/WebOpenSourceCapabilityMatrix.yaml`。
