using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [ESOnlyEditorSO("ESCmdAgent 只保存编辑器内 Codex 命令行 Agent 配置和会话状态，不应进入运行时构建或 AB 资源包。")]
    [CreateAssetMenu(fileName = "ESCmdAgent", menuName = MenuItemPathDefine.ASSET_GLOBAL_SO_PATH + "ES Cmd Agent")]
    public class ESCmdAgent : ESEditorGlobalSo<ESCmdAgent>
    {
        [Title("ES Cmd Agent")]
        [LabelText("启用 Agent")]
        public bool enableAgent = true;

        [LabelText("Codex 命令")]
        public string codexCommand = "codex.cmd";

        [LabelText("工作目录")]
        [FolderPath(AbsolutePath = true)]
        public string workspacePath = "";

        [LabelText("恢复指定会话 ID")]
        public string resumeSessionId = "";

        [LabelText("最近恢复会话 ID"), ReadOnly]
        public string lastResumeSessionId = "";

        [LabelText("最近启动时间"), ReadOnly]
        public string lastStartTime = "";

        [LabelText("打开入口后自动 resume")]
        public bool autoResumeOnOpen = true;

        [LabelText("自动记录恢复 Key")]
        public bool autoCaptureResumeKey = true;

        [LabelText("本地页签上限")]
        [Range(1, 12)]
        public int maxLocalTabsToKeep = 4;

        [LabelText("单页签输出上限")]
        [Range(2000, 200000)]
        public int maxOutputCharsPerTab = 12000;

        public string GetWorkspacePath()
        {
            if (!string.IsNullOrWhiteSpace(workspacePath))
                return workspacePath.Trim();

#if UNITY_EDITOR
            return Application.dataPath.EndsWith("/Assets")
                ? Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length)
                : Application.dataPath;
#else
            return string.Empty;
#endif
        }
    }
}
