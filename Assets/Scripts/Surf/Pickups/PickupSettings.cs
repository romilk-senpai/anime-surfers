using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Coin trails and power-ups: where they are placed, how they are collected and what each power-up does.
	/// </summary>
	[CreateAssetMenu(menuName = "Anime Surfers/Pickup Settings", fileName = "PickupSettings")]
	public sealed class PickupSettings : ScriptableObject
	{
		[Header("Placement")]
		[SerializeField] private float firstTrailDistance = 25f;
		[SerializeField] private Vector2 trailGap = new Vector2(6f, 18f);
		[Tooltip("Just past the fog end, so trails appear unseen. At medium view distance; scaled with the player's " +
		         "setting. Trails also stay the longest trail's length short of the last generated obstacle row, so " +
		         "they are only shaped around obstacles that already exist.")]
		[SerializeField] private float spawnAhead = 80f;
		[SerializeField] private float despawnBehind = 10f;

		[Header("Trail")]
		[SerializeField] private Vector2Int coinCount = new Vector2Int(5, 12);
		[SerializeField] private float coinSpacing = 2.2f;
		[Tooltip("Height of the coins above the surface they hover over.")]
		[SerializeField] private float hoverHeight = 0.7f;
		[Tooltip("Coins within this distance of a barrier arc over it.")]
		[SerializeField] private float barrierArcLength = 5f;
		[Tooltip("Height of the arc over a barrier, above the water.")]
		[SerializeField] private float barrierArcHeight = 2.6f;
		[Tooltip("A trail ends where the surface rises more than this between two coins (a train without a ramp).")]
		[SerializeField] private float maxStep = 1.5f;

		[Header("Power-Ups")]
		[SerializeField, Range(0f, 1f)] private float powerUpChance = 0.08f;

		[Header("Power-Up Weights")]
		[SerializeField, Min(0f)] private float nitroWeight = 2f;
		[SerializeField, Min(0f)] private float magnetWeight = 1f;
		[SerializeField, Min(0f)] private float multiplierWeight = 1f;
		[SerializeField, Min(0f)] private float shieldWeight = 1f;
		[SerializeField, Min(0f)] private float superJumpWeight = 1f;
		[SerializeField, Min(0f)] private float coinDoublerWeight = 1f;

		[Header("Collection")]
		[Tooltip("How far from the rider's body a pickup is still collected.")]
		[SerializeField, Min(0f)] private float pickupRadius = 0.6f;

		[Header("Magnet")]
		[SerializeField, Min(0f)] private float magnetDuration = 10f;
		[Tooltip("Coins up to this far ahead are pulled in, from every lane.")]
		[SerializeField, Min(0f)] private float magnetRange = 10f;
		[Tooltip("Pull speed relative to the board.")]
		[SerializeField, Min(0f)] private float magnetPullSpeed = 25f;

		[Header("Score Multiplier")]
		[SerializeField, Min(0f)] private float multiplierDuration = 10f;
		[SerializeField, Min(1)] private int scoreMultiplier = 2;

		[Header("Shield")]
		[Tooltip("How long the shield lasts if nothing breaks it.")]
		[SerializeField, Min(0f)] private float shieldDuration = 20f;
		[Tooltip("After the shield breaks, anything else hit head-on within this time is smashed too.")]
		[SerializeField, Min(0f)] private float shieldBreakGrace = 0.5f;

		[Header("Super Jump")]
		[SerializeField, Min(0f)] private float superJumpDuration = 10f;
		[Tooltip("Jump height while active; above the train height minus the step height, so trains can be jumped onto.")]
		[SerializeField, Min(0f)] private float superJumpHeight = 4.5f;

		[Header("Coin Doubler")]
		[SerializeField, Min(0f)] private float coinDoublerDuration = 10f;
		[SerializeField, Min(1)] private int coinMultiplier = 2;

		[Header("Shop Upgrades")]
		[Tooltip("Coins for each duration upgrade of a power-up, cheapest first; one level per entry.")]
		[SerializeField] private int[] upgradeCosts = { 500, 800, 1000, 1200, 1500 };
		[Tooltip("Duration each upgrade level adds, as a share of the base duration.")]
		[SerializeField, Min(0f)] private float upgradeDurationStep = 0.2f;

		public float FirstTrailDistance => firstTrailDistance;
		public Vector2 TrailGap => trailGap;
		public float SpawnAhead => spawnAhead;
		public float DespawnBehind => despawnBehind;

		public Vector2Int CoinCount => coinCount;
		public float CoinSpacing => coinSpacing;
		public float HoverHeight => hoverHeight;
		public float BarrierArcLength => barrierArcLength;
		public float BarrierArcHeight => barrierArcHeight;
		public float MaxStep => maxStep;

		public float PowerUpChance => powerUpChance;

		public float NitroWeight => nitroWeight;
		public float MagnetWeight => magnetWeight;
		public float MultiplierWeight => multiplierWeight;
		public float ShieldWeight => shieldWeight;
		public float SuperJumpWeight => superJumpWeight;
		public float CoinDoublerWeight => coinDoublerWeight;

		public float PickupRadius => pickupRadius;

		public float MagnetDuration => magnetDuration;
		public float MagnetRange => magnetRange;
		public float MagnetPullSpeed => magnetPullSpeed;

		public float MultiplierDuration => multiplierDuration;
		public int ScoreMultiplier => scoreMultiplier;

		public float ShieldDuration => shieldDuration;
		public float ShieldBreakGrace => shieldBreakGrace;

		public float SuperJumpDuration => superJumpDuration;
		public float SuperJumpHeight => superJumpHeight;

		public float CoinDoublerDuration => coinDoublerDuration;
		public int CoinMultiplier => coinMultiplier;

		public int UpgradeLevels => upgradeCosts.Length;
		public float UpgradeDurationStep => upgradeDurationStep;

		/// <summary>Coins to go from <paramref name="level"/> to the next one.</summary>
		public int UpgradeCost(int level) => upgradeCosts[level];

		/// <summary>How long a timed power-up lasts; 0 for pickups that are not one.</summary>
		public float Duration(PickupKind kind)
		{
			return kind switch
			{
				PickupKind.Magnet => magnetDuration,
				PickupKind.ScoreMultiplier => multiplierDuration,
				PickupKind.Shield => shieldDuration,
				PickupKind.SuperJump => superJumpDuration,
				PickupKind.CoinDoubler => coinDoublerDuration,
				_ => 0f
			};
		}
	}
}
