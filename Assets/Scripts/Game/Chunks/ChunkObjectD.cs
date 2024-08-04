using System.Collections.Generic;
using UnityEngine;

namespace Game.Chunks
{
    public class ChunkObjectD : MonoBehaviour
    {
        private const int ChunkWidth = 3;

        [SerializeField] private float chunkLength = 1;
        [SerializeField] private GameObject obstaclePrefab;

        public float Length => chunkLength;

        private bool[,] _chunkArray;
        private List<AStarGrid.Node> _path;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            int floorLength = Mathf.FloorToInt(chunkLength);

            Gizmos.DrawLine(transform.TransformPoint(new Vector3(1.5f, 0, 0)),
                transform.TransformPoint(new Vector3(1.5f, 0, 0) + new Vector3(0, 0, floorLength)));

            Gizmos.DrawLine(transform.TransformPoint(new Vector3(0.5f, 0, 0)),
                transform.TransformPoint(new Vector3(0.5f, 0, 0) + new Vector3(0, 0, floorLength)));

            Gizmos.DrawLine(transform.TransformPoint(new Vector3(-0.5f, 0, 0)),
                transform.TransformPoint(new Vector3(-0.5f, 0, 0) + new Vector3(0, 0, floorLength)));

            Gizmos.DrawLine(transform.TransformPoint(new Vector3(-1.5f, 0, 0)),
                transform.TransformPoint(new Vector3(-1.5f, 0, 0) + new Vector3(0, 0, floorLength)));

            for (int i = 0; i <= floorLength; i++)
            {
                Gizmos.DrawLine(transform.TransformPoint(new Vector3(1.5f, 0, 0) + new Vector3(0, 0, i)),
                    transform.TransformPoint(new Vector3(-1.5f, 0, 0) + new Vector3(0, 0, i)));
            }

            if (_chunkArray == null)
            {
                return;
            }
            
            for (int i = 0; i < chunkLength; i++)
            {
                for (int j = 0; j < ChunkWidth; j++)
                {
                    if (_chunkArray[i, j])
                    {
                        //Gizmos.DrawCube(transform.TransformPoint(new Vector3(j - 1, 0, i + 0.5f)), new Vector3(1, 0.1f, 1));
                    }
                }
            }
        }
#endif
        
        public void Generate()
        {
            _chunkArray = new bool[Mathf.FloorToInt(chunkLength), ChunkWidth];

            for (int i = 0; i < chunkLength - 1; i++)
            {
                for (int j = 0; j < ChunkWidth; j++)
                {
                    _chunkArray[i, j] = false;
                }
            }

            for (int i = 3; i < chunkLength; i++)
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
                                if (i - k >= 0 && _chunkArray[i - k, l])
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
                                if (i - k >= 0 && _chunkArray[i - k, ChunkWidth - l - 1])
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
                                if (i - k >= 0 && _chunkArray[i - k, Mathf.FloorToInt(ChunkWidth / 2f) - 1 + l])
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

                    _chunkArray[i, j] = generate;

                    if (generate)
                    {
                        generate = AStarGrid.Calculate(_chunkArray,
                            new Vector2Int(0, 1),
                            new Vector2Int((int)chunkLength - 1, 1),
                            null, out _path);
                    }

                    _chunkArray[i, j] = generate;

                    if (generate)
                    {
                        var obstacle = Instantiate(obstaclePrefab);
                        obstacle.transform.position =
                            transform.TransformPoint(new Vector3(j - 1, 0, i + 0.5f));

                        obstacle.layer = LayerMask.NameToLayer("Obstacle");
                    }
                }
            }
        }
    }
}