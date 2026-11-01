using UnityEngine;
using VContainer;

namespace Game.Surf
{
	/// <summary>
	/// Splashes where Miko's body hits the water after a crash, using the board's splash effects. Each tracked body
	/// part splashes as it enters the water fast enough, so a fall splashes where she lands and the limbs follow.
	/// Each splash throws up glinting droplets and leaves the water around it twinkling for a moment.
	/// </summary>
	[RequireComponent(typeof(Animator))]
	// After RiderIK has placed the fallen body for the frame.
	[DefaultExecutionOrder(110)]
	public class RiderWaterFX : MonoBehaviour
	{
		private static readonly HumanBodyBones[] Parts =
		{
			HumanBodyBones.Hips, HumanBodyBones.Head, HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
			HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot
		};

		[Tooltip("Slowest downward speed of a body part entering the water that splashes; sinking stays quiet.")]
		[SerializeField] private float minEntrySpeed = 2f;
		[Tooltip("Entry speed of the strongest splash.")]
		[SerializeField] private float fullSplashSpeed = 6f;
		[Tooltip("Seconds before the same body part can splash again.")]
		[SerializeField] private float cooldown = 0.3f;

		[Header("Sparkles")]
		[Tooltip("Additive glint sprite.")]
		[SerializeField] private Material sparkleMaterial;
		[SerializeField] private Color sparkleColor = new Color(0.75f, 0.95f, 1f, 1f);
		[Tooltip("Glinting droplets thrown up by the strongest splash.")]
		[SerializeField] private int glintsPerSplash = 26;
		[Tooltip("Twinkles per second on the water around the last splash, at full strength.")]
		[SerializeField] private float twinkleRate = 22f;
		[Tooltip("Seconds the water keeps twinkling after a splash.")]
		[SerializeField] private float twinkleTime = 1.4f;
		[SerializeField] private float twinkleRadius = 1.3f;

		private readonly Transform[] _bones = new Transform[Parts.Length];
		private readonly float[] _previousHeights = new float[Parts.Length];
		private readonly bool[] _wasAbove = new bool[Parts.Length];
		private readonly float[] _nextSplashTimes = new float[Parts.Length];

		private BoardState _state;
		private WaterSurface _water;
		private SurfboardWaterFX _boardFX;
		private bool _tracking;

		private ParticleSystem _glints;
		private ParticleSystem _twinkles;
		private Vector3 _twinkleCenter;
		private float _twinkleStrength;
		private float _twinkleEndTime;
		private float _twinkleBudget;

		[Inject]
		public void Construct(BoardState state, WaterSurface water)
		{
			_state = state;
			_water = water;
		}

		private void Awake()
		{
			// The board she rides on.
			_boardFX = GetComponentInParent<SurfboardWaterFX>();

			var animator = GetComponent<Animator>();
			for (int i = 0; i < Parts.Length; i++)
			{
				_bones[i] = animator.GetBoneTransform(Parts[i]);
			}

			CreateSparkles();
		}

		private void LateUpdate()
		{
			float dt = Time.deltaTime;
			if (_state == null || !_state.IsCrashed || dt <= 0f)
			{
				return;
			}

			for (int i = 0; i < Parts.Length; i++)
			{
				Vector3 position = _bones[i].position;
				float surface = _water.GetHeight(position);
				bool above = position.y > surface;

				// Starts from the first crashed frame, so standing on the board doesn't count as entering.
				if (_tracking && _wasAbove[i] && !above && Time.time >= _nextSplashTimes[i])
				{
					float speed = (_previousHeights[i] - position.y) / dt;
					if (speed >= minEntrySpeed)
					{
						var point = new Vector3(position.x, surface, position.z);
						float strength = Mathf.Clamp01(speed / fullSplashSpeed);
						_boardFX.Splash(point, strength);
						Sparkle(point, strength);
						_nextSplashTimes[i] = Time.time + cooldown;
					}
				}

				_wasAbove[i] = above;
				_previousHeights[i] = position.y;
			}

			_tracking = true;
			UpdateTwinkles(dt);
		}

		private void Sparkle(Vector3 point, float strength)
		{
			_glints.Emit(new ParticleSystem.EmitParams { position = point, applyShapeToPosition = true },
				Mathf.Max(1, Mathf.RoundToInt(glintsPerSplash * strength)));

			// The strongest splash so far keeps the twinkles; a weaker one only extends them.
			if (Time.time >= _twinkleEndTime || strength >= _twinkleStrength)
			{
				_twinkleCenter = point;
				_twinkleStrength = strength;
			}

			_twinkleEndTime = Time.time + twinkleTime;
		}

		private void UpdateTwinkles(float dt)
		{
			if (Time.time >= _twinkleEndTime)
			{
				_twinkleBudget = 0f;
				return;
			}

			// Thins out towards the end.
			float fade = (_twinkleEndTime - Time.time) / twinkleTime;
			_twinkleBudget += twinkleRate * _twinkleStrength * fade * dt;

			for (; _twinkleBudget >= 1f; _twinkleBudget -= 1f)
			{
				Vector2 offset = Random.insideUnitCircle * (twinkleRadius * (0.5f + 0.5f * _twinkleStrength));
				Vector3 position = _twinkleCenter + new Vector3(offset.x, 0f, offset.y);
				// Just above the waves so they aren't hidden in a trough.
				position.y = _water.GetHeight(position) + 0.05f;
				_twinkles.Emit(new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true }, 1);
			}
		}

		private void CreateSparkles()
		{
			// Droplets thrown up and falling back.
			_glints = RuntimeParticles.Create("SplashGlints", transform, sparkleMaterial,
				ParticleSystemSimulationSpace.World, 128);
			ParticleSystem.MainModule main = _glints.main;
			main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
			main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
			main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5.5f);
			main.startRotation = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
			main.startColor = new ParticleSystem.MinMaxGradient(sparkleColor, Color.white);
			main.gravityModifier = 1.2f;
			ParticleSystem.ShapeModule shape = _glints.shape;
			shape.enabled = true;
			shape.shapeType = ParticleSystemShapeType.Cone;
			shape.angle = 40f;
			shape.radius = 0.25f;
			// Cones emit along their local Z: point it up, whatever the rider's pose.
			shape.rotation = new Vector3(-90f, 0f, 0f);
			RuntimeParticles.SetSizeOverLifetime(_glints, RuntimeParticles.Twinkle);
			// Out of the rider's hierarchy, so the cone stays upright whatever her pose.
			_glints.transform.SetParent(null, false);

			// Glints flashing on the surface where she went in.
			_twinkles = RuntimeParticles.Create("WaterTwinkles", transform, sparkleMaterial,
				ParticleSystemSimulationSpace.World, 64);
			main = _twinkles.main;
			main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
			main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
			main.startRotation = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
			main.startColor = new ParticleSystem.MinMaxGradient(sparkleColor, Color.white);
			RuntimeParticles.SetSizeOverLifetime(_twinkles, RuntimeParticles.Twinkle);
			_twinkles.transform.SetParent(null, false);
		}

		private void OnDestroy()
		{
			if (_glints != null)
			{
				Destroy(_glints.gameObject);
				Destroy(_twinkles.gameObject);
			}
		}
	}
}
