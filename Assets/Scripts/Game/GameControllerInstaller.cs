using Zenject;

namespace Game
{
    public class GameControllerInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.Bind<IGameController>()
                .To<GameController>()
                .FromNewComponentOnNewGameObject()
                .AsSingle()
                .NonLazy();
        }
    }
}