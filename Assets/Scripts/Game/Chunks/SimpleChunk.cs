using Game.Chunks;
using UnityEngine;

public class SimpleChunk : Chunk
{
    

    [SerializeField] private ChunkObject floorObstaclePrefab;
    [SerializeField] private ChunkObject sideObstacleLPrefab;
    [SerializeField] private ChunkObject sideObstacleRPrefab;
    [SerializeField] private ChunkObject highgroundPrefab;
    [SerializeField] private ChunkObject entranceHighgroundPrefab;

    [SerializeField] private float minCellX = -1;
    [SerializeField] private float cellWidth = 1;

    [SerializeField] private int randomSpeed = 777;

    public override void Generate(Vector2Int start, Vector2Int dest)
    {
        Random.InitState(randomSpeed);

        throw new System.NotImplementedException();
    }

    /*public override void Generate(Vector2Int start, Vector2Int dest)
    {
        Random.InitState(randomSpeed);

        int[] arr = new int[ChunkLength * ChunkWidth];

        for (int i = 0; i < ChunkLength; i++)
        {
            int r = Random.Range(0, ChunkWidth);

            for (int j = 0; j < ChunkWidth; j++)
            {
                if (arr[ChunkWidth * i + j] > 0)
                {
                    continue;
                }

                if (ChunkWidth * (i + highgroundPrefab.Length) + j > arr.Length) {
                    continue;
                }

                ChunkObject spawn = Instantiate(highgroundPrefab, transform);
                spawn.transform.localPosition = new Vector3(minCellX + cellWidth * j, 0f, i);
                spawn.gameObject.SetActive(true);

                for (int k = i; k < i + spawn.Length; k++)
                {
                    arr[ChunkWidth * k + j] = 1;
                }
            }
        }
    }*/
}
