using UnityEngine;
using VContainer;

namespace Game.Surf
{
	/// <summary>
	/// Draws the big wave's lip: a thick shell curling up from the crest and over the barrel, with the water material's
	/// lip variant. Its tip hangs above the water, so the tube stays open on the side the board comes in and goes out.
	/// The mesh holds the shell's cross-section over a stretch around the rider, thrown out of the crest as far as the
	/// lip has pitched at each point: fully from the tube's mouth to just ahead of the rider, then less and less toward
	/// the front, which leaves the tube's eye open ahead, and less where the ridge tapers off. Each cross-section carries
	/// on from the face's slope at the crest and turns over as it goes (its tangent turning, as in the SNOWFLOW wake);
	/// a lip pitched partway is shorter and turned less, a smaller curl whose tip still heads out and down, instead of
	/// the start of the full one. The shader places it on the
	/// wave (BigWaveLipPosition in WaterWaves.hlsl); this rebuilds the cross-sections every frame, keeps the renderer near
	/// the rider for culling, shares the water's time-of-day tint and hides the lip while there's none.
	/// </summary>
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	public class BigWaveLip : MonoBehaviour
	{
		// The curl is laid out for a 15 m reach and a 7 m wave, in meters from the crest (across toward the wave,
		// height), and scaled to the settings'. Its inner surface starts here, just under the face.
		private static readonly Vector2 CurlRoot = new Vector2(-1f, -1.6f);
		private const float CurlReach = 15f;
		private const float CurlHeight = 7f;
		/// <summary>Integration steps per cross segment.</summary>
		private const int CurlSubsteps = 4;

		[Tooltip("The water, whose time-of-day tint the lip shares.")]
		[SerializeField] private Renderer waterRenderer;
		[SerializeField, Min(4)] private int crossSegments = 24;
		[SerializeField, Min(0.5f)] private float alongSpacing = 2f;
		[Tooltip("Length of the fully thrown curl's inner surface, root to tip, for a 15 m reach and a 7 m wave. With " +
		         "the tip angle it sets where the tip hangs: about 15 m out and 4 m above the still water.")]
		[SerializeField, Min(1f)] private float curlLength = 23.5f;
		[Tooltip("Degrees the fully thrown tip points (0 = away from the passage, 90 = up, 180 = over the passage, " +
		         "270 = straight down). The curl turns from the face's slope at the crest to this.")]
		[SerializeField, Range(180f, 300f)] private float tipAngle = 254f;
		[Tooltip("How much more the curl turns near its root than at its tip, which falls straighter.")]
		[SerializeField, Range(0f, 1f)] private float curlBend = 0.7f;
		[Tooltip("A lip pitched partway is turned by its pitch to this power (and as long as its pitch): low, it turns " +
		         "over early, a small curl whose tip already hangs down rather than a ledge sticking out.")]
		[SerializeField, Range(0.05f, 1f)] private float turnLead = 0.15f;
		[Tooltip("The lip rolls back into the crest over this far at its back end, where the wave has broken.")]
		[SerializeField, Min(0.1f)] private float backGrowth = 10f;
		[Tooltip("Thickness of the fully thrown lip where it leaves the crest, and at its tip; a lip pitched partway " +
		         "is thinner.")]
		[SerializeField] private Vector2 thickness = new Vector2(1.6f, 0.35f);
		[Tooltip("The tip's reach wobbles along the ridge by up to this share of the curl, so its edge isn't straight.")]
		[SerializeField, Range(0f, 0.5f)] private float tipWobble = 0.1f;
		[Tooltip("Meters along the ridge between the tip's wobbles.")]
		[SerializeField, Min(0.5f)] private float tipWobbleLength = 8f;
		[Tooltip("How fast the tip's wobbles shift.")]
		[SerializeField, Min(0f)] private float tipWobbleSpeed = 0.4f;

		private BigWaveSettings _settings;
		private IWaterSurface _water;
		private MeshRenderer _renderer;
		private MaterialPropertyBlock _properties;
		private Mesh _mesh;
		private Vector2 _curlScale;
		/// <summary>The root's angle in the curl's layout: the face's slope at the crest.</summary>
		private float _rootAngle;
		// One cross-section's inner surface, root to tip: points, unit tangents and meters along it.
		private Vector2[] _section;
		private Vector2[] _sectionTangents;
		private float[] _sectionArc;
		private Vector3[] _vertices;
		private Vector3[] _normals;
		private Vector4[] _uvs;
		/// <summary>The shell's thickness at each vertex, for the light passing through it, how far short of fully
		/// pitched it is there (where it feathers into whitewater), and how far it has pitched (the shader only shows
		/// it once it's a curl).</summary>
		private Vector3[] _thicknessUVs;
		private int _alongSegments;

		[Inject]
		public void Construct(BigWaveSettings settings, IWaterSurface water)
		{
			_settings = settings;
			_water = water;
		}

		private void Awake()
		{
			_renderer = GetComponent<MeshRenderer>();
			_renderer.enabled = false;
			_properties = new MaterialPropertyBlock();
		}

		private void OnDestroy()
		{
			if (_mesh != null)
			{
				Destroy(_mesh);
			}
		}

		private void LateUpdate()
		{
			if (_water == null)
			{
				return;
			}

			BigWaveShape wave = _water.BigWave;
			bool visible = wave.Exists && wave.Throw > 0f;
			_renderer.enabled = visible;

			if (!visible)
			{
				return;
			}

			if (_mesh == null)
			{
				BuildMesh();
			}

			UpdateSections(wave);

			// Only for culling: the shader places the vertices itself.
			Vector2 crest = wave.Origin + wave.Direction * wave.LipAlong + wave.SideDirection * wave.Crest;
			transform.SetPositionAndRotation(new Vector3(crest.x, _water.BaseHeight, crest.y), Quaternion.identity);

			if (waterRenderer != null)
			{
				waterRenderer.GetPropertyBlock(_properties);
				_renderer.SetPropertyBlock(_properties);
			}
		}

		private void BuildMesh()
		{
			float behind = _settings.LipBehind;
			float ahead = _settings.LipAhead;
			_alongSegments = Mathf.Max(1, Mathf.CeilToInt((behind + ahead) / alongSpacing));
			// The inner surface root to tip, then the outer one tip to root.
			int columns = (crossSegments + 1) * 2;
			int rows = _alongSegments + 1;
			_curlScale = new Vector2(_settings.LipReach / CurlReach, _settings.WaveHeight / CurlHeight);
			// The face's slope at the crest, carried into the curl's layout.
			float faceWidth = Mathf.Max(_settings.CrestOffset - _settings.FaceBase, 1e-3f);
			float crestSlope = _settings.WaveHeight / faceWidth *
			                   Mathf.Lerp(BigWaveShape.EvenCrestSlope, BigWaveShape.HollowCrestSlope, _settings.FaceHollow);
			_rootAngle = Mathf.Atan(crestSlope * _curlScale.x / _curlScale.y);
			_section = new Vector2[crossSegments + 1];
			_sectionTangents = new Vector2[crossSegments + 1];
			_sectionArc = new float[crossSegments + 1];

			_vertices = new Vector3[columns * rows];
			_normals = new Vector3[_vertices.Length];
			_uvs = new Vector4[_vertices.Length];
			_thicknessUVs = new Vector3[_vertices.Length];

			var triangles = new int[(columns - 1) * _alongSegments * 6];
			int t = 0;
			AddStrip(triangles, ref t, 0, columns);

			_mesh = new Mesh { name = "Big Wave Lip", hideFlags = HideFlags.DontSave };
			_mesh.MarkDynamic();
			_mesh.SetVertices(_vertices);
			_mesh.SetNormals(_normals);
			_mesh.SetUVs(0, _uvs);
			_mesh.SetUVs(1, _thicknessUVs);
			_mesh.SetTriangles(triangles, 0);
			// Placed by the shader anywhere around the rider: bounds wide enough for the whole stretch.
			float extent = behind + ahead + _settings.LipReach + _settings.CrestOffset;
			_mesh.bounds = new Bounds(Vector3.zero, new Vector3(extent * 2f, _settings.WaveHeight * 4f, extent * 2f));
			GetComponent<MeshFilter>().sharedMesh = _mesh;
		}

		/// <summary>Quads between each row of a strip of vertices and the next, rows of columns from first on.</summary>
		private void AddStrip(int[] triangles, ref int t, int first, int columns)
		{
			for (int j = 0; j < _alongSegments; j++)
			{
				for (int i = 0; i < columns - 1; i++)
				{
					int a = first + j * columns + i;
					int b = a + columns;
					triangles[t++] = a;
					triangles[t++] = b;
					triangles[t++] = a + 1;
					triangles[t++] = a + 1;
					triangles[t++] = b;
					triangles[t++] = b + 1;
				}
			}
		}

		/// <summary>Throws each cross-section of the shell out as far as the lip has pitched there.</summary>
		private void UpdateSections(BigWaveShape wave)
		{
			float behind = _settings.LipBehind;
			float ahead = _settings.LipAhead;
			int sides = crossSegments + 1;
			int columns = sides * 2;

			for (int j = 0; j <= _alongSegments; j++)
			{
				float along = -behind + (behind + ahead) * j / _alongSegments;
				// The shader shrinks the lip with the ridge's ends; it pitches less there too, so it never comes down.
				float fullPitch = Pitch(wave, along, wave.Ends(wave.LipAlong + along));
				float pitch = fullPitch * (1f - tipWobble * Mathf.PerlinNoise((wave.LipAlong + along) / tipWobbleLength,
					Time.time * tipWobbleSpeed));
				float v = (float)j / _alongSegments;
				float feather = 1f - fullPitch / Mathf.Max(wave.Throw * wave.Ends(wave.LipAlong + along), 1e-3f);
				TraceSection(pitch);

				for (int i = 0; i < sides; i++)
				{
					float u = (float)i / crossSegments;
					Vector2 point = _section[i];
					Vector2 tangent = _sectionTangents[i];
					// Toward the inside of the tube.
					Vector2 normal = new Vector2(-tangent.y, tangent.x);
					// As thick as the curl is big: a lip just pitching out is a thin sheet, or its outer surface would
					// stand out past the little curl in humps.
					float shell = Mathf.Lerp(thickness.x, thickness.y, u) * Mathf.Clamp01(0.15f + pitch);
					Vector2 outer = point - normal * shell;
					float arc = _sectionArc[i];

					int inner = j * columns + i;
					_vertices[inner] = new Vector3(point.x, point.y, along);
					_normals[inner] = new Vector3(normal.x, normal.y, 0f);
					_uvs[inner] = new Vector4(u, v, arc, 0f);
					_thicknessUVs[inner] = new Vector3(shell, feather, fullPitch);

					int back = j * columns + columns - 1 - i;
					_vertices[back] = new Vector3(outer.x, outer.y, along);
					_normals[back] = new Vector3(-normal.x, -normal.y, 0f);
					_uvs[back] = new Vector4(u, v, arc, 1f);
					_thicknessUVs[back] = new Vector3(shell, feather, fullPitch);
				}
			}

			_mesh.SetVertices(_vertices);
			_mesh.SetNormals(_normals);
			_mesh.SetUVs(0, _uvs);
			_mesh.SetUVs(1, _thicknessUVs);
		}

		/// <summary>How far the lip has pitched out at along from its place, 0..1.</summary>
		private float Pitch(BigWaveShape wave, float along, float ends)
		{
			// Until the rider is well into the barrel, the tube is formed past its mouth, so it's there ahead of her
			// instead of forming as she arrives.
			float full = Mathf.Max(wave.PitchAhead, wave.MouthLength * 1.5f - wave.LipAlong);
			float opening = Mathf.Max(_settings.LipAhead - full, 0.1f);
			return wave.Throw * ends *
			       // Rolled back in at the back end, where the wave has broken,
			       Mathf.SmoothStep(0f, 1f, (along + _settings.LipBehind) / backGrowth) *
			       // pitching out over the tube's mouth,
			       Mathf.SmoothStep(0f, 1f, (wave.LipAlong + along) / Mathf.Max(wave.MouthLength, 0.1f)) *
			       // and less and less ahead of the rider.
			       (1f - Mathf.SmoothStep(0f, 1f, (along - full) / opening));
		}

		/// <summary>
		/// Traces the inner surface of the lip pitched this far, 0..1, into the section arrays: from the root along the
		/// face's slope, its tangent turning over toward the tip (more near the root, less toward the tip), as long as
		/// the full curl times its pitch.
		/// </summary>
		private void TraceSection(float pitch)
		{
			float turn = (tipAngle * Mathf.Deg2Rad - _rootAngle) * Mathf.Pow(Mathf.Max(pitch, 0f), turnLead);
			int steps = crossSegments * CurlSubsteps;
			float step = curlLength * Mathf.Max(pitch, 0f) / steps;
			Vector2 layout = CurlRoot;
			Vector2 point = Vector2.Scale(layout, _curlScale);
			float arc = 0f;

			for (int k = 0; ; k++)
			{
				if (k % CurlSubsteps == 0)
				{
					int i = k / CurlSubsteps;
					float angle = TurnedAngle(turn, (float)k / steps);
					_section[i] = point;
					_sectionTangents[i] = Vector2.Scale(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), _curlScale)
						.normalized;
					_sectionArc[i] = arc;
				}

				if (k == steps)
				{
					break;
				}

				// Midpoint rule over the step.
				float midAngle = TurnedAngle(turn, (k + 0.5f) / steps);
				layout += new Vector2(Mathf.Cos(midAngle), Mathf.Sin(midAngle)) * step;
				Vector2 next = Vector2.Scale(layout, _curlScale);
				arc += Vector2.Distance(point, next);
				point = next;
			}
		}

		/// <summary>The tangent's angle in the curl's layout a share along it, turned by turn in all.</summary>
		private float TurnedAngle(float turn, float along)
		{
			return _rootAngle + turn * (along + curlBend * (along - along * along));
		}
	}
}
