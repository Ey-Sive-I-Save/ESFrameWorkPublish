#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gaskellgames.EditorOnly
{
    [Overlay(typeof(SceneView), "GgCore")]
    public class GgToolbar : ToolbarOverlay
    {
        GgToolbar() : base( AddComment.id, IEditorUpdateCount.id ) { }

    } // class end

    //----------------------------------------------------------------------------------------------------

    [EditorToolbarElement(id, typeof(SceneView))]
    public class AddComment : EditorToolbarButton
    {
        private const string packageRefName = "GgCore";
        private const string relativePath = "/Editor/Icons/";
        
        public const string id = "GGToolbar/AddComment";

        public AddComment()
        {
            text = "";
            if (!GgPackageRef.TryGetFullFilePath(packageRefName, relativePath, out string filePath)) { return; }
            icon = AssetDatabase.LoadAssetAtPath(filePath + "Icon_Comment.png", typeof(Texture2D)) as Texture2D;
            tooltip = "Add a comment at the scene view camera look at position.";
            clicked += OnClick;
        }

        private void OnClick()
        {
            // create comment at scene view look at
            EditorExtensions.GetSceneViewLookAt(out Vector3 point);
            GameObject go = new GameObject();
            go.name = "Comment";
            go.transform.position = point;
            go.AddComponent<Comment>();
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);

            // select and ping comment
            EditorGUIUtility.PingObject(go);
            Selection.activeObject = go;
        }

    } // class end
    
    //----------------------------------------------------------------------------------------------------

    [EditorToolbarElement(id, typeof(SceneView))]
    public class IEditorUpdateCount : EditorToolbarButton
    {
        public const string id = "GGToolbar/IEditorUpdateCount";

        public IEditorUpdateCount()
        {
            text = GgEditorUpdateLoop.iEditorUpdateList.Count.ToString();
            tooltip = "IEditorUpdate count: Click to refresh editor callbacks.";
            clicked += OnClick;
            
            GgEditorUpdateLoop.OnIEditorUpdateListUpdated += UpdateText;
        }

        private void OnClick()
        {
            GgEditorUpdateLoop.ForceUpdateComponentList();
        }

        private void UpdateText()
        {
            text = GgEditorUpdateLoop.iEditorUpdateList.Count.ToString();
        }

    } // class end
    
    //----------------------------------------------------------------------------------------------------

    // [EditorToolbarElement(id, typeof(SceneView))]
    // public class SceneViewZoom : EditorToolbarButton
    // {
    //     public const string id = "GGToolbar/SceneViewZoom";
    //
    //     public SceneViewZoom()
    //     {
    //         text = GetTextValue();
    //         tooltip = "SceneView Zoom: Click to refresh.";
    //         clicked += OnClick;
    //     }
    //
    //     private void OnClick()
    //     {
    //         text = GetTextValue();
    //     }
    //
    //     private string GetTextValue()
    //     {
    //         return SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView.size.ToString() : "?";
    //     }
    //
    // } // class end

    //----------------------------------------------------------------------------------------------------

    /*
    [EditorToolbarElement(id, typeof(SceneView))]
    class SceneViewDebugToggle : EditorToolbarToggle
    {
        public const string id = "GGToolbar/SceneViewDebug";
        
        public SceneViewDebugToggle()
        {
            text = "OFF";
            icon = EditorGUIUtility.IconContent("d_DebuggerDisabled").image as Texture2D;
            tooltip = "Toggle scene view debug.";

            // Register the class to a callback for when the toggle’s state changes
            this.RegisterValueChangedCallback(OnStateChange);
        }

        private void OnStateChange(ChangeEvent<bool> evt)
        {
            if (evt.newValue)
            {
                OnToggleOn();

                text = "ON";
                icon = EditorGUIUtility.IconContent("d_DebuggerAttached").image as Texture2D;
            }
            else
            {
                OnToggleOff();

                text = "OFF";
                icon = EditorGUIUtility.IconContent("d_DebuggerDisabled").image as Texture2D;
            }
        }

        private void OnToggleOn()
        {
            // DO SOMETHING
        }

        private void OnToggleOff()
        {
            // DO SOMETHING
        }
        
    } // class end
    */
}

#endif