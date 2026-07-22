using ES;

namespace ES.Internal
{
    public static class ESRuntimeModeDefaults
    {
        private const int ModePatchCapacity = (int)ESRuntimeMode.Spectator + 1;
        private const int TagPatchCapacity = (int)ESRuntimeModeTag.NetworkBusy + 1;

        private static readonly ESRuntimeModePolicyPatch[] ModePatches = new ESRuntimeModePolicyPatch[ModePatchCapacity];
        private static readonly ESRuntimeModePolicyPatch[] TagPatches = new ESRuntimeModePolicyPatch[TagPatchCapacity];

        static ESRuntimeModeDefaults()
        {
            ModePatches[(int)ESRuntimeMode.Gameplay] = CreateModePatch(ESRuntimeMode.Gameplay);
            ModePatches[(int)ESRuntimeMode.MainMenu] = CreateModePatch(ESRuntimeMode.MainMenu);
            ModePatches[(int)ESRuntimeMode.PauseMenu] = CreateModePatch(ESRuntimeMode.PauseMenu);
            ModePatches[(int)ESRuntimeMode.Loading] = CreateModePatch(ESRuntimeMode.Loading);
            ModePatches[(int)ESRuntimeMode.SceneTransition] = CreateModePatch(ESRuntimeMode.SceneTransition);
            ModePatches[(int)ESRuntimeMode.Cutscene] = CreateModePatch(ESRuntimeMode.Cutscene);
            ModePatches[(int)ESRuntimeMode.Dialogue] = CreateModePatch(ESRuntimeMode.Dialogue);
            ModePatches[(int)ESRuntimeMode.Inventory] = CreateModePatch(ESRuntimeMode.Inventory);
            ModePatches[(int)ESRuntimeMode.Map] = CreateModePatch(ESRuntimeMode.Map);
            ModePatches[(int)ESRuntimeMode.Settings] = CreateModePatch(ESRuntimeMode.Settings);
            ModePatches[(int)ESRuntimeMode.RebindInput] = CreateModePatch(ESRuntimeMode.RebindInput);
            ModePatches[(int)ESRuntimeMode.ConfirmDialog] = CreateModePatch(ESRuntimeMode.ConfirmDialog);
            ModePatches[(int)ESRuntimeMode.PhotoMode] = CreateModePatch(ESRuntimeMode.PhotoMode);
            ModePatches[(int)ESRuntimeMode.Spectator] = CreateModePatch(ESRuntimeMode.Spectator);

            TagPatches[(int)ESRuntimeModeTag.Combat] = CreateTagPatch(ESRuntimeModeTag.Combat);
            TagPatches[(int)ESRuntimeModeTag.Aiming] = CreateTagPatch(ESRuntimeModeTag.Aiming);
            TagPatches[(int)ESRuntimeModeTag.Mounted] = CreateTagPatch(ESRuntimeModeTag.Mounted);
            TagPatches[(int)ESRuntimeModeTag.Climbing] = CreateTagPatch(ESRuntimeModeTag.Climbing);
            TagPatches[(int)ESRuntimeModeTag.Dead] = CreateTagPatch(ESRuntimeModeTag.Dead);
            TagPatches[(int)ESRuntimeModeTag.Stunned] = CreateTagPatch(ESRuntimeModeTag.Stunned);
            TagPatches[(int)ESRuntimeModeTag.NetworkBusy] = CreateTagPatch(ESRuntimeModeTag.NetworkBusy);
        }

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
            int index = (int)mode;
            return index >= 0 && index < ModePatches.Length
                ? ModePatches[index]
                : ESRuntimeModePolicyPatch.IgnoreAll;
        }

        public static ESRuntimeModePolicyPatch GetTagPatch(ESRuntimeModeTag tag)
        {
            int index = (int)tag;
            return index >= 0 && index < TagPatches.Length
                ? TagPatches[index]
                : ESRuntimeModePolicyPatch.IgnoreAll;
        }

        private static ESRuntimeModePolicyPatch CreateModePatch(ESRuntimeMode mode)
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

        private static ESRuntimeModePolicyPatch CreateTagPatch(ESRuntimeModeTag tag)
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
