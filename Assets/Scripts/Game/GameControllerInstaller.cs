using UnityEngine;
using Zenject;

namespace Game
{
    public class GameControllerInstaller : MonoInstaller
    {
        [SerializeField] private GameController gameControllerPrefab;
        
        public override void InstallBindings()
        {
            Container.Bind<IGameController>()
                .FromComponentInNewPrefab(gameControllerPrefab)
                .AsSingle()
                .NonLazy();
        }
    }
}