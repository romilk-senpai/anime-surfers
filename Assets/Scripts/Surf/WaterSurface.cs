using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Surf
{
    /// <summary>
    /// Owns the Gerstner wave settings, pushes them to the water shader (WaterWaves.hlsl) and lets gameplay
    /// sample the exact same surface the GPU renders.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WaterSurface : MonoBehaviour
    {
        [Serializable]
        public struct Wave
        {
            [Range(0f, 360f)] public float direction;
            [Tooltip("Gerstner steepness; the sum over all waves should stay below 1 or crests start to loop.")]
            [Range(0f, 1f)] public float steepness;
            [Tooltip("Keep this at least ~6 mesh cells long, otherwise the rendered mesh can't follow the wave " +
                     "and floating objects visibly sink into troughs. Finer detail belongs in the normal map.")]
            [Min(0.5f)] public float wavelength;
            [Min(0f)] public float speed;
        }

        public const int MaxWaves = 8;
        private const float Gravity = 9.81f;
        // Fixed-point iterations to find which grid point gets displaced onto a queried XZ position.
        private const int SourceIterations = 4;

        private static readonly int WavesId = Shader.PropertyToID("_WaterGerstnerWaves");
        private static readonly int TimeId = Shader.PropertyToID("_WaterWaveTime");
        private static readonly int AmplitudeSumId = Shader.PropertyToID("_WaterAmplitudeSum");
        private static readonly int BaseHeightId = Shader.PropertyToID("_WaterBaseHeight");

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
        [SerializeField] private float meshSize = 160f;
        [SerializeField, Range(16, 250)] private int meshResolution = 250;
        [SerializeField] private Transform followTarget;

        private readonly Vector4[] _packedWaves = new Vector4[MaxWaves];
        private float _amplitudeSum;
        private float _time;
        private Mesh _mesh;
        private bool _meshDirty;

        public float WaveTime => _time;
        public float BaseHeight => transform.position.y;

        private void OnEnable()
        {
            BuildMesh();
            PackWaves();
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
            if (_meshDirty)
            {
                BuildMesh();
            }

            SyncClock();

            FollowTarget();
            PushGlobals();
        }

        /// <summary>World-space height of the water surface at the given XZ position.</summary>
        public float GetHeight(Vector3 position)
        {
            SyncClock();
            Vector2 source = FindSource(position.x, position.z);
            EvaluateWaves(source.x, source.y, out Vector3 offset, out _, out _);
            return BaseHeight + offset.y;
        }

        /// <summary>World-space surface normal at the given XZ position.</summary>
        public Vector3 GetNormal(Vector3 position)
        {
            SyncClock();
            Vector2 source = FindSource(position.x, position.z);
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
        private Vector2 FindSource(float x, float z)
        {
            float sourceX = x;
            float sourceZ = z;

            for (int i = 0; i < SourceIterations; i++)
            {
                EvaluateWaves(sourceX, sourceZ, out Vector3 offset, out _, out _);
                sourceX = x - offset.x;
                sourceZ = z - offset.z;
            }

            return new Vector2(sourceX, sourceZ);
        }

        // Mirrors EvaluateWaterWaves in WaterWaves.hlsl.
        private void EvaluateWaves(float x, float z, out Vector3 offset, out Vector3 tangent, out Vector3 binormal)
        {
            offset = Vector3.zero;
            tangent = new Vector3(1f, 0f, 0f);
            binormal = new Vector3(0f, 0f, 1f);

            for (int i = 0; i < MaxWaves; i++)
            {
                Vector4 wave = _packedWaves[i];
                float k = Mathf.Max(Mathf.Sqrt(wave.x * wave.x + wave.y * wave.y), 1e-4f);
                float dx = wave.x / k;
                float dz = wave.y / k;
                float steepness = wave.z;
                float a = steepness / k;
                float f = wave.x * x + wave.y * z - wave.w * _time;
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
        }

        private void PackWaves()
        {
            _amplitudeSum = 0f;

            for (int i = 0; i < MaxWaves; i++)
            {
                if (waves == null || i >= waves.Length)
                {
                    _packedWaves[i] = Vector4.zero;
                    continue;
                }

                Wave wave = waves[i];
                float k = 2f * Mathf.PI / Mathf.Max(wave.wavelength, 0.5f);
                float angle = wave.direction * Mathf.Deg2Rad;
                // Deep-water dispersion keeps long waves fast and short ones slow.
                float omega = Mathf.Sqrt(Gravity * k) * wave.speed;

                _packedWaves[i] = new Vector4(Mathf.Sin(angle) * k, Mathf.Cos(angle) * k, wave.steepness, omega);
                _amplitudeSum += wave.steepness / k;
            }
        }

        private void PushGlobals()
        {
            Shader.SetGlobalVectorArray(WavesId, _packedWaves);
            Shader.SetGlobalFloat(TimeId, _time);
            Shader.SetGlobalFloat(AmplitudeSumId, _amplitudeSum);
            Shader.SetGlobalFloat(BaseHeightId, BaseHeight);
        }

        private void FollowTarget()
        {
            if (followTarget == null)
            {
                return;
            }

            // Snap to the grid so vertices stay on the same world lattice and the waves don't swim.
            float cell = meshSize / meshResolution;
            Vector3 target = followTarget.position;
            transform.position = new Vector3(Mathf.Round(target.x / cell) * cell, transform.position.y,
                Mathf.Round(target.z / cell) * cell);
        }

        private void BuildMesh()
        {
            _meshDirty = false;

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "Water Grid", hideFlags = HideFlags.DontSave };
            }

            int side = meshResolution + 1;
            var vertices = new Vector3[side * side];
            var triangles = new int[meshResolution * meshResolution * 6];
            float cell = meshSize / meshResolution;
            float half = meshSize * 0.5f;

            for (int z = 0; z < side; z++)
            {
                for (int x = 0; x < side; x++)
                {
                    vertices[z * side + x] = new Vector3(x * cell - half, 0f, z * cell - half);
                }
            }

            int t = 0;
            for (int z = 0; z < meshResolution; z++)
            {
                for (int x = 0; x < meshResolution; x++)
                {
                    int i = z * side + x;
                    triangles[t++] = i;
                    triangles[t++] = i + side;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + side;
                    triangles[t++] = i + side + 1;
                }
            }

            _mesh.Clear();
            _mesh.indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _mesh.RecalculateNormals();
            // Waves are displaced on the GPU, so grow the bounds to avoid culling crests.
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(meshSize + 10f, 20f, meshSize + 10f));

            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }
    }
}
