using UnityEngine.InputSystem;

namespace ES
{
    public static class ESInputActionBindingUtility
    {
        public static InputAction CreateInputAction(ESInputActionDefine action, string schemeId = null)
        {
            if (action == null)
                return new InputAction("Empty", InputActionType.Button);

            string actionName = string.IsNullOrEmpty(action.actionName) ? action.id.ToString() : action.actionName;
            InputAction result = new InputAction(actionName, ToInputActionType(action.valueType));
            AddBindings(result, action, schemeId);
            return result;
        }

        public static ESInputBindingDefine ExtractBinding(InputBinding binding, string schemeId = null)
        {
            string path = !string.IsNullOrEmpty(binding.path)
                ? binding.path
                : binding.effectivePath;

            return new ESInputBindingDefine
            {
                schemeId = string.IsNullOrEmpty(schemeId) ? binding.groups : schemeId,
                source = ESInputBindingSource.InputSystem,
                path = path,
                interactions = binding.interactions,
                processors = binding.processors,
                name = binding.name,
                isComposite = binding.isComposite,
                isPartOfComposite = binding.isPartOfComposite
            };
        }

        public static void AddBindings(InputAction action, ESInputActionDefine define, string schemeId = null)
        {
            if (action == null || define == null || define.bindings == null)
                return;

            InputActionSetupExtensions.CompositeSyntax composite = default;
            bool hasComposite = false;
            for (int i = 0; i < define.bindings.Count; i++)
            {
                ESInputBindingDefine binding = define.bindings[i];
                if (binding == null || binding.source != ESInputBindingSource.InputSystem)
                    continue;

                if (!IsActiveScheme(binding.schemeId, schemeId))
                    continue;

                AddBinding(action, binding, ref composite, ref hasComposite);
            }
        }

        private static void AddBinding(
            InputAction action,
            ESInputBindingDefine binding,
            ref InputActionSetupExtensions.CompositeSyntax composite,
            ref bool hasComposite)
        {
            if (string.IsNullOrEmpty(binding.path))
                return;

            if (binding.isComposite)
            {
                composite = action.AddCompositeBinding(
                    binding.path,
                    binding.interactions,
                    binding.processors);
                hasComposite = true;
                return;
            }

            if (binding.isPartOfComposite)
            {
                if (!hasComposite)
                {
                    composite = action.AddCompositeBinding("2DVector");
                    hasComposite = true;
                }

                composite.With(
                    string.IsNullOrEmpty(binding.name) ? "Part" : binding.name,
                    binding.path,
                    binding.schemeId,
                    binding.processors);
                return;
            }

            action.AddBinding(
                binding.path,
                binding.interactions,
                binding.processors,
                binding.schemeId);
            hasComposite = false;
        }

        private static bool IsActiveScheme(string bindingSchemeId, string activeSchemeId)
        {
            return string.IsNullOrEmpty(activeSchemeId)
                   || string.Equals(bindingSchemeId, activeSchemeId, System.StringComparison.Ordinal);
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
