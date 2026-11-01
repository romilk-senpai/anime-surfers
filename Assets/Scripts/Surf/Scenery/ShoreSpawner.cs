using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

namespace Game.Surf
{
	/// <summary>
	/// Background scenery along the passage, organised into fixed-length locations that cycle through a sequence:
	/// open water, or a quay with walls along one bank. Landmarks (ships, lighthouses) show up in the background
	/// throughout, on a side without walls. Props are pooled per prefab and released once left behind.
	/// </summary>
	public class ShoreSpawner : MonoBehaviour
	{
		private enum LocationType
		{
			Open,
			Quay
		}

		private struct Location
		{
			public LocationType Type;
			public float WallSide;
		}

		private struct Spawned
		{
			public GameObject Instance;
			public ObjectPool<GameObject> Pool;
			public float Distance;
		}

		// Path distance step used to measure bank-line length on curves.
		private const float MarchStep = 0.5f;

		[Header("Placement")]
		[Tooltip("At medium view distance; scaled with the player's setting.")]
		[SerializeField] private float spawnAhead = 110f;
		[SerializeField] private float despawnBehind = 30f;
		[Tooltip("Distance from the passage edge to the bank line.")]
		[SerializeField] private float bankOffset = 4f;

		[Header("Locations")]
		[SerializeField] private float locationLength = 400f;
		[Tooltip("Cycled in order.")]
		[SerializeField] private LocationType[] locationSequence = { LocationType.Open, LocationType.Quay };

		[Header("Quay")]
		[Tooltip("Wall segment with its water face at local x = 0 facing +X, centered on z.")]
		[SerializeField] private GameObject wallPrefab;
		[SerializeField] private GameObject[] wallVariants;
		[SerializeField, Range(0f, 1f)] private float wallVariantChance = 0.2f;
		[SerializeField] private float wallLength = 10f;
		[Tooltip("Caps both ends of a quay.")]
		[SerializeField] private GameObject quayEndPrefab;
		[Tooltip("How far the cap's center sits behind the wall face.")]
		[SerializeField] private float quayEndInset = 3f;

		[Header("Landmarks")]
		[SerializeField] private GameObject[] landmarkPrefabs;
		[SerializeField] private float firstLandmark = 40f;
		[SerializeField] private Vector2 landmarkSpacing = new Vector2(60f, 140f);
		[Tooltip("Lateral distance beyond the bank line.")]
		[SerializeField] private Vector2 landmarkOffset = new Vector2(16f, 28f);

		private readonly List<Location> _locations = new List<Location>();
		private readonly List<Spawned> _spawned = new List<Spawned>();
		private readonly Dictionary<GameObject, ObjectPool<GameObject>> _pools =
			new Dictionary<GameObject, ObjectPool<GameObject>>();

		private BoardState _board;
		private BoardSettings _boardSettings;
		private SurfPath _path;
		private IGraphicsSettings _graphics;
		private int _wallLocation;
		private float _wallCursor;
		private float _nextLandmark;

		[Inject]
		public void Construct(BoardState board, BoardSettings boardSettings, SurfPath path, IGraphicsSettings graphics)
		{
			_board = board;
			_boardSettings = boardSettings;
			_path = path;
			_graphics = graphics;
		}

		private void Start()
		{
			_nextLandmark = firstLandmark;
		}

		private void Update()
		{
			float distance = _board.Distance;
			float ahead = spawnAhead * _graphics.DistanceScale;

			while (_wallCursor < distance + ahead)
			{
				FillWalls();
			}

			while (_nextLandmark < distance + ahead)
			{
				SpawnLandmark(_nextLandmark);
				_nextLandmark += Random.Range(landmarkSpacing.x, landmarkSpacing.y);
			}

			for (int i = _spawned.Count - 1; i >= 0; i--)
			{
				if (_spawned[i].Distance < distance - despawnBehind)
				{
					_spawned[i].Pool.Release(_spawned[i].Instance);
					_spawned.RemoveAt(i);
				}
			}
		}

		/// <summary>Locations are rolled on demand; the list only grows by one entry per location.</summary>
		private Location GetLocation(int index)
		{
			while (_locations.Count <= index)
			{
				_locations.Add(new Location
				{
					Type = locationSequence[_locations.Count % locationSequence.Length],
					WallSide = Random.value < 0.5f ? -1f : 1f
				});
			}

			return _locations[index];
		}

		private Location LocationAt(float distance) =>
			GetLocation(Mathf.Max(0, Mathf.FloorToInt(distance / locationLength)));

		private float BankLateral(float side) => side * (_boardSettings.PassageHalfWidth + bankOffset);

		/// <summary>Places the next wall segment, or caps the quay and moves on to the next location.</summary>
		private void FillWalls()
		{
			Location location = GetLocation(_wallLocation);
			float start = _wallLocation * locationLength;
			float end = start + locationLength;

			if (location.Type == LocationType.Quay && _wallCursor < end - wallLength * 0.5f)
			{
				if (_wallCursor <= start)
				{
					SpawnQuayEnd(location.WallSide, _wallCursor);
				}

				_wallCursor = SpawnWall(location.WallSide, _wallCursor);
				return;
			}

			if (location.Type == LocationType.Quay)
			{
				SpawnQuayEnd(location.WallSide, _wallCursor);
			}

			_wallLocation++;
			_wallCursor = end;
		}

		private float SpawnWall(float side, float distance)
		{
			float end = AdvanceAlongBank(distance, BankLateral(side), wallLength, out Vector3 from, out Vector3 to);

			GameObject prefab = wallVariants.Length > 0 && Random.value < wallVariantChance
				? wallVariants[Random.Range(0, wallVariants.Length)]
				: wallPrefab;

			Spawn(prefab, (from + to) * 0.5f, FacePassage(side, to - from), end);
			return end;
		}

		private void SpawnQuayEnd(float side, float distance)
		{
			Vector3 position = PathPoint(distance, BankLateral(side) + side * quayEndInset, out Vector3 forward);
			Spawn(quayEndPrefab, position, FacePassage(side, forward), distance);
		}

		private void SpawnLandmark(float distance)
		{
			if (landmarkPrefabs.Length == 0)
			{
				return;
			}

			Location location = LocationAt(distance);
			float side = location.Type == LocationType.Quay
				? -location.WallSide
				: Random.value < 0.5f ? -1f : 1f;

			float lateral = BankLateral(side) + side * Random.Range(landmarkOffset.x, landmarkOffset.y);
			Vector3 position = PathPoint(distance, lateral, out Vector3 forward);

			// Roughly along the passage, either way, so ships read side-on.
			Quaternion rotation = Quaternion.LookRotation(forward) *
			                      Quaternion.Euler(0f, Random.Range(-35f, 35f) + (Random.value < 0.5f ? 180f : 0f), 0f);

			Spawn(landmarkPrefabs[Random.Range(0, landmarkPrefabs.Length)], position, rotation, distance);
		}

		/// <summary>
		/// Walks the path from the given distance until the bank line has covered the given length (longer on the
		/// outside of a curve, shorter on the inside) and returns the path distance where it ends.
		/// </summary>
		private float AdvanceAlongBank(float distance, float lateral, float length, out Vector3 from, out Vector3 to)
		{
			from = PathPoint(distance, lateral, out _);
			Vector3 previous = from;
			float previousLength = 0f;

			while (true)
			{
				Vector3 point = PathPoint(distance + MarchStep, lateral, out _);
				float covered = Vector3.Distance(from, point);

				if (covered >= length)
				{
					float t = (length - previousLength) / Mathf.Max(covered - previousLength, 0.0001f);
					to = Vector3.Lerp(previous, point, t);
					return distance + MarchStep * t;
				}

				distance += MarchStep;
				previous = point;
				previousLength = covered;
			}
		}

		/// <summary>Props are authored facing +X; on the right bank they are turned around to face the passage.</summary>
		private static Quaternion FacePassage(float side, Vector3 forward)
		{
			Quaternion rotation = Quaternion.LookRotation(forward);
			return side > 0f ? rotation * Quaternion.Euler(0f, 180f, 0f) : rotation;
		}

		private Vector3 PathPoint(float distance, float lateral, out Vector3 forward)
		{
			_path.Sample(distance, out Vector3 center, out forward);
			return center + Vector3.Cross(Vector3.up, forward) * lateral;
		}

		private void Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float distance)
		{
			ObjectPool<GameObject> pool = GetPool(prefab);
			GameObject instance = pool.Get();
			instance.transform.SetPositionAndRotation(position, rotation);

			_spawned.Add(new Spawned { Instance = instance, Pool = pool, Distance = distance });
		}

		private ObjectPool<GameObject> GetPool(GameObject prefab)
		{
			if (!_pools.TryGetValue(prefab, out ObjectPool<GameObject> pool))
			{
				pool = new ObjectPool<GameObject>(
					() => Instantiate(prefab, transform),
					instance => instance.SetActive(true),
					instance => instance.SetActive(false));
				_pools.Add(prefab, pool);
			}

			return pool;
		}
	}
}
