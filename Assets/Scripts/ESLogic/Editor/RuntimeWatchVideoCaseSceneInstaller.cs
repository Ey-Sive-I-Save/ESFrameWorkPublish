#if UNITY_EDITOR
using ES;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RuntimeWatchVideoCaseSceneInstaller : EditorInvoker_Level2
{
    private const string ParentName = "RuntimeWatch_视频演示组";

    public override void InitInvoke()
    {
        InstallOrUpdate();
    }

    private static void InstallOrUpdate()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        if (scene.name != "New Scene 1")
            return;

        GameObject parent = GameObject.Find(ParentName);
        bool changed = false;
        if (parent == null)
        {
            parent = new GameObject(ParentName);
            parent.transform.position = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(parent, "Create RuntimeWatch video cases");
            changed = true;
        }

        changed |= CreateOrAttach<RuntimeWatchVideoCase_1_BasicTypes>(parent.transform, "RW_01_基础类型", new Vector3(-3f, 0f, 0f));
        changed |= CreateOrAttach<RuntimeWatchVideoCase_2_Methods>(parent.transform, "RW_02_方法调用", new Vector3(-1f, 0f, 0f));
        changed |= CreateOrAttach<RuntimeWatchVideoCase_3_FilterAndNested>(parent.transform, "RW_03_筛选嵌套", new Vector3(1f, 0f, 0f));
        changed |= CreateOrAttach<RuntimeWatchVideoCase_4_UnityTypes>(parent.transform, "RW_04_Unity类型", new Vector3(3f, 0f, 0f));

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[RuntimeWatch] 已安装视频演示空对象: " + ParentName);
        }
    }

    private static bool CreateOrAttach<T>(Transform parent, string objectName, Vector3 localPosition) where T : Component
    {
        bool changed = false;
        Transform child = parent.Find(objectName);
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
        }

        if (go.GetComponent<T>() == null)
        {
            Undo.AddComponent<T>(go);
            changed = true;
        }

        return changed;
    }
}
#endif
