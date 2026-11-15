using UnityEngine;

namespace Game.Surf
{
	[CreateAssetMenu(menuName = "Anime Surfers/Big Wave Settings", fileName = "BigWaveSettings")]
	public sealed class BigWaveSettings : ScriptableObject
	{
		[Header("Schedule")]
		[Tooltip("Where the first wave starts coming in, picked at random in this range.")]
		[SerializeField] private Vector2 firstDistance = new Vector2(1200f, 1800f);
		[Tooltip("Distance between one wave's exit and the next one coming in, picked at random in this range.")]
		[SerializeField] private Vector2 gapDistance = new Vector2(2500f, 4000f);

		[Header("Phases")]
		[Tooltip("The wave rises on the horizon while the passage runs straight and clear of obstacles.")]
		[SerializeField, Min(1f)] private float approachLength = 150f;
		[Tooltip("The passage turns to run along the wave, into the barrel.")]
		[SerializeField, Min(1f)] private float dropInLength = 60f;
		[Tooltip("Locked forward speed inside the barrel; with the duration it sets the barrel's length.")]
		[SerializeField, Min(1f)] private float barrelSpeed = 20f;
		[SerializeField, Min(1f)] private float barrelDuration = 30f;
		[Tooltip("The passage turns back out of the wave.")]
		[SerializeField, Min(1f)] private float exitLength = 60f;
		[Tooltip("Obstacles and pickups stay away for this far past the exit.")]
		[SerializeField, Min(0f)] private float clearAfter = 40f;

		[Header("Path")]
		[Tooltip("Degrees the passage turns into the wave, and back out after it. Capped at the path's maximum " +
		         "heading deviation.")]
		[SerializeField, Range(5f, 85f)] private float turnAngle = 45f;
		[Tooltip("The path's own curves fade out over this distance before the wave and back in after it.")]
		[SerializeField, Min(0f)] private float pathBlend = 40f;

		public Vector2 FirstDistance => firstDistance;
		public Vector2 GapDistance => gapDistance;
		public float ApproachLength => approachLength;
		public float DropInLength => dropInLength;
		public float BarrelSpeed => barrelSpeed;
		public float BarrelDuration => barrelDuration;
		public float BarrelLength => barrelSpeed * barrelDuration;
		public float ExitLength => exitLength;
		public float ClearAfter => clearAfter;
		public float TurnAngle => turnAngle;
		public float PathBlend => pathBlend;
	}
}
