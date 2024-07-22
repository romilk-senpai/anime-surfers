using UnityEngine;
using Zenject;

namespace Player
{
    public class PlayerCameraInstaller : MonoInstaller
    {
        [SerializeField] private PlayerCameraController cameraControllerPrefab;

        public override void InstallBindings()
        {
            Container.Bind<PlayerCameraController>()
                .FromComponentInNewPrefab(cameraControllerPrefab)
                .AsSingle();
        }
    }
}