using System.Collections.Generic;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

namespace Game.Surf
{
	/// <summary>
	/// Builds the models of the obstacles in the <see cref="ObstacleField"/> as <see cref="ObstacleGenerator"/> adds
	/// them: boats for each car of a train, a ramp in front of a climbable one, a rope for a barrier. Visuals are pooled
	/// and reused once their obstacle is removed, and hidden once the shield smashes the obstacle. Their outlines are
	/// only drawn once they come close.
	/// </summary>
	public class ObstacleSpawner : MonoBehaviour
	{
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

		private readonly Dictionary<ObstacleBox, Spawned> _spawned = new Dictionary<ObstacleBox, Spawned>();
		private readonly Stack<Spawned> _freeSpawned = new Stack<Spawned>();
		private readonly List<Renderer> _modelRenderers = new List<Renderer>();

		private ObjectPool<Transform> _roots;
		private ObjectPool<Transform>[] _boats;
		private ObjectPool<Transform> _barriers;
		private ObjectPool<Transform> _ramps;

		private IBoardState _board;
		private ObstacleSettings _settings;
		private SurfPath _path;
		private IWaterSurface _water;
		private ObstacleField _field;

		[Inject]
		public void Construct(IBoardState board, ObstacleSettings settings, SurfPath path, IWaterSurface water,
			ObstacleField field)
		{
			_board = board;
			_settings = settings;
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
			_field.Added += OnAdded;
			_field.Removed += OnRemoved;

			foreach (ObstacleBox box in _field.Boxes)
			{
				OnAdded(box);
			}
		}

		private void OnDestroy()
		{
			if (_field != null)
			{
				_field.Added -= OnAdded;
				_field.Removed -= OnRemoved;
			}
		}

		private void Update()
		{
			float distance = _board.Distance;

			foreach (Spawned spawned in _spawned.Values)
			{
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

		private void OnAdded(ObstacleBox box)
		{
			if (box.Kind == ObstacleKind.Barrier)
			{
				BuildBarrier(box);
			}
			else
			{
				BuildTrain(box);
			}
		}

		private void OnRemoved(ObstacleBox box)
		{
			if (_spawned.Remove(box, out Spawned spawned))
			{
				Despawn(spawned);
			}
		}

		private void BuildTrain(ObstacleBox box)
		{
			float start = box.Start;
			float lateral = (box.Left + box.Right) * 0.5f;
			float ramp = box.RampLength;
			bool withRamp = box.Kind == ObstacleKind.ClimbableTrain;

			Spawned spawned = Begin(withRamp ? "ClimbableTrain" : "Train", start, lateral);

			if (withRamp)
			{
				Vector3 bottom = PathPoint(start, lateral, box.RampStartHeight, out _);
				Vector3 top = PathPoint(start + ramp, lateral, box.Top, out _);
				Vector3 run = Vector3.ProjectOnPlane(top - bottom, Vector3.up);

				Transform rampModel = AddModel(spawned, _ramps);
				rampModel.SetPositionAndRotation(bottom, Quaternion.LookRotation(run));
				rampModel.localScale = new Vector3(1f, (top.y - bottom.y) / rampModelSize.y, run.magnitude / rampModelSize.x);
			}

			ObjectPool<Transform> boats = _boats[Random.Range(0, _boats.Length)];
			float carLength = _settings.CarLength;
			float carGap = _settings.CarGap;

			for (int i = 0; i < box.Cars; i++)
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
		}

		private void BuildBarrier(ObstacleBox box)
		{
			// Built in the root's local space so it can bob with the water as a whole.
			Spawned spawned = Begin("Barrier", (box.Start + box.End) * 0.5f, (box.Left + box.Right) * 0.5f);

			AddModel(spawned, _barriers);

			Register(spawned, box, true);
		}

		private void Register(Spawned spawned, ObstacleBox box, bool followsWater)
		{
			spawned.Box = box;
			spawned.FollowsWater = followsWater;
			// Spawned far ahead; Update turns the outline on once it comes close.
			spawned.Outlined = false;
			ToonOutlineFeature.SetOutlined(spawned.Renderers, false);
			_spawned.Add(box, spawned);
		}

		private void Despawn(Spawned spawned)
		{
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
