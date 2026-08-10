using UnityEditor;
using UnityEngine;

namespace ES.Editor
{
    public sealed class ESDynamicAtlasMonitorWindow : EditorWindow
    {
        private const int SnapshotEntryDetailLimit = 300;

        private Vector2 scroll;
        private bool autoRefresh = true;
        private double nextRefreshTime;
        private ESDynamicAtlasSnapshot snapshot;
        private ESDynamicAtlasSnapshot pendingSnapshot;
        private bool hasSelectedEntry;
        private ESDynamicAtlasDomainKey selectedDomain;
        private ESDynamicAtlasContentKey selectedContent;
        private int selectedSlotGeneration;

        [MenuItem("【ES】/运行时诊断/动态图集监视器")]
        private static void Open()
        {
            GetWindow<ESDynamicAtlasMonitorWindow>("ES 动态图集");
        }

        private void OnEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            RefreshSnapshot();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            snapshot = null;
            pendingSnapshot = null;
            hasSelectedEntry = false;
        }

        private void OnEditorUpdate()
        {
            if (!autoRefresh || EditorApplication.timeSinceStartup < nextRefreshTime)
                return;

            nextRefreshTime = EditorApplication.timeSinceStartup + 0.25d;
            RefreshSnapshot();
            Repaint();
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.Layout)
                PromotePendingSnapshot();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                autoRefresh = GUILayout.Toggle(autoRefresh, "自动刷新", EditorStyles.toolbarButton);
                if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                    RefreshSnapshot();
                GUILayout.FlexibleSpace();
                GUILayout.Label(EditorApplication.isPlaying ? "运行状态" : "非运行状态", EditorStyles.miniLabel);
            }

            if (snapshot == null)
            {
                EditorGUILayout.HelpBox("当前没有已注册的 ESDynamicAtlasModule。监视器只读，不会为了查询而创建模块。", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawOverview();
            DrawPages();
            DrawEntries();
            DrawSelectedPreview();
            DrawBatchingNotes();
            EditorGUILayout.EndScrollView();
        }

        private void DrawOverview()
        {
            EditorGUILayout.LabelField("运行概览", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField("接受请求", snapshot.acceptingRequests ? "是" : "否");
                EditorGUILayout.LabelField("Provider 就绪", snapshot.providerReady ? "是" : "否");
                EditorGUILayout.LabelField("Provider 代际", snapshot.providerGeneration.ToString());
                EditorGUILayout.LabelField("估算页面显存", EditorUtility.FormatBytes(snapshot.estimatedGpuBytes));
                EditorGUILayout.LabelField("条目", $"Pending {snapshot.pendingCount} / Ready {snapshot.readyCount} / Retired {snapshot.retiredCount} / Failed {snapshot.failedCount} / Lost {snapshot.lostCount}");
                EditorGUILayout.LabelField("GPU 完成令牌", $"等待 {snapshot.waitingFenceCount} / 延迟释放 {snapshot.pendingFenceReleaseCount}");
                EditorGUILayout.LabelField("GPU 完成隔离", $"活动 {snapshot.quarantinedCount} / 终态 {snapshot.quarantinedTerminalCount} / 停机保留 {snapshot.shutdownQuarantinedCount} / 诊断折叠 {snapshot.shutdownQuarantineFoldedCount} / 累计探测 {snapshot.quarantineRetryCount} / 累计失败 {snapshot.quarantineFailureCount}");
                EditorGUILayout.LabelField("上传路径", $"精确 GPU 复制 {snapshot.copyTextureCount} / GPU 转换与扩边 {snapshot.paddingShaderCount} / AsyncGPUReadback 完成回退 {snapshot.deferredFenceFallbackCount}");
                EditorGUILayout.LabelField("上传耗时", $"p50 {snapshot.uploadP50Milliseconds:F2} ms / p95 {snapshot.uploadP95Milliseconds:F2} ms / p99 {snapshot.uploadP99Milliseconds:F2} ms");
                EditorGUILayout.LabelField("页面丢失/重建", snapshot.pageLostCount.ToString());
            }

            if (snapshot.quarantinedCount > 0 || snapshot.quarantinedTerminalCount > 0
                || snapshot.shutdownQuarantinedCount > 0)
            {
                string pages = snapshot.quarantinedPageIds.Count == 0
                    ? "未记录"
                    : string.Join(", ", snapshot.quarantinedPageIds);
                string reason = snapshot.quarantineReasons.Count == 0
                    ? "未返回详细原因。"
                    : snapshot.quarantineReasons[0];
                string status = snapshot.quarantinedTerminalCount > 0 || snapshot.shutdownQuarantinedCount > 0
                    ? "终态隔离不会自动释放；请保留该进程诊断并重启运行环境后复查图形后端。"
                    : "系统会以 0.25 秒间隔发起安全探针；确认完成前，该 Page 不接收新上传。";
                EditorGUILayout.HelpBox(
                    "检测到 GPU 完成状态隔离。影响：源 Texture Lease 与 Page 会继续保留，避免未知 GPU 使用导致过早释放。"
                    + " 页面：" + pages + "。原因：" + reason + " 恢复：" + status,
                    snapshot.quarantinedTerminalCount > 0 || snapshot.shutdownQuarantinedCount > 0
                        ? MessageType.Error
                        : MessageType.Warning);
            }
            EditorGUILayout.Space();
        }

        private void DrawPages()
        {
            EditorGUILayout.LabelField($"页面 ({snapshot.pages.Count})", EditorStyles.boldLabel);
            if (snapshot.pages.Count == 0)
            {
                EditorGUILayout.LabelField("暂无页面", EditorStyles.miniLabel);
                EditorGUILayout.Space();
                return;
            }

            for (int i = 0; i < snapshot.pages.Count; i++)
            {
                ESDynamicAtlasPageSnapshot page = snapshot.pages[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"页面 {page.pageId} · {page.size}² · {page.colorSpace} · {page.alphaMode}", EditorStyles.boldLabel);
                    Rect progress = EditorGUILayout.GetControlRect(false, 18f);
                    EditorGUI.ProgressBar(progress, page.Occupancy,
                        $"占用 {page.Occupancy:P1} · 碎片 {page.fragmentation:P1} · 可用区域 {page.freeRectCount} · 页面代际 {page.pageGeneration}");
                }
            }
            EditorGUILayout.Space();
        }

        private void DrawEntries()
        {
            EditorGUILayout.LabelField($"条目 ({snapshot.entries.Count} / {snapshot.totalEntryCount})", EditorStyles.boldLabel);
            for (int i = 0; i < snapshot.entries.Count; i++)
            {
                ESDynamicAtlasEntrySnapshot entry = snapshot.entries[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(DescribeEntryState(entry.state), GUILayout.Width(92f)))
                    {
                        hasSelectedEntry = true;
                        selectedDomain = entry.domain;
                        selectedContent = entry.content;
                        selectedSlotGeneration = entry.slotGeneration;
                    }
                    GUILayout.Label(entry.domain.ToString(), GUILayout.Width(110f));
                    GUILayout.Label(entry.content.ToString(), GUILayout.MinWidth(180f));
                    GUILayout.Label($"{entry.pixelSize.x}×{entry.pixelSize.y}", GUILayout.Width(74f));
                    GUILayout.Label($"引用 {entry.refCount}", GUILayout.Width(74f));
                    GUILayout.Label(entry.sourceHeld ? "源图保留中" : string.Empty, GUILayout.Width(82f));
                    GUILayout.Label($"页{entry.pageId} 槽{entry.slotGeneration} 放置{entry.placementRevision}", GUILayout.Width(170f));
                }
            }

            if (snapshot.omittedEntryCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"为避免监视器自身复制全部 Entry 详情，本次仅显示前 {SnapshotEntryDetailLimit} 条；其余 {snapshot.omittedEntryCount} 条仍计入概览统计。",
                    MessageType.Info);
            }
            EditorGUILayout.Space();
        }

        private void DrawSelectedPreview()
        {
            EditorGUILayout.LabelField("无损预览", EditorStyles.boldLabel);
            if (!hasSelectedEntry)
            {
                EditorGUILayout.HelpBox("点击上方任意条目的状态，即可查看真实图集页面中的采样区域。", MessageType.Info);
                return;
            }

            if (!TryGetSelectedEntry(out ESDynamicAtlasEntrySnapshot entry))
            {
                EditorGUILayout.HelpBox("所选条目已经释放、迁移或失效，请重新选择当前条目。", MessageType.Warning);
                return;
            }
            if (entry.pageTexture == null
                || (entry.pageTexture is RenderTexture renderTexture && !renderTexture.IsCreated()))
            {
                EditorGUILayout.HelpBox("当前条目没有可预览的图集页面。", MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("条目", entry.content.ToString());
                EditorGUILayout.LabelField("页面格式", entry.pageGraphicsFormat.ToString());
                EditorGUILayout.LabelField("源格式", entry.sourceGraphicsFormat.ToString());
                EditorGUILayout.LabelField("上传方式", DescribeUploadPath(entry.uploadPath));
                if (!string.IsNullOrEmpty(entry.failureMessage))
                    EditorGUILayout.HelpBox(entry.failureMessage, MessageType.Error);

                Rect previewRect = GUILayoutUtility.GetRect(240f, 240f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(previewRect, new Color(0.16f, 0.16f, 0.16f, 1f));
                GUI.DrawTextureWithTexCoords(previewRect, entry.pageTexture, entry.uvRect, true);
                GUI.Label(previewRect, "实际 RenderTexture / UV 区域", EditorStyles.centeredGreyMiniLabel);
            }
        }

        private static string DescribeUploadPath(ESDynamicAtlasUploadPath path)
        {
            switch (path)
            {
                case ESDynamicAtlasUploadPath.CopyTexture:
                    return "精确 GPU 复制";
                case ESDynamicAtlasUploadPath.PaddingShader:
                    return "GPU 转换 + 边缘留白";
                case ESDynamicAtlasUploadPath.DeferredFenceFallback:
                    return "AsyncGPUReadback 完成回退";
                default:
                    return "未完成";
            }
        }

        private static string DescribeEntryState(ESDynamicAtlasEntryState state)
        {
            switch (state)
            {
                case ESDynamicAtlasEntryState.PendingSource:
                    return "等待源图";
                case ESDynamicAtlasEntryState.QueuedUpload:
                    return "等待上传";
                case ESDynamicAtlasEntryState.WaitingGpuFence:
                    return "等待 GPU";
                case ESDynamicAtlasEntryState.Ready:
                    return "可用";
                case ESDynamicAtlasEntryState.Retired:
                    return "已退役";
                case ESDynamicAtlasEntryState.Failed:
                    return "失败";
                case ESDynamicAtlasEntryState.Lost:
                    return "页面丢失";
                case ESDynamicAtlasEntryState.Quarantined:
                    return "GPU 隔离";
                default:
                    return "未知";
            }
        }

        private static void DrawBatchingNotes()
        {
            EditorGUILayout.LabelField("合批判读", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "同一图集页面只是必要条件，不代表已经合批。请用 Frame Debugger 验证；常见阻断包括：不同 Canvas、不同 Page、不同材质/Alpha 模式、不同 Stencil 或 Mask 状态，以及渲染顺序中插入其他材质。",
                MessageType.None);
        }

        private void RefreshSnapshot()
        {
            pendingSnapshot = ESDynamicAtlas.TryGetSnapshot(out ESDynamicAtlasSnapshot current, SnapshotEntryDetailLimit)
                ? current
                : null;
        }

        private void PromotePendingSnapshot()
        {
            if (ReferenceEquals(snapshot, pendingSnapshot))
                return;

            snapshot = pendingSnapshot;
        }

        private bool TryGetSelectedEntry(out ESDynamicAtlasEntrySnapshot selected)
        {
            if (snapshot != null)
            {
                for (int i = 0; i < snapshot.entries.Count; i++)
                {
                    ESDynamicAtlasEntrySnapshot entry = snapshot.entries[i];
                    if (entry.slotGeneration == selectedSlotGeneration
                        && entry.domain.Equals(selectedDomain)
                        && entry.content.Equals(selectedContent))
                    {
                        selected = entry;
                        return true;
                    }
                }
            }

            selected = default;
            return false;
        }
    }
}
