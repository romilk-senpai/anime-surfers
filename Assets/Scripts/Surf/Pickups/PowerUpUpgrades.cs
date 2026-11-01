using System;
using Game.Core;
using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Duration upgrades bought in the shop, one track per timed power-up. Each level lengthens the power-up by
	/// <see cref="PickupSettings.UpgradeDurationStep"/> of its base duration. Levels are kept in PlayerPrefs.
	/// </summary>
	public sealed class PowerUpUpgrades
	{
		private const string KeyPrefix = "upgrades.duration.";

		private readonly PickupSettings _settings;
		private readonly IPlayerProgress _progress;
		private readonly int[] _levels = new int[Enum.GetValues(typeof(PickupKind)).Length];

		public PowerUpUpgrades(PickupSettings settings, IPlayerProgress progress)
		{
			_settings = settings;
			_progress = progress;

			for (int i = 0; i < _levels.Length; i++)
			{
				_levels[i] = Mathf.Clamp(PlayerPrefs.GetInt(Key((PickupKind)i), 0), 0, MaxLevel);
			}
		}

		/// <summary>Fired after an upgrade is bought.</summary>
		public event Action Changed;

		public int MaxLevel => _settings.UpgradeLevels;

		public int Level(PickupKind kind) => _levels[(int)kind];

		public bool IsMaxed(PickupKind kind) => Level(kind) >= MaxLevel;

		/// <summary>Coins for the next level. Only valid while not maxed.</summary>
		public int NextCost(PickupKind kind) => _settings.UpgradeCost(Level(kind));

		public bool CanAfford(PickupKind kind) => !IsMaxed(kind) && _progress.TotalCoins >= NextCost(kind);

		/// <summary>Duration multiplier at the current level: 1 at level 0.</summary>
		public float DurationScale(PickupKind kind) => DurationScaleAt(Level(kind));

		public float DurationScaleAt(int level) => 1f + level * _settings.UpgradeDurationStep;

		/// <summary>Spends the coins and raises the level. Returns false when maxed or short of coins.</summary>
		public bool TryBuy(PickupKind kind)
		{
			if (IsMaxed(kind) || !_progress.TrySpend(NextCost(kind)))
			{
				return false;
			}

			int level = ++_levels[(int)kind];
			PlayerPrefs.SetInt(Key(kind), level);
			PlayerPrefs.Save();
			Changed?.Invoke();
			return true;
		}

		private static string Key(PickupKind kind) => KeyPrefix + kind;
	}
}
