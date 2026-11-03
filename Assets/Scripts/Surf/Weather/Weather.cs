using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Random storms along the run: picks where each storm starts and how long it lasts, exposes its strength for
	/// the views and scales the waves the board rides on. Ticked by <see cref="RunLoop"/> after the board systems.
	/// </summary>
	public sealed class Weather
	{
		private readonly WeatherSettings _settings;
		private readonly BoardState _board;
		private readonly IWaterSurface _water;
		private readonly RandomStream _random;

		private float _stormStart;
		private float _stormEnd;

		public Weather(WeatherSettings settings, BoardState board, IWaterSurface water, RunRandom random)
		{
			_settings = settings;
			_board = board;
			_water = water;
			_random = random.Stream(RunRandom.WeatherStream);

			// The first storm can come at any point, even right at the start.
			_stormStart = _random.Range(0f, settings.CalmDistance.y);
			_stormEnd = _stormStart + StormLength();
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
				_stormStart = _stormEnd + ramp + _random.Range(_settings.CalmDistance.x, _settings.CalmDistance.y);
				_stormEnd = _stormStart + StormLength();
			}

			float rise = Mathf.InverseLerp(_stormStart - ramp, _stormStart, distance);
			float fall = Mathf.InverseLerp(_stormEnd + ramp, _stormEnd, distance);
			Storm = Mathf.SmoothStep(0f, 1f, Mathf.Min(rise, fall));

			_water.WaveScale = Mathf.Lerp(_settings.CalmWaveScale, _settings.StormWaveScale, Storm);
		}

		private float StormLength()
		{
			return _random.Range(_settings.StormDistance.x, _settings.StormDistance.y);
		}
	}
}
