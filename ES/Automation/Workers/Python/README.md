# ES Automation Python Workers

本目录只放受 `ESAutomationCenter` 注册、指纹锁定的 Python Worker。它们不直接写 `Assets/`、不启动 Unity、不通过 stdout 传递控制协议。

## 场景扫描原型

`es_scene_scan_worker.py` 是 `es.scene.scan@1` 的唯一入口：

```text
Unity 当前场景
  -> C# 导出规范化快照到 ES/Automation/Temp/<RunId>/
  -> Python 阶段 0 写 NeedsInput 后退出
  -> Unity 高级对话框写规范化 InputResponse.json
  -> Python 阶段 1 写报告到同一 RunId 临时目录
  -> C# 校验哈希并原子提升至 ES/Automation/Reports/<RunId>/
```

配置真实解释器后，可在项目根目录运行定向测试：

```powershell
$env:PYTHONDONTWRITEBYTECODE = '1'
& $env:ES_AUTOMATION_PYTHON ES/Automation/Workers/Python/tests/test_scene_scan_worker.py
```

`ES_AUTOMATION_PYTHON` 可指定 Python 3 的绝对解释器路径，例如 `C:\Python312\python.exe`；未指定时会查找项目受管 `ES/Automation/Environments/Python/python-runtime.lock.json`。启动前会复核 Python 3 版本和解释器 SHA-256。Unity 不会回退到 PATH、`py.exe`、Windows Store `python.exe` 别名，也不会从调用方接收任意 Python 或脚本路径。

当前 Worker 不创建子进程；因此原型阶段由 C# 直接终止该 Python 进程即可。若未来 Worker 创建子进程，必须先升级为受管进程树取消方案。
