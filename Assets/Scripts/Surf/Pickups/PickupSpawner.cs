using System.Collections.Generic;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

namespace Game.Surf
{
	/// <summary>
	/// Draws the pickups of the <see cref="PickupField"/> that <see cref="PickupGenerator"/> places. Coins are drawn
	/// with GPU instancing: the model with an outline up close, a low-poly stand-in without one beyond. Power-up visuals
	/// are pooled per kind, kinds without a model are cubes, and their outlines are only drawn once they come close.
	/// </summary>
	public class PickupSpawner : MonoBehaviour
	{
		private class View
		{
			public Transform Transform;
			public readonly List<Renderer> Renderers = new List<Renderer>();
			public bool Outlined;
		}

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

		/// <summary>Power-ups only; coins are drawn instanced.</summary>
		private readonly Dictionary<Pickup, View> _spawned = new Dictionary<Pickup, View>();
		private readonly ObjectPool<View>[] _views =
			new ObjectPool<View>[System.Enum.GetValues(typeof(PickupKind)).Length];

		private IBoardState _board;
		private SurfPath _path;
		private IWaterSurface _water;
		private PickupField _field;

		private Mesh _coinMesh;
		private Mesh _farCoinMesh;
		/// <summary>From the coin's pivot to its mesh.</summary>
		private Matrix4x4 _coinModel;
		private RenderParams _coinRenderParams;
		private RenderParams _farCoinRenderParams;
		private Matrix4x4[] _nearCoins = new Matrix4x4[32];
		private Matrix4x4[] _farCoins = new Matrix4x4[128];

		[Inject]
		public void Construct(IBoardState board, SurfPath path, IWaterSurface water, PickupField field)
		{
			_board = board;
			_path = path;
			_water = water;
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

		private void Start()
		{
			_field.Added += OnAdded;
			_field.Removed += OnRemoved;

			foreach (Pickup pickup in _field.Pickups)
			{
				OnAdded(pickup);
			}
		}

		private void OnDestroy()
		{
			if (_field != null)
			{
				_field.Added -= OnAdded;
				_field.Removed -= OnRemoved;
			}

			Destroy(_farCoinMesh);
		}

		private void Update()
		{
			float distance = _board.Distance;
			Quaternion spin = Quaternion.Euler(0f, Time.time * spinSpeed, 0f);
			int nearCoins = 0;
			int farCoins = 0;
			IReadOnlyList<Pickup> pickups = _field.Pickups;

			for (int i = 0; i < pickups.Count; i++)
			{
				Pickup pickup = pickups[i];
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

				View view = _spawned[pickup];
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

		private void OnAdded(Pickup pickup)
		{
			if (pickup.Kind != PickupKind.Coin)
			{
				_spawned.Add(pickup, _views[(int)pickup.Kind].Get());
			}
		}

		private void OnRemoved(Pickup pickup)
		{
			if (_spawned.Remove(pickup, out View view))
			{
				_views[(int)pickup.Kind].Release(view);
			}
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
