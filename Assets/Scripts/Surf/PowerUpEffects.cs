using UnityEngine;
using UnityEngine.Rendering;
using VContainer;

namespace Game.Surf
{
	/// <summary>
	/// Power-up visuals around the rider. A bubble surrounds her while the shield is up: it pops in when picked up,
	/// blinks before running out and bursts when it breaks. Super jumps leave a shockwave ring on the water at the take-off and a glowing trail through the air. The nitro
	/// boost streams two exhaust trails, billowing smoke and sparks from the board's tail, with a puff as it kicks in.
	/// Built from primitives and particle systems at runtime; the bubble, ring and trail materials are meant for the
	/// PowerUpGlow shader, whose colour is faded through a property block.
	/// </summary>
	public class PowerUpEffects : MonoBehaviour
	{
		private static readonly int ColorId = Shader.PropertyToID("_Color");
		private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

		[Header("Shield")]
		[SerializeField] private Material shieldMaterial;
		[Tooltip("Bubble diameter, about her standing height. It rests on the board, so it covers her standing or sliding.")]
		[SerializeField] private float shieldDiameter = 1.8f;
		[Tooltip("How far the bubble dips below the board, as a fraction of its radius.")]
		[SerializeField, Range(0f, 0.5f)] private float shieldSink;
		[SerializeField] private float shieldPopTime = 0.25f;
		[SerializeField] private float shieldPulseRate = 1.5f;
		[SerializeField, Range(0f, 0.3f)] private float shieldPulse = 0.04f;
		[Tooltip("Blinks for this long before running out.")]
		[SerializeField] private float shieldWarningTime = 3f;
		[SerializeField] private float shieldBlinkRate = 6f;
		[SerializeField] private float shieldBurstTime = 0.35f;
		[SerializeField] private float shieldBurstScale = 1.8f;

		[Header("Super Jump")]
		[SerializeField] private Material jumpRingMaterial;
		[SerializeField] private Material jumpTrailMaterial;
		[SerializeField] private float ringTime = 0.45f;
		[SerializeField] private float ringStartRadius = 0.6f;
		[SerializeField] private float ringEndRadius = 3.5f;
		[SerializeField] private float ringThickness = 0.08f;
		[SerializeField] private float trailTime = 0.45f;
		[SerializeField] private float trailWidth = 0.6f;
		[Tooltip("Height of the trail above the board's bottom.")]
		[SerializeField] private float trailHeight = 0.3f;

		[Header("Nitro")]
		[SerializeField] private Material nitroTrailMaterial;
		[SerializeField] private float nitroTrailTime = 0.3f;
		[Tooltip("Trail width at full boost, glow included; thinner while the boost blends in.")]
		[SerializeField] private float nitroTrailWidth = 0.22f;
		[Tooltip("Side offset of each trail from the board's centerline.")]
		[SerializeField] private float nitroTrailSpacing = 0.2f;
		[SerializeField] private float nitroTrailHeight = 0.15f;
		[Tooltip("White-hot at the board, cooling to orange and red along the trail.")]
		[SerializeField] private Gradient nitroTrailGradient = DefaultNitroTrailGradient();
		[Tooltip("How many times the streak texture repeats along a trail.")]
		[SerializeField] private float nitroTrailTiling = 3f;
		[Tooltip("How fast the streaks flow back along the trail, in texture lengths per second.")]
		[SerializeField] private float nitroTrailScroll = 4f;
		[Tooltip("Width jitter, as a fraction of the width.")]
		[SerializeField, Range(0f, 0.5f)] private float nitroTrailFlicker = 0.15f;
		[SerializeField] private float nitroTrailFlickerRate = 18f;
		[Tooltip("Flipbook smoke billowing behind the board.")]
		[SerializeField] private Material nitroSmokeMaterial;
		[Tooltip("Additive glint sprite, stretched into sparks.")]
		[SerializeField] private Material sparkleMaterial;
		[SerializeField] private float nitroSmokeRate = 40f;
		[SerializeField] private float nitroSparkRate = 45f;
		[Tooltip("Smoke puffs thrown out at once as the boost kicks in; twice as many sparks.")]
		[SerializeField] private int nitroKickPuffs = 14;
		[SerializeField] private Color nitroSmokeColor = new Color(1f, 0.92f, 0.82f, 0.55f);
		[SerializeField] private Color nitroSparkColor = new Color(1f, 0.7f, 0.25f, 1f);

		private BoardState _board;
		private BoardSettings _settings;
		private PowerUps _powerUps;
		private Nitro _nitro;
		private MaterialPropertyBlock _block;
		private MaterialPropertyBlock _nitroTrailBlock;

		private Renderer _shield;
		private Color _shieldColor;
		private float _shieldVisibility;
		private float _burstAge = float.PositiveInfinity;

		private Renderer _ring;
		private Color _ringColor;
		private float _ringAge = float.PositiveInfinity;

		private TrailRenderer _trail;
		private readonly TrailRenderer[] _nitroTrails = new TrailRenderer[2];

		private ParticleSystem _nitroSmoke;
		private ParticleSystem _nitroSparks;
		private bool _wasBoosting;

		[Inject]
		public void Construct(BoardState board, BoardSettings settings, PowerUps powerUps, Nitro nitro)
		{
			_board = board;
			_settings = settings;
			_powerUps = powerUps;
			_nitro = nitro;

			// Subscribed at injection, before the run starts.
			_board.Jumped += OnJumped;
			_powerUps.ShieldBroken += OnShieldBroken;
		}

		private void Awake()
		{
			_block = new MaterialPropertyBlock();
			_shield = CreateSphere("ShieldBubble", shieldMaterial, out _shieldColor);
			_ring = CreateSphere("JumpRing", jumpRingMaterial, out _ringColor);
			_trail = CreateTrail("JumpTrail", jumpTrailMaterial, trailTime, trailWidth);

			for (int i = 0; i < _nitroTrails.Length; i++)
			{
				TrailRenderer trail = CreateTrail("NitroTrail", nitroTrailMaterial, nitroTrailTime, nitroTrailWidth);
				// The streak texture spans the trail and scrolls along it.
				trail.textureMode = LineTextureMode.Stretch;
				trail.colorGradient = nitroTrailGradient;
				trail.widthCurve = new AnimationCurve(
					new Keyframe(0f, 1f), new Keyframe(0.2f, 0.75f), new Keyframe(1f, 0f));
				_nitroTrails[i] = trail;
			}

			_nitroTrailBlock = new MaterialPropertyBlock();

			CreateNitroParticles();
		}

		private void OnDestroy()
		{
			if (_board == null)
			{
				return;
			}

			_board.Jumped -= OnJumped;
			_powerUps.ShieldBroken -= OnShieldBroken;
		}

		private void OnJumped()
		{
			if (!_powerUps.IsActive(PickupKind.SuperJump))
			{
				return;
			}

			Vector3 position = _board.PlanarPosition;
			_ring.transform.position = new Vector3(position.x, _board.WaterHeight, position.z);
			_ringAge = 0f;
		}

		private void OnShieldBroken()
		{
			_burstAge = 0f;
			_shieldVisibility = 0f;
		}

		private void LateUpdate()
		{
			if (_board == null)
			{
				return;
			}

			float dt = Time.deltaTime;
			UpdateShield(dt);
			UpdateRing(dt);
			UpdateTrail();
			UpdateNitroTrails();
		}

		private void UpdateShield(float dt)
		{
			bool active = _powerUps.IsShieldActive && !_board.IsCrashed;
			float popRate = dt / Mathf.Max(shieldPopTime, 0.0001f);
			_shieldVisibility = Mathf.MoveTowards(_shieldVisibility, active ? 1f : 0f, popRate);
			_burstAge += dt;

			float scale;
			float alpha;
			Color color = _shieldColor;

			if (_burstAge < shieldBurstTime)
			{
				float t = _burstAge / shieldBurstTime;
				scale = Mathf.Lerp(1f, shieldBurstScale, 1f - (1f - t) * (1f - t));
				alpha = 1f - t;
				// Flashes white as it shatters.
				color = Color.Lerp(Color.white, _shieldColor, t);
			}
			else if (_shieldVisibility > 0f)
			{
				// Overshoots as it pops in, shrinks straight away as it runs out.
				scale = active ? EaseOutBack(_shieldVisibility) : _shieldVisibility;
				scale *= 1f + Mathf.Sin(Time.time * shieldPulseRate * 2f * Mathf.PI) * shieldPulse;
				alpha = _shieldVisibility;

				if (active && _powerUps.Remaining(PickupKind.Shield) < shieldWarningTime &&
					Mathf.Repeat(Time.time * shieldBlinkRate, 1f) < 0.5f)
				{
					alpha *= 0.3f;
				}
			}
			else
			{
				_shield.enabled = false;
				return;
			}

			// Centred at its full size, so it grows and bursts around her rather than from the board.
			float radius = shieldDiameter * 0.5f;
			Transform bubble = _shield.transform;
			bubble.SetPositionAndRotation(
				_board.PlanarPosition + Vector3.up * (_board.Height + radius * (1f - shieldSink)),
				Quaternion.LookRotation(_board.PathForward));
			bubble.localScale = Vector3.one * (shieldDiameter * scale);

			_shield.enabled = true;
			SetColor(_shield, color, alpha);
		}

		private void UpdateRing(float dt)
		{
			_ringAge += dt;
			if (_ringAge >= ringTime)
			{
				_ring.enabled = false;
				return;
			}

			float t = _ringAge / ringTime;
			float radius = Mathf.Lerp(ringStartRadius, ringEndRadius, 1f - (1f - t) * (1f - t));
			_ring.transform.localScale = new Vector3(radius * 2f, ringThickness, radius * 2f);
			_ring.enabled = true;
			SetColor(_ring, _ringColor, 1f - t);
		}

		private void UpdateTrail()
		{
			Vector3 position = _board.PlanarPosition;
			_trail.transform.position = new Vector3(position.x, _board.Height + trailHeight, position.z);
			_trail.emitting = _powerUps.IsActive(PickupKind.SuperJump) && !_board.IsGrounded && !_board.IsCrashed;
		}

		private void UpdateNitroTrails()
		{
			float blend = _nitro.Blend;
			Vector3 tail = _board.PlanarPosition - _board.PathForward * (_settings.BoardLength * 0.5f);
			tail.y = _board.Height + nitroTrailHeight;

			// Each trail flickers on its own; the streaks flow back towards the end.
			_nitroTrailBlock.SetVector(BaseMapStId,
				new Vector4(nitroTrailTiling, 1f, -Time.time * nitroTrailScroll, 0f));

			for (int i = 0; i < _nitroTrails.Length; i++)
			{
				TrailRenderer trail = _nitroTrails[i];
				float side = i == 0 ? -1f : 1f;
				float flicker = 1f + (Mathf.PerlinNoise(Time.time * nitroTrailFlickerRate, i * 7.3f) * 2f - 1f) *
					nitroTrailFlicker;
				trail.transform.position = tail + _board.PathRight * (side * nitroTrailSpacing);
				trail.widthMultiplier = nitroTrailWidth * blend * flicker;
				trail.emitting = blend > 0f && !_board.IsCrashed;
				trail.SetPropertyBlock(_nitroTrailBlock);
			}

			bool boosting = blend > 0f && !_board.IsCrashed;
			Quaternion backward = Quaternion.LookRotation(-_board.PathForward);
			_nitroSmoke.transform.SetPositionAndRotation(tail, backward);
			_nitroSparks.transform.SetPositionAndRotation(tail, backward);
			RuntimeParticles.SetRate(_nitroSmoke, boosting ? nitroSmokeRate * blend : 0f);
			RuntimeParticles.SetRate(_nitroSparks, boosting ? nitroSparkRate * blend : 0f);

			if (boosting && !_wasBoosting)
			{
				_nitroSmoke.Emit(nitroKickPuffs);
				_nitroSparks.Emit(nitroKickPuffs * 2);
			}

			_wasBoosting = boosting;
		}

		private void CreateNitroParticles()
		{
			// Left behind in the world as the board speeds away, rising and spreading.
			_nitroSmoke = RuntimeParticles.Create("NitroSmoke", transform, nitroSmokeMaterial,
				ParticleSystemSimulationSpace.World, 96);
			ParticleSystem.MainModule main = _nitroSmoke.main;
			main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
			main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
			main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
			main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
			main.startColor = nitroSmokeColor;
			ParticleSystem.ShapeModule shape = _nitroSmoke.shape;
			shape.enabled = true;
			shape.shapeType = ParticleSystemShapeType.Cone;
			shape.angle = 35f;
			shape.radius = 0.15f;
			ParticleSystem.VelocityOverLifetimeModule rise = _nitroSmoke.velocityOverLifetime;
			rise.enabled = true;
			rise.space = ParticleSystemSimulationSpace.World;
			rise.x = 0f;
			rise.y = 0.8f;
			rise.z = 0f;
			ParticleSystem.TextureSheetAnimationModule sheet = _nitroSmoke.textureSheetAnimation;
			sheet.enabled = true;
			sheet.numTilesX = 8;
			sheet.numTilesY = 8;
			sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
			RuntimeParticles.SetSizeOverLifetime(_nitroSmoke, AnimationCurve.Linear(0f, 0.5f, 1f, 1.6f));
			RuntimeParticles.SetFadeOut(_nitroSmoke, 0.3f);

			// Hot streaks thrown back off the tail.
			_nitroSparks = RuntimeParticles.Create("NitroSparks", transform, sparkleMaterial,
				ParticleSystemSimulationSpace.World, 96);
			main = _nitroSparks.main;
			main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
			main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
			main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
			main.startColor = new ParticleSystem.MinMaxGradient(nitroSparkColor, Color.white);
			main.gravityModifier = 1f;
			shape = _nitroSparks.shape;
			shape.enabled = true;
			shape.shapeType = ParticleSystemShapeType.Cone;
			shape.angle = 25f;
			shape.radius = 0.1f;
			var renderer = _nitroSparks.GetComponent<ParticleSystemRenderer>();
			renderer.renderMode = ParticleSystemRenderMode.Stretch;
			renderer.velocityScale = 0.06f;
			renderer.lengthScale = 1f;
			RuntimeParticles.SetFadeOut(_nitroSparks, 0.5f);
		}

		private void SetColor(Renderer target, Color color, float alpha)
		{
			color.a *= alpha;
			_block.SetColor(ColorId, color);
			target.SetPropertyBlock(_block);
		}

		private Renderer CreateSphere(string objectName, Material material, out Color color)
		{
			GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			sphere.name = objectName;
			Destroy(sphere.GetComponent<Collider>());
			sphere.transform.SetParent(transform, false);

			var target = sphere.GetComponent<MeshRenderer>();
			target.sharedMaterial = material;
			target.shadowCastingMode = ShadowCastingMode.Off;
			target.receiveShadows = false;
			target.enabled = false;

			color = material != null && material.HasProperty(ColorId) ? material.GetColor(ColorId) : Color.white;
			return target;
		}

		private TrailRenderer CreateTrail(string objectName, Material material, float time, float width)
		{
			var trailObject = new GameObject(objectName);
			trailObject.transform.SetParent(transform, false);

			var trail = trailObject.AddComponent<TrailRenderer>();
			trail.sharedMaterial = material;
			trail.time = time;
			trail.minVertexDistance = 0.2f;
			trail.widthMultiplier = width;
			trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
			trail.shadowCastingMode = ShadowCastingMode.Off;
			trail.receiveShadows = false;
			trail.emitting = false;

			var gradient = new Gradient();
			gradient.SetKeys(
				new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
				new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
			trail.colorGradient = gradient;
			return trail;
		}

		private static Gradient DefaultNitroTrailGradient()
		{
			var gradient = new Gradient();
			gradient.SetKeys(
				new[]
				{
					new GradientColorKey(new Color(1f, 0.97f, 0.85f), 0f),
					new GradientColorKey(new Color(1f, 0.62f, 0.18f), 0.3f),
					new GradientColorKey(new Color(1f, 0.25f, 0.08f), 1f)
				},
				new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.4f), new GradientAlphaKey(0f, 1f) });
			return gradient;
		}

		private static float EaseOutBack(float t)
		{
			const float overshoot = 1.70158f;
			float u = t - 1f;
			return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
		}
	}
}
