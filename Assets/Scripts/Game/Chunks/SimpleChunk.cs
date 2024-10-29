using System.Collections.Generic;
using System.Linq;
using Game.Chunks;
using UnityEngine;

public class SimpleChunk : Chunk
{
    [SerializeField] private ChunkObject floorObstaclePrefab;
    [SerializeField] private ChunkObject floorTallObstaclePrefab;
    [SerializeField] private ChunkObject sideObstacleLPrefab;
    [SerializeField] private ChunkObject sideObstacleRPrefab;
    [SerializeField] private ChunkObject highgroundPrefab;
    [SerializeField] private ChunkObject entranceHighgroundPrefab;

    [SerializeField] private bool useSeed = true;
    [SerializeField] private int randomSpeed = 777;

    [SerializeField] private Vector2Int[] _testPoints = new Vector2Int[] { };
    private List<ChunkObject> _spawnedHighgrounds;

    private const int MinSegmentLength = 10;
    private const int FreeCellsAfterPoint = 3;

    private const int UnsetCell = 0;
    private const int ObstacleCell = 1;
    private const int ReservedFreeCell = 2;
    private const int PathCell = 3;

    public override void Generate(Vector2Int start, Vector2Int dest)
    {
        if (useSeed)
        {
            Random.InitState(randomSpeed);
        }

        _spawnedHighgrounds = new List<ChunkObject>();

        int maxPointCount = Mathf.RoundToInt(ChunkLength / MinSegmentLength);

        int pointsCount = 2 + Random.Range(maxPointCount / 2, maxPointCount);
        var points = new Vector2Int[pointsCount];

        int currentX = 0;
        int lastY = start.y;

        for (int i = 1; i < points.Length - 1; i++)
        {
            currentX += ChunkLength / pointsCount;
            int y = lastY;
            while (y == lastY)
            {
                y = Random.Range(0, ChunkWidth);
            }
            points[i] = new Vector2Int(currentX, y);
            lastY = y;
        }

        points[0] = start;
        points[^1] = dest;

        _testPoints = points;

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

        for (int i = FreeCellsAfterPoint; i < ChunkLength - 1; i++)
        {
            while (i >= points[targetPointIndex].x && targetPointIndex < points.Length - 1)
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
                    float r2 = Random.Range(0f, 1f);

                    if (r1 > 0.02f)
                        continue;

                    if (j == 0 && r2 > 0.5f && arr[ChunkWidth * i + j + 1] != ObstacleCell)
                    {
                        ChunkObject obstacleL = Instantiate(sideObstacleLPrefab, transform);
                        obstacleL.transform.localPosition = new Vector3(MinCellX + CellWidth * j, 0f, i);
                        obstacleL.gameObject.SetActive(true);

                        arr[ChunkWidth * i + j] = ObstacleCell;
                        arr[ChunkWidth * (i - 1) + j + 1] = ObstacleCell;
                        arr[ChunkWidth * i + j + 1] = ObstacleCell;
                        arr[ChunkWidth * (i + 1) + j + 1] = ObstacleCell;
                    }
                    else if (j == ChunkWidth - 1 && r2 > 0.5f && arr[ChunkWidth * i + j - 1] != ObstacleCell)
                    {
                        ChunkObject obstacleR = Instantiate(sideObstacleRPrefab, transform);
                        obstacleR.transform.localPosition = new Vector3(MinCellX + CellWidth * j, 0f, i);
                        obstacleR.gameObject.SetActive(true);

                        arr[ChunkWidth * i + j] = ObstacleCell;
                        arr[ChunkWidth * (i - 1) + j - 1] = ObstacleCell;
                        arr[ChunkWidth * i + j - 1] = ObstacleCell;
                        arr[ChunkWidth * (i + 1) + j - 1] = ObstacleCell;
                    }
                    else
                    {
                        float r3 = Random.Range(0f, 1f);

                        bool spawnTall = false;

                        if (r3 < .25f)
                        {
                            bool lFree = j > 0
                                && arr[ChunkWidth * i + j - 1] != ObstacleCell
                                && arr[ChunkWidth * (i - 1) + j - 1] != ObstacleCell;
                            bool rFree = j < ChunkWidth - 1
                                && arr[ChunkWidth * i + j + 1] != ObstacleCell
                                && arr[ChunkWidth * (i - 1) + j + 1] != ObstacleCell;

                            spawnTall = lFree || rFree;
                        }

                        ChunkObject obstacleFloor = Instantiate(spawnTall ? floorTallObstaclePrefab : floorObstaclePrefab, transform);
                        obstacleFloor.transform.localPosition = new Vector3(MinCellX + CellWidth * j, 0f, i);
                        obstacleFloor.gameObject.SetActive(true);

                        arr[ChunkWidth * i + j] = ObstacleCell;

                        if (spawnTall)
                        {
                            arr[ChunkWidth * (i - 1) + j + 1] = ObstacleCell;
                            arr[ChunkWidth * i + j + 1] = ObstacleCell;
                            arr[ChunkWidth * (i + 1) + j + 1] = ObstacleCell;
                        }
                    }

                    continue;
                }

                if (arr[ChunkWidth * i + j] != UnsetCell)
                    continue;

                if (i + highgroundPrefab.Length >= points[targetPointIndex].x)
                    continue;

                if (ChunkWidth * (i + highgroundPrefab.Length) + j >= arr.Length)
                    continue;

                if (arr[ChunkWidth * i + j] > 0)
                    continue;

                float r = Random.Range(0f, 1f);

                if (r > 0.5f)
                    continue;

                bool canSpawnEntrance = ChunkWidth * (i + entranceHighgroundPrefab.Length) + j < arr.Length
                    && i + entranceHighgroundPrefab.Length < points[targetPointIndex].x;
                r = Random.Range(0f, 1f);
                ChunkObject spawnPrefab = canSpawnEntrance && r < 0.25f ? entranceHighgroundPrefab : highgroundPrefab;
                ChunkObject spawn = Instantiate(spawnPrefab, transform);
                spawn.transform.localPosition = new Vector3(MinCellX + CellWidth * j, 0f, i);
                spawn.gameObject.SetActive(true);
                _spawnedHighgrounds.Add(spawn);

                for (int k = i; k < i + spawn.Length; k++)
                {
                    arr[ChunkWidth * k + j] = ObstacleCell;
                }
            }
        }

        GameObject[] batchedObjects = _spawnedHighgrounds
            .SelectMany(spawn => spawn.GetComponentsInChildren<MeshRenderer>())
            .Select(spawn => spawn.gameObject)
            .ToArray();

        StaticBatchingUtility.Combine(batchedObjects, gameObject);
    }

#if UNITY_EDITOR
    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        foreach (var point in _testPoints)
        {
            Gizmos.DrawSphere(transform.TransformPoint(new Vector3(MinCellX + CellWidth * point.y, 0f, point.x + .5f)), .5f);
        }
    }
#endif
}
