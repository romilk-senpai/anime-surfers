using System.Collections.Generic;
using System.Linq;
using Player;
using UnityEngine;
using Zenject;

namespace Game.Chunks
{
    public class ChunkController : MonoBehaviour
    {
        [SerializeField] private float spawnDistance = 50f;
        [SerializeField] private float despawnDistance = 10f;

        [Space(10)]
        [SerializeField] private Chunk startingChunkPrefab;
        [SerializeField] private Chunk[] chunkPrefabs;

        private PlayerObject _playerObject;
        private Vector3 _nextChunkPosition;
        private int _lastChunkIndex = -1;
        private Queue<Chunk> _despawnQueue;

        [Inject]
        private void Inject(PlayerObject playerObject)
        {
            _playerObject = playerObject;
        }

        private void Start()
        {
            _despawnQueue = new Queue<Chunk>();

            var chunkObject = Instantiate(startingChunkPrefab, _nextChunkPosition, Quaternion.identity);
            _nextChunkPosition.z += chunkObject.ChunkLength;
            _despawnQueue.Enqueue(chunkObject);
        }

        private void Update()
        {
            while (_nextChunkPosition.z - _playerObject.PlayerTransform.position.z < spawnDistance)
            {
                SpawnNext();
            }

            if (_despawnQueue.Count == 0)
                return;

            Chunk chunkToDespawn = _despawnQueue.Peek();

            if (_playerObject.PlayerTransform.position.z > chunkToDespawn.transform.position.z + chunkToDespawn.ChunkLength + despawnDistance)
            {
                chunkToDespawn = _despawnQueue.Dequeue();
                Destroy(chunkToDespawn.gameObject);
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

            var chunkObject = Instantiate(chunkPrefabs[nextChunkIndex], _nextChunkPosition, Quaternion.identity);

            _nextChunkPosition.z += chunkObject.ChunkLength;

            chunkObject.Generate(new Vector2Int(0, 1),
                new Vector2Int(Mathf.FloorToInt(chunkObject.ChunkLength) - 1, 1));

            _despawnQueue.Enqueue(chunkObject);
        }
    }
}