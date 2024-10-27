using UnityEngine;
using Zenject;

namespace Player
{
    public class PlayerObjectInstaller : MonoInstaller
    {
        [SerializeField] private PlayerObject playerObjectPrefab;

        public override void InstallBindings()
        {
            Container.Bind<PlayerObject>()
                .FromComponentInNewPrefab(playerObjectPrefab)
                .AsSingle();
        }
    }
}