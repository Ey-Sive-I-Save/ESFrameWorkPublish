using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    public sealed partial class ESCompositeShaderGUI
    {
        private static readonly GUIContent OpenBakeWindowButtonContent = new GUIContent("烘焙 / 导出 PNG");

        private static void DrawProductionTools(MaterialEditor editor)
        {
            Material material = editor.target as Material;
            if (!ESCompositeShaderBakeWindow.IsSupportedMaterial(material))
                return;

            EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle);
            GUILayout.Label("生产工具", ESEditorPresentation.SubtitleStyle);
            if (GUILayout.Button(OpenBakeWindowButtonContent, EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                ESCompositeShaderBakeWindow.Open(material);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }
    }
}
