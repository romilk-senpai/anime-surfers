using Player;
using UnityEngine;
using Zenject;

namespace Game.Chunks
{
    public class ChunkController : MonoBehaviour
    {
        [SerializeField] private float spawnDistance = 50;

        [Space(10)] [SerializeField] private Chunk[] chunkPrefabs;

        private PlayerObject _playerObject;
        private Vector3 _nextChunkPosition;
        private int _lastChunkIndex = -1;

        [Inject]
        private void Inject(PlayerObject playerObject)
        {
            _playerObject = playerObject;
        }

        private void Update()
        {
            while (_nextChunkPosition.z - _playerObject.PlayerTransform.position.z < spawnDistance)
            {
                SpawnNext();
            }
        }

        private void SpawnNext()
        {
            int nextChunkIndex;

            do
            {
                nextChunkIndex = Random.Range(0, chunkPrefabs.Length);
            } while (nextChunkIndex == _lastChunkIndex && chunkPrefabs.Length > 1);

            _lastChunkIndex = nextChunkIndex;

            var chunkObject =
                Instantiate(chunkPrefabs[nextChunkIndex], _nextChunkPosition, Quaternion.identity);

            _nextChunkPosition.z += chunkObject.ChunkLength;

            chunkObject.Generate(new Vector2Int(0, 1),
                new Vector2Int(Mathf.FloorToInt(chunkObject.ChunkLength) - 1, 1));
        }
    }
}