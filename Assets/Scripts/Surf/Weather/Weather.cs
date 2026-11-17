using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Random storms along the run: picks where each storm starts and how long it lasts, keeping clear of the big
	/// waves, exposes its strength for the views and scales the waves the board rides on. Ticked by <see cref="RunLoop"/> after the board systems.
	/// </summary>
	public sealed class Weather
	{
		private readonly WeatherSettings _settings;
		private readonly BoardState _board;
		private readonly IWaterSurface _water;
		private readonly WaveSchedule _waves;
		private readonly RandomStream _random;

		private float _stormStart;
		private float _stormEnd;

		public Weather(WeatherSettings settings, BoardState board, IWaterSurface water, WaveSchedule waves,
			RunRandom random)
		{
			_settings = settings;
			_board = board;
			_water = water;
			_waves = waves;
			_random = random.Stream(RunRandom.WeatherStream);

			// The first storm can come at any point, even right at the start.
			ScheduleStorm(_random.Range(0f, settings.CalmDistance.y));
		}

		/// <summary>Storm strength, 0 when calm and 1 at its peak.</summary>
		public float Storm { get; private set; }

		public void Tick()
		{
			float distance = _board.Distance;
			float ramp = _settings.RampDistance;

			// Past this storm: schedule the next one after a calm stretch.
			while (distance > _stormEnd + ramp)
			{
				ScheduleStorm(_stormEnd + ramp + _random.Range(_settings.CalmDistance.x, _settings.CalmDistance.y));
			}

			float rise = Mathf.InverseLerp(_stormStart - ramp, _stormStart, distance);
			float fall = Mathf.InverseLerp(_stormEnd + ramp, _stormEnd, distance);
			Storm = Mathf.SmoothStep(0f, 1f, Mathf.Min(rise, fall));

			_water.WaveScale = Mathf.Lerp(_settings.CalmWaveScale, _settings.StormWaveScale, Storm);
		}

		private void ScheduleStorm(float start)
		{
			float length = _random.Range(_settings.StormDistance.x, _settings.StormDistance.y);
			float ramp = _settings.RampDistance;

			// A storm that would build up or clear during a big wave comes after it instead.
			while (_waves.TryGetOverlap(start - ramp, start + length + ramp, out WaveEvent wave))
			{
				start = wave.End + ramp;
			}

			_stormStart = start;
			_stormEnd = start + length;
		}
	}
}
