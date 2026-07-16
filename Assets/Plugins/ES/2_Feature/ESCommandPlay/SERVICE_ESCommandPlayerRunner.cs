using System.Collections.Generic;

namespace ES
{
    public static class ESCommandPlayerRunner
    {
        private static readonly List<ESCommandPlayer> ActivePlayers = new List<ESCommandPlayer>(16);

        public static int ActiveCount
        {
            get { return ActivePlayers.Count; }
        }

        public static void Register(ESCommandPlayer player)
        {
            if (player == null)
                return;

            if (!ActivePlayers.Contains(player))
                ActivePlayers.Add(player);
        }

        public static void Unregister(ESCommandPlayer player)
        {
            if (player == null)
                return;

            for (int i = ActivePlayers.Count - 1; i >= 0; i--)
            {
                if (ActivePlayers[i] == player)
                {
                    ActivePlayers.RemoveAt(i);
                    return;
                }
            }
        }

        public static void TickAll(float time, float deltaTime)
        {
            if (ActivePlayers.Count == 0)
                return;

            for (int i = ActivePlayers.Count - 1; i >= 0; i--)
            {
                ESCommandPlayer player = ActivePlayers[i];
                if (player == null || !player.IsPlaying)
                {
                    ActivePlayers.RemoveAt(i);
                    continue;
                }

                ESRunState state = player.Tick(time, deltaTime);
                if (state != ESRunState.Running)
                    ActivePlayers.RemoveAt(i);
            }
        }

        public static void Clear()
        {
            ActivePlayers.Clear();
        }
    }
}
