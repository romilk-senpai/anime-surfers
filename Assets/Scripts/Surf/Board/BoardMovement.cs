using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Runner-style motion along the passage: forward speed (boosted by nitro) and lane changes. Riding into a big
	/// wave the board eases to the barrel speed and the middle lane, holds them through the barrel and eases back
	/// to its own speed on the way out. On the glider it holds its speed.
	/// </summary>
	public sealed class BoardMovement
	{
		private readonly BoardSettings _settings;
		private readonly BoardState _state;
		private readonly SurfPath _path;
		private readonly BoardInput _input;
		private readonly Nitro _nitro;
		private readonly BarrelRide _barrel;
		private readonly BigWaveSettings _waveSettings;
		private readonly GlideFlight _glide;

		private bool _running;
		/// <summary>Forward speed before the nitro boost.</summary>
		private float _baseSpeed;

		public BoardMovement(BoardSettings settings, BoardState state, SurfPath path, BoardInput input,
			Nitro nitro, BarrelRide barrel, BigWaveSettings waveSettings, GlideFlight glide)
		{
			_settings = settings;
			_state = state;
			_path = path;
			_input = input;
			_nitro = nitro;
			_barrel = barrel;
			_waveSettings = waveSettings;
			_glide = glide;
		}

		public void Initialize()
		{
			_state.Lane = _settings.LaneCount / 2;
			_state.Lateral = _settings.LaneOffset(_state.Lane);
			SamplePath();
		}

		public void StartRunning()
		{
			_running = true;
			_baseSpeed = _settings.StartSpeed;
			_state.Speed = _baseSpeed;
		}

		public void Tick(float dt)
		{
			if (_state.IsCrashed)
			{
				Coast(dt);
				return;
			}

			_state.PreviousDistance = _state.Distance;

			if (_running)
			{
				ReadLaneInput();

				if (_barrel.LocksControls)
				{
					_state.Lane = _settings.LaneCount / 2;
				}

				// Held on the glider, so it lands where the flight started out heading for.
				if (!_glide.IsActive)
				{
					_baseSpeed = Mathf.MoveTowards(_baseSpeed, _settings.MaxSpeed, _settings.Acceleration * dt);
				}

				_state.Speed = WaveSpeed(_baseSpeed * _nitro.SpeedMultiplier);
				_state.Distance += _state.Speed * dt;
			}

			_state.PreviousLateral = _state.Lateral;

			float lateralVelocity = _state.LateralVelocity;
			_state.Lateral = Mathf.SmoothDamp(_state.Lateral, _settings.LaneOffset(_state.Lane), ref lateralVelocity,
				_settings.LaneChangeTime, Mathf.Infinity, dt);
			_state.LateralVelocity = lateralVelocity;

			SamplePath();
			_path.TrimBehind(_state.Distance);
		}

		/// <summary>The riderless board drifts on with the speed the crash left it, free of the lanes.</summary>
		private void Coast(float dt)
		{
			float drag = Mathf.Exp(-_settings.CrashDrag * dt);
			_state.Speed *= drag;
			_state.PreviousDistance = _state.Distance;
			_state.Distance += _state.Speed * dt;

			_state.PreviousLateral = _state.Lateral;
			_state.LateralVelocity *= drag;
			float edge = _settings.PassageHalfWidth - _settings.BoardWidth * 0.5f;
			_state.Lateral += _state.LateralVelocity * dt;
			if (Mathf.Abs(_state.Lateral) > edge)
			{
				_state.Lateral = Mathf.Clamp(_state.Lateral, -edge, edge);
				_state.LateralVelocity = 0f;
			}

			SamplePath();
		}

		private float WaveSpeed(float speed)
		{
			WaveEvent wave = _barrel.Wave;
			float barrelSpeed = _waveSettings.BarrelSpeed;

			return _barrel.Phase switch
			{
				WavePhase.DropIn => Mathf.Lerp(speed, barrelSpeed, Ease(wave.DropInStart, wave.BarrelStart)),
				WavePhase.Barrel => barrelSpeed,
				WavePhase.Exit => Mathf.Lerp(barrelSpeed, speed, Ease(wave.BarrelEnd, wave.End)),
				_ => speed
			};
		}

		private float Ease(float from, float to)
		{
			return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, _state.Distance));
		}

		private void ReadLaneInput()
		{
			int direction = _input.Current.LaneChange;

			if (direction != 0)
			{
				_state.Lane = Mathf.Clamp(_state.Lane + direction, 0, _settings.LaneCount - 1);
			}
		}

		private void SamplePath()
		{
			_path.Sample(_state.Distance, out Vector3 position, out Vector3 forward);
			_state.PathPosition = position;
			_state.PathForward = forward;
		}
	}
}
