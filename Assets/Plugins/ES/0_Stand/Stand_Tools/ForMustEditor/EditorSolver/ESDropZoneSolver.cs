using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
    public class ESDropZoneSolver : ESEditorSolver
    {
        public Color NormalColor = new Color(1f, 1f, 0f, 0.12f);
        public Color HoverColor = new Color(0.2f, 0.7f, 1f, 0.28f);
        public Color RejectColor = new Color(1f, 0.25f, 0.2f, 0.22f);
        public bool DrawWhenIdle = true;
        public bool AllowFolderExpand = true;
        public bool RejectScripts = true;
        public int MaxCount = 0;
        public string LastRejectReason { get; private set; }
        public int LastAcceptedCount { get; private set; }

        private Type acceptType;
        private int cachedDragHash;
        private readonly List<UnityEngine.Object> cachedPreview = new List<UnityEngine.Object>();

        public ESDropZoneSolver InitSolver()
        {
            return InitSolver((Type)null);
        }

        public ESDropZoneSolver InitSolver<T>(
            bool allowFolderExpand = true,
            bool rejectScripts = true,
            int maxCount = 0,
            bool drawWhenIdle = true) where T : UnityEngine.Object
        {
            return InitSolver(typeof(T), allowFolderExpand, rejectScripts, maxCount, drawWhenIdle);
        }

        public ESDropZoneSolver InitSolver(
            Type acceptType,
            bool allowFolderExpand = true,
            bool rejectScripts = true,
            int maxCount = 0,
            bool drawWhenIdle = true)
        {
            this.acceptType = acceptType;
            AllowFolderExpand = allowFolderExpand;
            RejectScripts = rejectScripts;
            MaxCount = maxCount;
            DrawWhenIdle = drawWhenIdle;
            ClearPreviewCache();
            LastRejectReason = string.Empty;
            return CompleteInitSolver<ESDropZoneSolver>();
        }

        public ESDropZoneSolver Accept<T>() where T : UnityEngine.Object
        {
            acceptType = typeof(T);
            return this;
        }

        public ESDropZoneSolver Accept(Type type)
        {
            acceptType = type;
            return this;
        }

        public bool Draw(Rect area, out UnityEngine.Object[] dropped, Event ev = null)
        {
            dropped = Array.Empty<UnityEngine.Object>();
#if UNITY_EDITOR
            ev ??= Event.current;
            if (ev == null) return false;

            var isDragEvent = ev.type == EventType.DragUpdated || ev.type == EventType.DragPerform;
            var inArea = area.Contains(ev.mousePosition);

            if (!isDragEvent)
            {
                LastRejectReason = string.Empty;
                LastAcceptedCount = 0;
                ClearPreviewCache();
                if (DrawWhenIdle)
                    EditorGUI.DrawRect(area, NormalColor);
                return false;
            }

            if (!inArea)
            {
                LastRejectReason = string.Empty;
                LastAcceptedCount = 0;
                ClearPreviewCache();
                return false;
            }

            var preview = GetPreviewObjects(DragAndDrop.objectReferences);
            var canAccept = preview.Count > 0;

            EditorGUI.DrawRect(area, canAccept ? HoverColor : RejectColor);

            DragAndDrop.visualMode = canAccept ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;

            if (ev.type != EventType.DragPerform)
            {
                ev.Use();
                return false;
            }

            if (!canAccept)
            {
                ev.Use();
                return false;
            }

            dropped = preview.ToArray();
            DragAndDrop.AcceptDrag();
            ClearPreviewCache();
            ev.Use();
            return true;
#else
            return false;
#endif
        }

        private List<UnityEngine.Object> GetPreviewObjects(UnityEngine.Object[] rawObjects)
        {
            var hash = GetDragHash(rawObjects);
            if (hash == cachedDragHash)
                return cachedPreview;

            cachedDragHash = hash;
            cachedPreview.Clear();
            cachedPreview.AddRange(BuildAcceptedObjects(rawObjects));
            LastAcceptedCount = cachedPreview.Count;
            return cachedPreview;
        }

        private void ClearPreviewCache()
        {
            cachedDragHash = 0;
            cachedPreview.Clear();
            LastAcceptedCount = 0;
        }

        private static int GetDragHash(UnityEngine.Object[] rawObjects)
        {
            if (rawObjects == null || rawObjects.Length == 0) return 0;
            unchecked
            {
                var hash = 17;
                for (int i = 0; i < rawObjects.Length; i++)
                    hash = hash * 31 + (rawObjects[i] == null ? 0 : rawObjects[i].GetInstanceID());
                return hash;
            }
        }

        private List<UnityEngine.Object> BuildAcceptedObjects(UnityEngine.Object[] rawObjects)
        {
            LastRejectReason = string.Empty;
            var result = new List<UnityEngine.Object>();
            if (rawObjects == null) return result;

            foreach (var raw in rawObjects)
            {
                if (raw == null) continue;

                if (AllowFolderExpand && ESStandUtility.SafeEditor.IsObjectAsFolder(raw))
                {
                    result.AddRange(ESStandUtility.SafeEditor.ExpandFolder(raw, false, IsAccepted));
                }
                else if (IsAccepted(raw))
                {
                    result.Add(raw);
                }
            }

            if (MaxCount > 0 && result.Count > MaxCount)
            {
                LastRejectReason = $"对象数量超过上限：最多 {MaxCount} 个。";
                result.RemoveRange(MaxCount, result.Count - MaxCount);
            }

            return result;
        }

        private bool IsAccepted(UnityEngine.Object asset)
        {
            if (asset == null) return false;
            if (RejectScripts && ESStandUtility.SafeEditor.IsScriptAsset(asset))
            {
                LastRejectReason = "已过滤 C# 脚本资源。";
                return false;
            }
            if (acceptType != null && !acceptType.IsInstanceOfType(asset))
            {
                LastRejectReason = $"类型不匹配，需要 {acceptType.Name}。";
                return false;
            }
            return true;
        }
    }
}
