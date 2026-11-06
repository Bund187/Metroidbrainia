using UnityEngine;

namespace Metroidbrainia
{
    public enum GameLaunchMode { NewGame, Continue }

    public static class GameLaunchContext
    {
        private static GameLaunchMode requestedMode;

        public static void Request(GameLaunchMode mode) => requestedMode = mode;

        public static GameLaunchMode Consume()
        {
            GameLaunchMode mode = requestedMode;
            requestedMode = GameLaunchMode.NewGame;
            return mode;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => requestedMode = GameLaunchMode.NewGame;
    }
}
