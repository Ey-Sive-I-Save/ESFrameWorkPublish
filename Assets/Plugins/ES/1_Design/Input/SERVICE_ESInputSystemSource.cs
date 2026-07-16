using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ES
{
    public sealed class ESInputSystemSource : IDisposable
    {
        private ESInputService inputService;
        private ESInputRuntimeBuildResult buildResult;
        private InputAction[] actionByIndex;
        private ESInputActionId[] enabledActionIds;
        private int enabledActionCount;
        private bool enabled;

        public bool Enabled
        {
            get { return enabled; }
        }

        public void Initialize(ESInputRuntimeBuildResult build, ESInputService input)
        {
            Disable();
            DisposeActions();

            buildResult = build;
            inputService = input;

            if (build == null || build.cache == null || build.cache.values == null)
            {
                actionByIndex = null;
                enabledActionIds = null;
                enabledActionCount = 0;
                return;
            }

            int actionCapacity = build.cache.values.Length;
            actionByIndex = new InputAction[actionCapacity];
            enabledActionIds = new ESInputActionId[actionCapacity];
            enabledActionCount = 0;

            bool[] activeInputActions = BuildActiveInputActionMask(actionCapacity);
            CreateActions(actionCapacity, activeInputActions);
            AddBindings();
            BuildEnabledActionList(actionCapacity);
        }

        public void Enable()
        {
            if (enabled || actionByIndex == null)
                return;

            for (int i = 0; i < enabledActionCount; i++)
            {
                int index = (int)enabledActionIds[i];
                InputAction action = actionByIndex[index];
                if (action != null)
                    action.Enable();
            }

            enabled = true;
        }

        public void Disable()
        {
            if (!enabled || actionByIndex == null)
            {
                enabled = false;
                return;
            }

            for (int i = 0; i < enabledActionCount; i++)
            {
                int index = (int)enabledActionIds[i];
                InputAction action = actionByIndex[index];
                if (action != null)
                    action.Disable();
            }

            enabled = false;
        }

        public void Update(float time, bool clearFrameState = true)
        {
            if (!enabled)
                return;

            if (clearFrameState)
                inputService.BeginFrame();

            for (int i = 0; i < enabledActionCount; i++)
            {
                ESInputActionId id = enabledActionIds[i];
                int index = (int)id;
                InputAction action = actionByIndex[index];
                ESInputValueType valueType = buildResult.cache.metas[index].valueType;
                switch (valueType)
                {
                    case ESInputValueType.Button:
                        inputService.WriteButton(id, action.IsPressed(), time);
                        break;
                    case ESInputValueType.Axis:
                        inputService.WriteAxis(id, action.ReadValue<float>());
                        break;
                    case ESInputValueType.Vector2:
                        inputService.WriteVector2(id, action.ReadValue<Vector2>());
                        break;
                }
            }

            if (clearFrameState)
                inputService.EndFrame(time);
        }

        public void Dispose()
        {
            Disable();
            DisposeActions();
            inputService = null;
            buildResult = null;
            enabledActionIds = null;
            enabledActionCount = 0;
        }

        private bool[] BuildActiveInputActionMask(int actionCapacity)
        {
            bool[] activeInputActions = new bool[actionCapacity];
            if (buildResult == null || buildResult.bindings == null)
                return activeInputActions;

            string activeSchemeId = buildResult.activeSchemeId;
            for (int i = 0; i < buildResult.bindingCount; i++)
            {
                ESInputCompiledBinding binding = buildResult.bindings[i];
                if (binding.source != ESInputBindingSource.InputSystem)
                    continue;

                if (!IsActiveScheme(binding.schemeId, activeSchemeId))
                    continue;

                if (string.IsNullOrEmpty(binding.effectivePath))
                    continue;

                int actionIndex = (int)binding.actionId;
                if (actionIndex < 0 || actionIndex >= actionCapacity)
                    continue;

                activeInputActions[actionIndex] = true;
            }

            return activeInputActions;
        }

        private void CreateActions(int actionCapacity, bool[] activeInputActions)
        {
            for (int i = 0; i < actionCapacity; i++)
            {
                if (activeInputActions == null || i >= activeInputActions.Length || !activeInputActions[i])
                    continue;

                ESInputActionMeta meta = buildResult.cache.metas[i];
                if (meta.id == ESInputActionId.Dynamic && string.IsNullOrEmpty(meta.actionName))
                    continue;

                string fallbackName = !string.IsNullOrEmpty(meta.actionName) ? meta.actionName : meta.id.ToString();
                actionByIndex[i] = new InputAction(fallbackName, ToInputActionType(meta.valueType));
            }
        }

        private void AddBindings()
        {
            string activeSchemeId = buildResult.activeSchemeId;

            for (int i = 0; i < buildResult.bindingCount; i++)
            {
                ESInputCompiledBinding binding = buildResult.bindings[i];
                if (binding.source != ESInputBindingSource.InputSystem)
                    continue;

                if (!IsActiveScheme(binding.schemeId, activeSchemeId))
                    continue;

                int actionIndex = (int)binding.actionId;
                if (actionByIndex == null || actionIndex < 0 || actionIndex >= actionByIndex.Length)
                    continue;

                InputAction action = actionByIndex[actionIndex];
                if (action == null || string.IsNullOrEmpty(binding.effectivePath))
                    continue;

                if (binding.isComposite)
                {
                    InputActionSetupExtensions.CompositeSyntax composite =
                        action.AddCompositeBinding(
                            binding.effectivePath,
                            binding.interactions,
                            binding.processors);

                    i = AddCompositeParts(action, composite, i + 1, binding.schemeId);
                    continue;
                }

                if (binding.isPartOfComposite)
                    continue;

                action.AddBinding(
                    binding.effectivePath,
                    binding.interactions,
                    binding.processors,
                    binding.schemeId);
            }
        }

        private int AddCompositeParts(
            InputAction action,
            InputActionSetupExtensions.CompositeSyntax composite,
            int startIndex,
            string schemeId)
        {
            int i = startIndex;
            for (; i < buildResult.bindingCount; i++)
            {
                ESInputCompiledBinding part = buildResult.bindings[i];
                if (!part.isPartOfComposite)
                    break;

                if (!string.Equals(part.schemeId, schemeId, StringComparison.Ordinal))
                    continue;

                if (string.IsNullOrEmpty(part.name) || string.IsNullOrEmpty(part.effectivePath))
                    continue;

                composite.With(part.name, part.effectivePath, part.schemeId, part.processors);
            }

            return i - 1;
        }

        private static bool IsActiveScheme(string bindingSchemeId, string activeSchemeId)
        {
            return string.IsNullOrEmpty(activeSchemeId)
                   || string.Equals(bindingSchemeId, activeSchemeId, StringComparison.Ordinal);
        }

        private void BuildEnabledActionList(int actionCapacity)
        {
            enabledActionCount = 0;
            for (int i = 0; i < actionCapacity; i++)
            {
                InputAction action = actionByIndex[i];
                if (action == null || action.bindings.Count == 0)
                    continue;

                enabledActionIds[enabledActionCount++] = (ESInputActionId)i;
            }
        }

        private void DisposeActions()
        {
            if (actionByIndex == null)
                return;

            for (int i = 0; i < actionByIndex.Length; i++)
            {
                if (actionByIndex[i] == null)
                    continue;

                actionByIndex[i].Dispose();
                actionByIndex[i] = null;
            }

            actionByIndex = null;
        }

        private static InputActionType ToInputActionType(ESInputValueType valueType)
        {
            switch (valueType)
            {
                case ESInputValueType.Button:
                    return InputActionType.Button;
                default:
                    return InputActionType.Value;
            }
        }
    }
}
