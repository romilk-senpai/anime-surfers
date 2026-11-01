using Game.Audio;
using UnityEngine;
using VContainer;

namespace Game.Surf
{
	/// <summary>
	/// Board-to-water feedback: leaves a wake trail on the surface, throws spray sheets off the rails,
	/// leaves mist behind and splashes on landings, with the rush of water under the board and splash sounds.
	/// </summary>
	[DefaultExecutionOrder(10)]
	public class SurfboardWaterFX : MonoBehaviour
	{
		[SerializeField] private WaterSurface water;
		[SerializeField] private WaterWake wake;

		[Header("Wake")]
		[SerializeField] private Vector3 tailOffset = new Vector3(0f, 0f, -0.7f);

		[Header("Rail Spray")]
		[SerializeField] private ParticleSystem leftSpray;
		[SerializeField] private ParticleSystem rightSpray;
		[SerializeField] private float sprayPerSpeed = 9f;
		[Tooltip("Extra spray from the outer rail while carving between lanes.")]
		[SerializeField] private float sprayPerCarve = 260f;
		[Tooltip("How quickly spray and mist emission rates follow speed/carve changes.")]
		[SerializeField] private float rateSmoothing = 8f;

		[Header("Mist")]
		[SerializeField] private ParticleSystem mist;
		[SerializeField] private float mistPerSpeed = 1.2f;
		[SerializeField] private float mistPerCarve = 30f;

		[Header("Water Foam")]
		[Tooltip("Flat foam stamps drawn into the water by WaterFXBuffer; emits over distance while on the water.")]
		[SerializeField] private ParticleSystem foam;
		[SerializeField] private float foamPerCarve = 40f;

		[Header("Splash")]
		[SerializeField] private ParticleSystem splash;
		[SerializeField] private float minLandingSpeed = 3.5f;
		[SerializeField] private float splashParticlesPerSpeed = 8f;
		[SerializeField] private float fullSplashSpeed = 12f;

		[Header("Sound")]
		[Tooltip("Looping rush of water, louder with speed while the board is on the water.")]
		[SerializeField] private AudioSource rideLoop;
		[SerializeField] private float rideVolumePerSpeed = 0.0035f;
		[SerializeField] private float maxRideVolume = 0.055f;
		[SerializeField] private AudioClip[] splashClips;
		[Tooltip("Splash volume for the hardest landing; softer landings scale down from it.")]
		[SerializeField, Range(0f, 1f)] private float splashVolume = 0.5f;
		[Tooltip("The kick of water as the board leaves it on a jump.")]
		[SerializeField, Range(0f, 1f)] private float jumpSplashVolume = 0.3f;
		[Tooltip("Pitched up so the take-off sounds lighter than a landing.")]
		[SerializeField] private float jumpSplashPitch = 1.15f;
		[Tooltip("Seconds after a splash sound in which only a louder one plays, so a body hitting the water at " +
		         "several points makes one sound.")]
		[SerializeField] private float splashSoundInterval = 0.12f;

		private BoardState _board;
		private ISoundController _sound;
		private float _nextSplashSoundTime;
		private float _lastSplashVolume;
		private int _lastSplashClip = -1;
		private bool _wasGrounded;
		private float _leftRate;
		private float _rightRate;
		private float _mistRate;

		[Inject]
		public void Construct(BoardState board, ISoundController sound)
		{
			_board = board;
			_sound = sound;
		}

		private void Start()
		{
			_board.Landed += OnLanded;
			_board.Jumped += OnJumped;
		}

		private void OnDestroy()
		{
			if (_board != null)
			{
				_board.Landed -= OnLanded;
				_board.Jumped -= OnJumped;
			}
		}

		private void Update()
		{
			bool grounded = _board.IsOnWater;
			Vector3 tail = transform.TransformPoint(tailOffset);

			if (grounded)
			{
				wake.UpdateTrail(tail);
			}
			else if (_wasGrounded)
			{
				wake.BreakTrail();
			}

			_wasGrounded = grounded;

			// Carving right throws water off the left rail and vice versa.
			float carve = _board.Carve;
			// Bouncing back off a wall the speed is negative.
			float speed = Mathf.Abs(_board.Speed);
			float baseSpray = speed * sprayPerSpeed;
			float t = 1f - Mathf.Exp(-rateSmoothing * Time.deltaTime);
			_leftRate = grounded ? Mathf.Lerp(_leftRate, baseSpray + Mathf.Max(0f, carve) * sprayPerCarve, t) : 0f;
			_rightRate = grounded ? Mathf.Lerp(_rightRate, baseSpray + Mathf.Max(0f, -carve) * sprayPerCarve, t) : 0f;
			_mistRate = grounded ? Mathf.Lerp(_mistRate, speed * mistPerSpeed + Mathf.Abs(carve) * mistPerCarve, t) : 0f;

			SetRate(leftSpray, _leftRate);
			SetRate(rightSpray, _rightRate);
			SetRate(mist, _mistRate);

			if (rideLoop != null)
			{
				float rideVolume = grounded ? Mathf.Min(speed * rideVolumePerSpeed, maxRideVolume) * _sound.EffectsVolume : 0f;
				rideLoop.volume = Mathf.Lerp(rideLoop.volume, rideVolume, t);
			}

			if (foam != null)
			{
				ParticleSystem.EmissionModule emission = foam.emission;
				emission.enabled = grounded;
				emission.rateOverTime = Mathf.Abs(carve) * foamPerCarve;
			}
		}

		private void OnLanded(float impactSpeed)
		{
			if (impactSpeed < minLandingSpeed)
			{
				return;
			}

			Vector3 position = SurfacePoint(transform.position);
			float strength = Mathf.Clamp01(impactSpeed / fullSplashSpeed);
			wake.AddSplash(position, strength);
			EmitSplash(position, Mathf.RoundToInt(impactSpeed * splashParticlesPerSpeed));
			PlaySplashSound(position, strength * splashVolume, 1f);
		}

		/// <summary>Splashes at a point on the water, such as where the rider falls in; strength is 0..1.</summary>
		public void Splash(Vector3 position, float strength)
		{
			wake.AddSplash(position, strength);
			EmitSplash(position, Mathf.RoundToInt(strength * fullSplashSpeed * splashParticlesPerSpeed));
			PlaySplashSound(position, strength * splashVolume, 1f);
		}

		private void OnJumped()
		{
			if (!_board.IsOnWater)
			{
				return;
			}
			Vector3 position = SurfacePoint(transform.TransformPoint(tailOffset));
			wake.AddSplash(position, 0.5f);
			EmitSplash(position, 25);
			PlaySplashSound(position, jumpSplashVolume, jumpSplashPitch);
		}

		private void EmitSplash(Vector3 position, int count)
		{
			if (splash == null)
			{
				return;
			}

			splash.transform.position = position;
			splash.Emit(count);

			// At the splash rather than behind the board, which may be elsewhere.
			var emitParams = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true };

			if (mist != null)
			{
				mist.Emit(emitParams, count / 6);
			}

			if (foam != null)
			{
				foam.Emit(emitParams, count / 4);
			}
		}

		private void PlaySplashSound(Vector3 position, float volume, float pitch)
		{
			if (splashClips == null || splashClips.Length == 0)
			{
				return;
			}

			if (Time.time < _nextSplashSoundTime && volume <= _lastSplashVolume)
			{
				return;
			}

			_nextSplashSoundTime = Time.time + splashSoundInterval;
			_lastSplashVolume = volume;

			int clip = Random.Range(0, splashClips.Length);
			if (clip == _lastSplashClip && splashClips.Length > 1)
			{
				clip = (clip + 1) % splashClips.Length;
			}

			_lastSplashClip = clip;
			_sound.PlayClipAtPosition(splashClips[clip], position, volume, pitch);
		}

		private static void SetRate(ParticleSystem system, float rate)
		{
			if (system == null)
			{
				return;
			}

			ParticleSystem.EmissionModule emission = system.emission;
			emission.rateOverTime = rate;
		}

		private Vector3 SurfacePoint(Vector3 position)
		{
			return new Vector3(position.x, water.GetHeight(position), position.z);
		}
	}
}
