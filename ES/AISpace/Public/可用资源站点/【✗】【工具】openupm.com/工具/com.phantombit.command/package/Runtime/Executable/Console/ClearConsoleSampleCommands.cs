using JetBrains.Annotations;
using WellFired.Command.Unity.Runtime.CommandHandlers;

namespace WellFired.Command.Unity.Runtime.Console
{
    public static class ClearConsoleSampleCommands
    {
        [ConsoleCommand(Name = "Clear", Description = "Clears the entire debug console")]
        [UsedImplicitly]
        private static void Clear()
        {
            DevelopmentConsole.Instance.Clear();
        }
    }

    public static class AutoScrollCommands
    {
        [ConsoleCommand(Description = "Scroll to last entry and autos croll")]
        private static bool AutoScroll
        {
            get => DevelopmentConsole.Instance.AutoScroll;
            set => DevelopmentConsole.Instance.AutoScroll = value;
        }
    }
}