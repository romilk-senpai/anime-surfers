using System.Collections.Generic;
using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

namespace Game.Surf
{
	/// <summary>
	/// Spawns rows of obstacles ahead of the board, one obstacle (or nothing) per lane, always leaving a passable lane:
	/// trains (rows of moored boats) block the lane but can be ridden on, climbable trains have a ramp in front, and
	/// barriers (shimenawa ropes) are jumped over or slid under. Visuals are pooled and reused once left behind, and
	/// hidden once the shield smashes the obstacle. Their outlines are only drawn once they come close.
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

		private readonly struct PooledModel
		{
			public readonly Transform Model;
			public readonly ObjectPool<Transform> Pool;

			public PooledModel(Transform model, ObjectPool<Transform> pool)
			{
				Model = model;
				Pool = pool;
			}
		}

		private class Spawned
		{
			public readonly List<PooledModel> Models = new List<PooledModel>();
			public readonly List<Renderer> Renderers = new List<Renderer>();
			public ObstacleBox Box;
			public Transform Root;
			public bool FollowsWater;
			public bool Outlined;
		}

		[Header("Placement")]
		[SerializeField] private float firstRowDistance = 60f;
		[SerializeField] private Vector2 rowGap = new Vector2(16f, 28f);
		[Tooltip("At medium view distance; scaled with the player's setting.")]
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

		[Header("Barrier")]
		[SerializeField] private float barrierWidth = 2.3f;
		[Tooltip("Bar height above the water: above the sliding rider, below the standing one.")]
		[SerializeField] private float barrierBottom = 1.15f;
		[SerializeField] private float barrierTop = 1.55f;
		[SerializeField] private float barrierDepth = 0.3f;

		[Header("Visuals")]
		[Tooltip("One car long, pivot at the waterline. One is picked per train for all of its cars.")]
		[SerializeField] private GameObject[] boatPrefabs;
		[Tooltip("Color variants, one picked per boat. Empty keeps the prefabs' own material.")]
		[SerializeField] private Material[] boatMaterials;
		[Tooltip("Pivot at the water surface, bar inside the barrier height range.")]
		[SerializeField] private GameObject barrierPrefab;
		[Tooltip("Pivot at the bottom of the ramp surface, rising along +Z.")]
		[SerializeField] private GameObject rampPrefab;
		[Tooltip("Run and rise the ramp model is built for; it is stretched to the actual ramp.")]
		[SerializeField] private Vector2 rampModelSize = new Vector2(9f, 4f);

		private readonly List<Spawned> _spawned = new List<Spawned>();
		private readonly LaneObstacle[] _row = new LaneObstacle[8];
		private readonly Stack<Spawned> _freeSpawned = new Stack<Spawned>();
		private readonly List<Renderer> _modelRenderers = new List<Renderer>();

		private ObjectPool<Transform> _roots;
		private ObjectPool<Transform>[] _boats;
		private ObjectPool<Transform> _barriers;
		private ObjectPool<Transform> _ramps;

		private BoardState _board;
		private BoardSettings _boardSettings;
		private SurfPath _path;
		private WaterSurface _water;
		private ObstacleField _field;
		private IGraphicsSettings _graphics;
		private float _nextRowDistance;

		[Inject]
		public void Construct(BoardState board, BoardSettings boardSettings, SurfPath path, WaterSurface water,
			ObstacleField field, IGraphicsSettings graphics)
		{
			_graphics = graphics;
			_board = board;
			_boardSettings = boardSettings;
			_path = path;
			_water = water;
			_field = field;
		}

		private void Awake()
		{
			_roots = new ObjectPool<Transform>(CreateRoot, OnGetPooled, OnReleasePooled);
			_barriers = CreateModelPool(barrierPrefab);
			_ramps = CreateModelPool(rampPrefab);
			_boats = new ObjectPool<Transform>[boatPrefabs.Length];

			for (int i = 0; i < boatPrefabs.Length; i++)
			{
				_boats[i] = CreateModelPool(boatPrefabs[i]);
			}
		}

		private void Start()
		{
			_nextRowDistance = firstRowDistance;
		}

		private void Update()
		{
			float distance = _board.Distance;

			while (_nextRowDistance < distance + spawnAhead * _graphics.DistanceScale)
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
					continue;
				}

				bool outlined = spawned.Box.Start - distance < ToonOutlineFeature.MaxDistance;
				if (outlined != spawned.Outlined)
				{
					spawned.Outlined = outlined;
					ToonOutlineFeature.SetOutlined(spawned.Renderers, outlined);
				}

				if (spawned.Box.Smashed)
				{
					// Hidden until it is despawned; releasing the root keeps it hidden.
					spawned.Root.gameObject.SetActive(false);
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
				Vector3 run = Vector3.ProjectOnPlane(top - bottom, Vector3.up);

				Transform rampModel = AddModel(spawned, _ramps);
				rampModel.SetPositionAndRotation(bottom, Quaternion.LookRotation(run));
				rampModel.localScale = new Vector3(1f, (top.y - bottom.y) / rampModelSize.y, run.magnitude / rampModelSize.x);
			}

			ObjectPool<Transform> boats = _boats[Random.Range(0, _boats.Length)];

			for (int i = 0; i < cars; i++)
			{
				float carCenter = start + ramp + i * (carLength + carGap) + carLength * 0.5f;
				Vector3 position = PathPoint(carCenter, lateral, 0f, out Vector3 forward);

				Transform boat = AddModel(spawned, boats);
				boat.SetPositionAndRotation(position, Quaternion.LookRotation(forward));

				if (boatMaterials.Length > 0)
				{
					boat.GetComponent<Renderer>().sharedMaterial = boatMaterials[Random.Range(0, boatMaterials.Length)];
				}
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

			AddModel(spawned, _barriers);

			Register(spawned, box, true);
			return box;
		}

		private void Register(Spawned spawned, ObstacleBox box, bool followsWater)
		{
			spawned.Box = box;
			spawned.FollowsWater = followsWater;
			// Spawned far ahead; Update turns the outline on once it comes close.
			spawned.Outlined = false;
			ToonOutlineFeature.SetOutlined(spawned.Renderers, false);
			_field.Add(box);
			_spawned.Add(spawned);
		}

		private void Despawn(Spawned spawned)
		{
			_field.Remove(spawned.Box);

			foreach (PooledModel pooled in spawned.Models)
			{
				pooled.Pool.Release(pooled.Model);
			}

			spawned.Models.Clear();
			spawned.Renderers.Clear();
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

		/// <summary>Adds a model at the root's origin, unscaled; it goes back to its pool on despawn.</summary>
		private Transform AddModel(Spawned spawned, ObjectPool<Transform> pool)
		{
			Transform model = pool.Get();
			model.SetParent(spawned.Root, false);
			model.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			model.localScale = Vector3.one;
			spawned.Models.Add(new PooledModel(model, pool));
			model.GetComponentsInChildren(_modelRenderers);
			spawned.Renderers.AddRange(_modelRenderers);
			return model;
		}

		private Transform CreateRoot()
		{
			var root = new GameObject().transform;
			root.SetParent(transform, false);
			return root;
		}

		private ObjectPool<Transform> CreateModelPool(GameObject prefab)
		{
			return new ObjectPool<Transform>(() =>
			{
				Transform model = Instantiate(prefab, transform, false).transform;
				model.name = prefab.name;
				return model;
			}, OnGetPooled, OnReleasePooled);
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
