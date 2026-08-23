using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    public sealed partial class ESCompositeShaderGUI
    {
        private static readonly GUIContent UpgradeMaterialsButtonContent = new GUIContent("升级材质");

        private static void DrawMaterialMigrationPanel(MaterialEditor editor)
        {
            int outdated = 0;
            int future = 0;
            for (int i = 0; i < editor.targets.Length; i++)
            {
                Material material = editor.targets[i] as Material;
                int version = ESCompositeMaterialMigration.GetStoredVersion(material);
                if (version >= 0 && version < ESCompositeMaterialMigration.CurrentVersion)
                    outdated++;
                else if (version > ESCompositeMaterialMigration.CurrentVersion)
                    future++;
            }

            if (outdated == 0 && future == 0)
                return;

            EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle);
            if (future > 0)
                EditorGUILayout.HelpBox(
                    future + " 个材质来自更高版本，当前工具不会自动降级。",
                    MessageType.Warning);

            if (outdated > 0)
            {
                EditorGUILayout.HelpBox(
                    outdated + " 个材质需要升级到 Schema v" + ESCompositeMaterialMigration.CurrentVersion
                    + "。升级会补齐兼容数据并同步关键词。",
                    MessageType.Info);
                if (GUILayout.Button(UpgradeMaterialsButtonContent, EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                {
                    for (int i = 0; i < editor.targets.Length; i++)
                    {
                        Material material = editor.targets[i] as Material;
                        if (ESCompositeMaterialMigration.NeedsMigration(material))
                            ESCompositeMaterialMigration.Migrate(material);
                    }
                }
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }
    }
}
