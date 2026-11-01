using UnityEngine;
using VContainer;

namespace Game.Surf
{
	/// <summary>
	/// Rain and wind-blown streaks that follow the board and fade in with the <see cref="Weather"/> storm. The wind
	/// is only visual: it slants the rain and drives the streaks, gusting over time.
	/// </summary>
	public class StormEffects : MonoBehaviour
	{
		[Tooltip("World-space particles; their emission is scaled by the storm.")]
		[SerializeField] private ParticleSystem rain;
		[SerializeField] private ParticleSystem windStreaks;
		[SerializeField, Min(0f)] private float rainRate = 2500f;
		[SerializeField, Min(0f)] private float windStreakRate = 40f;
		[SerializeField, Min(0f)] private float rainFallSpeed = 18f;

		[Header("Wind")]
		[SerializeField, Min(0f)] private float windSpeed = 9f;
		[Tooltip("Wind heading relative to the passage: 0 blows from the left across it, 90 blows head-on.")]
		[SerializeField, Range(-180f, 180f)] private float windAngle = 30f;
		[Tooltip("How much the wind speed varies with gusts.")]
		[SerializeField, Range(0f, 1f)] private float gustStrength = 0.4f;
		[SerializeField, Min(0.01f)] private float gustFrequency = 0.35f;

		[Header("Follow")]
		[Tooltip("The emitters stay this far ahead of the board, so drops reach it as it moves forward.")]
		[SerializeField] private float aheadDistance = 12f;

		private BoardState _board;
		private Weather _weather;

		[Inject]
		public void Construct(BoardState board, Weather weather)
		{
			_board = board;
			_weather = weather;
		}

		private void LateUpdate()
		{
			float storm = _weather.Storm;
			Vector3 forward = _board.PathForward;
			transform.SetPositionAndRotation(_board.PlanarPosition + forward * aheadDistance,
				Quaternion.LookRotation(forward));

			float gust = 1f + gustStrength * (Mathf.PerlinNoise(Time.time * gustFrequency, 0.5f) * 2f - 1f);
			Quaternion heading = Quaternion.AngleAxis(windAngle, Vector3.up);
			Vector3 wind = heading * _board.PathRight * (windSpeed * gust * storm);

			SetEmission(rain, rainRate * storm, wind + Vector3.down * rainFallSpeed);
			SetEmission(windStreaks, windStreakRate * storm, wind * 2f);
		}

		private static void SetEmission(ParticleSystem system, float rate, Vector3 velocity)
		{
			if (system == null)
			{
				return;
			}

			ParticleSystem.EmissionModule emission = system.emission;
			emission.rateOverTime = rate;

			ParticleSystem.VelocityOverLifetimeModule motion = system.velocityOverLifetime;
			motion.x = velocity.x;
			motion.y = velocity.y;
			motion.z = velocity.z;
		}
	}
}
