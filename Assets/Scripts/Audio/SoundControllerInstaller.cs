using UnityEngine;
using Zenject;

namespace Game.Audio
{
    public class SoundControllerInstaller : MonoInstaller
    {
        [SerializeField] private SoundController soundControllerPrefab;

        public override void InstallBindings()
        {
            Container.Bind<ISoundController>()
                .FromComponentInNewPrefab(soundControllerPrefab)
                .AsSingle()
                .NonLazy();
        }
    }
}