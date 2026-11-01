using Game.Core;
using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Nitro tank: holding boost burns it to speed the board up, which also scores faster since score is per meter.
	/// Refilled by the nitro pickup. Gameplay input is off outside the running phase, so the boost is too.
	/// </summary>
	public sealed class Nitro
	{
		private readonly BoardSettings _settings;
		private readonly BoardState _board;
		private readonly IInputService _input;

		public Nitro(BoardSettings settings, BoardState board, IInputService input)
		{
			_settings = settings;
			_board = board;
			_input = input;
			Fill = settings.NitroStartFill;
		}

		/// <summary>Tank level in 0..1.</summary>
		public float Fill { get; private set; }

		public bool IsActive { get; private set; }

		/// <summary>Blends between 1 and the boost multiplier as the boost starts and stops.</summary>
		public float SpeedMultiplier { get; private set; } = 1f;

		/// <summary>How far the boost has blended in, 0..1; drives the boost effects.</summary>
		public float Blend => _settings.NitroSpeedMultiplier > 1f
			? Mathf.Clamp01((SpeedMultiplier - 1f) / (_settings.NitroSpeedMultiplier - 1f))
			: IsActive ? 1f : 0f;

		public void Refill()
		{
			Fill = 1f;
		}

		public void Tick(float dt)
		{
			IsActive = !_board.IsCrashed && Fill > 0f && _input.BoostHeld;

			if (IsActive)
			{
				Fill = Mathf.Max(Fill - dt / _settings.NitroCapacity, 0f);
			}

			float boost = _settings.NitroSpeedMultiplier;
			float rate = (boost - 1f) / Mathf.Max(_settings.NitroBlendTime, 0.0001f);
			SpeedMultiplier = Mathf.MoveTowards(SpeedMultiplier, IsActive ? boost : 1f, rate * dt);
		}
	}
}
