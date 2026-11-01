using UnityEngine;

namespace Game.Surf
{
	[CreateAssetMenu(menuName = "Anime Surfers/Path Settings", fileName = "PathSettings")]
	public sealed class PathSettings : ScriptableObject
	{
		[SerializeField, Min(0.25f)] private float pointSpacing = 1f;
		[SerializeField] private float straightStartLength = 60f;
		[SerializeField, Min(5f)] private float minTurnRadius = 35f;
		[SerializeField] private float turnFrequency = 0.008f;
		[SerializeField, Range(0f, 0.9f)] private float straightBias = 0.3f;
		[Tooltip("How far, in degrees, the path may turn away from its starting direction. Below 90 it always " +
		         "moves on in that direction, so it can never loop back over itself.")]
		[SerializeField, Range(10f, 85f)] private float maxHeadingDeviation = 55f;
		[SerializeField] private float keepBehind = 60f;
		[Tooltip("0 picks a random layout every run.")]
		[SerializeField] private int seed;

		public float PointSpacing => pointSpacing;
		public float StraightStartLength => straightStartLength;
		public float MinTurnRadius => minTurnRadius;
		public float TurnFrequency => turnFrequency;
		public float StraightBias => straightBias;
		public float MaxHeadingDeviation => maxHeadingDeviation;
		public float KeepBehind => keepBehind;
		public int Seed => seed;
	}
}
