using System;
using UnityEngine;

namespace Game.Surf
{
	public enum GlidePhase
	{
		None,
		/// <summary>Rising off the water to the glide height.</summary>
		Climb,
		Glide
	}

	/// <summary>
	/// The glider power-up's flight, for as long as its timer runs: the board climbs off the water to the glide
	/// height and glides along it with a gentle bob. When the timer runs out the glider is gone at once and the board
	/// falls back to the water under its own gravity. Lane changes are the only control and the board holds its speed,
	/// so where it comes down is known from the start. Ticked by <see cref="RunLoop"/> after the input, before the
	/// board systems.
	/// </summary>
	public sealed class GlideFlight
	{
		private readonly PickupSettings _settings;
		private readonly BoardSettings _boardSettings;
		private readonly BoardState _board;
		private readonly PowerUps _powerUps;
		private readonly IWaterSurface _water;

		private float _launchHeight;

		public GlideFlight(PickupSettings settings, BoardSettings boardSettings, BoardState board, PowerUps powerUps,
			IWaterSurface water)
		{
			_settings = settings;
			_boardSettings = boardSettings;
			_board = board;
			_powerUps = powerUps;
			_water = water;
		}

		public GlidePhase Phase { get; private set; }
		public bool IsActive => Phase != GlidePhase.None;
		/// <summary>Lane changes are the only control: jumps, slides and nitro are off.</summary>
		public bool LocksControls => IsActive;
		/// <summary>Seconds since take-off.</summary>
		public float Elapsed { get; private set; }
		/// <summary>Seconds until the glider is gone.</summary>
		public float Remaining => _powerUps.Remaining(PickupKind.Glider);
		/// <summary>Where the climb levels out, at the current speed.</summary>
		public float ClimbEndDistance =>
			_board.Distance + Mathf.Max(_settings.GlideClimbTime - Elapsed, 0f) * _board.Speed;
		/// <summary>Where the glider is gone and the fall starts, at the current speed.</summary>
		public float EndDistance => _board.Distance + Remaining * _board.Speed;
		/// <summary>Where the board touches down after the fall, at the current speed.</summary>
		public float LandingDistance => EndDistance + FallTime * _board.Speed;
		/// <summary>Board height while gliding, without the bob.</summary>
		public float GlideHeight => _water.BaseHeight + _settings.GlideAltitude;

		/// <summary>Seconds to fall from the glide height to riding on the still water.</summary>
		private float FallTime => Mathf.Sqrt(2f * Mathf.Max(_settings.GlideAltitude - _boardSettings.RideHeight, 0f) /
		                                     _boardSettings.Gravity);

		public event Action Started;
		/// <summary>Fired when the glider is gone and the fall starts.</summary>
		public event Action Ended;

		public void Tick(float dt)
		{
			bool timerRunning = _powerUps.IsActive(PickupKind.Glider);

			if (_board.IsCrashed)
			{
				Phase = GlidePhase.None;
				_board.IsGliding = false;
				return;
			}

			if (!IsActive)
			{
				if (!timerRunning)
				{
					return;
				}

				Begin();
			}
			else if (!timerRunning)
			{
				End();
				return;
			}
			else
			{
				Elapsed += dt;
			}

			Phase = Elapsed < _settings.GlideClimbTime ? GlidePhase.Climb : GlidePhase.Glide;
		}

		/// <summary>Board height along the flight.</summary>
		public float Height()
		{
			float rise = Mathf.SmoothStep(0f, 1f, Elapsed / _settings.GlideClimbTime);
			float bob = _settings.GlideBobHeight * Mathf.Sin(2f * Mathf.PI * Elapsed / _settings.GlideBobPeriod);
			return Mathf.Lerp(_launchHeight, GlideHeight + bob * rise, rise);
		}

		/// <summary>
		/// Whether from..to overlaps where obstacles stay away during a flight: under the fall and
		/// <see cref="PickupSettings.GlideClearAfter"/> past the touchdown, which ends at <paramref name="clearEnd"/>.
		/// </summary>
		public bool TryGetLandingClear(float from, float to, out float clearEnd)
		{
			clearEnd = LandingDistance + _settings.GlideClearAfter;
			return IsActive && EndDistance <= to && from < clearEnd;
		}

		private void Begin()
		{
			Elapsed = 0f;
			_launchHeight = _board.Height;
			_board.IsGliding = true;
			_board.SlideUntil = float.NegativeInfinity;
			_board.JumpRequestTime = float.NegativeInfinity;
			Started?.Invoke();
		}

		private void End()
		{
			Phase = GlidePhase.None;
			_board.IsGliding = false;
			Ended?.Invoke();
		}
	}
}
