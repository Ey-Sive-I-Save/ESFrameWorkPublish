namespace ES
{
    public enum ESRuntimeModePolicyField
    {
        PlayerInput,
        MoveInput,
        CameraLook,
        CombatInput,
        InteractionInput,
        UIInput,
        CursorVisible,
        CursorLocked,
        WorldPause,
        GameplayHud
    }

    public struct ESRuntimeModePolicyPatch
    {
        public ESPermitLaw playerInput;
        public ESPermitLaw moveInput;
        public ESPermitLaw cameraLook;
        public ESPermitLaw combatInput;
        public ESPermitLaw interactionInput;
        public ESPermitLaw uiInput;
        public ESPermitLaw cursorVisible;
        public ESPermitLaw cursorLocked;
        public ESPermitLaw worldPause;
        public ESPermitLaw gameplayHud;

        public static ESRuntimeModePolicyPatch IgnoreAll
        {
            get
            {
                return new ESRuntimeModePolicyPatch
                {
                    playerInput = ESPermitLaw.Ignore,
                    moveInput = ESPermitLaw.Ignore,
                    cameraLook = ESPermitLaw.Ignore,
                    combatInput = ESPermitLaw.Ignore,
                    interactionInput = ESPermitLaw.Ignore,
                    uiInput = ESPermitLaw.Ignore,
                    cursorVisible = ESPermitLaw.Ignore,
                    cursorLocked = ESPermitLaw.Ignore,
                    worldPause = ESPermitLaw.Ignore,
                    gameplayHud = ESPermitLaw.Ignore
                };
            }
        }
    }

    public struct ESRuntimeModePolicy
    {
        public bool allowPlayerInput;
        public bool allowMoveInput;
        public bool allowCameraLook;
        public bool allowCombatInput;
        public bool allowInteractionInput;
        public bool allowUIInput;
        public bool showCursor;
        public bool lockCursor;
        public bool pauseWorld;
        public bool showGameplayHud;

        public static ESRuntimeModePolicy Default
        {
            get
            {
                return new ESRuntimeModePolicy
                {
                    allowPlayerInput = true,
                    allowMoveInput = true,
                    allowCameraLook = true,
                    allowCombatInput = true,
                    allowInteractionInput = true,
                    allowUIInput = false,
                    showCursor = false,
                    lockCursor = true,
                    pauseWorld = false,
                    showGameplayHud = true
                };
            }
        }
    }

    public struct ESRuntimeModePolicyTrace
    {
        public ESPermitLawResult playerInput;
        public ESPermitLawResult moveInput;
        public ESPermitLawResult cameraLook;
        public ESPermitLawResult combatInput;
        public ESPermitLawResult interactionInput;
        public ESPermitLawResult uiInput;
        public ESPermitLawResult cursorVisible;
        public ESPermitLawResult cursorLocked;
        public ESPermitLawResult worldPause;
        public ESPermitLawResult gameplayHud;
    }
}
