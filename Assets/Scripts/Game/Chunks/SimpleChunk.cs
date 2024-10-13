using Game.Chunks;
using Game.Chunks.WFC;
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

    [SerializeField] private Vector2Int[] test_points;

    private const int MinSegmentLength = 10;
    private const int FreeCellsAfterPoint = 3;

    private const int ObstacleCell = 1;
    private const int ReservedFreeCell = 2;
    private const int PathCell = 3;

    public override void Generate(Vector2Int start, Vector2Int dest)
    {
        //Random.InitState(randomSpeed);

        int pointsCount = 2 + Random.Range(1, Mathf.RoundToInt(ChunkLength / (1.5f * MinSegmentLength)));
        var points = new Vector2Int[pointsCount];

        int currentX = 0;

        for (int i = 1; i < points.Length - 1; i++)
        {
            int x = Random.Range(currentX + MinSegmentLength, ChunkLength - MinSegmentLength * (points.Length - i - 1) + 1);
            int y = Random.Range(0, ChunkWidth);
            points[i] = new Vector2Int(x, y);
            currentX = x;
        }

        points[0] = start;
        points[^1] = dest;

        test_points = points;

        var arr = new int[ChunkLength * ChunkWidth];

        for (int i = 0; i < points.Length - 1; i++)
        {
            for (int j = points[i].x; j < points[i].x + FreeCellsAfterPoint; j++)
            {
                for (int k = 0; k < ChunkWidth; k++)
                {
                    arr[ChunkWidth * j + k] = ReservedFreeCell;
                }
            }

            int l = points[i].y;
            for (int j = points[i].x; j < points[i + 1].x; j++)
            {
                arr[ChunkWidth * j + l] = PathCell;
            }
        }

        int targetPointIndex = 1;

        for (int i = 0; i < ChunkLength; i++)
        {
            if (i >= points[targetPointIndex].x && targetPointIndex < points.Length - 1)
            {
                targetPointIndex++;
            }

            for (int j = 0; j < ChunkWidth; j++)
            {
                if (arr[ChunkWidth * i + j] == ReservedFreeCell)
                    continue;

                if (arr[ChunkWidth * i + j] == PathCell)
                {
                    float r1 = Random.Range(0f, 1f);

                    if (r1 > .025f)
                        continue;

                    if (j == 0)
                    {
                        if (arr[ChunkWidth * i + j + 1] == ObstacleCell)
                            continue;

                        ChunkObject obstacleL = Instantiate(sideObstacleLPrefab, transform);
                        obstacleL.transform.localPosition = new Vector3(minCellX + cellWidth * j, 0f, i);
                        obstacleL.gameObject.SetActive(true);

                        arr[ChunkWidth * i + j] = ObstacleCell;
                    }
                    else if (j == ChunkWidth - 1)
                    {
                        if (arr[ChunkWidth * i + j - 1] == ObstacleCell)
                            continue;

                        ChunkObject obstacleR = Instantiate(sideObstacleRPrefab, transform);
                        obstacleR.transform.localPosition = new Vector3(minCellX + cellWidth * j, 0f, i);
                        obstacleR.gameObject.SetActive(true);

                        arr[ChunkWidth * i + j] = ObstacleCell;
                    }
                    else
                    {
                        ChunkObject obstacleFloor = Instantiate(floorObstaclePrefab, transform);
                        obstacleFloor.transform.localPosition = new Vector3(minCellX + cellWidth * j, 0f, i);
                        obstacleFloor.gameObject.SetActive(true);

                        arr[ChunkWidth * i + j] = ObstacleCell;
                    }

                    continue;
                }

                if (i + highgroundPrefab.Length > points[targetPointIndex].x)
                    continue;

                if (ChunkWidth * (i + highgroundPrefab.Length) + j > arr.Length)
                    continue;

                float r2 = Random.Range(0f, 1f);

                if (r2 > 0.25f)
                    continue;

                ChunkObject spawn = Instantiate(highgroundPrefab, transform);
                spawn.transform.localPosition = new Vector3(minCellX + cellWidth * j, 0f, i);
                spawn.gameObject.SetActive(true);

                for (int k = i; k < i + spawn.Length; k++)
                {
                    arr[ChunkWidth * k + j] = ObstacleCell;
                }
            }
        }
    }

#if UNITY_EDITOR
    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        foreach (var point in test_points)
        {
            Gizmos.DrawSphere(transform.TransformPoint(new Vector3(minCellX + cellWidth * point.y, 0f, point.x + .5f)), .5f);
        }
    }
#endif
}
