using ES;

namespace ES.Internal
{
    public static class ESRuntimeModeDefaults
    {
        public static int GetModePriority(ESRuntimeMode mode)
        {
            switch (mode)
            {
                case ESRuntimeMode.Loading:
                    return 1000;
                case ESRuntimeMode.SceneTransition:
                    return 950;
                case ESRuntimeMode.RebindInput:
                    return 900;
                case ESRuntimeMode.ConfirmDialog:
                    return 850;
                case ESRuntimeMode.PauseMenu:
                    return 800;
                case ESRuntimeMode.Cutscene:
                    return 750;
                case ESRuntimeMode.Dialogue:
                    return 650;
                case ESRuntimeMode.Inventory:
                case ESRuntimeMode.Map:
                case ESRuntimeMode.Settings:
                    return 500;
                case ESRuntimeMode.PhotoMode:
                    return 450;
                case ESRuntimeMode.MainMenu:
                    return 400;
                case ESRuntimeMode.Spectator:
                    return 300;
                default:
                    return 0;
            }
        }

        public static int GetTagPriority(ESRuntimeModeTag tag)
        {
            switch (tag)
            {
                case ESRuntimeModeTag.Dead:
                    return 700;
                case ESRuntimeModeTag.Stunned:
                    return 650;
                case ESRuntimeModeTag.NetworkBusy:
                    return 600;
                case ESRuntimeModeTag.Climbing:
                case ESRuntimeModeTag.Mounted:
                    return 200;
                case ESRuntimeModeTag.Combat:
                case ESRuntimeModeTag.Aiming:
                    return 100;
                default:
                    return 0;
            }
        }

        public static ESRuntimeModePolicyPatch GetModePatch(ESRuntimeMode mode)
        {
            ESRuntimeModePolicyPatch patch = ESRuntimeModePolicyPatch.IgnoreAll;

            switch (mode)
            {
                case ESRuntimeMode.Gameplay:
                    patch.playerInput = ESPermitLaw.AllowEnable;
                    patch.moveInput = ESPermitLaw.AllowEnable;
                    patch.cameraLook = ESPermitLaw.AllowEnable;
                    patch.combatInput = ESPermitLaw.AllowEnable;
                    patch.interactionInput = ESPermitLaw.AllowEnable;
                    patch.uiInput = ESPermitLaw.AllowDisable;
                    patch.cursorVisible = ESPermitLaw.AllowDisable;
                    patch.cursorLocked = ESPermitLaw.AllowEnable;
                    patch.worldPause = ESPermitLaw.AllowDisable;
                    patch.gameplayHud = ESPermitLaw.AllowEnable;
                    break;

                case ESRuntimeMode.MainMenu:
                    patch.playerInput = ESPermitLaw.AllowDisable;
                    patch.moveInput = ESPermitLaw.AllowDisable;
                    patch.cameraLook = ESPermitLaw.AllowDisable;
                    patch.combatInput = ESPermitLaw.AllowDisable;
                    patch.interactionInput = ESPermitLaw.AllowDisable;
                    patch.uiInput = ESPermitLaw.AllowEnable;
                    patch.cursorVisible = ESPermitLaw.AllowEnable;
                    patch.cursorLocked = ESPermitLaw.AllowDisable;
                    patch.gameplayHud = ESPermitLaw.AllowDisable;
                    break;

                case ESRuntimeMode.PauseMenu:
                    patch.playerInput = ESPermitLaw.AllowDisable;
                    patch.moveInput = ESPermitLaw.AllowDisable;
                    patch.cameraLook = ESPermitLaw.AllowDisable;
                    patch.combatInput = ESPermitLaw.AllowDisable;
                    patch.interactionInput = ESPermitLaw.AllowDisable;
                    patch.uiInput = ESPermitLaw.AllowEnable;
                    patch.cursorVisible = ESPermitLaw.AllowEnable;
                    patch.cursorLocked = ESPermitLaw.AllowDisable;
                    patch.worldPause = ESPermitLaw.AllowEnable;
                    break;

                case ESRuntimeMode.Loading:
                case ESRuntimeMode.SceneTransition:
                    patch.playerInput = ESPermitLaw.HardDisable;
                    patch.moveInput = ESPermitLaw.HardDisable;
                    patch.cameraLook = ESPermitLaw.HardDisable;
                    patch.combatInput = ESPermitLaw.HardDisable;
                    patch.interactionInput = ESPermitLaw.HardDisable;
                    patch.uiInput = ESPermitLaw.AllowDisable;
                    patch.cursorVisible = ESPermitLaw.Ignore;
                    patch.worldPause = ESPermitLaw.HardEnable;
                    break;

                case ESRuntimeMode.Cutscene:
                    patch.playerInput = ESPermitLaw.HardDisable;
                    patch.moveInput = ESPermitLaw.HardDisable;
                    patch.cameraLook = ESPermitLaw.HardDisable;
                    patch.combatInput = ESPermitLaw.HardDisable;
                    patch.interactionInput = ESPermitLaw.HardDisable;
                    patch.gameplayHud = ESPermitLaw.AllowDisable;
                    break;

                case ESRuntimeMode.Dialogue:
                    patch.moveInput = ESPermitLaw.AllowDisable;
                    patch.cameraLook = ESPermitLaw.AllowDisable;
                    patch.combatInput = ESPermitLaw.AllowDisable;
                    patch.interactionInput = ESPermitLaw.AllowDisable;
                    patch.uiInput = ESPermitLaw.AllowEnable;
                    patch.cursorVisible = ESPermitLaw.AllowEnable;
                    patch.cursorLocked = ESPermitLaw.AllowDisable;
                    break;

                case ESRuntimeMode.Inventory:
                case ESRuntimeMode.Map:
                case ESRuntimeMode.Settings:
                    patch.moveInput = ESPermitLaw.AllowDisable;
                    patch.cameraLook = ESPermitLaw.AllowDisable;
                    patch.combatInput = ESPermitLaw.AllowDisable;
                    patch.interactionInput = ESPermitLaw.AllowDisable;
                    patch.uiInput = ESPermitLaw.AllowEnable;
                    patch.cursorVisible = ESPermitLaw.AllowEnable;
                    patch.cursorLocked = ESPermitLaw.AllowDisable;
                    break;

                case ESRuntimeMode.RebindInput:
                    patch.playerInput = ESPermitLaw.HardDisable;
                    patch.moveInput = ESPermitLaw.HardDisable;
                    patch.cameraLook = ESPermitLaw.HardDisable;
                    patch.combatInput = ESPermitLaw.HardDisable;
                    patch.interactionInput = ESPermitLaw.HardDisable;
                    patch.uiInput = ESPermitLaw.HardDisable;
                    patch.cursorVisible = ESPermitLaw.HardEnable;
                    patch.cursorLocked = ESPermitLaw.HardDisable;
                    break;

                case ESRuntimeMode.ConfirmDialog:
                    patch.uiInput = ESPermitLaw.HardEnable;
                    patch.cursorVisible = ESPermitLaw.AllowEnable;
                    patch.cursorLocked = ESPermitLaw.AllowDisable;
                    break;

                case ESRuntimeMode.PhotoMode:
                    patch.moveInput = ESPermitLaw.AllowDisable;
                    patch.combatInput = ESPermitLaw.AllowDisable;
                    patch.interactionInput = ESPermitLaw.AllowDisable;
                    patch.cameraLook = ESPermitLaw.AllowEnable;
                    patch.uiInput = ESPermitLaw.AllowEnable;
                    patch.gameplayHud = ESPermitLaw.AllowDisable;
                    break;

                case ESRuntimeMode.Spectator:
                    patch.moveInput = ESPermitLaw.HardDisable;
                    patch.combatInput = ESPermitLaw.HardDisable;
                    patch.interactionInput = ESPermitLaw.HardDisable;
                    patch.cameraLook = ESPermitLaw.AllowEnable;
                    patch.gameplayHud = ESPermitLaw.AllowDisable;
                    break;
            }

            return patch;
        }

        public static ESRuntimeModePolicyPatch GetTagPatch(ESRuntimeModeTag tag)
        {
            ESRuntimeModePolicyPatch patch = ESRuntimeModePolicyPatch.IgnoreAll;

            switch (tag)
            {
                case ESRuntimeModeTag.Combat:
                    patch.combatInput = ESPermitLaw.AllowEnable;
                    patch.gameplayHud = ESPermitLaw.AllowEnable;
                    break;

                case ESRuntimeModeTag.Aiming:
                    patch.cameraLook = ESPermitLaw.AllowEnable;
                    break;

                case ESRuntimeModeTag.Mounted:
                    patch.moveInput = ESPermitLaw.AllowEnable;
                    patch.combatInput = ESPermitLaw.AllowDisable;
                    break;

                case ESRuntimeModeTag.Climbing:
                    patch.moveInput = ESPermitLaw.AllowEnable;
                    patch.combatInput = ESPermitLaw.AllowDisable;
                    patch.interactionInput = ESPermitLaw.AllowDisable;
                    break;

                case ESRuntimeModeTag.Dead:
                    patch.playerInput = ESPermitLaw.HardDisable;
                    patch.moveInput = ESPermitLaw.HardDisable;
                    patch.combatInput = ESPermitLaw.HardDisable;
                    patch.interactionInput = ESPermitLaw.HardDisable;
                    patch.gameplayHud = ESPermitLaw.AllowDisable;
                    break;

                case ESRuntimeModeTag.Stunned:
                    patch.moveInput = ESPermitLaw.HardDisable;
                    patch.combatInput = ESPermitLaw.HardDisable;
                    patch.interactionInput = ESPermitLaw.AllowDisable;
                    break;

                case ESRuntimeModeTag.NetworkBusy:
                    patch.playerInput = ESPermitLaw.AllowDisable;
                    patch.combatInput = ESPermitLaw.AllowDisable;
                    patch.interactionInput = ESPermitLaw.AllowDisable;
                    break;
            }

            return patch;
        }
    }
}
