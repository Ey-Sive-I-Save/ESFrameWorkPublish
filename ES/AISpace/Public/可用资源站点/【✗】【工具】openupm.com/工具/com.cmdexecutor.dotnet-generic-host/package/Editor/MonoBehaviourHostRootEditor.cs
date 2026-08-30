using System.Diagnostics;
using UnityEditor;

namespace Microsoft.Extensions.Hosting.Unity.Editor
{
    [CustomEditor(typeof(MonoBehaviourHostRoot))]
    public class MonoBehaviourHostRootEditor : UnityEditor.Editor
    {
        private const string GITHUB_URL = "https://github.com/amelkor/Microsoft.Extensions.Hosting.Unity";

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "This script is controlled by HostManager component and keeps resolved MonoBehaviour services.",
                MessageType.Info);

            // ReSharper disable once InvertIf
            if (EditorGUILayout.LinkButton("For additional information visit the GitHub link"))
            {
                var ps = new ProcessStartInfo(GITHUB_URL)
                {
                    UseShellExecute = true,
                    Verb = "open"
                };
                Process.Start(ps);
            }
        }
    }
}