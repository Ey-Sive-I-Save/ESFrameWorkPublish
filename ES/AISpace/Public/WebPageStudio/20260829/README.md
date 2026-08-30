# WebPageStudio 运行验证索引

此目录只登记可协作发现的索引，不复制截图、DOM、缓存或 Automation 正文。

## 已注册能力

- `runtime-receipt`：本地 Chromium 无头预览回执，网络必须为 `disabled`。
- `visual-baseline`：按 profile/theme/state 组织的 PNG 基线索引。
- `pixel-comparison`：像素级差异回执，记录尺寸、差异像素和 SHA-256。
- `performance-probe`：本地性能探针索引；LCP、INP、CLS 缺失时必须保持 `review`，不得升级为通过。
- `performance-baseline`：固定 HTML 哈希、预热/样本轮次及 p50/p75 墙钟统计；缺失真实 LCP/INP 或 Lighthouse 时保持 `review`。
- `static-signals-receipt`：一次性聚合 Quality、Accessibility、Contract 与 UTF-8 四个静态信号。
- `artifact-integrity-receipt`：对生成目录执行必需文件、哈希、Manifest、Sitemap 与外链策略检查。
- `staging-readiness-receipt`：区分本地证据、staging HTTP、Lighthouse/Trace 与回滚门禁，未执行项保持 `review`。
- `design-tokens`：登记页面实际风格、动效级别、主题与响应式 Token 的机器可读快照。
- `cache-policy`：路由级 `prerender|cached|dynamic|client-only`、TTL、失效标签和回退策略。
- `dynamic-state-replay`：离线重放 idle/loading/success/empty/error 与缓存命中、过期、失效恢复。
- `backend-contract`：静态生成入口输出 mock 或 local-adapter 动态数据合同；真实 HTTP 仍需单独授权。
- `local-adapter-receipt`：零网络的本地 fixture 动态适配器回执，验证重试/取消/脱敏合同边界。

权威实现仍位于 `ES/Automation/WebPageStudio/`，实际运行产物保留在 `ES/Output/WebPageStudio/`；AISpace 只保存本索引和对应 Skill 绑定，避免产生第二份事实源。

## 外部校准入口

- Core Web Vitals：LCP、INP、CLS 及阈值以 web.dev 当前定义为准。
- 可访问性：WCAG 2.2 的 Focus Appearance、Non-text Contrast 和 WAI 评估边界。
- 主题兼容：MDN `forced-colors` 与 `prefers-reduced-motion` 行为。

外部页面仅用于校准；项目长期事实仍必须回到本仓库 SourceRef、哈希和运行回执。
