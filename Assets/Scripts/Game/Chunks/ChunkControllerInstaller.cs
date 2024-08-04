using UnityEngine;
using Zenject;

namespace Game.Chunks
{
    public class ChunkControllerInstaller : MonoInstaller
    {
        [SerializeField] private ChunkController chunkControllerPrefab;
        
        public override void InstallBindings()
        {
            Container.Bind<ChunkController>()
                .FromComponentInNewPrefab(chunkControllerPrefab)
                .AsSingle()
                .NonLazy();
        }
    }
}