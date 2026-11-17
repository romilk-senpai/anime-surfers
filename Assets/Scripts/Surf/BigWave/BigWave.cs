using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Shapes the big wave the board is in and hands it to the water every frame. Its ridge runs along the barrel's
	/// straight, on the side the passage heads toward on the way in, so it stands ahead on the way in and alongside
	/// through the barrel. It rises over the approach, the lip throws out from the barrel's start over the drop-in so the
	/// board rides in through the tube's mouth (the lip's curtain lands on the side the board comes in from, so the lip
	/// never reaches back past the mouth), and both flatten over the exit. Ticked by <see cref="RunLoop"/> once the board has moved, before it settles on the water.
	/// </summary>
	public sealed class BigWave
	{
		private readonly BigWaveSettings _settings;
		private readonly WaveSchedule _waves;
		private readonly SurfPath _path;
		private readonly BoardState _board;
		private readonly IWaterSurface _water;

		private WaveEvent _wave;
		private Vector2 _origin;
		private Vector2 _direction;

		public BigWave(BigWaveSettings settings, WaveSchedule waves, SurfPath path, BoardState board,
			IWaterSurface water)
		{
			_settings = settings;
			_waves = waves;
			_path = path;
			_board = board;
			_water = water;
		}

		public void Tick()
		{
			float distance = _board.Distance;

			if (!_waves.TryGetOverlap(distance, distance, out WaveEvent wave))
			{
				if (_wave != null)
				{
					_wave = null;
					_water.BigWave = default;
				}

				return;
			}

			if (wave != _wave)
			{
				_wave = wave;
				_path.Sample(wave.BarrelStart, out Vector3 position, out Vector3 forward);
				_origin = new Vector2(position.x, position.z);
				_direction = new Vector2(forward.x, forward.z).normalized;
			}

			Vector3 board = _board.PlanarPosition;
			float riderAlong = Vector2.Dot(new Vector2(board.x, board.z) - _origin, _direction);

			_water.BigWave = new BigWaveShape
			{
				Origin = _origin,
				Direction = _direction,
				// The passage heads toward it on the way in, then turns to run along it.
				Side = -wave.Turn,
				Height = _settings.WaveHeight * Rise(wave, distance),
				AlongStart = -_settings.RidgeBefore,
				AlongEnd = wave.BarrelEnd - wave.BarrelStart,
				Taper = _settings.RidgeTaper,
				FaceBase = _settings.FaceBase,
				Crest = _settings.CrestOffset,
				BackWidth = _settings.BackWidth,
				FaceHollow = _settings.FaceHollow,
				LipAlong = Mathf.Max(riderAlong, 0f),
				Throw = Throw(wave, distance, _settings.ThrowDistance),
				LipReach = _settings.LipReach,
				LipBehind = _settings.LipBehind,
				LipAhead = _settings.LipAhead,
				MouthLength = _settings.MouthLength,
				PitchAhead = _settings.PitchAhead
			};
		}

		/// <summary>Up from nothing over the approach, down again over the exit.</summary>
		private static float Rise(WaveEvent wave, float distance)
		{
			if (distance < wave.BarrelEnd)
			{
				return Ease(wave.Start, wave.DropInStart, distance);
			}

			return 1f - Ease(wave.BarrelEnd, wave.End, distance);
		}

		/// <summary>Out as the board lines up in the barrel, collapsing over the first half of the exit.</summary>
		private static float Throw(WaveEvent wave, float distance, float throwDistance)
		{
			if (distance < wave.BarrelEnd)
			{
				return Ease(wave.BarrelStart - throwDistance, wave.BarrelStart, distance);
			}

			return 1f - Ease(wave.BarrelEnd, (wave.BarrelEnd + wave.End) * 0.5f, distance);
		}

		private static float Ease(float from, float to, float distance)
		{
			return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, distance));
		}
	}
}
