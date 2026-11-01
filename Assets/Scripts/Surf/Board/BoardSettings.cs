using UnityEngine;

namespace Game.Surf
{
	[CreateAssetMenu(menuName = "Anime Surfers/Board Settings", fileName = "BoardSettings")]
	public sealed class BoardSettings : ScriptableObject
	{
		[Header("Forward")]
		[SerializeField] private float startSpeed = 10f;
		[SerializeField] private float maxSpeed = 22f;
		[SerializeField] private float acceleration = 0.2f;

		[Header("Lanes")]
		[SerializeField, Min(1)] private int laneCount = 3;
		[SerializeField] private float laneWidth = 2.5f;
		[SerializeField] private float laneChangeTime = 0.18f;

		[Header("Buoyancy")]
		[SerializeField] private float gravity = 25f;
		[Tooltip("Upward acceleration per meter of submersion.")]
		[SerializeField] private float buoyancy = 120f;
		[Tooltip("Damps vertical velocity relative to the water surface while in contact.")]
		[SerializeField] private float waterDrag = 9f;
		[Tooltip("How high the board floats above the surface at rest.")]
		[SerializeField] private float rideHeight = 0.08f;
		[Tooltip("How far below ride height the board may be pushed on landings.")]
		[SerializeField] private float maxSink = 0.08f;
		[SerializeField] private float jumpHeight = 2.2f;
		[SerializeField] private float jumpBufferTime = 0.15f;

		[Header("Slide")]
		[SerializeField] private float slideDuration = 0.7f;
		[Tooltip("Downward speed forced when sliding in the air, to drop back quickly.")]
		[SerializeField] private float slideDropSpeed = 15f;

		[Header("Body")]
		[SerializeField] private float boardLength = 1.9f;
		[SerializeField] private float boardWidth = 0.5f;
		[SerializeField] private float bodyHeight = 1.7f;
		[SerializeField] private float slideBodyHeight = 0.8f;
		[SerializeField] private float bodyHalfLength = 0.5f;
		[Tooltip("Walkable surfaces up to this far above the board are stepped onto instead of hit.")]
		[SerializeField] private float stepHeight = 0.5f;

		[Header("Nitro")]
		[Tooltip("Seconds of boost a full tank holds.")]
		[SerializeField, Min(0.01f)] private float nitroCapacity = 9f;
		[SerializeField, Range(0f, 1f)] private float nitroStartFill = 1f;
		[SerializeField, Min(1f)] private float nitroSpeedMultiplier = 1.4f;
		[Tooltip("Time the speed takes to blend into and out of the boost.")]
		[SerializeField, Min(0f)] private float nitroBlendTime = 0.25f;

		[Header("Crash")]
		[Tooltip("How quickly the board slows down on its own after a crash, forward and sideways.")]
		[SerializeField] private float crashDrag = 1.5f;
		[Tooltip("Speed the board bounces back off a wall at.")]
		[SerializeField] private float wallBounceSpeed = 3f;
		[Tooltip("Sideways speed of the bounce, toward the middle of the passage.")]
		[SerializeField] private float wallBounceSideSpeed = 4.5f;
		[SerializeField] private float wallBounceHeight = 1.5f;

		public float StartSpeed => startSpeed;
		public float MaxSpeed => maxSpeed;
		public float Acceleration => acceleration;

		public int LaneCount => laneCount;
		public float LaneWidth => laneWidth;
		public float LaneChangeTime => laneChangeTime;

		public float Gravity => gravity;
		public float Buoyancy => buoyancy;
		public float WaterDrag => waterDrag;
		public float RideHeight => rideHeight;
		public float MaxSink => maxSink;
		public float JumpHeight => jumpHeight;
		public float JumpBufferTime => jumpBufferTime;

		public float SlideDuration => slideDuration;
		public float SlideDropSpeed => slideDropSpeed;

		public float BoardLength => boardLength;
		public float BoardWidth => boardWidth;
		public float BodyHeight => bodyHeight;
		public float SlideBodyHeight => slideBodyHeight;
		public float BodyHalfLength => bodyHalfLength;
		public float StepHeight => stepHeight;

		public float NitroCapacity => nitroCapacity;
		public float NitroStartFill => nitroStartFill;
		public float NitroSpeedMultiplier => nitroSpeedMultiplier;
		public float NitroBlendTime => nitroBlendTime;

		public float CrashDrag => crashDrag;
		public float WallBounceSpeed => wallBounceSpeed;
		public float WallBounceSideSpeed => wallBounceSideSpeed;
		public float WallBounceHeight => wallBounceHeight;

		public float PassageHalfWidth => laneCount * laneWidth * 0.5f;

		public float LaneOffset(int lane)
		{
			return (lane - (laneCount - 1) * 0.5f) * laneWidth;
		}

		public int NearestLane(float lateral)
		{
			return Mathf.Clamp(Mathf.RoundToInt(lateral / laneWidth + (laneCount - 1) * 0.5f), 0, laneCount - 1);
		}
	}
}
