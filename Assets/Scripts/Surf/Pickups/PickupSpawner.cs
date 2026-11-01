using System.Collections.Generic;
using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

namespace Game.Surf
{
	/// <summary>
	/// Spawns coin trails along single lanes ahead of the board, shaped around the obstacles already placed there:
	/// arcs over barriers, up ramps and along train roofs, ending before walls that cannot be climbed. While super jump
	/// will still be active when the board gets there, trains without a ramp get a coin arc along the jump onto the roof.
	/// Some trails carry a power-up in the middle. Coins are drawn with GPU instancing: the model with an outline up
	/// close, a low-poly stand-in without one beyond. Power-up visuals are pooled per kind, kinds without a model are
	/// cubes, and their outlines are only drawn once they come close.
	/// Runs after <see cref="ObstacleSpawner"/> so the obstacles of this frame exist before trails are shaped.
	/// </summary>
	[DefaultExecutionOrder(100)]
	public class PickupSpawner : MonoBehaviour
	{
		private struct Spawned
		{
			public Pickup Pickup;
			/// <summary>None for coins, which are drawn instanced.</summary>
			public View View;
		}

		private class View
		{
			public Transform Transform;
			public readonly List<Renderer> Renderers = new List<Renderer>();
			public bool Outlined;
		}

		[Header("Placement")]
		[SerializeField] private float firstTrailDistance = 25f;
		[SerializeField] private Vector2 trailGap = new Vector2(6f, 18f);
		[Tooltip("Just past the fog end, so trails appear unseen. Keep below the obstacle spawner's spawn distance " +
		         "minus the longest trail, so trails are only shaped around obstacles that already exist. At medium " +
		         "view distance; all three scale with the player's setting.")]
		[SerializeField] private float spawnAhead = 80f;
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

		[Header("Power-Up Weights")]
		[SerializeField, Min(0f)] private float nitroWeight = 2f;
		[SerializeField, Min(0f)] private float magnetWeight = 1f;
		[SerializeField, Min(0f)] private float multiplierWeight = 1f;
		[SerializeField, Min(0f)] private float shieldWeight = 1f;
		[SerializeField, Min(0f)] private float superJumpWeight = 1f;
		[SerializeField, Min(0f)] private float coinDoublerWeight = 1f;

		[Header("Visuals")]
		[Tooltip("Coin model, lying flat with its faces along local Y; only its mesh, material and layer are used. " +
		         "For the far stand-in the mesh needs Read/Write enabled and its faces along its own Z, textured by a " +
		         "flat projection.")]
		[SerializeField] private GameObject coinPrefab;
		[Tooltip("Up to this distance ahead coins use the model with an outline; beyond it a low-poly stand-in " +
		         "without one.")]
		[SerializeField] private float coinDetailDistance = 15f;
		[Tooltip("Power-up models, upright and centered on their pivot. A cube is used for any left empty.")]
		[SerializeField] private GameObject magnetPrefab;
		[SerializeField] private GameObject multiplierPrefab;
		[SerializeField] private GameObject nitroPrefab;
		[SerializeField] private GameObject shieldPrefab;
		[SerializeField] private GameObject superJumpPrefab;
		[SerializeField] private GameObject coinDoublerPrefab;
		[Tooltip("Size of the placeholder cube.")]
		[SerializeField] private Vector3 powerUpSize = new Vector3(0.8f, 0.8f, 0.8f);
		[SerializeField] private float spinSpeed = 180f;
		[SerializeField] private Material magnetMaterial;
		[SerializeField] private Material multiplierMaterial;
		[SerializeField] private Material nitroMaterial;
		[SerializeField] private Material shieldMaterial;
		[SerializeField] private Material superJumpMaterial;
		[SerializeField] private Material coinDoublerMaterial;

		private readonly List<Spawned> _spawned = new List<Spawned>();
		private readonly Stack<Pickup> _freePickups = new Stack<Pickup>();
		private readonly ObjectPool<View>[] _views =
			new ObjectPool<View>[System.Enum.GetValues(typeof(PickupKind)).Length];

		private BoardState _board;
		private BoardSettings _boardSettings;
		private PickupSettings _pickupSettings;
		private PowerUps _powerUps;
		private SurfPath _path;
		private WaterSurface _water;
		private ObstacleField _obstacles;
		private PickupField _field;
		private IGraphicsSettings _graphics;
		private float _nextTrailDistance;

		private Mesh _coinMesh;
		private Mesh _farCoinMesh;
		/// <summary>From the coin's pivot to its mesh.</summary>
		private Matrix4x4 _coinModel;
		private RenderParams _coinRenderParams;
		private RenderParams _farCoinRenderParams;
		private Matrix4x4[] _nearCoins = new Matrix4x4[32];
		private Matrix4x4[] _farCoins = new Matrix4x4[128];

		[Inject]
		public void Construct(BoardState board, BoardSettings boardSettings, PickupSettings pickupSettings,
			PowerUps powerUps, SurfPath path, WaterSurface water, ObstacleField obstacles, PickupField field,
			IGraphicsSettings graphics)
		{
			_graphics = graphics;
			_board = board;
			_boardSettings = boardSettings;
			_pickupSettings = pickupSettings;
			_powerUps = powerUps;
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
				_views[i] = new ObjectPool<View>(() => CreateView(kind), OnGetView, OnReleaseView);
			}

			var coinFilter = coinPrefab.GetComponentInChildren<MeshFilter>();
			_coinMesh = coinFilter.sharedMesh;
			_farCoinMesh = CreateFarCoinMesh(_coinMesh);
			_coinModel = coinPrefab.transform.worldToLocalMatrix * coinFilter.transform.localToWorldMatrix;

			// Too small for their shadows to show.
			_coinRenderParams = new RenderParams(coinFilter.GetComponent<MeshRenderer>().sharedMaterial)
			{
				layer = coinFilter.gameObject.layer,
				shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
				receiveShadows = true,
				renderingLayerMask = ToonOutlineFeature.RenderingLayer(true)
			};

			_farCoinRenderParams = _coinRenderParams;
			_farCoinRenderParams.renderingLayerMask = ToonOutlineFeature.RenderingLayer(false);
		}

		private void OnDestroy()
		{
			Destroy(_farCoinMesh);
		}

		private void Start()
		{
			_nextTrailDistance = firstTrailDistance;
		}

		private void Update()
		{
			float distance = _board.Distance;

			while (_nextTrailDistance < distance + spawnAhead * _graphics.DistanceScale)
			{
				_nextTrailDistance = SpawnTrail(_nextTrailDistance) + Random.Range(trailGap.x, trailGap.y);
			}

			Quaternion spin = Quaternion.Euler(0f, Time.time * spinSpeed, 0f);
			int nearCoins = 0;
			int farCoins = 0;

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
				// Collection checks against the board's own water height, so the view can use the cheaper one.
				position.y = (pickup.FollowsWater ? _water.GetHoverHeight(position) : center.y) + pickup.Height;

				Quaternion rotation = Quaternion.LookRotation(forward) * spin;
				float ahead = pickup.Distance - distance;

				if (pickup.Kind == PickupKind.Coin)
				{
					// Stand the flat coin up so it faces the rider.
					rotation *= Quaternion.Euler(90f, 0f, 0f);
					Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, Vector3.one) * _coinModel;

					if (ahead < coinDetailDistance)
					{
						AddInstance(ref _nearCoins, ref nearCoins, matrix);
					}
					else
					{
						AddInstance(ref _farCoins, ref farCoins, matrix);
					}

					continue;
				}

				View view = spawned.View;
				view.Transform.SetPositionAndRotation(position, rotation);

				bool outlined = ahead < ToonOutlineFeature.MaxDistance;
				if (outlined != view.Outlined)
				{
					view.Outlined = outlined;
					ToonOutlineFeature.SetOutlined(view.Renderers, outlined);
				}
			}

			if (nearCoins > 0)
			{
				Graphics.RenderMeshInstanced(_coinRenderParams, _coinMesh, 0, _nearCoins, nearCoins);
			}

			if (farCoins > 0)
			{
				Graphics.RenderMeshInstanced(_farCoinRenderParams, _farCoinMesh, 0, _farCoins, farCoins);
			}
		}

		private static void AddInstance(ref Matrix4x4[] instances, ref int count, Matrix4x4 matrix)
		{
			if (count == instances.Length)
			{
				System.Array.Resize(ref instances, count * 2);
			}

			instances[count++] = matrix;
		}

		/// <summary>Spawns one trail starting at the given distance and returns where it ends.</summary>
		private float SpawnTrail(float start)
		{
			float lateral = _boardSettings.LaneOffset(Random.Range(0, _boardSettings.LaneCount));
			int count = Random.Range(coinCount.x, coinCount.y + 1);
			int powerUpIndex = Random.value < powerUpChance ? count / 2 : -1;
			float previousSurface = float.NaN;
			float end = start;
			// Where super jump runs out, at the current speed.
			float superJumpUntil = _board.Distance + _powerUps.Remaining(PickupKind.SuperJump) * _board.Speed;

			for (int i = 0; i < count; i++)
			{
				float distance = start + i * coinSpacing;
				float surface = Placement(distance, lateral, superJumpUntil, out float height, out bool followsWater,
					out bool onJumpArc);

				if (!onJumpArc && surface - previousSurface > maxStep)
				{
					break;
				}

				previousSurface = surface;
				end = distance;

				PickupKind kind = i != powerUpIndex ? PickupKind.Coin : RandomPowerUp();
				Spawn(kind, distance, lateral, height, followsWater);
			}

			return end;
		}

		private PickupKind RandomPowerUp()
		{
			float roll = Random.Range(0f,
				nitroWeight + magnetWeight + multiplierWeight + shieldWeight + superJumpWeight + coinDoublerWeight);

			if ((roll -= nitroWeight) < 0f)
			{
				return PickupKind.Nitro;
			}

			if ((roll -= magnetWeight) < 0f)
			{
				return PickupKind.Magnet;
			}

			if ((roll -= multiplierWeight) < 0f)
			{
				return PickupKind.ScoreMultiplier;
			}

			if ((roll -= shieldWeight) < 0f)
			{
				return PickupKind.Shield;
			}

			return roll - superJumpWeight < 0f ? PickupKind.SuperJump : PickupKind.CoinDoubler;
		}

		/// <summary>
		/// Where a coin goes at the given point: hovering over the water, a barrier arc, a super jump arc or a train
		/// roof. Returns the nominal height of the surface under it, or of the board along a jump arc.
		/// </summary>
		private float Placement(float distance, float lateral, float superJumpUntil, out float height,
			out bool followsWater, out bool onJumpArc)
		{
			float waterLevel = _water.BaseHeight;
			float surface = waterLevel;
			height = hoverHeight;
			followsWater = true;
			onJumpArc = false;

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

				if (distance < box.Start && box.RampLength <= 0f &&
					TryJumpArc(box.Start - distance, box.Top - waterLevel, superJumpUntil - box.Start, out float rise))
				{
					float arc = waterLevel + rise;
					if (arc > surface)
					{
						surface = arc;
						height = arc + hoverHeight;
						followsWater = false;
						onJumpArc = true;
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

		/// <summary>
		/// Height of the board along a super jump that clears a wall of the given height exactly at it, at the given
		/// distance before the wall. Fails before the take-off, if the jump is too low, or if super jump runs out
		/// before the take-off (<paramref name="activeAfterWall"/> is how far past the wall it lasts).
		/// </summary>
		private bool TryJumpArc(float beforeWall, float wallHeight, float activeAfterWall, out float rise)
		{
			rise = 0f;
			float speed = _board.Speed;
			float gravity = _boardSettings.Gravity;
			float jumpHeight = Mathf.Max(_boardSettings.JumpHeight, _pickupSettings.SuperJumpHeight);

			if (speed <= 0f || wallHeight >= jumpHeight)
			{
				return false;
			}

			float launchSpeed = Mathf.Sqrt(2f * gravity * jumpHeight);
			float climbTime = (launchSpeed - Mathf.Sqrt(launchSpeed * launchSpeed - 2f * gravity * wallHeight)) / gravity;
			float runUp = speed * climbTime;

			if (beforeWall > runUp || activeAfterWall < -runUp)
			{
				return false;
			}

			float time = (runUp - beforeWall) / speed;
			rise = launchSpeed * time - 0.5f * gravity * time * time;
			return true;
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
			View view = kind != PickupKind.Coin ? _views[(int)kind].Get() : null;
			_spawned.Add(new Spawned { Pickup = pickup, View = view });
		}

		private void Despawn(Spawned spawned)
		{
			_field.Remove(spawned.Pickup);
			if (spawned.View != null)
			{
				_views[(int)spawned.Pickup.Kind].Release(spawned.View);
			}

			_freePickups.Push(spawned.Pickup);
		}

		private View CreateView(PickupKind kind)
		{
			var view = new View { Transform = CreateModel(kind) };
			view.Transform.GetComponentsInChildren(view.Renderers);
			return view;
		}

		private Transform CreateModel(PickupKind kind)
		{
			GameObject prefab = kind switch
			{
				PickupKind.Magnet => magnetPrefab,
				PickupKind.ScoreMultiplier => multiplierPrefab,
				PickupKind.Nitro => nitroPrefab,
				PickupKind.Shield => shieldPrefab,
				PickupKind.SuperJump => superJumpPrefab,
				PickupKind.CoinDoubler => coinDoublerPrefab,
				_ => null
			};

			if (prefab != null)
			{
				GameObject model = Instantiate(prefab, transform, false);
				model.name = kind.ToString();
				return model.transform;
			}

			GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
			primitive.name = kind.ToString();
			// Collection is resolved in path space by PickupCollector.
			Destroy(primitive.GetComponent<Collider>());

			Material material = kind switch
			{
				PickupKind.Magnet => magnetMaterial,
				PickupKind.ScoreMultiplier => multiplierMaterial,
				PickupKind.Nitro => nitroMaterial,
				PickupKind.Shield => shieldMaterial,
				PickupKind.SuperJump => superJumpMaterial,
				PickupKind.CoinDoubler => coinDoublerMaterial,
				_ => null
			};

			if (material != null)
			{
				primitive.GetComponent<MeshRenderer>().sharedMaterial = material;
			}

			Transform primitiveTransform = primitive.transform;
			primitiveTransform.SetParent(transform, false);
			primitiveTransform.localScale = powerUpSize;
			return primitiveTransform;
		}

		private static void OnGetView(View view)
		{
			view.Transform.gameObject.SetActive(true);
			// Spawned far ahead; Update turns the outline on once it comes close.
			view.Outlined = false;
			ToonOutlineFeature.SetOutlined(view.Renderers, false);
		}

		private static void OnReleaseView(View view)
		{
			view.Transform.gameObject.SetActive(false);
		}

		/// <summary>
		/// A twelve-sided, 44-triangle disc as wide and thick as the coin mesh, for coins too far away to show its
		/// bevels. The faces keep the model's texture by repeating its flat projection; the rim takes the texture
		/// coordinates of the nearest rim vertex of the model.
		/// </summary>
		private static Mesh CreateFarCoinMesh(Mesh source)
		{
			const int sides = 12;
			Vector3[] sourceVertices = source.vertices;
			Vector3[] sourceNormals = source.normals;
			Vector2[] sourceUVs = source.uv;
			Bounds bounds = source.bounds;
			float radius = Mathf.Max(bounds.extents.x, bounds.extents.y);

			// The face projection, from three face vertices spread as far apart as possible.
			int a = System.Array.FindIndex(sourceNormals, normal => normal.z > 0.9f);
			int b = a;
			int c = a;
			for (int i = 0; i < sourceVertices.Length; i++)
			{
				if (sourceNormals[i].z > 0.9f &&
				    (sourceVertices[i] - sourceVertices[a]).sqrMagnitude > (sourceVertices[b] - sourceVertices[a]).sqrMagnitude)
				{
					b = i;
				}
			}

			for (int i = 0; i < sourceVertices.Length; i++)
			{
				if (sourceNormals[i].z > 0.9f &&
				    Mathf.Abs(Cross(sourceVertices[b] - sourceVertices[a], sourceVertices[i] - sourceVertices[a])) >
				    Mathf.Abs(Cross(sourceVertices[b] - sourceVertices[a], sourceVertices[c] - sourceVertices[a])))
				{
					c = i;
				}
			}

			Vector2 ab = sourceVertices[b] - sourceVertices[a];
			Vector2 ac = sourceVertices[c] - sourceVertices[a];
			float determinant = ab.x * ac.y - ab.y * ac.x;

			Vector2 FaceUV(Vector3 position)
			{
				Vector2 offset = position - sourceVertices[a];
				float u = (offset.x * ac.y - offset.y * ac.x) / determinant;
				float v = (ab.x * offset.y - ab.y * offset.x) / determinant;
				return sourceUVs[a] + u * (sourceUVs[b] - sourceUVs[a]) + v * (sourceUVs[c] - sourceUVs[a]);
			}

			Vector2 RimUV(Vector3 position)
			{
				int nearest = -1;
				for (int i = 0; i < sourceVertices.Length; i++)
				{
					if (Mathf.Abs(sourceNormals[i].z) < 0.3f && (nearest < 0 ||
					    (sourceVertices[i] - position).sqrMagnitude < (sourceVertices[nearest] - position).sqrMagnitude))
					{
						nearest = i;
					}
				}

				return nearest >= 0 ? sourceUVs[nearest] : FaceUV(position);
			}

			// Front face, back face, rim front edge, rim back edge.
			var vertices = new Vector3[sides * 4];
			var normals = new Vector3[vertices.Length];
			var uvs = new Vector2[vertices.Length];

			for (int side = 0; side < sides; side++)
			{
				float angle = side * 2f * Mathf.PI / sides;
				var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
				Vector3 front = bounds.center + direction * radius + Vector3.forward * bounds.extents.z;
				Vector3 back = bounds.center + direction * radius - Vector3.forward * bounds.extents.z;

				vertices[side] = front;
				normals[side] = Vector3.forward;
				uvs[side] = FaceUV(front);

				vertices[sides + side] = back;
				normals[sides + side] = Vector3.back;
				uvs[sides + side] = FaceUV(back);

				vertices[sides * 2 + side] = front;
				normals[sides * 2 + side] = direction;
				uvs[sides * 2 + side] = RimUV(front);

				vertices[sides * 3 + side] = back;
				normals[sides * 3 + side] = direction;
				uvs[sides * 3 + side] = RimUV(back);
			}

			var triangles = new int[((sides - 2) * 2 + sides * 2) * 3];
			int index = 0;
			for (int side = 1; side < sides - 1; side++)
			{
				triangles[index++] = 0;
				triangles[index++] = side;
				triangles[index++] = side + 1;

				triangles[index++] = sides;
				triangles[index++] = sides + side + 1;
				triangles[index++] = sides + side;
			}

			for (int side = 0; side < sides; side++)
			{
				int next = (side + 1) % sides;

				triangles[index++] = sides * 3 + side;
				triangles[index++] = sides * 3 + next;
				triangles[index++] = sides * 2 + next;

				triangles[index++] = sides * 3 + side;
				triangles[index++] = sides * 2 + next;
				triangles[index++] = sides * 2 + side;
			}

			var mesh = new Mesh { name = "Coin Low", vertices = vertices, normals = normals, uv = uvs, triangles = triangles };
			mesh.RecalculateBounds();
			return mesh;
		}

		/// <summary>The Z of the cross product of the XY parts of two vectors.</summary>
		private static float Cross(Vector3 first, Vector3 second)
		{
			return first.x * second.y - first.y * second.x;
		}
	}
}
