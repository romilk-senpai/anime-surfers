using System.Collections.Generic;
using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Endless procedural passage centerline on the XZ plane: straights mixed with gentle curves. The heading stays
	/// within a set angle of the starting direction, so the path never circles back over itself. At each big wave
	/// the curves give way to a straight run-up, a turn into the wave, a straight through the barrel and a turn back
	/// out. Points are generated on demand ahead and trimmed behind.
	/// </summary>
	public sealed class SurfPath
	{
		private struct PathPoint
		{
			public Vector3 Position;
			public float Heading;
		}

		private readonly PathSettings _settings;
		private readonly BigWaveSettings _waveSettings;
		private readonly WaveSchedule _waves;
		private readonly List<PathPoint> _points = new List<PathPoint>();
		private readonly float _noiseOffset;
		private readonly float _baseHeading;
		private int _firstIndex;

		public SurfPath(PathSettings settings, BigWaveSettings waveSettings, WaveSchedule waves, Pose origin,
			RunRandom random)
		{
			_settings = settings;
			_waveSettings = waveSettings;
			_waves = waves;
			_noiseOffset = random.Stream(RunRandom.PathStream).Range(0, 10000) * 0.37f;
			_baseHeading = origin.rotation.eulerAngles.y * Mathf.Deg2Rad;
			_points.Add(new PathPoint
			{
				Position = origin.position,
				Heading = _baseHeading
			});
		}

		public float PointSpacing => _settings.PointSpacing;
		/// <summary>The centerline has been generated up to this distance.</summary>
		public float GeneratedUntil => (_firstIndex + _points.Count - 1) * _settings.PointSpacing;

		/// <summary>Position and forward direction of the centerline at the given distance.</summary>
		public void Sample(float distance, out Vector3 position, out Vector3 forward)
		{
			float spacing = _settings.PointSpacing;
			EnsureLength(distance + spacing * 2f);

			float local = Mathf.Max(distance / spacing - _firstIndex, 0f);
			int i = Mathf.Min(Mathf.FloorToInt(local), _points.Count - 2);
			float t = local - i;

			PathPoint a = _points[i];
			PathPoint b = _points[i + 1];
			position = Vector3.Lerp(a.Position, b.Position, t);

			float heading = Mathf.Lerp(a.Heading, b.Heading, t);
			forward = new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading));
		}

		/// <summary>Drops points that are well behind the given distance.</summary>
		public void TrimBehind(float distance)
		{
			int removable = Mathf.FloorToInt((distance - _settings.KeepBehind) / _settings.PointSpacing) - _firstIndex;

			// Remove in batches to avoid shifting the list every frame.
			if (removable > 128 && removable < _points.Count - 2)
			{
				_points.RemoveRange(0, removable);
				_firstIndex += removable;
			}
		}

		private void EnsureLength(float distance)
		{
			float spacing = _settings.PointSpacing;

			while ((_firstIndex + _points.Count - 1) * spacing < distance)
			{
				PathPoint last = _points[^1];
				float nextDistance = (_firstIndex + _points.Count) * spacing;
				float heading = last.Heading + Curvature(nextDistance, last.Heading - _baseHeading) * spacing;

				_points.Add(new PathPoint
				{
					Position = last.Position + new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading)) * spacing,
					Heading = heading
				});
			}
		}

		/// <param name="deviation">Current heading relative to the starting direction, in radians.</param>
		private float Curvature(float distance, float deviation)
		{
			float curvature = NoiseCurvature(distance, deviation);
			float blend = _waveSettings.PathBlend;

			if (!_waves.TryGetOverlap(distance - blend, distance + blend, out WaveEvent wave))
			{
				return curvature;
			}

			// The path's own curves fade out toward the wave and back in after it.
			float outside = Mathf.Max(wave.Start - distance, distance - wave.End);
			return outside > 0f
				? curvature * Mathf.SmoothStep(0f, 1f, outside / blend)
				: WaveCurvature(wave, distance, deviation);
		}

		private float WaveCurvature(WaveEvent wave, float distance, float deviation)
		{
			float limit = _settings.MaxHeadingDeviation * Mathf.Deg2Rad;
			float angle = Mathf.Min(_waveSettings.TurnAngle * Mathf.Deg2Rad, limit);

			// Picked on the wave's first point. Turning back toward the starting direction always stays inside the
			// heading limit; with room to spare either way, the wave picks the side.
			if (wave.Turn == 0)
			{
				wave.Turn = Mathf.Abs(deviation) + angle <= limit ? wave.PreferredTurn : deviation > 0f ? -1 : 1;
			}

			return wave.PhaseAt(distance) switch
			{
				WavePhase.DropIn => wave.Turn * TurnCurvature(distance, wave.DropInStart, wave.BarrelStart, angle),
				WavePhase.Exit => -wave.Turn * TurnCurvature(distance, wave.BarrelEnd, wave.End, angle),
				_ => 0f
			};
		}

		/// <summary>Curvature along a turn by the angle between the two distances, easing in and out.</summary>
		private static float TurnCurvature(float distance, float from, float to, float angle)
		{
			float length = to - from;
			return angle * Mathf.PI / (2f * length) * Mathf.Sin(Mathf.PI * (distance - from) / length);
		}

		private float NoiseCurvature(float distance, float deviation)
		{
			if (distance < _settings.StraightStartLength)
			{
				return 0f;
			}

			float noise = Mathf.PerlinNoise(_noiseOffset + distance * _settings.TurnFrequency, 0.5f) * 2f - 1f;
			noise = Mathf.Clamp(noise * 1.6f, -1f, 1f);

			// Dead zone around zero gives proper straights between turns.
			float straightBias = _settings.StraightBias;
			float turn = Mathf.Sign(noise) * Mathf.Max(0f, Mathf.Abs(noise) - straightBias) / (1f - straightBias);

			// Turning further away from the starting direction eases off over the last half of the allowed angle and
			// stops at the limit; turning back is never held back.
			if (turn * deviation > 0f)
			{
				float limit = _settings.MaxHeadingDeviation * Mathf.Deg2Rad;
				turn *= Mathf.Clamp01((limit - Mathf.Abs(deviation)) / (limit * 0.5f));
			}

			return turn / _settings.MinTurnRadius;
		}
	}
}
