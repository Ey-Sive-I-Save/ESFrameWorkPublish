# ES Automation 受管 Python 环境

Python Worker 的解释器优先级固定为：

1. `ES_AUTOMATION_PYTHON`：当前机器显式指定的绝对 `python.exe`；适合开发机调试和 CI。
2. `ES/Automation/Environments/Python/python-runtime.lock.json`：项目受管运行时；适合团队统一环境和离线构建。

不会回退到 `PATH`、`py.exe` 或 Windows Store 占位别名。

## 项目受管布局

```text
ES/Automation/Environments/Python/
├─ python-runtime.lock.json
├─ Runtime/
│  └─ python.exe
└─ requirements.lock.txt          # 当前没有第三方依赖时可省略
```

`python-runtime.lock.json` 必须符合 `ES/Automation/Contracts/es-automation-python-runtime.schema.json`；解释器、整个 `Runtime/` 目录树和可选依赖锁文件都必须写入 SHA-256。启动 Worker 前，C# 会重新校验解释器、Runtime 内容树指纹并执行 `python.exe --version`，只接受 Python 3 与锁定版本。

执行器不会在运行时从公网静默下载或安装解释器。团队若不将 Runtime 纳入版本库，应通过受审核的软件包、内部制品库或 CI 镜像把固定运行时部署到上述目录，并同步锁文件和制品来源说明。

## 当前工作区制品来源

- 制品：CPython 3.12.10 Windows x64 embeddable package
- 官方来源：`https://www.python.org/ftp/python/3.12.10/python-3.12.10-embed-amd64.zip`
- 下载包 SHA-256：`4acbed6dd1c744b0376e3b1cf57ce906f9dc9e95e68824584c8099a63025a3c3`
- 部署后 Runtime 内容树 SHA-256：`a138fee79f8975f0e4d73c0bfc6f28a61a86e7041ed9d7d0688bfa7d7815e39c`

该来源说明用于审计；实际执行只信任锁文件中的解释器、Runtime 内容树和版本校验。
