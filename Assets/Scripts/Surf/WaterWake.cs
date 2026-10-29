using System;
using UnityEngine;

namespace Game.Surf
{
    /// <summary>
    /// Collects board trail points and splash events and pushes them to the water shader,
    /// which draws the foam wake and ripple rings directly on the surface.
    /// </summary>
    public class WaterWake : MonoBehaviour
    {
        public const int MaxTrailPoints = 48;
        public const int MaxSplashes = 8;

        private static readonly int TrailPointsId = Shader.PropertyToID("_WakePoints");
        private static readonly int TrailBoundsId = Shader.PropertyToID("_WakeBounds");
        private static readonly int TrailParamsId = Shader.PropertyToID("_WakeParams");
        private static readonly int SplashPointsId = Shader.PropertyToID("_SplashPoints");
        private static readonly int RippleParamsId = Shader.PropertyToID("_RippleParams");

        [SerializeField] private WaterSurface water;

        [Header("Wake")]
        [SerializeField] private float wakeWidth = 0.3f;
        [Tooltip("How fast the wake widens, in meters per second.")]
        [SerializeField] private float wakeSpread = 0.7f;
        [SerializeField] private float wakeLifetime = 2.5f;
        [SerializeField] private float wakeEdgeWidth = 0.07f;

        [Header("Ripples")]
        [SerializeField] private float rippleSpeed = 3f;
        [SerializeField] private float rippleFrequency = 7f;
        [SerializeField] private float rippleLifetime = 1.6f;
        [SerializeField] private float rippleStrength = 0.6f;

        // xy = world xz, z = spawn time (water clock), w = strength (0 breaks the trail)
        private readonly Vector4[] _trail = new Vector4[MaxTrailPoints];
        private readonly Vector4[] _splashes = new Vector4[MaxSplashes];
        private int _nextSplash;

        private void OnEnable()
        {
            Clear();
        }

        private void OnDisable()
        {
            // Shader globals outlive play mode, so don't leave a frozen wake behind.
            Clear();
        }

        // Spaced so the buffer outlives the wake: the tail fades out fully before its points are dropped.
        private float PointInterval => wakeLifetime / (MaxTrailPoints - 2);

        /// <summary>
        /// Call every frame while on the water. The newest point follows the board, so the wake grows smoothly;
        /// it's left behind as a fixed point once it's a full interval old.
        /// </summary>
        public void UpdateTrail(Vector3 position)
        {
            float now = water.WaveTime;
            bool headIsLive = _trail[0].w > 0f && _trail[1].w > 0f && now - _trail[1].z < PointInterval;
            if (!headIsLive)
            {
                Shift();
            }
            _trail[0] = new Vector4(position.x, position.z, now, 1f);
        }

        public void BreakTrail()
        {
            if (_trail[0].w > 0f)
            {
                Shift();
                _trail[0] = new Vector4(_trail[1].x, _trail[1].y, water.WaveTime, 0f);
            }
        }

        private void Shift()
        {
            Array.Copy(_trail, 0, _trail, 1, MaxTrailPoints - 1);
        }

        public void AddSplash(Vector3 position, float strength)
        {
            _splashes[_nextSplash] = new Vector4(position.x, position.z, water.WaveTime, strength);
            _nextSplash = (_nextSplash + 1) % MaxSplashes;
        }

        private void LateUpdate()
        {
            float now = water.WaveTime;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < MaxTrailPoints; i++)
            {
                if (_trail[i].w <= 0f)
                {
                    continue;
                }

                if (now - _trail[i].z > wakeLifetime)
                {
                    _trail[i].w = 0f;
                    continue;
                }

                min = Vector2.Min(min, new Vector2(_trail[i].x, _trail[i].y));
                max = Vector2.Max(max, new Vector2(_trail[i].x, _trail[i].y));
            }

            // The shader skips every pixel outside these bounds; empty bounds skip everything.
            float margin = wakeWidth + wakeSpread * wakeLifetime + wakeEdgeWidth + 0.5f;
            Vector4 bounds = min.x <= max.x
                ? new Vector4(min.x - margin, min.y - margin, max.x + margin, max.y + margin)
                : new Vector4(1f, 1f, -1f, -1f);

            Push(bounds);
        }

        private void Clear()
        {
            Array.Clear(_trail, 0, _trail.Length);
            Array.Clear(_splashes, 0, _splashes.Length);
            Push(new Vector4(1f, 1f, -1f, -1f));
        }

        private void Push(Vector4 bounds)
        {
            Shader.SetGlobalVectorArray(TrailPointsId, _trail);
            Shader.SetGlobalVectorArray(SplashPointsId, _splashes);
            Shader.SetGlobalVector(TrailBoundsId, bounds);
            Shader.SetGlobalVector(TrailParamsId, new Vector4(wakeWidth, wakeSpread, wakeLifetime, wakeEdgeWidth));
            Shader.SetGlobalVector(RippleParamsId, new Vector4(rippleSpeed, rippleFrequency, rippleLifetime, rippleStrength));
        }
    }
}
