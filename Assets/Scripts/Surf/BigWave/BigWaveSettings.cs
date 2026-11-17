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
		[Tooltip("The wave rises on the horizon while the passage runs straight; obstacles keep coming until " +
		         "shortly before the drop-in.")]
		[SerializeField, Min(1f)] private float approachLength = 260f;
		[Tooltip("Obstacles and pickups stay away for this far before the drop-in.")]
		[SerializeField, Min(0f)] private float clearBefore = 15f;
		[Tooltip("The passage turns to run along the wave, into the barrel.")]
		[SerializeField, Min(1f)] private float dropInLength = 40f;
		[Tooltip("Locked forward speed inside the barrel; with the duration it sets the barrel's length.")]
		[SerializeField, Min(1f)] private float barrelSpeed = 20f;
		[SerializeField, Min(1f)] private float barrelDuration = 20f;
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

		[Header("Wave Shape")]
		[Tooltip("Crest height above the still water.")]
		[SerializeField, Min(0.5f)] private float waveHeight = 7f;
		[Tooltip("Across from the passage's centerline, toward the wave, where the face starts rising; negative is " +
		         "on the far side of the centerline.")]
		[SerializeField] private float faceBase = -6f;
		[Tooltip("Across from the passage's centerline, toward the wave, where the crest is.")]
		[SerializeField] private float crestOffset = 9f;
		[Tooltip("The back of the wave falls away over this far behind the crest.")]
		[SerializeField, Min(0.5f)] private float backWidth = 18f;
		[Tooltip("How much the face's upper half steepens toward the crest, where the lip carries on from it: 0 is " +
		         "an even curve (about 43° at the crest), 1 a hollow face (about 64°). The lower half stays put.")]
		[SerializeField, Range(0f, 1f)] private float faceHollow = 1f;
		[Tooltip("The ridge starts this far before the barrel, so it's in view on the way in.")]
		[SerializeField, Min(0f)] private float ridgeBefore = 120f;
		[Tooltip("The ridge's ends taper off over this far; the far one flattens over the end of the barrel.")]
		[SerializeField, Min(1f)] private float ridgeTaper = 30f;
		[Tooltip("The lip throws out over this far, done as the board lines up in the barrel: until then the wave " +
		         "stands as a wall with its crest feathering, and it pitches as the board comes to it.")]
		[SerializeField, Min(1f)] private float throwDistance = 80f;
		[Tooltip("The lip pitches out over this far from the barrel's start, the tube's mouth the board rides in " +
		         "through.")]
		[SerializeField, Min(1f)] private float mouthLength = 35f;
		[Tooltip("The lip is fully pitched up to this far ahead of the rider; past it it pitches less and less, " +
		         "opening the tube's eye.")]
		[SerializeField, Min(0f)] private float pitchAhead = 5f;
		[Tooltip("Across from the crest to the thrown lip's tip, which hangs above the water past the passage's " +
		         "far side.")]
		[SerializeField, Min(1f)] private float lipReach = 15f;
		[Tooltip("The lip spans this far behind the rider; behind it the wave has broken.")]
		[SerializeField, Min(1f)] private float lipBehind = 25f;
		[Tooltip("The lip spans this far ahead of the rider, folding back into the crest toward its end.")]
		[SerializeField, Min(1f)] private float lipAhead = 80f;

		[Header("Barrel Ride")]
		[Tooltip("Height of the catch zone the player steers, as a fraction of the bar.")]
		[SerializeField, Range(0.05f, 0.9f)] private float catchSize = 0.28f;
		[Tooltip("Upward acceleration of the catch zone while held, in bars per second squared.")]
		[SerializeField, Min(0f)] private float holdAcceleration = 3f;
		[Tooltip("Downward acceleration of the catch zone while released, in bars per second squared.")]
		[SerializeField, Min(0f)] private float fallAcceleration = 2.5f;
		[Tooltip("Share of its speed the catch zone keeps when it bounces off either end of the bar.")]
		[SerializeField, Range(0f, 1f)] private float bounce = 0.35f;
		[Tooltip("Seconds between the target picking a new spot, picked at random in this range.")]
		[SerializeField] private Vector2 targetInterval = new Vector2(0.5f, 1.4f);
		[Tooltip("Seconds the target takes to settle on a new spot, picked at random in this range.")]
		[SerializeField] private Vector2 targetSmoothTime = new Vector2(0.2f, 0.55f);
		[Tooltip("Chance that the target darts to its new spot instead.")]
		[SerializeField, Range(0f, 1f)] private float dartChance = 0.25f;
		[SerializeField, Min(0.01f)] private float dartSmoothTime = 0.1f;
		[Tooltip("The hold meter starts here; it fills while the target is in the catch zone, drains while it's " +
		         "out, and the rider wipes out when it runs empty.")]
		[SerializeField, Range(0.05f, 1f)] private float meterStart = 0.5f;
		[Tooltip("Meter gained per second while the target is in the catch zone.")]
		[SerializeField, Min(0f)] private float meterFill = 0.3f;
		[Tooltip("Meter lost per second while the target is out of the catch zone.")]
		[SerializeField, Min(0f)] private float meterDrain = 0.26f;
		[Tooltip("Seconds at the start of the ride during which the target holds still and the meter doesn't drain.")]
		[SerializeField, Min(0f)] private float graceTime = 1.2f;
		[Tooltip("How much faster the target gets, and the meter drains, with each barrel already ridden this run.")]
		[SerializeField, Min(0f)] private float difficultyPerWave = 0.05f;
		[Tooltip("Score for riding out of the barrel; the score multiplier doesn't apply.")]
		[SerializeField, Min(0)] private int successBonus = 1500;

		public Vector2 FirstDistance => firstDistance;
		public Vector2 GapDistance => gapDistance;
		public float ApproachLength => approachLength;
		public float ClearBefore => clearBefore;
		public float DropInLength => dropInLength;
		public float BarrelSpeed => barrelSpeed;
		public float BarrelDuration => barrelDuration;
		public float BarrelLength => barrelSpeed * barrelDuration;
		public float ExitLength => exitLength;
		public float ClearAfter => clearAfter;
		public float TurnAngle => turnAngle;
		public float PathBlend => pathBlend;

		public float WaveHeight => waveHeight;
		public float FaceBase => faceBase;
		public float CrestOffset => crestOffset;
		public float BackWidth => backWidth;
		public float FaceHollow => faceHollow;
		public float RidgeBefore => ridgeBefore;
		public float RidgeTaper => ridgeTaper;
		public float ThrowDistance => throwDistance;
		public float MouthLength => mouthLength;
		public float PitchAhead => pitchAhead;
		public float LipReach => lipReach;
		public float LipBehind => lipBehind;
		public float LipAhead => lipAhead;

		public float CatchSize => catchSize;
		public float HoldAcceleration => holdAcceleration;
		public float FallAcceleration => fallAcceleration;
		public float Bounce => bounce;
		public Vector2 TargetInterval => targetInterval;
		public Vector2 TargetSmoothTime => targetSmoothTime;
		public float DartChance => dartChance;
		public float DartSmoothTime => dartSmoothTime;
		public float MeterStart => meterStart;
		public float MeterFill => meterFill;
		public float MeterDrain => meterDrain;
		public float GraceTime => graceTime;
		public float DifficultyPerWave => difficultyPerWave;
		public int SuccessBonus => successBonus;
	}
}
