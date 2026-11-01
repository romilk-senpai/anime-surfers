using System;
using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Timers of the active power-ups, indexed by <see cref="PickupKind"/>. Picking up one that is already active
	/// restarts its timer. The shield also ends early when it absorbs a hit.
	/// </summary>
	public sealed class PowerUps
	{
		private readonly PickupSettings _settings;
		private readonly PowerUpUpgrades _upgrades;
		private readonly float[] _remaining = new float[Enum.GetValues(typeof(PickupKind)).Length];
		private readonly bool[] _wasActive = new bool[Enum.GetValues(typeof(PickupKind)).Length];
		private float _shieldGraceRemaining;

		public PowerUps(PickupSettings settings, PowerUpUpgrades upgrades)
		{
			_settings = settings;
			_upgrades = upgrades;
		}

		public bool IsMagnetActive => IsActive(PickupKind.Magnet);
		public bool IsShieldActive => IsActive(PickupKind.Shield);
		public int ScoreMultiplier => IsActive(PickupKind.ScoreMultiplier) ? _settings.ScoreMultiplier : 1;
		public int CoinValue => IsActive(PickupKind.CoinDoubler) ? _settings.CoinMultiplier : 1;

		/// <summary>Fired when a power-up starts or runs out.</summary>
		public event Action Changed;

		/// <summary>Fired when the shield breaks on a hit.</summary>
		public event Action ShieldBroken;

		public float Remaining(PickupKind kind) => _remaining[(int)kind];

		public bool IsActive(PickupKind kind) => _remaining[(int)kind] > 0f;

		/// <summary>How long the power-up lasts with its shop upgrades; 0 for pickups that are not one.</summary>
		public float Duration(PickupKind kind) => _settings.Duration(kind) * _upgrades.DurationScale(kind);

		/// <summary>Jump height while super jump is active, otherwise the given one.</summary>
		public float JumpHeight(float normal)
		{
			return IsActive(PickupKind.SuperJump) ? Mathf.Max(normal, _settings.SuperJumpHeight) : normal;
		}

		public void Activate(PickupKind kind)
		{
			float duration = Duration(kind);
			if (duration <= 0f)
			{
				return;
			}

			_remaining[(int)kind] = duration;
			NotifyIfChanged();
		}

		/// <summary>
		/// Called on a head-on hit. Breaks the shield if it is up and returns true while it, or the grace period
		/// after it broke, protects the rider.
		/// </summary>
		public bool TryAbsorbHit()
		{
			if (_shieldGraceRemaining > 0f)
			{
				return true;
			}

			if (!IsShieldActive)
			{
				return false;
			}

			_remaining[(int)PickupKind.Shield] = 0f;
			_shieldGraceRemaining = _settings.ShieldBreakGrace;
			NotifyIfChanged();
			ShieldBroken?.Invoke();
			return true;
		}

		public void Tick(float dt)
		{
			_shieldGraceRemaining = Mathf.Max(_shieldGraceRemaining - dt, 0f);

			for (int i = 0; i < _remaining.Length; i++)
			{
				_remaining[i] = Mathf.Max(_remaining[i] - dt, 0f);
			}

			NotifyIfChanged();
		}

		private void NotifyIfChanged()
		{
			bool changed = false;

			for (int i = 0; i < _remaining.Length; i++)
			{
				bool active = _remaining[i] > 0f;
				changed |= active != _wasActive[i];
				_wasActive[i] = active;
			}

			if (changed)
			{
				Changed?.Invoke();
			}
		}
	}
}
