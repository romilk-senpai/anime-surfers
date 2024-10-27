using Zenject;

namespace Player
{
    public class PlayerAnimatorControllerInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.Bind<IPlayerAnimatorController>()
                .To<PlayerAnimatorController>()
                .FromNewComponentOnNewGameObject()
                .AsSingle();
        }
    }
}