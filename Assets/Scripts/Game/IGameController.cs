using System;

namespace Game
{
    public interface IGameController
    {
        public int MaxHp { get;}
        public int CurrentHp { get;}
        public int GameScore { get;}
        public int ScoreGlobalMultiplier { get;}
        public event Action<int> OnPlayerHpUpdated;
        public event Action<int> OnPlayerScoreUpdated;
    }
}