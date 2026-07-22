using System.Collections.Generic;

namespace ES
{
    public static class ESCommandPlayerRunner
    {
        private static readonly List<ESCommandPlayer> ActivePlayers = new List<ESCommandPlayer>(16);
        private static readonly Dictionary<ESCommandPlayer, int> ActiveIndexMap = new Dictionary<ESCommandPlayer, int>(16);

        public static int ActiveCount
        {
            get { return ActivePlayers.Count; }
        }

        public static void Register(ESCommandPlayer player)
        {
            if (player == null)
                return;

            if (ActiveIndexMap.ContainsKey(player))
                return;

            ActiveIndexMap.Add(player, ActivePlayers.Count);
            ActivePlayers.Add(player);
        }

        public static void Unregister(ESCommandPlayer player)
        {
            if (player == null)
                return;

            if (ActiveIndexMap.TryGetValue(player, out int index))
                RemoveAtSwapBack(index);
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
                    RemoveAtSwapBack(i);
                    continue;
                }

                ESRunState state = player.Tick(TimeFrame.Now, time, deltaTime);
                if (state != ESRunState.Running)
                    RemoveAtSwapBack(i);
            }
        }

        public static ESRunState TickPlayerNow(ESCommandPlayer player, float time, float deltaTime)
        {
            return player == null ? ESRunState.Skipped : player.Tick(TimeFrame.Now, time, deltaTime);
        }

        public static void Clear()
        {
            ActivePlayers.Clear();
            ActiveIndexMap.Clear();
        }

        private static void RemoveAtSwapBack(int index)
        {
            int lastIndex = ActivePlayers.Count - 1;
            ESCommandPlayer removed = ActivePlayers[index];
            ActiveIndexMap.Remove(removed);

            if (index != lastIndex)
            {
                ESCommandPlayer moved = ActivePlayers[lastIndex];
                ActivePlayers[index] = moved;
                ActiveIndexMap[moved] = index;
            }

            ActivePlayers.RemoveAt(lastIndex);
        }

        private static class TimeFrame
        {
            public static int Now
            {
                get { return UnityEngine.Time.frameCount; }
            }
        }
    }
}
