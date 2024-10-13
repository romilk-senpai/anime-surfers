using UnityEngine;

namespace Game.Chunks
{
    public class WoodsWithHighground : Chunk
    {
        [SerializeField] private GameObject deadTreePrefab;

        [SerializeField] private GameObject highgroundEnterPrefab;
        [SerializeField] private GameObject highgroundPrefab;

        private const int GenerationIndent = 3;

        private const int HighgroundEnterLength = 3;
        private const int HighgroundObjectLength = 4;

        public override void Generate(Vector2Int start, Vector2Int dest)
        {
            var chunkArray = new bool[ChunkLength, ChunkWidth];

            for (int i = 0; i < ChunkLength - 1; i++)
            {
                for (int j = 0; j < ChunkWidth - 1; j++)
                {
                    chunkArray[i, j] = false;
                }
            }

            for (int i = GenerationIndent; i < GenerationIndent + HighgroundEnterLength + HighgroundObjectLength; i++)
            {
                chunkArray[i, 0] = true;
                chunkArray[i, ChunkWidth - 1] = true;
            }

            Instantiate(highgroundEnterPrefab,
                transform.TransformPoint(new Vector3(-1, 0, GenerationIndent)),
                Quaternion.identity, transform);

            Instantiate(highgroundPrefab,
                transform.TransformPoint(new Vector3(-1, 0, GenerationIndent + HighgroundEnterLength)),
                Quaternion.identity, transform);

            Instantiate(highgroundEnterPrefab,
                transform.TransformPoint(new Vector3(1, 0, GenerationIndent)),
                Quaternion.identity, transform);

            Instantiate(highgroundPrefab,
                transform.TransformPoint(new Vector3(1, 0, GenerationIndent + HighgroundEnterLength)),
                Quaternion.identity, transform);

            for (int i = GenerationIndent + HighgroundEnterLength + HighgroundObjectLength; i < ChunkLength - 1; i++)
            {
                bool generated = false;

                for (int j = 0; j < ChunkWidth; j++)
                {
                    if (chunkArray[i, j])
                    {
                        continue;
                    }

                    var (prefab, length) = GetRandomObject();

                    if (length == -1)
                    {
                        continue;
                    }

                    if (i + length > ChunkLength)
                    {
                        continue;
                    }

                    for (int k = i; k < i + length; k++)
                    {
                        chunkArray[k, j] = true;
                    }

                    if (AStarGrid.Calculate(chunkArray, start, dest, null, out _))
                    {
                        Instantiate(prefab,
                            transform.TransformPoint(new Vector3(j - 1, 0, i)),
                            Quaternion.identity, transform);

                        generated = true;
                    }
                    else
                    {
                        for (int k = i; k < i + length; k++)
                        {
                            chunkArray[k, j] = false;
                        }
                    }
                }
                
                if (generated)
                {
                    i += 2;
                }
            }
        }

        private (GameObject, int) GetRandomObject()
        {
            if (Random.Range(0, 3) == 0)
            {
                return (null, -1);
            }

            int random = Random.Range(0, 5);

            if (random >= 2)
            {
                return (deadTreePrefab, 1);
            }

            return (highgroundPrefab, HighgroundObjectLength);
        }
    }
}