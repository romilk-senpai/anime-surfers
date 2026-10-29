using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

namespace Game.Surf
{
    /// <summary>
    /// Spawns coin trails along single lanes ahead of the board, shaped around the obstacles already placed there:
    /// arcs over barriers, up ramps and along train roofs, ending before walls that cannot be climbed.
    /// Some trails carry a power-up in the middle. Visuals are pooled per kind; power-ups are placeholder primitives.
    /// Runs after <see cref="ObstacleSpawner"/> so the obstacles of this frame exist before trails are shaped.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class PickupSpawner : MonoBehaviour
    {
        private struct Spawned
        {
            public Pickup Pickup;
            public Transform View;
        }

        [Header("Placement")]
        [SerializeField] private float firstTrailDistance = 25f;
        [SerializeField] private Vector2 trailGap = new Vector2(6f, 18f);
        [Tooltip("Keep below the obstacle spawner's spawn distance minus the longest trail, " +
                 "so trails are only shaped around obstacles that already exist.")]
        [SerializeField] private float spawnAhead = 110f;
        [SerializeField] private float despawnBehind = 10f;

        [Header("Trail")]
        [SerializeField] private Vector2Int coinCount = new Vector2Int(5, 12);
        [SerializeField] private float coinSpacing = 2.2f;
        [Tooltip("Height of the coins above the surface they hover over.")]
        [SerializeField] private float hoverHeight = 0.7f;
        [Tooltip("Coins within this distance of a barrier arc over it.")]
        [SerializeField] private float barrierArcLength = 5f;
        [Tooltip("Height of the arc over a barrier, above the water.")]
        [SerializeField] private float barrierArcHeight = 2.6f;
        [Tooltip("A trail ends where the surface rises more than this between two coins (a train without a ramp).")]
        [SerializeField] private float maxStep = 1.5f;

        [Header("Power-Ups")]
        [SerializeField, Range(0f, 1f)] private float powerUpChance = 0.08f;
        [SerializeField, Range(0f, 1f)] private float magnetShare = 0.5f;

        [Header("Visuals")]
        [Tooltip("Coin model, lying flat with its faces along local Y. Falls back to a cylinder when empty.")]
        [SerializeField] private GameObject coinPrefab;
        [SerializeField] private Vector3 coinSize = new Vector3(0.7f, 0.04f, 0.7f);
        [SerializeField] private Vector3 powerUpSize = new Vector3(0.8f, 0.8f, 0.8f);
        [SerializeField] private float spinSpeed = 180f;
        [SerializeField] private Material coinMaterial;
        [SerializeField] private Material magnetMaterial;
        [SerializeField] private Material multiplierMaterial;

        private readonly List<Spawned> _spawned = new List<Spawned>();
        private readonly Stack<Pickup> _freePickups = new Stack<Pickup>();
        private readonly ObjectPool<Transform>[] _views = new ObjectPool<Transform>[3];

        private BoardState _board;
        private BoardSettings _boardSettings;
        private SurfPath _path;
        private WaterSurface _water;
        private ObstacleField _obstacles;
        private PickupField _field;
        private float _nextTrailDistance;

        [Inject]
        public void Construct(BoardState board, BoardSettings boardSettings, SurfPath path, WaterSurface water,
            ObstacleField obstacles, PickupField field)
        {
            _board = board;
            _boardSettings = boardSettings;
            _path = path;
            _water = water;
            _obstacles = obstacles;
            _field = field;
        }

        private void Awake()
        {
            for (int i = 0; i < _views.Length; i++)
            {
                var kind = (PickupKind)i;
                _views[i] = new ObjectPool<Transform>(() => CreateView(kind), OnGetView, OnReleaseView);
            }
        }

        private void Start()
        {
            _nextTrailDistance = firstTrailDistance;
        }

        private void Update()
        {
            float distance = _board.Distance;

            while (_nextTrailDistance < distance + spawnAhead)
            {
                _nextTrailDistance = SpawnTrail(_nextTrailDistance) + Random.Range(trailGap.x, trailGap.y);
            }

            Quaternion spin = Quaternion.Euler(0f, Time.time * spinSpeed, 0f);

            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                Spawned spawned = _spawned[i];
                Pickup pickup = spawned.Pickup;

                if (pickup.Collected || pickup.Distance < distance - despawnBehind)
                {
                    Despawn(spawned);
                    _spawned.RemoveAt(i);
                    continue;
                }

                _path.Sample(pickup.Distance, out Vector3 center, out Vector3 forward);
                Vector3 position = center + Vector3.Cross(Vector3.up, forward) * pickup.Lateral;
                position.y = (pickup.FollowsWater ? _water.GetHeight(position) : center.y) + pickup.Height;

                Quaternion rotation = Quaternion.LookRotation(forward) * spin;
                if (pickup.Kind == PickupKind.Coin)
                {
                    // Stand the flat coin up so it faces the rider.
                    rotation *= Quaternion.Euler(90f, 0f, 0f);
                }

                spawned.View.SetPositionAndRotation(position, rotation);
            }
        }

        /// <summary>Spawns one trail starting at the given distance and returns where it ends.</summary>
        private float SpawnTrail(float start)
        {
            float lateral = _boardSettings.LaneOffset(Random.Range(0, _boardSettings.LaneCount));
            int count = Random.Range(coinCount.x, coinCount.y + 1);
            int powerUpIndex = Random.value < powerUpChance ? count / 2 : -1;
            float previousSurface = float.NaN;
            float end = start;

            for (int i = 0; i < count; i++)
            {
                float distance = start + i * coinSpacing;
                float surface = Placement(distance, lateral, out float height, out bool followsWater);

                if (surface - previousSurface > maxStep)
                {
                    break;
                }

                previousSurface = surface;
                end = distance;

                PickupKind kind = i != powerUpIndex ? PickupKind.Coin
                    : Random.value < magnetShare ? PickupKind.Magnet : PickupKind.ScoreMultiplier;
                Spawn(kind, distance, lateral, height, followsWater);
            }

            return end;
        }

        /// <summary>
        /// Where a coin goes at the given point: hovering over the water, a barrier arc or a train roof.
        /// Returns the nominal height of the surface under it.
        /// </summary>
        private float Placement(float distance, float lateral, out float height, out bool followsWater)
        {
            float waterLevel = _water.BaseHeight;
            float surface = waterLevel;
            height = hoverHeight;
            followsWater = true;

            foreach (ObstacleBox box in _obstacles.Boxes)
            {
                if (!box.OverlapsLateral(lateral, 0f))
                {
                    continue;
                }

                if (!box.Walkable)
                {
                    float arc = Mathf.Abs(distance - (box.Start + box.End) * 0.5f) / barrierArcLength;
                    if (arc < 1f && followsWater)
                    {
                        height = Mathf.Max(height, Mathf.Lerp(hoverHeight, barrierArcHeight, 1f - arc * arc));
                    }

                    continue;
                }

                if (distance < box.Start || distance > box.End)
                {
                    continue;
                }

                // Ramps start below the water; follow the water until they rise out of it.
                float top = box.SurfaceAt(distance);
                if (top > surface)
                {
                    surface = top;
                    height = top + hoverHeight;
                    followsWater = false;
                }
            }

            return surface;
        }

        private void Spawn(PickupKind kind, float distance, float lateral, float height, bool followsWater)
        {
            Pickup pickup = _freePickups.Count > 0 ? _freePickups.Pop() : new Pickup();
            pickup.Kind = kind;
            pickup.Distance = distance;
            pickup.Lateral = lateral;
            pickup.Height = height;
            pickup.FollowsWater = followsWater;
            pickup.Attracted = false;
            pickup.Collected = false;

            _field.Add(pickup);
            _spawned.Add(new Spawned { Pickup = pickup, View = _views[(int)kind].Get() });
        }

        private void Despawn(Spawned spawned)
        {
            _field.Remove(spawned.Pickup);
            _views[(int)spawned.Pickup.Kind].Release(spawned.View);
            _freePickups.Push(spawned.Pickup);
        }

        private Transform CreateView(PickupKind kind)
        {
            bool coin = kind == PickupKind.Coin;
            if (coin && coinPrefab != null)
            {
                GameObject model = Instantiate(coinPrefab, transform, false);
                model.name = kind.ToString();
                return model.transform;
            }

            GameObject view = GameObject.CreatePrimitive(coin ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            view.name = kind.ToString();
            // Collection is resolved in path space by PickupCollector.
            Destroy(view.GetComponent<Collider>());

            Material material = kind switch
            {
                PickupKind.Magnet => magnetMaterial,
                PickupKind.ScoreMultiplier => multiplierMaterial,
                _ => coinMaterial
            };

            if (material != null)
            {
                view.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            Transform viewTransform = view.transform;
            viewTransform.SetParent(transform, false);
            viewTransform.localScale = coin ? coinSize : powerUpSize;
            return viewTransform;
        }

        private static void OnGetView(Transform view)
        {
            view.gameObject.SetActive(true);
        }

        private static void OnReleaseView(Transform view)
        {
            view.gameObject.SetActive(false);
        }
    }
}
