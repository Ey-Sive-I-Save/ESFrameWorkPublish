#if UNITY_EDITOR
using ES;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditorInternal;

public class RuntimeWatchVideoCaseSceneInstaller : EditorInvoker_Level2
{
    private const string ParentName = "★ ESRuntimeWatch · 产品展示";
    private const string LegacyParentName = "RuntimeWatch_视频演示组";

    public override void InitInvoke()
    {
        InstallOrUpdate(onlyKnownShowcaseScene: true);
    }

    [MenuItem("【ES】/示例与测试/RuntimeWatch/安装或修复标准展示组", priority = 2100)]
    private static void InstallFromMenu()
    {
        InstallOrUpdate(onlyKnownShowcaseScene: false);
    }

    [MenuItem("【ES】/示例与测试/RuntimeWatch/选中标准展示组", priority = 2101)]
    private static void SelectShowcaseRoot()
    {
        GameObject parent = GameObject.Find(ParentName) ?? GameObject.Find(LegacyParentName);
        if (parent == null)
        {
            Debug.LogWarning("[RuntimeWatch] 当前场景尚未安装标准展示组。请先执行“安装或修复标准展示组”。");
            return;
        }

        Selection.activeGameObject = parent;
        EditorGUIUtility.PingObject(parent);
    }

    private static void InstallOrUpdate(bool onlyKnownShowcaseScene)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        if (onlyKnownShowcaseScene && scene.name != "New Scene 1")
            return;

        GameObject parent = GameObject.Find(ParentName) ?? GameObject.Find(LegacyParentName);
        bool changed = false;
        if (parent == null)
        {
            parent = new GameObject(ParentName);
            parent.transform.position = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(parent, "Create RuntimeWatch video cases");
            changed = true;
        }
        else if (parent.name != ParentName)
        {
            Undo.RecordObject(parent, "Rename RuntimeWatch showcase");
            parent.name = ParentName;
            changed = true;
        }

        if (InternalEditorUtility.tags != null && System.Array.IndexOf(InternalEditorUtility.tags, "Player") >= 0 && parent.tag != "Player")
        {
            Undo.RecordObject(parent, "Set RuntimeWatch showcase tag");
            parent.tag = "Player";
            changed = true;
        }

        RuntimeWatchVideoCase_1_BasicTypes basic = CreateOrAttach<RuntimeWatchVideoCase_1_BasicTypes>(parent.transform, "01 · 实时数据与基础类型", new Vector3(-4f, 0f, 0f), ref changed);
        CreateOrAttach<RuntimeWatchVideoCase_2_Methods>(parent.transform, "02 · 安全方法调用", new Vector3(-2f, 0f, 0f), ref changed);
        CreateOrAttach<RuntimeWatchVideoCase_3_FilterAndNested>(parent.transform, "03 · 搜索筛选与嵌套", Vector3.zero, ref changed);
        RuntimeWatchVideoCase_4_UnityTypes unityTypes = CreateOrAttach<RuntimeWatchVideoCase_4_UnityTypes>(parent.transform, "04 · Unity 类型与引用", new Vector3(2f, 0f, 0f), ref changed);
        CreateOrAttach<RuntimeWatchVideoCase_5_Diagnostics>(parent.transform, "05 · 异常与性能诊断", new Vector3(4f, 0f, 0f), ref changed);

        if (unityTypes != null && basic != null && !unityTypes.HasShowcaseTarget(basic.transform))
        {
            Undo.RecordObject(unityTypes, "Configure RuntimeWatch showcase reference");
            unityTypes.ConfigureShowcaseTarget(basic.transform);
            EditorUtility.SetDirty(unityTypes);
            changed = true;
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[RuntimeWatch] 标准展示组已安装或修复：" + ParentName + "。进入 Play Mode 后打开“ES简单工具/运行时观察”。");
        }
    }

    private static T CreateOrAttach<T>(Transform parent, string objectName, Vector3 localPosition, ref bool changed) where T : Component
    {
        Transform child = FindChildWithComponent<T>(parent) ?? parent.Find(objectName);
        GameObject go;
        if (child == null)
        {
            go = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(go, "Create RuntimeWatch video case");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            changed = true;
        }
        else
        {
            go = child.gameObject;
            if (go.name != objectName)
            {
                Undo.RecordObject(go, "Rename RuntimeWatch video case");
                go.name = objectName;
                changed = true;
            }

            if (go.transform.localPosition != localPosition)
            {
                Undo.RecordObject(go.transform, "Position RuntimeWatch video case");
                go.transform.localPosition = localPosition;
                changed = true;
            }
        }

        T component = go.GetComponent<T>();
        if (component == null)
        {
            component = Undo.AddComponent<T>(go);
            changed = true;
        }

        return component;
    }

    private static Transform FindChildWithComponent<T>(Transform parent) where T : Component
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.GetComponent<T>() != null)
                return child;
        }

        return null;
    }
}
#endif
