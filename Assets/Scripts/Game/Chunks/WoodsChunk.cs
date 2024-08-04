using UnityEngine;

namespace Game.Chunks
{
    public class WoodsChunk : ChunkObject
    {
        [SerializeField] private GameObject deadTreePrefab;

        public override void Generate(Vector2Int start, Vector2Int dest)
        {
            var chunkArray = new bool[Mathf.FloorToInt(ChunkLength), ChunkWidth];

            for (int i = 0; i < ChunkLength - 1; i++)
            {
                for (int j = 0; j < ChunkWidth; j++)
                {
                    chunkArray[i, j] = false;
                }
            }

            for (int i = 3; i < ChunkLength; i++)
            {
                for (int j = 0; j < ChunkWidth; j++)
                {
                    if (j == 0)
                    {
                        bool continueIteration = false;

                        for (int k = 2; k < 3; k++)
                        {
                            for (int l = 0; l < 2; l++)
                            {
                                if (i - k >= 0 && chunkArray[i - k, l])
                                {
                                    continueIteration = true;
                                    break;
                                }
                            }
                        }

                        if (continueIteration)
                        {
                            continue;
                        }
                    }

                    if (j == ChunkWidth - 1)
                    {
                        bool continueIteration = false;

                        for (int k = 1; k < 3; k++)
                        {
                            for (int l = 0; l < 2; l++)
                            {
                                if (i - k >= 0 && chunkArray[i - k, ChunkWidth - l - 1])
                                {
                                    continueIteration = true;
                                    break;
                                }
                            }
                        }

                        if (continueIteration)
                        {
                            continue;
                        }
                    }

                    if (ChunkWidth > 2 && j == Mathf.FloorToInt(ChunkWidth / 2f))
                    {
                        bool continueIteration = false;

                        for (int k = 2; k < 3; k++)
                        {
                            for (int l = 0; l < 3; l++)
                            {
                                if (i - k >= 0 && chunkArray[i - k, Mathf.FloorToInt(ChunkWidth / 2f) - 1 + l])
                                {
                                    continueIteration = true;
                                    break;
                                }
                            }
                        }

                        if (continueIteration)
                        {
                            continue;
                        }
                    }

                    bool generate = Random.Range(0, 5) == 0;

                    chunkArray[i, j] = generate;

                    if (generate)
                    {
                        generate = AStarGrid.Calculate(chunkArray, start, dest, null, out _);
                    }

                    chunkArray[i, j] = generate;

                    if (generate)
                    {
                        var obstacle = Instantiate(deadTreePrefab);
                        obstacle.transform.position =
                            transform.TransformPoint(new Vector3(j - 1, 0, i + 0.5f));

                        obstacle.layer = LayerMask.NameToLayer("Obstacle");
                    }
                }
            }
        }
    }
}