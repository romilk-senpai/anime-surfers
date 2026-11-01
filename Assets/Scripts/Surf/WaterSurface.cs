using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;

namespace Game.Surf
{
	/// <summary>
	/// Owns the Gerstner wave settings, pushes them to the water shader (WaterWaves.hlsl) and lets gameplay
	/// sample the exact same surface the GPU renders. The wave mesh is a set of nested levels around a point ahead of
	/// the board, each twice the size of the one inside it with cells twice as large, ringed by flat, static water
	/// out to the far extent. Waves too short for a level's cells flatten out on its geometry, and all of them
	/// flatten toward the outer edge to meet the flat water; the shading keeps every wave.
	/// </summary>
	[ExecuteAlways]
	[DefaultExecutionOrder(-100)]
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	public class WaterSurface : MonoBehaviour, IWaterHeight
	{
		[Serializable]
		public struct Wave
		{
			[Range(0f, 360f)] public float direction;
			[Tooltip("Gerstner steepness; the sum over all waves should stay below 1 or crests start to loop.")]
			[Range(0f, 1f)] public float steepness;
			[Tooltip("Keep this at least ~6 cells of the finest mesh level long, otherwise the rendered mesh can't " +
			         "follow the wave and floating objects visibly sink into troughs. Finer detail belongs in the " +
			         "normal map.")]
			[Min(0.5f)] public float wavelength;
			[Min(0f)] public float speed;
		}

		public const int MaxWaves = 8;
		private const float Gravity = 9.81f;
		// Fixed-point iterations to find which grid point gets displaced onto a queried XZ position.
		private const int SourceIterations = 4;
		private const int MaxLevels = 8;
		// A wave's geometry fades out between these many cells per wavelength. Mirrors WaterWaves.hlsl.
		private const float MinWaveCells = 3f;
		private const float FullWaveCells = 6f;

		private static readonly int WavesId = Shader.PropertyToID("_WaterGerstnerWaves");
		private static readonly int ShapesId = Shader.PropertyToID("_WaterGerstnerShapes");
		private static readonly int TimeId = Shader.PropertyToID("_WaterWaveTime");
		private static readonly int AmplitudeSumId = Shader.PropertyToID("_WaterAmplitudeSum");
		private static readonly int BaseHeightId = Shader.PropertyToID("_WaterBaseHeight");
		private static readonly int MeshBoundsId = Shader.PropertyToID("_WaterMeshBounds");
		private static readonly int LodParamsId = Shader.PropertyToID("_WaterLodParams");
		// Packed wave shapes by wavelength (z), longest first.
		private static readonly IComparer<Vector4> LongestFirst =
			Comparer<Vector4>.Create((a, b) => b.z.CompareTo(a.z));

		// Uneven angles and wavelengths keep the sum from visibly tiling.
		[SerializeField] private Wave[] waves =
		{
			new Wave { direction = 10f, steepness = 0.021f, wavelength = 30f, speed = 1f },
			new Wave { direction = 0f, steepness = 0.042f, wavelength = 18f, speed = 1f },
			new Wave { direction = 35f, steepness = 0.056f, wavelength = 12f, speed = 1f },
			new Wave { direction = 325f, steepness = 0.063f, wavelength = 8.5f, speed = 1f },
			new Wave { direction = 80f, steepness = 0.063f, wavelength = 6.3f, speed = 1f },
			new Wave { direction = 205f, steepness = 0.056f, wavelength = 5f, speed = 1f },
			new Wave { direction = 290f, steepness = 0.049f, wavelength = 4.3f, speed = 1f },
			new Wave { direction = 150f, steepness = 0.042f, wavelength = 4f, speed = 1f },
		};

		[SerializeField] private float timeScale = 1f;

		[Header("Mesh")]
		[Tooltip("Cell size of the finest level, around the board.")]
		[SerializeField, Min(0.1f)] private float cellSize = 0.64f;
		[Tooltip("Cells across every level. Each level is twice the size of the one inside it, with cells twice as " +
		         "large. Rounded down to a multiple of 4.")]
		[SerializeField, Range(16, 128)] private int levelCells = 64;
		[Tooltip("How far the waves reach from the center at medium view distance; levels are added until they cover " +
		         "it. The player's setting scales it.")]
		[SerializeField, Min(1f)] private float waveExtent = 80f;
		[Tooltip("The levels are centered this far ahead of the follow target, so little of the mesh lies behind " +
		         "the camera.")]
		[SerializeField] private float forwardOffset = 12f;
		[Tooltip("Waves flatten over this distance before the mesh's outer edge, so it meets the flat far water " +
		         "without a step.")]
		[SerializeField, Min(0.1f)] private float edgeFlatten = 12f;
		[Tooltip("Half size of the flat water around the waves; it only needs to reach past the camera's far plane.")]
		[SerializeField] private float farExtent = 2000f;
		[SerializeField] private Transform followTarget;

		private readonly Vector4[] _packedWaves = new Vector4[MaxWaves];
		private readonly Vector4[] _packedShapes = new Vector4[MaxWaves];
		private readonly List<Vector3> _vertices = new List<Vector3>();
		private readonly List<int> _triangles = new List<int>();
		private readonly Dictionary<long, int> _vertexLookup = new Dictionary<long, int>();
		private readonly List<int> _polygon = new List<int>(5);
		private float _amplitudeSum;
		private float _waveScale = 1f;
		private float _time;
		private Mesh _mesh;
		private bool _meshDirty;
		private IGraphicsSettings _graphics;
		private float _meshScale = 1f;
		private float _innerHalfSize;
		private float _outerHalfSize;
		private float _coarseCell;
		// Unsnapped center that the wave fades are measured from, so they move smoothly with the target while the
		// mesh jumps by whole coarse cells.
		private Vector2 _lodCenter;

		public float WaveTime => _time;
		public float BaseHeight => transform.position.y;

		/// <summary>
		/// Multiplies every wave's height, e.g. 2 in a storm. Gameplay queries see the change right away. Keep the
		/// summed steepness times this below 1.
		/// </summary>
		public float WaveScale
		{
			get => _waveScale;
			set
			{
				if (Mathf.Approximately(value, _waveScale))
				{
					return;
				}

				_waveScale = value;
				PackWaves();
			}
		}

		// The mesh is snapped up to half a coarse cell away from the fade center on each axis.
		private float SnapMargin => _coarseCell * 0.5f;

		[Inject]
		public void Construct(IGraphicsSettings graphics)
		{
			_graphics = graphics;
		}

		private void OnEnable()
		{
			BuildMesh();
			PackWaves();
			FollowTarget();
			PushGlobals();
		}

		private void OnDisable()
		{
			if (_mesh != null)
			{
				DestroyImmediate(_mesh);
				_mesh = null;
			}
		}

		private void OnValidate()
		{
			_meshDirty = true;
			PackWaves();
		}

		private void Update()
		{
			// Not injected in edit mode.
			float scale = _graphics != null ? _graphics.DistanceScale : 1f;

			if (_meshDirty || !Mathf.Approximately(scale, _meshScale))
			{
				_meshScale = scale;
				BuildMesh();
			}

			SyncClock();

			FollowTarget();
			PushGlobals();
		}

		/// <summary>World-space height of the water surface at the given XZ position.</summary>
		public float GetHeight(Vector3 position)
		{
			return GetHeight(position, SourceIterations);
		}

		/// <summary>
		/// Cheaper <see cref="GetHeight(Vector3)"/> for things that only hover over the water: a single step toward the
		/// grid point displaced onto the position, about a centimeter off instead of exact.
		/// </summary>
		public float GetHoverHeight(Vector3 position)
		{
			return GetHeight(position, 1);
		}

		private float GetHeight(Vector3 position, int sourceIterations)
		{
			SyncClock();
			Vector2 source = FindSource(position.x, position.z, sourceIterations);
			EvaluateWaves(source.x, source.y, out Vector3 offset, out _, out _);
			return BaseHeight + offset.y;
		}

		/// <summary>World-space surface normal at the given XZ position.</summary>
		public Vector3 GetNormal(Vector3 position)
		{
			SyncClock();
			Vector2 source = FindSource(position.x, position.z, SourceIterations);
			EvaluateWaves(source.x, source.y, out _, out Vector3 tangent, out Vector3 binormal);
			return Vector3.Cross(binormal, tangent).normalized;
		}

		// Gameplay can sample before this component's Update (VContainer ticks run first), so the clock is
		// refreshed on every query to always match what the GPU renders this frame.
		private void SyncClock()
		{
			_time = (Application.isPlaying ? Time.time : Time.realtimeSinceStartup) * timeScale;
		}

		// Gerstner waves also move points horizontally, so the surface above a position comes from a
		// grid point somewhere else. Converges quickly while the total steepness stays below 1.
		private Vector2 FindSource(float x, float z, int iterations)
		{
			float sourceX = x;
			float sourceZ = z;

			for (int i = 0; i < iterations; i++)
			{
				EvaluateWaves(sourceX, sourceZ, out Vector3 offset, out _, out _);
				sourceX = x - offset.x;
				sourceZ = z - offset.z;
			}

			return new Vector2(sourceX, sourceZ);
		}

		// Mirrors EvaluateWaterWavesLod in WaterWaves.hlsl and the vertex shader's edge flattening: the geometry
		// the GPU renders, which is what floating objects must sit on.
		private void EvaluateWaves(float x, float z, out Vector3 offset, out Vector3 tangent, out Vector3 binormal)
		{
			offset = Vector3.zero;
			tangent = new Vector3(1f, 0f, 0f);
			binormal = new Vector3(0f, 0f, 1f);

			// Gameplay can ask before the mesh exists (the container is built before this is enabled): flat water.
			if (_innerHalfSize <= 0f)
			{
				return;
			}

			float distance = CenterDistance(x, z);
			float inverseCell = LodInverseCell(distance);

			for (int i = 0; i < MaxWaves; i++)
			{
				Vector4 wave = _packedWaves[i];
				Vector4 shape = _packedShapes[i];
				float fade = Mathf.Clamp01((shape.z * inverseCell - MinWaveCells) / (FullWaveCells - MinWaveCells));
				float dx = wave.x;
				float dz = wave.y;
				float steepness = shape.x * fade;
				float a = shape.y * fade;
				float f = wave.z * (dx * x + dz * z) - wave.w * _time;
				float s = Mathf.Sin(f);
				float c = Mathf.Cos(f);

				offset.x += dx * a * c;
				offset.y += a * s;
				offset.z += dz * a * c;

				tangent.x -= dx * dx * steepness * s;
				tangent.y += dx * steepness * c;
				tangent.z -= dx * dz * steepness * s;

				binormal.x -= dx * dz * steepness * s;
				binormal.y += dz * steepness * c;
				binormal.z -= dz * dz * steepness * s;
			}

			float edgeFade = EdgeFade(distance);
			offset *= edgeFade;
			tangent = Vector3.right + (tangent - Vector3.right) * edgeFade;
			binormal = Vector3.forward + (binormal - Vector3.forward) * edgeFade;
		}

		/// <summary>Chebyshev distance from the fade center; the levels are squares.</summary>
		private float CenterDistance(float x, float z)
		{
			return Mathf.Max(Mathf.Abs(x - _lodCenter.x), Mathf.Abs(z - _lodCenter.y));
		}

		// Mirrors WaterLodInverseCell in WaterWaves.hlsl: one over an upper bound of the cell size at that distance,
		// wherever the snapped mesh is.
		private float LodInverseCell(float distance)
		{
			return 1f / (cellSize * Mathf.Max(1f, (distance + SnapMargin) * 2f / _innerHalfSize));
		}

		// Mirrors WaterEdgeFade in WaterWaves.hlsl; reaches 0 before the snapped mesh's edge.
		private float EdgeFade(float distance)
		{
			return Mathf.Clamp01((_outerHalfSize - SnapMargin - distance) / edgeFlatten);
		}

		private void PackWaves()
		{
			_amplitudeSum = 0f;

			for (int i = 0; i < MaxWaves; i++)
			{
				if (waves == null || i >= waves.Length)
				{
					_packedWaves[i] = Vector4.zero;
					_packedShapes[i] = Vector4.zero;
					continue;
				}

				Wave wave = waves[i];
				float wavelength = Mathf.Max(wave.wavelength, 0.5f);
				float k = 2f * Mathf.PI / wavelength;
				float angle = wave.direction * Mathf.Deg2Rad;
				// Deep-water dispersion keeps long waves fast and short ones slow.
				float omega = Mathf.Sqrt(Gravity * k) * wave.speed;
				float steepness = wave.steepness * _waveScale;
				float amplitude = steepness / k;

				_packedWaves[i] = new Vector4(Mathf.Sin(angle), Mathf.Cos(angle), k, omega);
				_packedShapes[i] = new Vector4(steepness, amplitude, wavelength, 0f);
				_amplitudeSum += amplitude;
			}

			// Longest first: on low quality the water shader shades the first ones from the vertices.
			Array.Sort(_packedShapes, _packedWaves, LongestFirst);
		}

		private void PushGlobals()
		{
			Shader.SetGlobalVectorArray(WavesId, _packedWaves);
			Shader.SetGlobalVectorArray(ShapesId, _packedShapes);
			Shader.SetGlobalFloat(TimeId, _time);
			Shader.SetGlobalFloat(AmplitudeSumId, _amplitudeSum);
			Shader.SetGlobalFloat(BaseHeightId, BaseHeight);
			Shader.SetGlobalVector(MeshBoundsId,
				new Vector4(_lodCenter.x, _lodCenter.y, _outerHalfSize - SnapMargin, edgeFlatten));
			Shader.SetGlobalVector(LodParamsId, new Vector4(cellSize, 2f / _innerHalfSize, SnapMargin, 0f));
		}

		private void FollowTarget()
		{
			if (followTarget == null)
			{
				Vector3 position = transform.position;
				_lodCenter = new Vector2(position.x, position.z);
				return;
			}

			Vector3 target = followTarget.position;
			Vector3 forward = followTarget.forward;
			forward.y = 0f;
			if (forward.sqrMagnitude > 1e-4f)
			{
				target += forward.normalized * forwardOffset;
			}

			_lodCenter = new Vector2(target.x, target.z);

			// Snap to the coarsest cell, a multiple of every level's cell, so all vertices stay on the same world
			// lattice and the waves don't swim.
			transform.position = new Vector3(Mathf.Round(target.x / _coarseCell) * _coarseCell, transform.position.y,
				Mathf.Round(target.z / _coarseCell) * _coarseCell);
		}

		private void BuildMesh()
		{
			_meshDirty = false;

			if (_mesh == null)
			{
				_mesh = new Mesh { name = "Water Grid", hideFlags = HideFlags.DontSave };
			}

			// Grid coordinates are in finest cells; a level's cells are 2^level of them.
			int cells = Mathf.Max(16, levelCells / 4 * 4);
			int innerHalf = cells / 2;
			_innerHalfSize = innerHalf * cellSize;

			int levels = 1;
			while (levels < MaxLevels && _innerHalfSize * (1 << (levels - 1)) < waveExtent * _meshScale)
			{
				levels++;
			}

			int coarseStep = 1 << (levels - 1);
			int outerHalf = innerHalf * coarseStep;
			_coarseCell = cellSize * coarseStep;
			_outerHalfSize = outerHalf * cellSize;

			_vertices.Clear();
			_triangles.Clear();
			_vertexLookup.Clear();

			for (int level = 0; level < levels; level++)
			{
				int step = 1 << level;
				int half = innerHalf * step;
				// Each level is a ring around the one inside it.
				int hole = level == 0 ? 0 : half / 2;

				for (int z = -half; z < half; z += step)
				{
					for (int x = -half; x < half; x += step)
					{
						if (x >= -hole && x < hole && z >= -hole && z < hole)
						{
							continue;
						}

						AddCell(x, z, step, hole);
					}
				}
			}

			// Flat far ring: the outer edge's vertices are joined to copies pushed out along the line from the
			// center. It shares the edge vertices, so there are no cracks.
			float far = Mathf.Max(farExtent, _outerHalfSize + _coarseCell);
			int edgeCells = 2 * outerHalf / coarseStep;

			for (int edge = 0; edge < 4; edge++)
			{
				int outerStart = _vertices.Count;

				for (int j = 0; j <= edgeCells; j++)
				{
					_vertices.Add(_vertices[EdgeVertex(edge, j, outerHalf, coarseStep)] * (far / _outerHalfSize));
				}

				for (int j = 0; j < edgeCells; j++)
				{
					int innerA = EdgeVertex(edge, j, outerHalf, coarseStep);
					int innerB = EdgeVertex(edge, j + 1, outerHalf, coarseStep);
					AddTriangle(innerA, innerB, outerStart + j);
					AddTriangle(innerB, outerStart + j + 1, outerStart + j);
				}
			}

			_mesh.Clear();
			_mesh.indexFormat = _vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
			_mesh.SetVertices(_vertices);
			_mesh.SetTriangles(_triangles, 0);
			_mesh.RecalculateNormals();
			// Waves are displaced on the GPU, so grow the bounds to avoid culling crests.
			_mesh.bounds = new Bounds(Vector3.zero, new Vector3(far * 2f, 20f, far * 2f));

			GetComponent<MeshFilter>().sharedMesh = _mesh;
		}

		/// <summary>
		/// Adds one cell of a level. An edge on the boundary of the finer level inside gets that level's midpoint
		/// vertex, so the levels share their vertices and the waves can't open cracks between them.
		/// </summary>
		private void AddCell(int x, int z, int step, int hole)
		{
			_polygon.Clear();
			int split = -1;

			for (int i = 0; i < 4; i++)
			{
				CellCorner(x, z, step, i, out int ax, out int az);
				CellCorner(x, z, step, (i + 1) % 4, out int bx, out int bz);
				_polygon.Add(Vertex(ax, az));

				if (hole > 0 && OnSquare(ax, az, hole) && OnSquare(bx, bz, hole))
				{
					split = i;
					_polygon.Add(Vertex((ax + bx) / 2, (az + bz) / 2));
				}
			}

			// Fan from a corner off the split edge, so no triangle is degenerate. Unsplit cells use the (1, 3)
			// diagonal.
			int origin = 1;
			if (split >= 0)
			{
				// The corner opposite the split edge's start, shifted past the midpoint inserted after that corner.
				int corner = (split + 2) % 4;
				origin = corner > split ? corner + 1 : corner;
			}

			int count = _polygon.Count;

			for (int k = 1; k < count - 1; k++)
			{
				AddTriangle(_polygon[origin], _polygon[(origin + k) % count], _polygon[(origin + k + 1) % count]);
			}
		}

		/// <summary>Corners in order around the cell.</summary>
		private static void CellCorner(int x, int z, int step, int corner, out int cornerX, out int cornerZ)
		{
			cornerX = corner == 1 || corner == 2 ? x + step : x;
			cornerZ = corner >= 2 ? z + step : z;
		}

		private static bool OnSquare(int x, int z, int half)
		{
			return Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) == half;
		}

		private int Vertex(int x, int z)
		{
			long key = ((long)x << 32) | (uint)z;

			if (!_vertexLookup.TryGetValue(key, out int index))
			{
				index = _vertices.Count;
				_vertices.Add(new Vector3(x * cellSize, 0f, z * cellSize));
				_vertexLookup.Add(key, index);
			}

			return index;
		}

		/// <summary>The j-th vertex along one side of the outer edge, walking around it: bottom, right, top, left.</summary>
		private int EdgeVertex(int edge, int j, int half, int step)
		{
			int along = -half + j * step;

			switch (edge)
			{
				case 0: return Vertex(along, -half);
				case 1: return Vertex(half, along);
				case 2: return Vertex(-along, half);
				default: return Vertex(-half, -along);
			}
		}

		/// <summary>Adds the triangle facing up (front faces wind clockwise seen from above).</summary>
		private void AddTriangle(int a, int b, int c)
		{
			if (Vector3.Cross(_vertices[b] - _vertices[a], _vertices[c] - _vertices[a]).y < 0f)
			{
				(b, c) = (c, b);
			}

			_triangles.Add(a);
			_triangles.Add(b);
			_triangles.Add(c);
		}
	}
}
