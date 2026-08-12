using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [ESOnlyEditorSO("ESCmdAgent 只保存编辑器内受管 Codex 工作台配置，不应进入运行时构建或 AB 资源包。")]
    [CreateAssetMenu(fileName = "ESCmdAgent", menuName = MenuItemPathDefine.ASSET_GLOBAL_SO_PATH + "ES Cmd Agent")]
    public class ESCmdAgent : ESEditorGlobalSo<ESCmdAgent>
    {
        [Title("ES Cmd Agent")]
        [LabelText("允许新建、恢复与投递")]
        [InfoBox("关闭后，工作台仍可查看、同步和关闭已有受管会话；不会新建、恢复或投递 AI 任务。")]
        public bool enableAgent = true;

        // Kept only to preserve existing assets. The workspace always uses the project bootstrap registry.
        [HideInInspector]
        public string codexCommand = "codex.cmd";

        [LabelText("工作目录")]
        [FolderPath(AbsolutePath = true)]
        public string workspacePath = "";

        [LabelText("打开时恢复工作台")]
        public bool restoreWorkspaceOnOpen = true;

        // Preserved only so existing configuration assets deserialize without data loss.
        // Session history is never silently deleted; this is intentionally not a user setting.
        [HideInInspector]
        public int maxLocalSessions = 12;

        [LabelText("每个会话消息上限")]
        [Range(20, 300)]
        public int maxMessagesPerSession = 120;

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
