using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

namespace Game.Surf
{
    /// <summary>
    /// Spawns rows of obstacles ahead of the board, one obstacle (or nothing) per lane, always leaving a passable lane:
    /// trains block the lane but can be ridden on, climbable trains have a ramp in front, and barriers are jumped
    /// over or slid under. Visuals are placeholder primitives, pooled and reused once left behind.
    /// </summary>
    public class ObstacleSpawner : MonoBehaviour
    {
        private enum LaneObstacle
        {
            None,
            Barrier,
            Train,
            ClimbableTrain
        }

        private class Spawned
        {
            public readonly List<MeshRenderer> Parts = new List<MeshRenderer>();
            public ObstacleBox Box;
            public Transform Root;
            public bool FollowsWater;
        }

        [Header("Placement")]
        [SerializeField] private float firstRowDistance = 60f;
        [SerializeField] private Vector2 rowGap = new Vector2(16f, 28f);
        [SerializeField] private float spawnAhead = 160f;
        [SerializeField] private float despawnBehind = 20f;

        [Header("Lane Weights")]
        [SerializeField, Min(0f)] private float noneWeight = 3f;
        [SerializeField, Min(0f)] private float barrierWeight = 2f;
        [SerializeField, Min(0f)] private float trainWeight = 2f;
        [SerializeField, Min(0f)] private float climbableTrainWeight = 1.5f;

        [Header("Train")]
        [SerializeField] private float trainWidth = 2.1f;
        [Tooltip("Higher than the jump so trains can only be climbed via a ramp.")]
        [SerializeField] private float trainHeight = 3.2f;
        [SerializeField] private float trainBottom = -1.5f;
        [SerializeField] private float carLength = 6f;
        [SerializeField] private float carGap = 0.6f;
        [SerializeField] private Vector2Int trainCars = new Vector2Int(2, 4);
        [SerializeField] private Vector2Int climbableTrainCars = new Vector2Int(3, 5);
        [SerializeField] private float rampLength = 9f;
        [Tooltip("Ramp start height; below the wave troughs so the board catches it from the water.")]
        [SerializeField] private float rampStartHeight = -0.8f;
        [SerializeField] private float rampThickness = 0.3f;

        [Header("Barrier")]
        [SerializeField] private float barrierWidth = 2.3f;
        [Tooltip("Bar height above the water: above the sliding rider, below the standing one.")]
        [SerializeField] private float barrierBottom = 1.15f;
        [SerializeField] private float barrierTop = 1.55f;
        [SerializeField] private float barrierDepth = 0.3f;
        [SerializeField] private float postWidth = 0.15f;
        [SerializeField] private float postDepth = 0.8f;
        [SerializeField] private float postBottom = -0.8f;

        [Header("Materials")]
        [SerializeField] private Material trainMaterial;
        [SerializeField] private Material rampMaterial;
        [SerializeField] private Material barrierMaterial;

        private readonly List<Spawned> _spawned = new List<Spawned>();
        private readonly LaneObstacle[] _row = new LaneObstacle[8];
        private readonly Stack<Spawned> _freeSpawned = new Stack<Spawned>();

        private ObjectPool<Transform> _roots;
        private ObjectPool<MeshRenderer> _parts;

        private BoardState _board;
        private BoardSettings _boardSettings;
        private SurfPath _path;
        private WaterSurface _water;
        private ObstacleField _field;
        private float _nextRowDistance;

        [Inject]
        public void Construct(BoardState board, BoardSettings boardSettings, SurfPath path, WaterSurface water,
            ObstacleField field)
        {
            _board = board;
            _boardSettings = boardSettings;
            _path = path;
            _water = water;
            _field = field;
        }

        private void Awake()
        {
            _roots = new ObjectPool<Transform>(CreateRoot, OnGetPooled, OnReleasePooled);
            _parts = new ObjectPool<MeshRenderer>(CreatePart, OnGetPooled, OnReleasePooled);
        }

        private void Start()
        {
            _nextRowDistance = firstRowDistance;
        }

        private void Update()
        {
            float distance = _board.Distance;

            while (_nextRowDistance < distance + spawnAhead)
            {
                _nextRowDistance = SpawnRow(_nextRowDistance) + Random.Range(rowGap.x, rowGap.y);
            }

            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                Spawned spawned = _spawned[i];

                if (spawned.Box.End < distance - despawnBehind)
                {
                    Despawn(spawned);
                    _spawned.RemoveAt(i);
                }
                else if (spawned.FollowsWater)
                {
                    Vector3 position = spawned.Root.position;
                    position.y = _water.GetHeight(position);
                    spawned.Root.position = position;
                }
            }
        }

        /// <summary>Spawns one row starting at the given distance and returns where it ends.</summary>
        private float SpawnRow(float start)
        {
            int laneCount = Mathf.Min(_boardSettings.LaneCount, _row.Length);
            bool passable = false;
            bool any = false;

            for (int lane = 0; lane < laneCount; lane++)
            {
                _row[lane] = PickObstacle();
                passable |= _row[lane] != LaneObstacle.Train;
                any |= _row[lane] != LaneObstacle.None;
            }

            if (!passable)
            {
                _row[Random.Range(0, laneCount)] = LaneObstacle.None;
            }
            else if (!any)
            {
                _row[Random.Range(0, laneCount)] = LaneObstacle.Barrier;
            }

            float end = start;

            for (int lane = 0; lane < laneCount; lane++)
            {
                float lateral = _boardSettings.LaneOffset(lane);

                ObstacleBox box = _row[lane] switch
                {
                    LaneObstacle.Barrier => SpawnBarrier(start, lateral),
                    LaneObstacle.Train => SpawnTrain(start, lateral, Random.Range(trainCars.x, trainCars.y + 1), false),
                    LaneObstacle.ClimbableTrain => SpawnTrain(start, lateral,
                        Random.Range(climbableTrainCars.x, climbableTrainCars.y + 1), true),
                    _ => null
                };

                if (box != null)
                {
                    end = Mathf.Max(end, box.End);
                }
            }

            return end;
        }

        private LaneObstacle PickObstacle()
        {
            float roll = Random.Range(0f, noneWeight + barrierWeight + trainWeight + climbableTrainWeight);

            if ((roll -= noneWeight) < 0f)
            {
                return LaneObstacle.None;
            }

            if ((roll -= barrierWeight) < 0f)
            {
                return LaneObstacle.Barrier;
            }

            return roll - trainWeight < 0f ? LaneObstacle.Train : LaneObstacle.ClimbableTrain;
        }

        private ObstacleBox SpawnTrain(float start, float lateral, int cars, bool withRamp)
        {
            float ramp = withRamp ? rampLength : 0f;

            var box = new ObstacleBox
            {
                Start = start,
                End = start + ramp + cars * carLength + (cars - 1) * carGap,
                Left = lateral - trainWidth * 0.5f,
                Right = lateral + trainWidth * 0.5f,
                Bottom = trainBottom,
                Top = trainHeight,
                RampLength = ramp,
                RampStartHeight = rampStartHeight,
                Walkable = true
            };

            Spawned spawned = Begin(withRamp ? "ClimbableTrain" : "Train", start, lateral);

            if (withRamp)
            {
                Vector3 bottom = PathPoint(start, lateral, rampStartHeight, out _);
                Vector3 top = PathPoint(start + ramp, lateral, trainHeight, out _);
                Vector3 slope = top - bottom;
                Quaternion rotation = Quaternion.LookRotation(slope);
                Vector3 center = (bottom + top) * 0.5f - rotation * Vector3.up * (rampThickness * 0.5f);

                AddPart(spawned, "Ramp", center, rotation, new Vector3(trainWidth, rampThickness, slope.magnitude),
                    rampMaterial);
            }

            for (int i = 0; i < cars; i++)
            {
                float carCenter = start + ramp + i * (carLength + carGap) + carLength * 0.5f;
                Vector3 center = PathPoint(carCenter, lateral, (trainBottom + trainHeight) * 0.5f, out Vector3 forward);

                AddPart(spawned, "Car", center, Quaternion.LookRotation(forward),
                    new Vector3(trainWidth, trainHeight - trainBottom, carLength), trainMaterial);
            }

            Register(spawned, box, false);
            return box;
        }

        private ObstacleBox SpawnBarrier(float start, float lateral)
        {
            var box = new ObstacleBox
            {
                Start = start,
                End = start + barrierDepth,
                Left = lateral - barrierWidth * 0.5f,
                Right = lateral + barrierWidth * 0.5f,
                Bottom = barrierBottom,
                Top = barrierTop,
                FollowsWater = true
            };

            // Built in the root's local space so it can bob with the water as a whole.
            Spawned spawned = Begin("Barrier", start + barrierDepth * 0.5f, lateral);

            AddLocalPart(spawned, "Bar", new Vector3(0f, (barrierBottom + barrierTop) * 0.5f, 0f),
                new Vector3(barrierWidth, barrierTop - barrierBottom, barrierDepth), barrierMaterial);

            for (int side = -1; side <= 1; side += 2)
            {
                AddLocalPart(spawned, "Post",
                    new Vector3(side * (barrierWidth - postWidth) * 0.5f, (postBottom + barrierBottom) * 0.5f, 0f),
                    new Vector3(postWidth, barrierBottom - postBottom, postDepth), barrierMaterial);
            }

            Register(spawned, box, true);
            return box;
        }

        private void Register(Spawned spawned, ObstacleBox box, bool followsWater)
        {
            spawned.Box = box;
            spawned.FollowsWater = followsWater;
            _field.Add(box);
            _spawned.Add(spawned);
        }

        private void Despawn(Spawned spawned)
        {
            _field.Remove(spawned.Box);

            foreach (MeshRenderer part in spawned.Parts)
            {
                _parts.Release(part);
            }

            spawned.Parts.Clear();
            _roots.Release(spawned.Root);
            spawned.Root = null;
            spawned.Box = null;
            _freeSpawned.Push(spawned);
        }

        private Vector3 PathPoint(float distance, float lateral, float height, out Vector3 forward)
        {
            _path.Sample(distance, out Vector3 center, out forward);
            return center + Vector3.Cross(Vector3.up, forward) * lateral + Vector3.up * height;
        }

        private Spawned Begin(string objectName, float distance, float lateral)
        {
            Vector3 position = PathPoint(distance, lateral, 0f, out Vector3 forward);

            Transform root = _roots.Get();
            root.name = objectName;
            root.SetPositionAndRotation(position, Quaternion.LookRotation(forward));

            Spawned spawned = _freeSpawned.Count > 0 ? _freeSpawned.Pop() : new Spawned();
            spawned.Root = root;
            return spawned;
        }

        private void AddPart(Spawned spawned, string objectName, Vector3 position, Quaternion rotation, Vector3 size,
            Material material)
        {
            Transform part = GetPart(spawned, objectName, size, material);
            part.SetPositionAndRotation(position, rotation);
        }

        private void AddLocalPart(Spawned spawned, string objectName, Vector3 localPosition, Vector3 size,
            Material material)
        {
            Transform part = GetPart(spawned, objectName, size, material);
            part.SetLocalPositionAndRotation(localPosition, Quaternion.identity);
        }

        private Transform GetPart(Spawned spawned, string objectName, Vector3 size, Material material)
        {
            MeshRenderer part = _parts.Get();
            part.name = objectName;
            part.sharedMaterial = material;
            spawned.Parts.Add(part);

            Transform partTransform = part.transform;
            partTransform.SetParent(spawned.Root, false);
            partTransform.localScale = size;
            return partTransform;
        }

        private Transform CreateRoot()
        {
            var root = new GameObject().transform;
            root.SetParent(transform, false);
            return root;
        }

        private MeshRenderer CreatePart()
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            // Collisions are resolved in path space by ObstacleField.
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(transform, false);
            return part.GetComponent<MeshRenderer>();
        }

        private void OnGetPooled(Component pooled)
        {
            pooled.gameObject.SetActive(true);
        }

        private void OnReleasePooled(Component pooled)
        {
            pooled.gameObject.SetActive(false);
            pooled.transform.SetParent(transform, false);
        }
    }
}
