using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GraphAsset = global::ES.ESGraphAssetBase;

namespace ES.EditorInternal
{
    [CustomEditor(typeof(GraphAsset), true)]
    internal sealed class ESStableGraphAssetEditor : UnityEditor.Editor
    {
        private GraphAsset validatedGraph;
        private int validatedDirtyCount = int.MinValue;
        private List<ESGraphValidationIssue> cachedIssues;

        public override void OnInspectorGUI()
        {
            GraphAsset graph = (GraphAsset)target;
            EditorGUILayout.HelpBox(
                "图资产是节点、端口和连线的唯一保存权威。请通过稳定图编辑器进行修改。",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("数据版本（内部）", graph.schemaVersion);
                EditorGUILayout.TextField("图领域标识（内部）", graph.DomainId);
                EditorGUILayout.TextField("图用途", ESGraphChinesePresentation.GetDomainName(graph.DomainId));
                EditorGUILayout.IntField("节点", graph.Nodes.Count);
                EditorGUILayout.IntField("连线", graph.Edges.Count);
            }

            if (GUILayout.Button("打开稳定图编辑器 V2"))
            {
                ESStableGraphViewWindow window = ESStableGraphViewWindow.ShowWindow();
                Selection.activeObject = graph;
                window.OpenGraph(graph);
            }

            int dirtyCount = EditorUtility.GetDirtyCount(graph);
            if (validatedGraph != graph || cachedIssues == null || validatedDirtyCount != dirtyCount)
                RefreshValidation(graph, dirtyCount);
            if (GUILayout.Button("重新检查图"))
                RefreshValidation(graph, EditorUtility.GetDirtyCount(graph));

            List<ESGraphValidationIssue> issues = cachedIssues;
            int errors = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                ESGraphValidationIssue issue = issues[i];
                if (issue != null && issue.severity == ESGraphValidationSeverity.Error)
                    errors++;
            }
            EditorGUILayout.HelpBox(errors == 0 ? "当前模型校验通过。" : "当前模型有 " + errors + " 个错误，请在图编辑器中定位处理。",
                errors == 0 ? MessageType.None : MessageType.Error);
        }

        private void RefreshValidation(GraphAsset graph, int dirtyCount)
        {
            validatedGraph = graph;
            validatedDirtyCount = dirtyCount;
            cachedIssues = ESGraphAuthoringRegistry.Validate(graph);
        }
    }
}
