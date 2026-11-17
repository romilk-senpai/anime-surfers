using System;
using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// The hold-to-climb minigame inside a big wave's barrel, like reeling in a fish: holding pushes the catch zone up
	/// the bar and letting go lets it fall, while the target (the pocket of the wave) wanders and darts about. The hold
	/// meter fills while the target is in the catch zone and drains while it's out; the ride fails when the meter runs
	/// empty, wiping the rider out (the shield doesn't help), and succeeds when the board rides out of the barrel.
	/// Bar positions run 0 (bottom) to 1 (top). Also tracks where the board is in the wave, since riding into and
	/// through the barrel the hold is the only control. Ticked by <see cref="RunLoop"/> after the input, before the
	/// board systems.
	/// </summary>
	public sealed class BarrelRide
	{
		private readonly BigWaveSettings _settings;
		private readonly WaveSchedule _waves;
		private readonly BoardState _board;
		private readonly BoardInput _input;
		private readonly RandomStream _random;

		/// <summary>The wave last ridden, so a failed ride doesn't start again in the same barrel.</summary>
		private WaveEvent _wave;
		private int _ridden;
		private float _difficulty;
		private float _elapsed;
		private float _catchVelocity;
		private float _targetGoal;
		private float _targetVelocity;
		private float _targetSmoothTime;
		private float _nextTargetMove;

		public BarrelRide(BigWaveSettings settings, WaveSchedule waves, BoardState board, BoardInput input,
			RunRandom random)
		{
			_settings = settings;
			_waves = waves;
			_board = board;
			_input = input;
			_random = random.Stream(RunRandom.BarrelStream);
		}

		/// <summary>The wave the board is in, or null.</summary>
		public WaveEvent Wave { get; private set; }
		public WavePhase Phase { get; private set; }
		/// <summary>Riding into and through the barrel: lanes, jumps, slides and nitro are off.</summary>
		public bool LocksControls => Phase == WavePhase.DropIn || Phase == WavePhase.Barrel;

		public bool IsActive { get; private set; }
		/// <summary>Center of the catch zone; it spans <see cref="CatchSize"/> around it.</summary>
		public float CatchPosition { get; private set; }
		public float CatchSize => _settings.CatchSize;
		public float TargetPosition { get; private set; }
		public bool IsCatching => Mathf.Abs(TargetPosition - CatchPosition) <= CatchSize * 0.5f;
		/// <summary>0..1; the ride fails when it runs empty.</summary>
		public float Meter { get; private set; }
		/// <summary>Seconds until the board rides out of the barrel, at its current speed.</summary>
		public float TimeLeft { get; private set; }
		/// <summary>Still in the opening stretch where the target holds still and the meter doesn't drain.</summary>
		public bool IsInGrace => _elapsed < _settings.GraceTime;

		public event Action Started;
		/// <summary>The argument is true if the board rode out of the barrel, false if the meter ran empty.</summary>
		public event Action<bool> Ended;

		public void Tick(float dt)
		{
			if (_board.IsCrashed)
			{
				IsActive = false;
				Wave = null;
				Phase = WavePhase.None;
				return;
			}

			float distance = _board.Distance;
			Wave = _waves.TryGetOverlap(distance, distance, out WaveEvent wave) ? wave : null;
			Phase = Wave?.PhaseAt(distance) ?? WavePhase.None;
			bool inBarrel = Phase == WavePhase.Barrel;

			if (!IsActive)
			{
				if (inBarrel && Wave != _wave)
				{
					Begin(Wave);
				}

				return;
			}

			if (!inBarrel)
			{
				End(true);
				return;
			}

			_elapsed += dt;
			TimeLeft = (_wave.BarrelEnd - distance) / Mathf.Max(_board.Speed, 0.1f);
			MoveCatchZone(dt);
			MoveTarget(dt);

			if (IsCatching)
			{
				Meter = Mathf.Min(Meter + _settings.MeterFill * dt, 1f);
			}
			else if (!IsInGrace)
			{
				Meter = Mathf.Max(Meter - _settings.MeterDrain * _difficulty * dt, 0f);

				if (Meter <= 0f)
				{
					End(false);
					WipeOut();
				}
			}
		}

		private void Begin(WaveEvent wave)
		{
			_wave = wave;
			_difficulty = 1f + _settings.DifficultyPerWave * _ridden;
			_elapsed = 0f;
			IsActive = true;
			Meter = _settings.MeterStart;
			CatchPosition = 0.5f;
			_catchVelocity = 0f;
			TargetPosition = 0.5f;
			_targetGoal = 0.5f;
			_targetVelocity = 0f;
			_targetSmoothTime = _settings.TargetSmoothTime.y;
			_nextTargetMove = _settings.GraceTime;
			TimeLeft = (wave.BarrelEnd - _board.Distance) / Mathf.Max(_board.Speed, 0.1f);
			Started?.Invoke();
		}

		private void End(bool success)
		{
			IsActive = false;
			_ridden++;
			Ended?.Invoke(success);
		}

		private void WipeOut()
		{
			_board.IsCrashed = true;
			_board.CrashKind = CrashKind.Wipeout;
			_board.SlideUntil = float.NegativeInfinity;
			_board.NotifyCrashed();
		}

		private void MoveCatchZone(float dt)
		{
			float acceleration = _input.Current.Hold ? _settings.HoldAcceleration : -_settings.FallAcceleration;
			_catchVelocity += acceleration * dt;
			CatchPosition += _catchVelocity * dt;

			float half = CatchSize * 0.5f;

			if (CatchPosition < half)
			{
				CatchPosition = half;
				_catchVelocity = Mathf.Max(-_catchVelocity * _settings.Bounce, 0f);
			}
			else if (CatchPosition > 1f - half)
			{
				CatchPosition = 1f - half;
				_catchVelocity = Mathf.Min(-_catchVelocity * _settings.Bounce, 0f);
			}
		}

		private void MoveTarget(float dt)
		{
			if (_elapsed >= _nextTargetMove)
			{
				Vector2 interval = _settings.TargetInterval;
				_nextTargetMove = _elapsed + _random.Range(interval.x, interval.y) / _difficulty;
				_targetGoal = _random.Range(0.05f, 0.95f);

				Vector2 smoothTime = _settings.TargetSmoothTime;
				_targetSmoothTime = _random.Value < _settings.DartChance
					? _settings.DartSmoothTime
					: _random.Range(smoothTime.x, smoothTime.y) / _difficulty;
			}

			TargetPosition = Mathf.SmoothDamp(TargetPosition, _targetGoal, ref _targetVelocity, _targetSmoothTime,
				Mathf.Infinity, dt);
		}
	}
}
