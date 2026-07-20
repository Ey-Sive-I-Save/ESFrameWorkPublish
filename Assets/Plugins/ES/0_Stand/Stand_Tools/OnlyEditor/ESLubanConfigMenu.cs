#if UNITY_EDITOR
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public static class ESLubanConfigMenu
    {
        private const string MenuRoot = MenuItemPathDefine.CONFIG_PATH + "Luban/";

        [MenuItem(MenuRoot + "\u751f\u6210 Json+CSharp", false, 0)]
        public static void GenerateJsonAndCSharp()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string scriptPath = Path.Combine(projectRoot, "LubanConfig", "gen-json.ps1");

            if (!File.Exists(scriptPath))
            {
                UnityEngine.Debug.LogError("Luban generate script not found: " + scriptPath);
                return;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-ExecutionPolicy Bypass -File \"" + scriptPath + "\"",
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(output))
                    UnityEngine.Debug.Log(output);

                if (process.ExitCode != 0)
                {
                    UnityEngine.Debug.LogError(error);
                    return;
                }
            }

            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("Luban config generated.");
        }

        [MenuItem(MenuRoot + "\u6253\u5f00 LubanConfig", false, 10)]
        public static void OpenLubanConfigFolder()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            EditorUtility.RevealInFinder(Path.Combine(projectRoot, "LubanConfig"));
        }
    }
}
#endif
