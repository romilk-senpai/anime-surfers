using System;
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
		CoinDoubler,
		/// <summary>Flies high above the passage, through coins in the sky.</summary>
		Glider
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
	/// Active pickups the rider can collect. Removed pickups are reused for the next ones added.
	/// </summary>
	public sealed class PickupField
	{
		private readonly List<Pickup> _pickups = new List<Pickup>();
		private readonly Stack<Pickup> _free = new Stack<Pickup>();

		public IReadOnlyList<Pickup> Pickups => _pickups;

		public event Action<Pickup> Added;
		public event Action<Pickup> Removed;

		public void Add(PickupKind kind, float distance, float lateral, float height, bool followsWater)
		{
			Pickup pickup = _free.Count > 0 ? _free.Pop() : new Pickup();
			pickup.Kind = kind;
			pickup.Distance = distance;
			pickup.Lateral = lateral;
			pickup.Height = height;
			pickup.FollowsWater = followsWater;
			pickup.Attracted = false;
			pickup.Collected = false;
			_pickups.Add(pickup);
			Added?.Invoke(pickup);
		}

		public void Remove(Pickup pickup)
		{
			if (_pickups.Remove(pickup))
			{
				Removed?.Invoke(pickup);
				_free.Push(pickup);
			}
		}
	}
}
