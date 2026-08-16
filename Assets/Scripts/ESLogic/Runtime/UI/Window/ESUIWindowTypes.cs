using System;

namespace ES
{
    /// <summary>
    /// Built-in window identities. Hot-update and DLC windows use the stable string key on
    /// <see cref="ESUIWindowDefinition"/> instead of extending this enum.
    /// </summary>
    public enum ESUIWindowId : ushort
    {
        None = 0,
        MainMenu = 10,
        PauseMenu = 20,
        Inventory = 30,
        Map = 40,
        Settings = 50,
        ConfirmDialog = 60
    }

    public enum ESUIWindowLayer : byte
    {
        Hud = 0,
        Page = 1,
        Modal = 2,
        Popup = 3,
        Toast = 4,
        System = 5
    }

    /// <summary>Authored default behavior when the final lease for a window instance closes.</summary>
    public enum ESUIWindowClosePolicy : byte
    {
        DestroyOnClose = 0,
        PoolOnClose = 1,
        KeepInactive = 2
    }

    /// <summary>One close operation's requested result. Default uses the definition policy.</summary>
    public enum ESUIWindowCloseEffect : byte
    {
        Default = 0,
        Destroy = 1,
        ReturnToPool = 2,
        KeepInactive = 3
    }

    public enum ESUIWindowState : byte
    {
        Invalid = 0,
        Queued = 1,
        Acquiring = 2,
        Materializing = 3,
        Binding = 4,
        Entering = 5,
        Visible = 6,
        Exiting = 7,
        Closed = 8,
        Failed = 9
    }

    /// <summary>
    /// Stable window lookup input. Built-in definitions normally expose both aliases; extension
    /// definitions use only <see cref="StringKey"/>. This is never a RuntimeKey.
    /// </summary>
    public readonly struct ESUIWindowIdentity : IEquatable<ESUIWindowIdentity>
    {
        public ESUIWindowIdentity(ESUIWindowId builtInId, string stringKey)
        {
            BuiltInId = builtInId;
            StringKey = stringKey;
        }

        public ESUIWindowId BuiltInId { get; }
        public string StringKey { get; }
        public bool HasBuiltInId => BuiltInId != ESUIWindowId.None;
        public bool HasStringKey => !string.IsNullOrEmpty(StringKey);

        public static ESUIWindowIdentity FromBuiltIn(ESUIWindowId builtInId)
        {
            if (builtInId == ESUIWindowId.None)
                throw new ArgumentOutOfRangeException(nameof(builtInId), "None 不是可打开的 UI 窗口标识。");

            return new ESUIWindowIdentity(builtInId, null);
        }

        public static ESUIWindowIdentity FromString(string stringKey)
        {
            if (string.IsNullOrWhiteSpace(stringKey))
                throw new ArgumentException("UI 窗口 StringKey 不能为空。", nameof(stringKey));

            return new ESUIWindowIdentity(ESUIWindowId.None, stringKey);
        }

        public bool Equals(ESUIWindowIdentity other)
        {
            return BuiltInId == other.BuiltInId
                   && string.Equals(StringKey, other.StringKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ESUIWindowIdentity other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)BuiltInId * 397) ^ (StringKey != null ? StringKey.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            return HasBuiltInId
                ? (HasStringKey ? BuiltInId + " / " + StringKey : BuiltInId.ToString())
                : StringKey ?? ESUIWindowId.None.ToString();
        }
    }
}
