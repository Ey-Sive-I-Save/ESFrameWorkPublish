# WebPageStudio 请求模板

复制以下字段作为任务输入；未填写字段使用安全默认值，并在结果中回显。

```yaml
pageKind: marketing # marketing | dashboard
objective: ""
audience: ""
primaryAction: ""
visualDirection:
  style: "高端、科技感"
  brandColors: []
  typography: ""
  referencePaths: []
responsiveProfiles:
  - id: desktop
    width: 1440
    height: 900
  - id: mobile
    width: 390
    height: 844
states: [default, loading, error]
backend:
  mode: mock-contract-only # mock-contract-only | local-adapter | user-authorized-service
  apiBase: ""
network:
  enabled: false
  allowlist: []
  timeoutSeconds: 10
output:
  format: static-html-css
  entryFile: index.html
  outputDirectory: ES/Output/WebPageStudio/<page-id>/
acceptance:
  requirePreview: false
  requireVisualReview: true
  requireRuntime: false
```

安全默认值：禁网、无依赖、无任意脚本、无自动安装、无业务数据写入。任何联网或服务端接入必须把 `mode`、allowlist、超时、取消和脱敏策略写入合同，并由用户单独授权运行。
