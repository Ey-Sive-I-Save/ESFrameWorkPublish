# WebPageStudio 分层验收矩阵

| 层级 | 证明内容 | 最低证据 | 缺失时结论 |
|---|---|---|---|
| Intent | 页面目标、主动作、非目标明确 | WebPageIntent + inputHash | `blocked` |
| Design | 节点、Token、组件、profile/state、资产映射闭合 | WebDesignSpec + specHash | `designed` |
| Static | HTML/CSS、白名单、路径、XSS/外链检查 | ArtifactPlan + static generator/validator receipt | `static-verified` |
| Backend | 接口 schema、错误/重试/取消语义 | Backend contract + test receipt | `mock-contract-only` |
| Network | 真实请求安全与稳定性 | allowlist、运行回执、脱敏日志 | `runtime-not-run` |
| Preview | 固定 Node/浏览器、DOM 与截图 | PreviewRun + Snapshot（由 `Invoke-ESWebPageStudioPreview.ps1` 产生独立 runtime receipt） | `runtime-not-run` |
| Visual | DOM、几何、Token、资产、像素、人工复核 | 六类 VisualCheck；人工项保持 `review` | `review` / `runtime-not-run` |
| Release | 目标平台、性能、发布产物 | 独立 ReleaseAcceptance | 不声明 |

任何一层都不能用下一层或相邻层证据替代；网页预览不能替代 Unity/Player 证据。
