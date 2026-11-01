using System.Collections.Generic;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;

namespace Game.Surf
{
	/// <summary>
	/// Visualization of the passage limits: buoys along both edges of the path joined by a sagging rope, drawn with GPU
	/// instancing. Nothing casts shadows. Near buoys use the prefab's model with an outline; beyond that a low-poly
	/// stand-in without one, and far ahead only every other pair is drawn.
	/// </summary>
	public class PassageBounds : MonoBehaviour
	{
		private struct PostPair
		{
			public Matrix4x4 Left;
			public Matrix4x4 Right;
			public float Distance;
			public bool Odd;
		}

		[SerializeField] private float wallMargin = 0.75f;
		[SerializeField] private float postSpacing = 3f;
		[SerializeField] private float spawnAhead = 140f;
		[Tooltip("Past the camera, which trails the board.")]
		[SerializeField] private float despawnBehind = 8f;
		[Tooltip("Up to this distance ahead buoys use the prefab's model and are outlined; beyond it a low-poly " +
		         "stand-in without outline.")]
		[SerializeField] private float detailDistance = 15f;
		[Tooltip("Beyond this distance ahead only every other pair is drawn.")]
		[SerializeField] private float sparseDistance = 60f;
		[Tooltip("Mesh and materials are taken from this prefab; it is never instantiated. The mesh needs Read/Write " +
		         "enabled, as the far stand-in is built from it.")]
		[SerializeField] private GameObject postPrefab;
		[SerializeField] private float postRadius = 0.5f;
		[SerializeField] private float postHeight;

		[Header("Rope")]
		[Tooltip("Rope between neighboring buoys, drawn without shadows; its material should have the outline pass " +
		         "disabled. None leaves the rope out.")]
		[SerializeField] private Material ropeMaterial;
		[Tooltip("Height of the rope ends above the buoy pivot.")]
		[SerializeField] private float ropeHeight = 0.1f;
		[SerializeField] private float ropeRadius = 0.015f;
		[SerializeField] private float ropeSag = 0.08f;
		[SerializeField, Min(1)] private int ropeSegments = 2;

		private readonly Queue<PostPair> _active = new Queue<PostPair>();
		private Matrix4x4[] _matrices = new Matrix4x4[128];
		private Mesh _mesh;
		private Mesh _farMesh;
		private RenderParams[] _renderParams;
		private RenderParams _farRenderParams;
		private Matrix4x4[] _ropeMatrices = new Matrix4x4[128];
		private Mesh _ropeMesh;
		private RenderParams _ropeRenderParams;
		private BoardState _board;
		private BoardSettings _boardSettings;
		private SurfPath _path;
		private float _nextDistance;
		private bool _nextOdd;

		[Inject]
		public void Construct(BoardState board, BoardSettings boardSettings, SurfPath path)
		{
			_board = board;
			_boardSettings = boardSettings;
			_path = path;
		}

		private void Awake()
		{
			_mesh = postPrefab.GetComponentInChildren<MeshFilter>().sharedMesh;
			_farMesh = CreateFarPostMesh(_mesh);
			Material[] materials = postPrefab.GetComponentInChildren<MeshRenderer>().sharedMaterials;

			_renderParams = new RenderParams[materials.Length];
			for (int i = 0; i < materials.Length; i++)
			{
				// Too small for their shadows to show.
				_renderParams[i] = new RenderParams(materials[i])
				{
					layer = gameObject.layer,
					shadowCastingMode = ShadowCastingMode.Off,
					receiveShadows = true,
					renderingLayerMask = ToonOutlineFeature.RenderingLayer(true)
				};
			}

			_farRenderParams = _renderParams[0];
			_farRenderParams.renderingLayerMask = ToonOutlineFeature.RenderingLayer(false);

			if (ropeMaterial != null)
			{
				_ropeMesh = CreateRopeMesh();
				_ropeRenderParams = new RenderParams(ropeMaterial)
				{
					layer = gameObject.layer,
					shadowCastingMode = ShadowCastingMode.Off,
					receiveShadows = true
				};
			}
		}

		private void OnDestroy()
		{
			Destroy(_farMesh);
			Destroy(_ropeMesh);
		}

		private void Update()
		{
			float distance = _board.Distance;

			while (_nextDistance < distance + spawnAhead)
			{
				SpawnPair(_nextDistance, _nextOdd);
				_nextDistance += postSpacing;
				_nextOdd = !_nextOdd;
			}

			while (_active.Count > 0 && _active.Peek().Distance < distance - despawnBehind)
			{
				_active.Dequeue();
			}

			Draw(distance);
		}

		private void SpawnPair(float distance, bool odd)
		{
			_path.Sample(distance, out Vector3 center, out Vector3 forward);
			Vector3 right = Vector3.Cross(Vector3.up, forward);
			Quaternion rotation = Quaternion.LookRotation(forward);
			float halfWidth = _boardSettings.PassageHalfWidth + wallMargin + postRadius;
			Vector3 up = Vector3.up * postHeight;

			_active.Enqueue(new PostPair
			{
				Left = Matrix4x4.TRS(center - right * halfWidth + up, rotation, Vector3.one),
				Right = Matrix4x4.TRS(center + right * halfWidth + up, rotation, Vector3.one),
				Distance = distance,
				Odd = odd
			});
		}

		private void Draw(float boardDistance)
		{
			if (_matrices.Length < _active.Count * 2)
			{
				_matrices = new Matrix4x4[Mathf.NextPowerOfTwo(_active.Count * 2)];
				_ropeMatrices = new Matrix4x4[_matrices.Length];
			}

			// The queue is ordered by distance, so the detailed near buoys come first.
			int count = 0;
			int nearCount = 0;
			foreach (PostPair pair in _active)
			{
				float ahead = pair.Distance - boardDistance;
				if (pair.Odd && ahead > sparseDistance)
				{
					continue;
				}

				// Each rope runs from the previous drawn buoy on its side to this one.
				if (count > 0)
				{
					_ropeMatrices[count - 2] = RopeMatrix(_matrices[count - 2], pair.Left);
					_ropeMatrices[count - 1] = RopeMatrix(_matrices[count - 1], pair.Right);
				}

				_matrices[count++] = pair.Left;
				_matrices[count++] = pair.Right;

				if (ahead < detailDistance)
				{
					nearCount = count;
				}
			}

			if (_ropeMesh != null && count > 2)
			{
				Graphics.RenderMeshInstanced(_ropeRenderParams, _ropeMesh, 0, _ropeMatrices, count - 2);
			}

			if (nearCount > 0)
			{
				for (int submesh = 0; submesh < _renderParams.Length; submesh++)
				{
					Graphics.RenderMeshInstanced(_renderParams[submesh], _mesh, submesh, _matrices, nearCount);
				}
			}

			if (count > nearCount)
			{
				Graphics.RenderMeshInstanced(_farRenderParams, _farMesh, 0, _matrices, count - nearCount, nearCount);
			}
		}

		/// <summary>
		/// A six-sided, 24-triangle stand-in turned from the buoy model: a cone below the waterline in the model's bottom
		/// color, then a band and a cone above it in the top color. Assumes a buoy that is round around Y, colored from a
		/// palette with one color below the waterline and one above.
		/// </summary>
		private static Mesh CreateFarPostMesh(Mesh source)
		{
			const int sides = 6;
			Vector3[] sourceVertices = source.vertices;
			Vector2[] sourceUVs = source.uv;

			int lowest = 0;
			int highest = 0;
			float radius = 0f;
			for (int i = 0; i < sourceVertices.Length; i++)
			{
				Vector3 vertex = sourceVertices[i];
				lowest = vertex.y < sourceVertices[lowest].y ? i : lowest;
				highest = vertex.y > sourceVertices[highest].y ? i : highest;
				radius = Mathf.Max(radius, new Vector2(vertex.x, vertex.z).magnitude);
			}

			float bottom = sourceVertices[lowest].y;
			float top = sourceVertices[highest].y;
			Vector2 bottomUV = sourceUVs[lowest];
			Vector2 topUV = sourceUVs[highest];

			// The top of the bottom color.
			float waterline = bottom;
			for (int i = 0; i < sourceVertices.Length; i++)
			{
				if (sourceUVs[i] == bottomUV)
				{
					waterline = Mathf.Max(waterline, sourceVertices[i].y);
				}
			}

			// Where the body starts to round off toward the top.
			float shoulder = Mathf.Lerp(waterline, top, 0.6f);

			// Bottom tip, waterline ring in each color, shoulder ring, top tip.
			var vertices = new Vector3[2 + sides * 3];
			var uvs = new Vector2[vertices.Length];
			vertices[0] = new Vector3(0f, bottom, 0f);
			uvs[0] = bottomUV;
			vertices[vertices.Length - 1] = new Vector3(0f, top, 0f);
			uvs[vertices.Length - 1] = topUV;

			for (int side = 0; side < sides; side++)
			{
				float angle = side * 2f * Mathf.PI / sides;
				var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
				vertices[1 + side] = direction * radius + Vector3.up * waterline;
				uvs[1 + side] = bottomUV;
				vertices[1 + sides + side] = vertices[1 + side];
				uvs[1 + sides + side] = topUV;
				vertices[1 + sides * 2 + side] = direction * (radius * 0.85f) + Vector3.up * shoulder;
				uvs[1 + sides * 2 + side] = topUV;
			}

			var triangles = new int[sides * 4 * 3];
			int index = 0;
			for (int side = 0; side < sides; side++)
			{
				int next = (side + 1) % sides;
				int lowerA = 1 + sides + side;
				int lowerB = 1 + sides + next;
				int upperA = 1 + sides * 2 + side;
				int upperB = 1 + sides * 2 + next;

				triangles[index++] = 0;
				triangles[index++] = 1 + side;
				triangles[index++] = 1 + next;

				triangles[index++] = lowerA;
				triangles[index++] = upperA;
				triangles[index++] = upperB;
				triangles[index++] = lowerA;
				triangles[index++] = upperB;
				triangles[index++] = lowerB;

				triangles[index++] = upperA;
				triangles[index++] = vertices.Length - 1;
				triangles[index++] = upperB;
			}

			var mesh = new Mesh { name = "Buoy Low", vertices = vertices, uv = uvs, triangles = triangles };
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}

		/// <summary>The rope mesh, which runs along local Z from 0 to 1, stretched between two buoys.</summary>
		private Matrix4x4 RopeMatrix(Matrix4x4 from, Matrix4x4 to)
		{
			Vector3 start = from.GetPosition() + Vector3.up * ropeHeight;
			Vector3 span = to.GetPosition() - from.GetPosition();
			return Matrix4x4.TRS(start, Quaternion.LookRotation(span), new Vector3(1f, 1f, span.magnitude));
		}

		/// <summary>
		/// A triangular tube along Z from 0 to 1 with a parabolic sag. Radius and sag do not change when the rope is
		/// stretched along Z.
		/// </summary>
		private Mesh CreateRopeMesh()
		{
			const int sides = 3;
			var vertices = new Vector3[(ropeSegments + 1) * sides];
			var normals = new Vector3[vertices.Length];
			var triangles = new int[ropeSegments * sides * 6];

			for (int ring = 0; ring <= ropeSegments; ring++)
			{
				float t = (float)ring / ropeSegments;
				var center = new Vector3(0f, -4f * ropeSag * t * (1f - t), t);

				for (int side = 0; side < sides; side++)
				{
					// One edge points down, so the flat top faces the camera.
					float angle = -0.5f * Mathf.PI + side * 2f * Mathf.PI / sides;
					var normal = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
					vertices[ring * sides + side] = center + normal * ropeRadius;
					normals[ring * sides + side] = normal;
				}
			}

			int index = 0;
			for (int ring = 0; ring < ropeSegments; ring++)
			{
				for (int side = 0; side < sides; side++)
				{
					int a = ring * sides + side;
					int b = ring * sides + (side + 1) % sides;
					int c = a + sides;
					int d = b + sides;

					triangles[index++] = a;
					triangles[index++] = b;
					triangles[index++] = d;
					triangles[index++] = a;
					triangles[index++] = d;
					triangles[index++] = c;
				}
			}

			var mesh = new Mesh { name = "Passage Rope", vertices = vertices, normals = normals, triangles = triangles };
			mesh.RecalculateBounds();
			return mesh;
		}
	}
}
