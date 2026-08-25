using System.IO;
using ES;
using UnityEditor;
using UnityEngine;

 [ES.ESWindowSleepContract(
     ES.ESWindowSleepMode.Transient,
     ES.ESWindowSurfaceKind.Utility,
     "短生命周期创建窗口")]
 [ES.ESWindowPresentationShortTitle("技能")]
public sealed class ESCreateSkillWindow : EditorWindow
{
    private const string DefaultSkillFolder = "Assets/ESNormalAssets/Data/Skill";
    private string keyName = "NewSkill";
    private bool placeInGroup;
    private SKillDataGroup targetGroup;

    private void OnEnable()
    {
        ES.ESWindowFoundation.BindTransient(this);
    }

    private void OnDisable()
    {
        ES.ESWindowFoundation.Suspend(this);
    }

    private void OnDestroy()
    {
        ES.ESWindowFoundation.Close(this);
    }

    public static void Open()
    {
        ESCreateSkillWindow window = GetWindow<ESCreateSkillWindow>(true, "新建技能");
        window.minSize = new Vector2(420f, 220f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("新建技能", EditorStyles.boldLabel);
        EditorGUILayout.Space(6f);

        keyName = EditorGUILayout.TextField("技能键名", keyName);
        EditorGUILayout.Space(4f);
        placeInGroup = EditorGUILayout.Toggle("放入技能组", placeInGroup);

        if (placeInGroup)
        {
            targetGroup = (SKillDataGroup)EditorGUILayout.ObjectField(
                "目标技能组",
                targetGroup,
                typeof(SKillDataGroup),
                false);
            EditorGUILayout.HelpBox(
                "创建技能资产后，按技能键名登记到所选技能组。",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "创建独立技能资产，并直接在轨道编辑器中打开。",
                MessageType.Info);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginHorizontal();
        try
        {
            if (GUILayout.Button("创建并打开", GUILayout.Height(28f)))
                TryCreateSkill();

            if (GUILayout.Button("取消", GUILayout.Height(28f)))
                Close();
        }
        finally
        {
            EditorGUILayout.EndHorizontal();
        }
    }

    private void TryCreateSkill()
    {
        string safeKey = keyName == null ? string.Empty : keyName.Trim();
        if (string.IsNullOrWhiteSpace(safeKey))
        {
            EditorUtility.DisplayDialog("无法创建技能", "请先输入技能键名。", "知道了");
            return;
        }

        if (placeInGroup && targetGroup == null)
        {
            EditorUtility.DisplayDialog("无法创建技能", "请选择目标技能组。", "知道了");
            return;
        }

        if (placeInGroup && !targetGroup.NotContainsInfoKey(safeKey))
        {
            EditorUtility.DisplayDialog("无法创建技能", "目标技能组已包含键名：" + safeKey, "知道了");
            return;
        }

        string fileName = string.Join("_", safeKey.Split(Path.GetInvalidFileNameChars()));
        string folder = placeInGroup && targetGroup != null
            ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(targetGroup)).Replace('\\', '/')
            : DefaultSkillFolder;
        EnsureFolder(folder);

        string assetPath = folder + "/" + fileName + ".asset";
        if (AssetDatabase.LoadAssetAtPath<SkillTrackProcessInfo>(assetPath) != null)
        {
            EditorUtility.DisplayDialog("无法创建技能", "已存在同名技能资产：" + assetPath, "知道了");
            return;
        }

        SkillTrackProcessInfo skill = ScriptableObject.CreateInstance<SkillTrackProcessInfo>();
        skill.name = safeKey;
        AssetDatabase.CreateAsset(skill, assetPath);

        bool usesUnifiedGameCoreRegistration = placeInGroup
                                               && targetGroup != null
                                               && ESScriptableObjectClassification.GetClass(targetGroup) == ESScriptableObjectClass.GameCore;
        if (placeInGroup && targetGroup != null && !usesUnifiedGameCoreRegistration)
            targetGroup._TryAddInfoToDic(safeKey, skill);

        EditorUtility.SetDirty(skill);
        if (targetGroup != null)
            EditorUtility.SetDirty(targetGroup);

        AssetDatabase.SaveAssets();
        if (usesUnifiedGameCoreRegistration)
            ESResourceCollectionWorkflowWindow.OpenForGameCoreRegistration(skill, targetGroup, null, safeKey);
        Selection.activeObject = skill;
        ESTrackViewWindow.TryUpdateTrackSequence(skill);
        Close();
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string normalized = folder.Replace('\\', '/').TrimEnd('/');
        int separator = normalized.LastIndexOf('/');
        if (separator <= 0)
            return;

        string parent = normalized.Substring(0, separator);
        string leaf = normalized.Substring(separator + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
