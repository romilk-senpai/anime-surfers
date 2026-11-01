using System.Collections.Generic;

namespace Game.Surf
{
	public enum PickupKind
	{
		Coin,
		Magnet,
		ScoreMultiplier,
		/// <summary>Refills the nitro tank.</summary>
		Nitro,
		/// <summary>Absorbs one head-on hit, smashing the obstacle.</summary>
		Shield,
		/// <summary>Jumps high enough to land on trains.</summary>
		SuperJump,
		/// <summary>Each coin counts more.</summary>
		CoinDoubler
	}

	/// <summary>
	/// Pickup in passage space: distance along the path, lateral offset from the centerline and height.
	/// </summary>
	public sealed class Pickup
	{
		public PickupKind Kind;
		public float Distance;
		public float Lateral;
		/// <summary>Height above the water surface when <see cref="FollowsWater"/>, absolute otherwise.</summary>
		public float Height;
		public bool FollowsWater;
		/// <summary>Pulled towards the rider by the magnet.</summary>
		public bool Attracted;
		public bool Collected;
	}

	/// <summary>
	/// Active pickups the rider can collect.
	/// </summary>
	public sealed class PickupField
	{
		private readonly List<Pickup> _pickups = new List<Pickup>();

		public IReadOnlyList<Pickup> Pickups => _pickups;

		public void Add(Pickup pickup)
		{
			_pickups.Add(pickup);
		}

		public void Remove(Pickup pickup)
		{
			_pickups.Remove(pickup);
		}
	}
}
