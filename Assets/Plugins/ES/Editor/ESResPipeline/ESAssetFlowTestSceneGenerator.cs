using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    public static class ESAssetFlowTestSceneGenerator
    {
        public const string Folder = "Assets/Plugins/ES/3_Examples/1_Runtime/Example_AssetFlowHotUpdate";
        public const string ScenePath = Folder + "/ESAssetGameCoreFlowHotUpdateTest.unity";

        [MenuItem("【ES】/验证与诊断/验证环境/资源热更新/创建或刷新 Asset 与 GameCore 热更新场景")]
        public static void CreateOrRefresh()
        {
            Directory.CreateDirectory(Folder);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "ESAssetGameCoreFlowHotUpdateTest";

            GameObject root = new GameObject("ES Asset + GameCore Flow Test");

            // The runtime sample assembly is optional, so keep this editor utility independent of it.
            AddOptionalRuntimeController(root);

            GameObject instructions = new GameObject("README - Initialize Consumer then use the runtime panel");
            instructions.transform.SetParent(root.transform, false);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log("[ESFlowTestScene][Editor] Scene created: " + ScenePath);
        }

        private static void AddOptionalRuntimeController(GameObject root)
        {
            System.Type controllerType = System.Type.GetType("ES.ESAssetFlowTestSceneController, ES_Samples.Runtime");
            if (controllerType == null || !typeof(Component).IsAssignableFrom(controllerType))
            {
                Debug.LogWarning("[ESFlowTestScene][Editor] ES_Samples.Runtime is unavailable. Scene was created without its optional test controller.");
                return;
            }

            root.AddComponent(controllerType);
        }
    }
}
