using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    internal static class ESAdvancedDialogExamples
    {
        [MenuItem("【ES】/自动化与开发/编辑器扩展/编辑器/ES 专用对话框演示", false, 9165)]
        private static void OpenExample()
        {
            var request = new ESAdvancedDialogRequest
            {
                dialogId = "examples.dialog.configuration",
                title = "创建 ES 功能面板",
                subtitle = "统一输入、校验、键盘操作与反馈动效",
                message = "配置一个无业务副作用的演示面板。",
                detail = "此示例只回传结构化输入，不会创建资产、修改场景或执行发布。",
                confirmText = "确认配置",
                cancelText = "取消",
                tone = ESDialogTone.Success,
                preferredSize = new Vector2(600f, 620f),
                owner = null,
                allowMainWorkspaceFallback = true,
            };
            request.AddText("panelName", "面板名称", "新版测试面板", true).help =
                "必填；清空后会立即显示阻断原因。";
            request.AddChoiceOptions("layout", "内容密度", new[]
            {
                new ESAdvancedDialogChoiceOption("compact", "紧凑"),
                new ESAdvancedDialogChoiceOption("comfortable", "舒适"),
                new ESAdvancedDialogChoiceOption("inspection", "检查视图"),
            }, "comfortable").help = "回调得到稳定 ID，不依赖显示文本。";
            request.AddMultiChoiceOptions("capabilities", "需要的内置能力", new[]
            {
                new ESAdvancedDialogChoiceOption("toolbar", "右上工具栏"),
                new ESAdvancedDialogChoiceOption("status", "状态与推送提示"),
                new ESAdvancedDialogChoiceOption("serialization", "Odin 序列化桥接"),
                new ESAdvancedDialogChoiceOption("rebuild", "页面局部重建"),
            }, new[] { "toolbar", "status" }, 1, 3).help =
                "可选择 1–3 项；结果仍使用稳定 OptionId。";
            request.AddRecommendation(
                "recommendation",
                "方案推荐程度",
                4,
                0,
                5,
                "不建议",
                "强烈推荐").help = "使用整数等级回传，适合排序、筛选与决策记录。";
            request.AddMultilineText(
                "description",
                "面板说明",
                "用于验证长中文换行、单一竖向滚动容器和窗口开场动画。",
                true);
            request.AddToggle("pinToolbar", "固定右上工具动作", true);
            request.AddFolderPath("output", "输出目录", Application.dataPath).readOnly = true;
            request.validateDetailed = values => values.GetString("panelName").Trim().Length < 2
                ? new ESAdvancedDialogValidation("面板名称至少需要 2 个字符。", "panelName")
                : null;
            request.AddAuxiliaryAction(
                "preview.summary",
                "预览摘要",
                values => Debug.Log(
                    "[ES Dialog Preview] 面板=" + values.GetString("panelName")
                    + "，布局=" + values.GetString("layout")),
                "预览当前输入，不关闭对话框。");
            request.completed = result =>
            {
                if (!result.accepted)
                    return;
                _ = ESDialog.InfoAsync(
                    "examples.dialog.configuration.received",
                    "配置已接收",
                    "对话框已返回稳定、结构化的页面配置。",
                    detail: "面板：" + result.values.GetString("panelName")
                    + "\n布局：" + result.values.GetString("layout")
                    + "\n能力：" + string.Join("、", result.values.GetSelections("capabilities"))
                    + "\n推荐程度：" + result.values.GetRecommendation("recommendation") + " / 5"
                    + "\n固定工具动作：" + result.values.GetToggle("pinToolbar"),
                    owner: null,
                    allowMainWorkspaceFallback: true);
            };
            ESDialogService.Show(request);
        }

        [MenuItem("【ES】/自动化与开发/编辑器扩展/编辑器/ES 危险确认对话框演示", false, 9166)]
        private static void OpenDangerExample()
        {
            _ = RunDangerExampleAsync();
        }

        private static async Task RunDangerExampleAsync()
        {
            bool accepted = await ESDialog.DangerAsync(
                "examples.dialog.danger",
                "危险操作确认",
                "这是危险语义的视觉与键盘交互演示，不会执行任何业务操作。",
                "确认演示",
                detail: "原因：验证危险强调色。影响：无。恢复：直接关闭演示窗口。",
                host: ESDialogHost.Editor,
                owner: null,
                allowMainWorkspaceFallback: true);
            if (accepted)
            {
                await ESDialog.InfoAsync(
                    "examples.dialog.danger.completed",
                    "演示完成",
                    "确认结果已回传，但没有执行任何修改。",
                    host: ESDialogHost.Editor,
                    owner: null,
                    allowMainWorkspaceFallback: true);
            }
        }

        [MenuItem("【ES】/自动化与开发/编辑器扩展/编辑器/ES 异步对话框与进度演示", false, 9167)]
        private static void OpenAsyncExample()
        {
            var request = new ESAdvancedDialogRequest
            {
                dialogId = "es.dialog.example.async",
                title = "异步任务配置",
                subtitle = "去重、Busy、取消、进度聚合与自定义内容",
                message = "确认后模拟一个可取消的异步操作。",
                detail = "详细进度默认收纳在右下角；失败或手动展开时才显示完整信息。",
                confirmText = "开始任务",
                duplicatePolicy = ESDialogDuplicatePolicy.FocusExisting,
                initialFocusFieldId = "taskName",
                preferredSize = new Vector2(590f, 520f),
                asyncValidationDelayMs = 220,
                owner = null,
                allowMainWorkspaceFallback = true,
            };
            request.AddText("taskName", "任务名称", "商业级窗口检查", true);
            request.AddRecommendation("priority", "执行优先级", 3, 1, 5, "普通", "最高");
            request.createCustomContent = values =>
            {
                var notice = new Label("自定义 VisualElement：这里可放预览、摘要或领域专属控件。")
                {
                    name = "ESDialogExampleCustomContent",
                };
                notice.style.whiteSpace = WhiteSpace.Normal;
                notice.style.paddingLeft = 10f;
                notice.style.paddingRight = 10f;
                notice.style.paddingTop = 8f;
                notice.style.paddingBottom = 8f;
                return notice;
            };
            request.validateAsync = async (values, token) =>
            {
                await Task.Delay(260, token);
                return values.GetString("taskName").Trim().Length < 4
                    ? new ESAdvancedDialogValidation("任务名称至少需要 4 个字符。", "taskName")
                    : null;
            };
            request.AddAuxiliaryAction(
                "child.preview",
                "打开子对话框",
                _ =>
                {
                    var child = new ESAdvancedDialogRequest
                    {
                        dialogId = "es.dialog.example.child",
                        owner = ESAdvancedDialogWindow.focusedWindow,
                        allowMainWorkspaceFallback = true,
                        title = "子对话框",
                        message = "父窗口关闭时，本窗口会随父窗口安全取消。",
                        showCancel = false,
                    };
                    ESDialogService.Show(child);
                });
            request.confirmAsync = async (values, progress, token) =>
            {
                for (int i = 0; i <= 12; i++)
                {
                    token.ThrowIfCancellationRequested();
                    float ratio = i / 12f;
                    progress.Report(ratio, "阶段 " + i + " / 12");
                    if (i % 3 == 0)
                        progress.AddDetail("已完成检查点 " + i);
                    await Task.Delay(140, token);
                }
            };
            ESDialogService.Show(request);
        }

        [MenuItem("【ES】/自动化与开发/编辑器扩展/编辑器/ES 分步同步进度演示", false, 9168)]
        private static async void OpenStepProgressExample()
        {
            var steps = new ESProgressStep[30];
            for (int i = 0; i < steps.Length; i++)
            {
                float ratio = (i + 1f) / steps.Length;
                steps[i] = new ESProgressStep(
                    ratio,
                    "整理窗口 " + (i + 1) + " / " + steps.Length,
                    i % 5 == 0 ? "已收纳一组低优先级细节。" : null);
            }
            try
            {
                await ESProgressCenter.RunSteps(
                    "es.progress.example.steps",
                    "分步同步进度演示",
                    steps);
            }
            catch (TaskCanceledException)
            {
            }
        }

        [MenuItem("【ES】/自动化与开发/编辑器扩展/编辑器/ES 迁移级 API 演示", false, 9169)]
        private static async void OpenMigrationApiExample()
        {
            bool accepted = await ESDialog.ConfirmAsync(
                "examples.dialog.migration.confirm",
                "迁移级确认",
                "该入口返回 Task<bool>，适合替换能够改为异步控制流的原生确认框。",
                "继续",
                "取消",
                host: ESDialogHost.Editor,
                owner: null,
                allowMainWorkspaceFallback: true);
            if (!accepted)
                return;

            ESDialogChoice choice = await ESDialog.ChooseAsync(
                "examples.dialog.migration.choice",
                "选择迁移策略",
                "三按钮返回强类型结果，不再依赖 0 / 1 / 2。",
                "异步迁移",
                "保留同步",
                "取消",
                host: ESDialogHost.Editor,
                owner: null,
                allowMainWorkspaceFallback: true);
            Debug.Log("[ES Dialog Migration] choice=" + choice);
        }
    }
}
