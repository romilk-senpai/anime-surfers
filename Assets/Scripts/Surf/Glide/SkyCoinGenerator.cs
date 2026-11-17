using Game.Core;
using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Lays a line of coins along a glider flight at the glide height, from where the climb levels out to where the
	/// glider is gone. The line holds a lane for a while, then weaves smoothly over to a neighboring one, rising and
	/// falling gently all along. Generated ahead of the board as it flies, as far ahead as the trails on the water;
	/// collected and passed coins are removed by <see cref="PickupGenerator"/>. Ticked after it.
	/// </summary>
	public sealed class SkyCoinGenerator
	{
		private readonly PickupSettings _settings;
		private readonly BoardSettings _boardSettings;
		private readonly BoardState _board;
		private readonly GlideFlight _glide;
		private readonly PickupField _field;
		private readonly IGraphicsSettings _graphics;
		private readonly RandomStream _random;

		private bool _flying;
		private float _nextCoinDistance;
		private int _lane;
		/// <summary>Lane the line is weaving over from.</summary>
		private int _fromLane;
		/// <summary>Coins placed along the current weave; the line holds its lane once all of them are.</summary>
		private int _weaveCoins;
		/// <summary>Coins left in the lane before the next weave.</summary>
		private int _runCoins;

		public SkyCoinGenerator(PickupSettings settings, BoardSettings boardSettings, BoardState board,
			GlideFlight glide, PickupField field, IGraphicsSettings graphics, RunRandom random)
		{
			_settings = settings;
			_boardSettings = boardSettings;
			_board = board;
			_glide = glide;
			_field = field;
			_graphics = graphics;
			_random = random.Stream(RunRandom.SkyCoinStream);
		}

		public void Tick()
		{
			if (!_glide.IsActive)
			{
				_flying = false;
				return;
			}

			if (!_flying)
			{
				Begin();
			}

			float horizon = Mathf.Min(_board.Distance + _settings.SpawnAhead * _graphics.DistanceScale,
				_glide.EndDistance);

			while (_nextCoinDistance <= horizon)
			{
				AddCoin(_nextCoinDistance);
				_nextCoinDistance += _settings.CoinSpacing;
			}
		}

		private void Begin()
		{
			_flying = true;
			_nextCoinDistance = _glide.ClimbEndDistance;
			_lane = _board.Lane;
			_fromLane = _lane;
			_weaveCoins = _settings.SkyCoinWeave;
			_runCoins = RandomRun();
		}

		private void AddCoin(float distance)
		{
			int weaveLength = _settings.SkyCoinWeave;
			float lateral;

			if (_weaveCoins < weaveLength)
			{
				_weaveCoins++;
				float t = Mathf.SmoothStep(0f, 1f, _weaveCoins / (weaveLength + 1f));
				lateral = Mathf.Lerp(_boardSettings.LaneOffset(_fromLane), _boardSettings.LaneOffset(_lane), t);
			}
			else
			{
				lateral = _boardSettings.LaneOffset(_lane);

				if (--_runCoins <= 0)
				{
					StartWeave();
				}
			}

			float wave = Mathf.Sin(2f * Mathf.PI * distance / _settings.SkyCoinWaveLength);
			float height = _glide.GlideHeight + _settings.HoverHeight + _settings.SkyCoinWaveHeight * wave;
			_field.Add(PickupKind.Coin, distance, lateral, height, false);
		}

		/// <summary>Over to a neighboring lane, inward from the edge lanes.</summary>
		private void StartWeave()
		{
			int lastLane = _boardSettings.LaneCount - 1;
			_fromLane = _lane;
			_lane = _lane == 0 ? 1
				: _lane == lastLane ? lastLane - 1
				: _lane + (_random.Value < 0.5f ? -1 : 1);
			_lane = Mathf.Clamp(_lane, 0, lastLane);
			_weaveCoins = 0;
			_runCoins = RandomRun();
		}

		private int RandomRun()
		{
			return _random.Range(_settings.SkyCoinRun.x, _settings.SkyCoinRun.y + 1);
		}
	}
}
