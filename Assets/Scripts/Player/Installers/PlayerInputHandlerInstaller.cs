using UnityEngine;
using Zenject;

namespace Player
{
    public class PlayerInputHandlerInstaller : MonoInstaller
    {
        [SerializeField] private PlayerController playerController;
        
        public override void InstallBindings()
        {
            Container.Bind<IPlayerInputHandler>()
                .FromInstance(playerController)
                .AsSingle();
        }
    }
}