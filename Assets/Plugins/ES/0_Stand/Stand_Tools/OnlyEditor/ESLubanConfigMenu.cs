#if UNITY_EDITOR
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public static class ESLubanConfigMenu
    {
        private static string GetConfigRoot(string projectRoot) => Path.Combine(projectRoot, "ES", "Config", "Luban");
        private const string MenuRoot = MenuItemPathDefine.CONFIG_PATH + "Luban/";

        [MenuItem(MenuRoot + "\u751f\u6210 Json+CSharp", false, 0)]
        public static void GenerateJsonAndCSharp()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string scriptPath = Path.Combine(GetConfigRoot(projectRoot), "gen-json.ps1");

            if (!File.Exists(scriptPath))
            {
                UnityEngine.Debug.LogError("Luban generate script not found: " + scriptPath);
                return;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(System.Environment.SystemDirectory, "powershell.exe"),
                Arguments = "-ExecutionPolicy Bypass -File \"" + scriptPath + "\"",
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using (ESManagedEditorProcess execution = ESManagedEditorProcessRunner.StartPowerShell(
                    startInfo, projectRoot, 120))
                {
                    if (!execution.WaitForExit(120000))
                    {
                        execution.Terminate();
                        UnityEngine.Debug.LogError("Luban 配置生成超时，受管 PowerShell 进程树已终止。\n脚本：" + scriptPath);
                        return;
                    }

                    string output = execution.ReadStandardOutputToEnd();
                    string error = execution.ReadStandardErrorToEnd();
                    execution.TryGetExitCode(out int exitCode);

                    if (!string.IsNullOrEmpty(output))
                        UnityEngine.Debug.Log(output);

                    if (exitCode != 0)
                    {
                        UnityEngine.Debug.LogError(error);
                        return;
                    }
                }
            }
            catch (System.Exception exception)
            {
                UnityEngine.Debug.LogError("Luban 配置生成失败：" + exception.Message);
                return;
            }

            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("Luban config generated.");
        }

        [MenuItem(MenuRoot + "\u6253\u5f00 LubanConfig", false, 10)]
        public static void OpenLubanConfigFolder()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            EditorUtility.RevealInFinder(GetConfigRoot(projectRoot));
        }
    }
}
#endif
