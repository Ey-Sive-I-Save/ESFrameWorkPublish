#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    public sealed class ESWorkbenchPrefabPlacementState
    {
        public GameObject sourcePrefab;
        public GameObject selectedInstance;
        public Vector3 placementEuler;
        public Vector3 placementScale = Vector3.one;
    }

    public sealed class ESWorkbenchPreviewScene : System.IDisposable
    {
        private readonly List<GameObject> instances = new List<GameObject>();
        private Scene scene;

        public bool IsOpen => scene.IsValid();
        public Scene Scene => scene;
        public IReadOnlyList<GameObject> Instances => instances;

        public bool EnsureOpen(out string error)
        {
            error = string.Empty;
            if (scene.IsValid()) return true;
            scene = EditorSceneManager.NewPreviewScene();
            if (scene.IsValid()) return true;
            error = "PreviewScene 创建失败。";
            return false;
        }

        public bool TryInstantiateRegisteredPrefab(GameObject prefab, Vector3 position, Quaternion rotation,
            Vector3 scale, out GameObject instance, out string stringKey, out string error)
        {
            instance = null;
            stringKey = string.Empty;
            if (!ESWorkbenchContentRegistration.TryResolveRegisteredAsset(prefab, ESAssetReferKind.Prefab,
                    out ESAssetPage page, out error))
                return false;
            if (!EnsureOpen(out error)) return false;
            if (!PrefabUtility.IsPartOfPrefabAsset(prefab))
            {
                error = "放置源必须是 Project 中的 Prefab 资产。";
                return false;
            }
            instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null) { error = "Prefab 预览实例创建失败。"; return false; }
            instance.name = prefab.name + " · ES 工作台预览";
            Undo.RegisterCreatedObjectUndo(instance, "放置 ES 工作台预览对象");
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = scale;
            instances.Add(instance);
            stringKey = page.EffectiveStringKey;
            return true;
        }

        public bool Contains(GameObject instance)
        {
            return instance != null && instances.Contains(instance);
        }

        public void ApplyTransform(GameObject instance, Vector3 position, Vector3 euler, Vector3 scale)
        {
            if (!Contains(instance)) return;
            Undo.RecordObject(instance.transform, "修正 ES 工作台预览对象");
            instance.transform.position = position;
            instance.transform.eulerAngles = euler;
            instance.transform.localScale = scale;
        }

        public void Remove(GameObject instance)
        {
            if (!Contains(instance)) return;
            instances.Remove(instance);
            Object.DestroyImmediate(instance);
        }

        public void Close()
        {
            for (int i = instances.Count - 1; i >= 0; i--)
                if (instances[i] != null) Object.DestroyImmediate(instances[i]);
            instances.Clear();
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            scene = default(Scene);
        }

        public void Dispose()
        {
            Close();
        }
    }

    public static class ESWorkbenchPreviewPlacementGUI
    {
        public static void Draw(ESWorkbenchPreviewScene preview, ESWorkbenchPrefabPlacementState state,
            bool hasSuggestedPosition, Vector3 suggestedPosition, System.Action<string, MessageType> setStatus)
        {
            if (preview == null || state == null) return;
            GUILayout.Label("Prefab 预览放置", ES.EditorInternal.ESEditorPresentation.HeaderStyle);
            state.sourcePrefab = (GameObject)EditorGUILayout.ObjectField("已注册 Prefab", state.sourcePrefab, typeof(GameObject), false);
            DrawDropArea(state);
            state.placementEuler = EditorGUILayout.Vector3Field("放置旋转", state.placementEuler);
            state.placementScale = EditorGUILayout.Vector3Field("放置缩放", state.placementScale);
            using (new EditorGUI.DisabledScope(!hasSuggestedPosition || state.sourcePrefab == null))
            {
                if (GUILayout.Button("放置到建议坐标", GUILayout.Height(28f)))
                {
                    if (preview.TryInstantiateRegisteredPrefab(state.sourcePrefab, suggestedPosition,
                            Quaternion.Euler(state.placementEuler), state.placementScale,
                            out GameObject instance, out string key, out string error))
                    {
                        state.selectedInstance = instance;
                        Selection.activeGameObject = instance;
                        setStatus?.Invoke("已放置预览对象：" + key + "。", MessageType.Info);
                    }
                    else setStatus?.Invoke(error, MessageType.Error);
                }
            }
            EditorGUILayout.LabelField("预览对象", preview.Instances.Count.ToString());
            GameObject selected = (GameObject)EditorGUILayout.ObjectField("当前修正对象", state.selectedInstance, typeof(GameObject), true);
            state.selectedInstance = preview.Contains(selected) ? selected : null;
            using (new EditorGUI.DisabledScope(state.selectedInstance == null))
            {
                GameObject instance = state.selectedInstance;
                EditorGUI.BeginChangeCheck();
                Vector3 position = EditorGUILayout.Vector3Field("位置", instance == null ? Vector3.zero : instance.transform.position);
                Vector3 euler = EditorGUILayout.Vector3Field("对象旋转", instance == null ? Vector3.zero : instance.transform.eulerAngles);
                Vector3 scale = EditorGUILayout.Vector3Field("对象缩放", instance == null ? Vector3.one : instance.transform.localScale);
                if (EditorGUI.EndChangeCheck() && instance != null)
                    preview.ApplyTransform(instance, position, euler, scale);
                if (GUILayout.Button("删除当前预览对象", GUILayout.Height(26f)) && instance != null)
                {
                    preview.Remove(instance);
                    state.selectedInstance = null;
                    setStatus?.Invoke("已删除当前预览对象。", MessageType.Info);
                }
            }
            EditorGUILayout.HelpBox("预览对象仅存在于 PreviewScene；关闭预览时自动释放，正式输出必须使用独立提交动作。", MessageType.None);
        }

        private static void DrawDropArea(ESWorkbenchPrefabPlacementState state)
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 38f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "也可从 Project 拖入 Prefab", EditorStyles.helpBox);
            Event current = Event.current;
            if (!rect.Contains(current.mousePosition) ||
                (current.type != EventType.DragUpdated && current.type != EventType.DragPerform)) return;
            GameObject candidate = null;
            for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
                if (DragAndDrop.objectReferences[i] is GameObject gameObject && PrefabUtility.IsPartOfPrefabAsset(gameObject))
                { candidate = gameObject; break; }
            DragAndDrop.visualMode = candidate == null ? DragAndDropVisualMode.Rejected : DragAndDropVisualMode.Copy;
            if (current.type == EventType.DragPerform && candidate != null)
            {
                DragAndDrop.AcceptDrag();
                state.sourcePrefab = candidate;
            }
            current.Use();
        }
    }
}
#endif
