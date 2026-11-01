using UnityEngine;

namespace Game.Surf
{
	[CreateAssetMenu(menuName = "Anime Surfers/Weather Settings", fileName = "WeatherSettings")]
	public sealed class WeatherSettings : ScriptableObject
	{
		[Tooltip("Calm distance between storms, picked at random in this range. The first storm comes anywhere " +
		         "between the start and the upper end.")]
		[SerializeField] private Vector2 calmDistance = new Vector2(600f, 1800f);
		[Tooltip("Length of a storm at full strength, picked at random in this range.")]
		[SerializeField] private Vector2 stormDistance = new Vector2(300f, 700f);
		[Tooltip("Distance the storm takes to build up, and again to clear.")]
		[SerializeField, Min(1f)] private float rampDistance = 120f;
		[Tooltip("Wave height multiplier in calm weather.")]
		[SerializeField, Min(0f)] private float calmWaveScale = 1.5f;
		[Tooltip("Wave height multiplier at full storm. Crests fold over once the waves' steepness, summed along " +
		         "any one direction, goes past 1; at 2.8 the current waves reach about 0.64.")]
		[SerializeField, Min(0f)] private float stormWaveScale = 2.8f;

		public Vector2 CalmDistance => calmDistance;
		public Vector2 StormDistance => stormDistance;
		public float RampDistance => rampDistance;
		public float CalmWaveScale => calmWaveScale;
		public float StormWaveScale => stormWaveScale;
	}
}
