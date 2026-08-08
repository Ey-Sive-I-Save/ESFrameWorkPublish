using System.Collections.Generic;
using ES.Editor;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ESGenericProfile))]
    public sealed class ESGenericProfileEditor : BasePreviewEditor<ESGenericProfile>
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Extension List 是唯一配置权威。Auto Awake / Enable / Pool 默认开启；关闭后由外部按真实阶段调用 NotifyAwake、NotifyEnable、NotifyDisable、NotifyPoolSpawned、NotifyPoolDespawned。NotifyDestroy 始终由 Profile 的 OnDestroy 收口。",
                MessageType.Info);

            base.OnInspectorGUI();
            EditorGUILayout.Space(8f);
            DrawActions();
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate", GUILayout.Height(26f)))
                    ValidateSelectedProfiles();

                if (GUILayout.Button("Migrate", GUILayout.Height(26f)))
                    MigrateSelectedProfiles();
            }
        }

        private void MigrateSelectedProfiles()
        {
            serializedObject.ApplyModifiedProperties();
            var profiles = new List<ESGenericProfile>(targets.Length);
            foreach (Object selected in targets)
            {
                if (selected is ESGenericProfile profile)
                    profiles.Add(profile);
            }

            if (!ESGenericProfileMigrationService.TryMigrate(profiles, out var report))
            {
                string failureState = report.Changed
                    ? "失败，回滚未能确认，请立即检查所选 Profile："
                    : "失败，事务未保留修改：";
                Debug.LogError(
                    "[ESGenericProfile] Migration " + failureState + report.Error,
                    profiles.Count > 0 ? profiles[0] : null);
                return;
            }

            serializedObject.UpdateIfRequiredOrScript();
            Repaint();
            if (!report.Changed)
            {
                Debug.Log("[ESGenericProfile] 所选 Profile 已是当前 SchemaVersion。", profiles[0]);
                return;
            }

            Debug.Log(
                "[ESGenericProfile] Migration 完成，共迁移 " + report.MigratedProfileCount
                + " 个 Profile。\n- " + string.Join("\n- ", report.Operations),
                profiles[0]);
        }

        private void ValidateSelectedProfiles()
        {
            serializedObject.ApplyModifiedProperties();
            foreach (Object selected in targets)
            {
                if (!(selected is ESGenericProfile profile))
                    continue;

                List<string> issues = new List<string>();
                if (profile.ValidateProfile(issues))
                {
                    Debug.Log("[ESGenericProfile] Validate 通过：" + profile.name, profile);
                    continue;
                }

                Debug.LogError(
                    "[ESGenericProfile] Validate 失败：" + profile.name + "\n- " + string.Join("\n- ", issues),
                    profile);
            }
        }
    }
}
