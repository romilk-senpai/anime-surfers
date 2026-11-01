using UnityEngine;
using UnityEngine.Rendering;
using VContainer;

namespace Game.Surf
{
	/// <summary>
	/// Power-up visuals around the rider. A bubble surrounds her while the shield is up: it pops in when picked up,
	/// blinks before running out and bursts when it breaks. Super jumps leave a shockwave ring on the water at the
	/// take-off and a glowing trail through the air. The nitro boost streams two exhaust trails from the board's tail.
	/// Built from primitives at runtime; the materials are meant for the PowerUpGlow shader, whose colour is faded
	/// through a property block.
	/// </summary>
	public class PowerUpEffects : MonoBehaviour
	{
		private static readonly int ColorId = Shader.PropertyToID("_Color");

		[Header("Shield")]
		[SerializeField] private Material shieldMaterial;
		[Tooltip("Bubble size around the standing rider: width, height, length. Squashed with her while sliding.")]
		[SerializeField] private Vector3 shieldSize = new Vector3(1.7f, 2.3f, 2.1f);
		[SerializeField] private float shieldPopTime = 0.25f;
		[SerializeField] private float shieldSquashSharpness = 20f;
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
		[Tooltip("Trail width at full boost; thinner while the boost blends in.")]
		[SerializeField] private float nitroTrailWidth = 0.22f;
		[Tooltip("Side offset of each trail from the board's centerline.")]
		[SerializeField] private float nitroTrailSpacing = 0.2f;
		[SerializeField] private float nitroTrailHeight = 0.15f;

		private BoardState _board;
		private BoardSettings _settings;
		private PowerUps _powerUps;
		private Nitro _nitro;
		private MaterialPropertyBlock _block;

		private Renderer _shield;
		private Color _shieldColor;
		private float _shieldVisibility;
		private float _shieldSquash = 1f;
		private float _burstAge = float.PositiveInfinity;

		private Renderer _ring;
		private Color _ringColor;
		private float _ringAge = float.PositiveInfinity;

		private TrailRenderer _trail;
		private readonly TrailRenderer[] _nitroTrails = new TrailRenderer[2];

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
				_nitroTrails[i] = CreateTrail("NitroTrail", nitroTrailMaterial, nitroTrailTime, nitroTrailWidth);
			}
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

			float squash = _board.IsSliding ? _settings.SlideBodyHeight / _settings.BodyHeight : 1f;
			_shieldSquash = Mathf.Lerp(_shieldSquash, squash, 1f - Mathf.Exp(-shieldSquashSharpness * dt));

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

			float bodyHeight = _settings.BodyHeight * _shieldSquash;
			Transform bubble = _shield.transform;
			bubble.SetPositionAndRotation(
				_board.PlanarPosition + Vector3.up * (_board.Height + bodyHeight * 0.5f),
				Quaternion.LookRotation(_board.PathForward));
			bubble.localScale = new Vector3(shieldSize.x, shieldSize.y * _shieldSquash, shieldSize.z) * scale;

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

			for (int i = 0; i < _nitroTrails.Length; i++)
			{
				TrailRenderer trail = _nitroTrails[i];
				float side = i == 0 ? -1f : 1f;
				trail.transform.position = tail + _board.PathRight * (side * nitroTrailSpacing);
				trail.widthMultiplier = nitroTrailWidth * blend;
				trail.emitting = blend > 0f && !_board.IsCrashed;
			}
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

		private static float EaseOutBack(float t)
		{
			const float overshoot = 1.70158f;
			float u = t - 1f;
			return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
		}
	}
}
