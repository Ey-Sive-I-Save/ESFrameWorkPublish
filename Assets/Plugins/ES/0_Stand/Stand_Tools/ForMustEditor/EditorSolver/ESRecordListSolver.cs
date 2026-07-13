using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace ES
{
    public class ESRecordListSolver<T> : ESEditorSolver
    {
        public string HeaderName = "Record List";
        public bool Draggable = true;
        public bool DisplayAdd = true;
        public bool DisplayRemove = true;
        public bool ShowIndex = true;
        public float DefaultElementHeight = 22f;

        public Func<T> OnCreateElement;
        public Action<T> OnRemoveElement;
        public Action<IList<T>> OnChanged;
        public Func<T, string> GetElementLabel;
        public Func<T, float> GetElementHeight;
        public Action<Rect, T, int, bool, bool> OnDrawElement;
        public Action<Rect> OnDrawHeaderRight;

        private IList<T> records;

#if UNITY_EDITOR
        private ReorderableList reorderableList;
        private System.Collections.IList rawRecords;
#endif

        public int Count => records?.Count ?? 0;

        public ESRecordListSolver<T> InitSolver(
            IList<T> source,
            string headerName = "Record List",
            bool draggable = true,
            bool displayAdd = true,
            bool displayRemove = true,
            Func<T> onCreateElement = null,
            Action<Rect, T, int, bool, bool> onDrawElement = null,
            Action<Rect> onDrawHeaderRight = null,
            Func<T, float> getElementHeight = null,
            Func<T, string> getElementLabel = null,
            Action<T> onRemoveElement = null,
            Action<IList<T>> onChanged = null)
        {
            HeaderName = headerName;
            Draggable = draggable;
            DisplayAdd = displayAdd;
            DisplayRemove = displayRemove;
            OnCreateElement = onCreateElement;
            OnDrawElement = onDrawElement;
            OnDrawHeaderRight = onDrawHeaderRight;
            GetElementHeight = getElementHeight;
            GetElementLabel = getElementLabel;
            OnRemoveElement = onRemoveElement;
            OnChanged = onChanged;
            SetRecords(source);
            Rebuild();
            return CompleteInitSolver<ESRecordListSolver<T>>();
        }

        public void SetRecords(IList<T> source)
        {
            if (ReferenceEquals(records, source))
                return;

            records = source;
            Rebuild();
        }

        public void Rebuild()
        {
#if UNITY_EDITOR
            reorderableList = null;
            EnsureList();
#endif
        }

        public void Draw()
        {
#if UNITY_EDITOR
            EnsureList();
            reorderableList?.DoLayoutList();
#endif
        }

        public void Sort(Comparison<T> comparison)
        {
            if (records == null || comparison == null)
                return;

            if (records is List<T> list)
            {
                list.Sort(comparison);
            }
            else
            {
                var copy = new List<T>(records);
                copy.Sort(comparison);
                for (int i = 0; i < copy.Count; i++)
                    records[i] = copy[i];
            }

            NotifyChanged();
        }

        public bool TryGetSelected(out T value)
        {
            value = default;
#if UNITY_EDITOR
            EnsureList();
            if (records == null || reorderableList == null || reorderableList.index < 0 || reorderableList.index >= records.Count)
                return false;

            value = records[reorderableList.index];
            return true;
#else
            return false;
#endif
        }

        private void NotifyChanged()
        {
            OnChanged?.Invoke(records);
        }

#if UNITY_EDITOR
        private void EnsureList()
        {
            if (records == null)
                return;

            if (reorderableList != null)
                return;

            rawRecords = records as System.Collections.IList;
            if (rawRecords == null)
            {
                Debug.LogError("ESRecordListSolver 需要绑定 List<T> 或实现非泛型 IList 的集合。");
                return;
            }

            reorderableList = new ReorderableList(rawRecords, typeof(T))
            {
                draggable = Draggable,
                displayAdd = DisplayAdd,
                displayRemove = DisplayRemove
            };

            reorderableList.drawHeaderCallback = DrawHeader;
            reorderableList.drawElementCallback = DrawElement;
            reorderableList.elementHeightCallback = GetHeight;
            reorderableList.onAddCallback = AddElement;
            reorderableList.onRemoveCallback = RemoveElement;
            reorderableList.onReorderCallback = _ => NotifyChanged();
        }

        private void DrawHeader(Rect rect)
        {
            var labelRect = rect;
            if (OnDrawHeaderRight != null)
                labelRect.width -= 160f;

            EditorGUI.LabelField(labelRect, $"{HeaderName} ({Count})", EditorStyles.boldLabel);

            if (OnDrawHeaderRight != null)
            {
                var rightRect = new Rect(rect.xMax - 155f, rect.y, 155f, rect.height);
                OnDrawHeaderRight.Invoke(rightRect);
            }
        }

        private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (records == null || index < 0 || index >= records.Count)
                return;

            var item = records[index];
            rect.y += 1f;
            rect.height -= 2f;

            if (isActive)
                EditorGUI.DrawRect(rect, new Color(0.24f, 0.49f, 0.90f, 0.18f));

            if (OnDrawElement != null)
            {
                OnDrawElement.Invoke(rect, item, index, isActive, isFocused);
                return;
            }

            var label = GetElementLabel != null ? GetElementLabel.Invoke(item) : item?.ToString() ?? "null";
            if (ShowIndex)
                label = $"{index}. {label}";

            EditorGUI.LabelField(rect, label);
        }

        private float GetHeight(int index)
        {
            if (records == null || index < 0 || index >= records.Count)
                return DefaultElementHeight;

            return Mathf.Max(DefaultElementHeight, GetElementHeight?.Invoke(records[index]) ?? DefaultElementHeight);
        }

        private void AddElement(ReorderableList list)
        {
            if (records == null)
                return;

            var value = OnCreateElement != null ? OnCreateElement.Invoke() : default;
            records.Add(value);
            list.index = records.Count - 1;
            NotifyChanged();
        }

        private void RemoveElement(ReorderableList list)
        {
            if (records == null || list.index < 0 || list.index >= records.Count)
                return;

            var value = records[list.index];
            OnRemoveElement?.Invoke(value);
            records.RemoveAt(list.index);
            list.index = Mathf.Clamp(list.index, 0, records.Count - 1);
            NotifyChanged();
        }
#endif
    }
}
