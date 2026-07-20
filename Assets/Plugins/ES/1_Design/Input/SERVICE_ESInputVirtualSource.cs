using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    public struct ESInputVirtualControlHandle
    {
        public int index;
        public ESInputValueType valueType;

        public bool IsValid
        {
            get { return index >= 0; }
        }

        public static ESInputVirtualControlHandle Invalid
        {
            get
            {
                return new ESInputVirtualControlHandle
                {
                    index = -1,
                    valueType = ESInputValueType.Button
                };
            }
        }
    }

    public sealed class ESInputVirtualSource : IDisposable
    {
        private ESInputService inputService;
        private Dictionary<string, ESInputVirtualControlHandle> virtualHandles;
        private bool[] buttonEnabled;
        private bool[] buttonHeld;
        private bool[] buttonPulse;
        private bool[] axisEnabled;
        private float[] axisValues;
        private bool[] vector2Enabled;
        private Vector2[] vector2Values;
        private bool[] buttonAllowed;
        private bool[] axisAllowed;
        private bool[] vector2Allowed;
        private int[] activeIndices;
        private int[] activePositions;
        private int activeCount;

        public void Initialize(ESInputRuntimeBuildResult build, ESInputService input)
        {
            Dispose();
            inputService = input;

            int capacity = 0;
            if (build != null && build.cache != null && build.cache.values != null)
                capacity = build.cache.values.Length;

            if (capacity <= 0)
                capacity = 1;

            virtualHandles = new Dictionary<string, ESInputVirtualControlHandle>(32, StringComparer.Ordinal);
            buttonEnabled = new bool[capacity];
            buttonHeld = new bool[capacity];
            buttonPulse = new bool[capacity];
            axisEnabled = new bool[capacity];
            axisValues = new float[capacity];
            vector2Enabled = new bool[capacity];
            vector2Values = new Vector2[capacity];
            buttonAllowed = new bool[capacity];
            axisAllowed = new bool[capacity];
            vector2Allowed = new bool[capacity];
            activeIndices = new int[capacity];
            activePositions = new int[capacity];
            for (int i = 0; i < activePositions.Length; i++)
                activePositions[i] = -1;
            activeCount = 0;

            BuildAllowedMask(build);
        }

        public void Dispose()
        {
            ClearAll();
            inputService = null;
            virtualHandles = null;
            buttonEnabled = null;
            buttonHeld = null;
            buttonPulse = null;
            axisEnabled = null;
            axisValues = null;
            vector2Enabled = null;
            vector2Values = null;
            buttonAllowed = null;
            axisAllowed = null;
            vector2Allowed = null;
            activeIndices = null;
            activePositions = null;
            activeCount = 0;
        }

        public bool TryGetHandle(string virtualControlId, out ESInputVirtualControlHandle handle)
        {
            if (virtualHandles != null && !string.IsNullOrEmpty(virtualControlId))
                return virtualHandles.TryGetValue(virtualControlId, out handle);

            handle = ESInputVirtualControlHandle.Invalid;
            return false;
        }

        public ESInputVirtualControlHandle GetHandleOrInvalid(string virtualControlId)
        {
            return TryGetHandle(virtualControlId, out ESInputVirtualControlHandle handle)
                ? handle
                : ESInputVirtualControlHandle.Invalid;
        }

        public void SetButton(string virtualControlId, bool held)
        {
            if (TryGetHandle(virtualControlId, out ESInputVirtualControlHandle handle))
                SetButton(handle, held);
        }

        public void PulseButton(string virtualControlId)
        {
            if (TryGetHandle(virtualControlId, out ESInputVirtualControlHandle handle))
                PulseButton(handle);
        }

        public void ClearButton(string virtualControlId)
        {
            if (TryGetHandle(virtualControlId, out ESInputVirtualControlHandle handle))
                ClearButton(handle);
        }

        public void SetAxis(string virtualControlId, float value)
        {
            if (TryGetHandle(virtualControlId, out ESInputVirtualControlHandle handle))
                SetAxis(handle, value);
        }

        public void ClearAxis(string virtualControlId)
        {
            if (TryGetHandle(virtualControlId, out ESInputVirtualControlHandle handle))
                ClearAxis(handle);
        }

        public void SetVector2(string virtualControlId, Vector2 value)
        {
            if (TryGetHandle(virtualControlId, out ESInputVirtualControlHandle handle))
                SetVector2(handle, value);
        }

        public void ClearVector2(string virtualControlId)
        {
            if (TryGetHandle(virtualControlId, out ESInputVirtualControlHandle handle))
                ClearVector2(handle);
        }

        public void SetButton(ESInputVirtualControlHandle handle, bool held)
        {
            if (!handle.IsValid || handle.valueType != ESInputValueType.Button)
                return;

            SetButtonByIndex(handle.index, held);
        }

        public void ClearButton(ESInputVirtualControlHandle handle)
        {
            if (!handle.IsValid || handle.valueType != ESInputValueType.Button)
                return;

            ClearButtonByIndex(handle.index);
        }

        public void PulseButton(ESInputVirtualControlHandle handle)
        {
            if (!handle.IsValid || handle.valueType != ESInputValueType.Button)
                return;

            PulseButtonByIndex(handle.index);
        }

        public void SetButton(ESInputActionId id, bool held)
        {
            if (!CanWriteAction(id, ESInputValueType.Button))
                return;

            SetButtonByIndex((int)id, held, false);
        }

        public void PulseButton(ESInputActionId id)
        {
            if (!CanWriteAction(id, ESInputValueType.Button))
                return;

            PulseButtonByIndex((int)id, false);
        }

        private void SetButtonByIndex(int index, bool held, bool requireAllowed = true)
        {
            if (!IsValid(index, buttonHeld))
                return;

            if (requireAllowed && !buttonAllowed[index])
                return;

            buttonEnabled[index] = true;
            buttonHeld[index] = held;
            buttonPulse[index] = false;
            MarkActive(index);
        }

        public void ClearButton(ESInputActionId id)
        {
            ClearButtonByIndex((int)id);
        }

        private void PulseButtonByIndex(int index, bool requireAllowed = true)
        {
            if (!IsValid(index, buttonHeld))
                return;

            if (requireAllowed && !buttonAllowed[index])
                return;

            buttonEnabled[index] = true;
            buttonHeld[index] = true;
            buttonPulse[index] = true;
            MarkActive(index);
        }

        private void ClearButtonByIndex(int index)
        {
            if (!IsValid(index, buttonHeld))
                return;

            buttonEnabled[index] = false;
            buttonHeld[index] = false;
            if (IsValid(index, buttonPulse))
                buttonPulse[index] = false;
            TryRemoveActive(index);
        }

        public void SetAxis(ESInputVirtualControlHandle handle, float value)
        {
            if (!handle.IsValid || handle.valueType != ESInputValueType.Axis)
                return;

            SetAxisByIndex(handle.index, value);
        }

        public void ClearAxis(ESInputVirtualControlHandle handle)
        {
            if (!handle.IsValid || handle.valueType != ESInputValueType.Axis)
                return;

            ClearAxisByIndex(handle.index);
        }

        public void SetAxis(ESInputActionId id, float value)
        {
            if (!CanWriteAction(id, ESInputValueType.Axis))
                return;

            SetAxisByIndex((int)id, value, false);
        }

        private void SetAxisByIndex(int index, float value, bool requireAllowed = true)
        {
            if (!IsValid(index, axisValues))
                return;

            if (requireAllowed && !axisAllowed[index])
                return;

            axisEnabled[index] = true;
            axisValues[index] = value;
            MarkActive(index);
        }

        public void ClearAxis(ESInputActionId id)
        {
            ClearAxisByIndex((int)id);
        }

        private void ClearAxisByIndex(int index)
        {
            if (!IsValid(index, axisValues))
                return;

            axisEnabled[index] = false;
            axisValues[index] = 0f;
            TryRemoveActive(index);
        }

        public void SetVector2(ESInputVirtualControlHandle handle, Vector2 value)
        {
            if (!handle.IsValid || handle.valueType != ESInputValueType.Vector2)
                return;

            SetVector2ByIndex(handle.index, value);
        }

        public void ClearVector2(ESInputVirtualControlHandle handle)
        {
            if (!handle.IsValid || handle.valueType != ESInputValueType.Vector2)
                return;

            ClearVector2ByIndex(handle.index);
        }

        public void SetVector2(ESInputActionId id, Vector2 value)
        {
            if (!CanWriteAction(id, ESInputValueType.Vector2))
                return;

            SetVector2ByIndex((int)id, value, false);
        }

        private void SetVector2ByIndex(int index, Vector2 value, bool requireAllowed = true)
        {
            if (!IsValid(index, vector2Values))
                return;

            if (requireAllowed && !vector2Allowed[index])
                return;

            vector2Enabled[index] = true;
            vector2Values[index] = value;
            MarkActive(index);
        }

        public void ClearVector2(ESInputActionId id)
        {
            ClearVector2ByIndex((int)id);
        }

        private void ClearVector2ByIndex(int index)
        {
            if (!IsValid(index, vector2Values))
                return;

            vector2Enabled[index] = false;
            vector2Values[index] = Vector2.zero;
            TryRemoveActive(index);
        }

        public void Update(float time)
        {
            int i = 0;
            while (i < activeCount)
            {
                int index = activeIndices[i];
                if (buttonEnabled[index])
                {
                    inputService.WriteButton((ESInputActionId)index, buttonHeld[index], time);

                    if (IsValid(index, buttonPulse) && buttonPulse[index])
                    {
                        buttonPulse[index] = false;
                        buttonEnabled[index] = false;
                        buttonHeld[index] = false;
                    }
                }

                if (axisEnabled[index])
                    inputService.WriteAxis((ESInputActionId)index, axisValues[index]);

                if (vector2Enabled[index])
                    inputService.WriteVector2((ESInputActionId)index, vector2Values[index]);

                if (!buttonEnabled[index] && !axisEnabled[index] && !vector2Enabled[index])
                {
                    TryRemoveActive(index);
                    continue;
                }

                i++;
            }
        }

        public void ClearAll()
        {
            if (activeIndices == null || activePositions == null)
            {
                activeCount = 0;
                return;
            }

            for (int i = 0; i < activeCount; i++)
            {
                int index = activeIndices[i];
                if (IsValid(index, buttonEnabled))
                    buttonEnabled[index] = false;
                if (IsValid(index, buttonHeld))
                    buttonHeld[index] = false;
                if (IsValid(index, buttonPulse))
                    buttonPulse[index] = false;
                if (IsValid(index, axisEnabled))
                    axisEnabled[index] = false;
                if (IsValid(index, axisValues))
                    axisValues[index] = 0f;
                if (IsValid(index, vector2Enabled))
                    vector2Enabled[index] = false;
                if (IsValid(index, vector2Values))
                    vector2Values[index] = Vector2.zero;
                activePositions[index] = -1;
                activeIndices[i] = 0;
            }

            activeCount = 0;
        }

        private static bool IsValid<T>(int index, T[] array)
        {
            return array != null && index >= 0 && index < array.Length;
        }

        private bool CanWriteAction(ESInputActionId id, ESInputValueType valueType)
        {
            int index = (int)id;
            ESInputRuntimeCache cache = inputService.Cache;
            return cache != null
                   && cache.IsValidIndex(index)
                   && cache.metas[index].valueType == valueType;
        }

        private void BuildAllowedMask(ESInputRuntimeBuildResult build)
        {
            if (build == null || build.bindings == null || build.cache == null)
                return;

            string activeSchemeId = build.activeSchemeId;
            for (int i = 0; i < build.bindingCount; i++)
            {
                ESInputCompiledBinding binding = build.bindings[i];
                if (binding.source != ESInputBindingSource.VirtualControl)
                    continue;

                if (!IsActiveScheme(binding.schemeId, activeSchemeId))
                    continue;

                if (string.IsNullOrEmpty(binding.virtualControlId))
                    continue;

                int index = (int)binding.actionId;
                if (!IsValid(index, buttonAllowed) || !build.cache.IsValidIndex(index))
                    continue;

                ESInputValueType valueType = build.cache.metas[index].valueType;
                virtualHandles[binding.virtualControlId] = new ESInputVirtualControlHandle
                {
                    index = index,
                    valueType = valueType
                };

                switch (valueType)
                {
                    case ESInputValueType.Button:
                        buttonAllowed[index] = true;
                        break;
                    case ESInputValueType.Axis:
                        axisAllowed[index] = true;
                        break;
                    case ESInputValueType.Vector2:
                        vector2Allowed[index] = true;
                        break;
                }
            }
        }

        private static bool IsActiveScheme(string bindingSchemeId, string activeSchemeId)
        {
            return string.IsNullOrEmpty(activeSchemeId)
                   || string.Equals(bindingSchemeId, activeSchemeId, System.StringComparison.Ordinal);
        }

        private void MarkActive(int index)
        {
            if (!IsValid(index, activePositions) || activePositions[index] >= 0)
                return;

            activePositions[index] = activeCount;
            activeIndices[activeCount++] = index;
        }

        private void TryRemoveActive(int index)
        {
            if (!IsValid(index, activePositions) || activePositions[index] < 0)
                return;

            if (buttonEnabled[index] || axisEnabled[index] || vector2Enabled[index])
                return;

            int position = activePositions[index];
            int lastPosition = activeCount - 1;
            int lastIndex = activeIndices[lastPosition];

            activeIndices[position] = lastIndex;
            activePositions[lastIndex] = position;
            activeIndices[lastPosition] = 0;
            activePositions[index] = -1;
            activeCount = lastPosition;
        }
    }
}
