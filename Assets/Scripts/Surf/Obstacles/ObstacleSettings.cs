using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Where obstacle rows go and what they are made of: placement, how often each obstacle comes up in a lane and
	/// the sizes of trains and barriers.
	/// </summary>
	[CreateAssetMenu(menuName = "Anime Surfers/Obstacle Settings", fileName = "ObstacleSettings")]
	public sealed class ObstacleSettings : ScriptableObject
	{
		[Header("Placement")]
		[SerializeField] private float firstRowDistance = 60f;
		[SerializeField] private Vector2 rowGap = new Vector2(16f, 28f);
		[Tooltip("At medium view distance; scaled with the player's setting.")]
		[SerializeField] private float spawnAhead = 160f;
		[SerializeField] private float despawnBehind = 20f;

		[Header("Lane Weights")]
		[SerializeField, Min(0f)] private float noneWeight = 3f;
		[SerializeField, Min(0f)] private float barrierWeight = 2f;
		[SerializeField, Min(0f)] private float trainWeight = 2f;
		[SerializeField, Min(0f)] private float climbableTrainWeight = 1.5f;

		[Header("Train")]
		[SerializeField] private float trainWidth = 2.1f;
		[Tooltip("Higher than the jump so trains can only be climbed via a ramp.")]
		[SerializeField] private float trainHeight = 3.2f;
		[SerializeField] private float trainBottom = -1.5f;
		[SerializeField] private float carLength = 6f;
		[SerializeField] private float carGap = 0.6f;
		[SerializeField] private Vector2Int trainCars = new Vector2Int(2, 4);
		[SerializeField] private Vector2Int climbableTrainCars = new Vector2Int(3, 5);
		[SerializeField] private float rampLength = 9f;
		[Tooltip("Ramp start height; below the wave troughs so the board catches it from the water.")]
		[SerializeField] private float rampStartHeight = -0.8f;

		[Header("Barrier")]
		[SerializeField] private float barrierWidth = 2.3f;
		[Tooltip("Bar height above the water: above the sliding rider, below the standing one.")]
		[SerializeField] private float barrierBottom = 1.15f;
		[SerializeField] private float barrierTop = 1.55f;
		[SerializeField] private float barrierDepth = 0.3f;

		public float FirstRowDistance => firstRowDistance;
		public Vector2 RowGap => rowGap;
		public float SpawnAhead => spawnAhead;
		public float DespawnBehind => despawnBehind;

		public float NoneWeight => noneWeight;
		public float BarrierWeight => barrierWeight;
		public float TrainWeight => trainWeight;
		public float ClimbableTrainWeight => climbableTrainWeight;

		public float TrainWidth => trainWidth;
		public float TrainHeight => trainHeight;
		public float TrainBottom => trainBottom;
		public float CarLength => carLength;
		public float CarGap => carGap;
		public Vector2Int TrainCars => trainCars;
		public Vector2Int ClimbableTrainCars => climbableTrainCars;
		public float RampLength => rampLength;
		public float RampStartHeight => rampStartHeight;

		public float BarrierWidth => barrierWidth;
		public float BarrierBottom => barrierBottom;
		public float BarrierTop => barrierTop;
		public float BarrierDepth => barrierDepth;
	}
}
