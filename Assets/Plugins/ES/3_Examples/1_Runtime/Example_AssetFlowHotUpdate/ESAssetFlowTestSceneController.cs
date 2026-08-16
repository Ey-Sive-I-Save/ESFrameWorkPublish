using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ES
{
    /// <summary>Runtime entry for the hot-update AssetRefer + GameCore acceptance scene.</summary>
    public sealed class ESAssetFlowTestSceneController : MonoBehaviour
    {
        [SerializeField] private ESFlowTestEnumKey testKey = ESFlowTestEnumKey.CommercialFlowTest;
        [SerializeField] private bool runAutomatically;
        [SerializeField] private float automaticDelaySeconds = 0.5f;

        private ESAssetGameCoreFlowTestDataInfo testData;
        private Vector2 scroll;
        private string status = "Waiting for GameCore runtime data...";
        private bool running;

        public bool ResolveTestData()
        {
            if (ESFlowTestGameCore.Table.TryGet((int)testKey, out ESFlowTestRuntimeData runtimeData) && runtimeData?.source != null)
            {
                testData = runtimeData.source;
                status = "[PASS] GameCore test root resolved: " + testData.name;
                return true;
            }
            testData = null;
            status = "[FAIL] GameCore test root unavailable. Initialize its Consumer before loading this scene.";
            return false;
        }

        private void Start()
        {
            Debug.Log("[ESFlowTestScene][HotUpdate] Script revision=" + ESAssetFlowTestHotUpdateProbe.ScriptRevision, this);
            ResolveTestData();
            if (runAutomatically) RunAutomaticAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private void OnEnable() => Debug.Log("[ESFlowTestScene][Lifecycle] Enabled", this);
        private void OnDisable() => Debug.Log("[ESFlowTestScene][Lifecycle] Disabled", this);
        private void OnDestroy()
        {
            bool released = testData != null && testData.ReleaseTestReferences(this);
            Debug.Log("[ESFlowTestScene][Lifecycle] Destroyed; owned test references released=" + released, this);
        }

        public void RunConfigurationTest() { if (EnsureData()) { testData.DebugConfiguration(); status = testData.lastReport; } }
        public void RunGameCoreTest() { if (EnsureData()) { testData.DebugGameCoreQueries(); status = testData.lastReport; } }
        public void RunReadyTest() { if (EnsureData()) { testData.DebugReadyHotPath(); status = testData.lastReport; } }
        public void RunAssetLoadTest() => RunAssetLoadTestAsync(this.GetCancellationTokenOnDestroy()).Forget();
        public void ReleaseTestAssets()
        {
            if (!EnsureData()) return;
            bool released = testData.ReleaseTestReferences(this);
            status = released
                ? "[ESFlowTestScene][Release][PASS]"
                : "[ESFlowTestScene][Release][SKIP] This controller does not own test references.";
        }

        private async UniTask RunAssetLoadTestAsync(CancellationToken token)
        {
            if (!EnsureData() || running) return;
            running = true;
            status = "[ESFlowTestScene][Load] Running...";
            try { status = await testData.RunAssetLoadTestAsync(this, token); }
            catch (OperationCanceledException) { status = "[ESFlowTestScene][Load] Cancelled."; }
            catch (Exception exception) { status = "[ESFlowTestScene][Load][FAIL] " + exception; Debug.LogException(exception, this); }
            finally { running = false; }
        }

        private async UniTaskVoid RunAutomaticAsync(CancellationToken token)
        {
            if (automaticDelaySeconds > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(automaticDelaySeconds), DelayType.Realtime, PlayerLoopTiming.Update, token);
            if (!EnsureData()) return;
            RunConfigurationTest();
            RunGameCoreTest();
            await RunAssetLoadTestAsync(token);
            RunReadyTest();
        }

        private bool EnsureData() => testData != null || ResolveTestData();

        private void OnGUI()
        {
            Rect area = new Rect(20f, 20f, Mathf.Min(760f, Screen.width - 40f), Mathf.Max(260f, Screen.height - 40f));
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("ES AssetRef + GameCore Hot-Update Acceptance Scene");
            GUILayout.Label("Hot-update script revision: " + ESAssetFlowTestHotUpdateProbe.ScriptRevision);
            GUILayout.BeginHorizontal();
            GUI.enabled = !running;
            if (GUILayout.Button("Resolve", GUILayout.Height(30f))) ResolveTestData();
            if (GUILayout.Button("Config", GUILayout.Height(30f))) RunConfigurationTest();
            if (GUILayout.Button("GameCore", GUILayout.Height(30f))) RunGameCoreTest();
            if (GUILayout.Button("Load", GUILayout.Height(30f))) RunAssetLoadTest();
            if (GUILayout.Button("Ready O(1)", GUILayout.Height(30f))) RunReadyTest();
            if (GUILayout.Button("Release", GUILayout.Height(30f))) ReleaseTestAssets();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll, GUI.skin.box);
            GUILayout.TextArea(status ?? string.Empty, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }

    public static class ESAssetFlowTestHotUpdateProbe
    {
        public const string ScriptRevision = "1.0.0";
    }
}
