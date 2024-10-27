using Zenject;

namespace Player
{
    public class PlayerInputControllerInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.Bind<IPlayerInputController>()
                .To<PlayerInputController>()
                .FromNewComponentOnNewGameObject()
                .AsSingle()
                .NonLazy();
        }
    }
}
