using System;

namespace Game
{
    public interface IGameController
    {
        public int MaxHp { get;}
        public int CurrentHp { get;}
        public event Action<int> OnPlayerHpUpdated;
    }
}