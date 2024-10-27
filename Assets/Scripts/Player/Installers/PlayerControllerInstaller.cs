using UnityEngine;
using Zenject;

namespace Player
{
    public class PlayerControllerInstaller : MonoInstaller
    {
        [SerializeField] private PlayerController playerControllerPrefab;
        
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<PlayerController>()
                .FromComponentInNewPrefab(playerControllerPrefab)
                .AsSingle();
        }
    }
}