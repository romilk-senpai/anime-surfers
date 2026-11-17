using Game.Audio;
using UnityEngine;
using VContainer;

namespace Game.Surf
{
	/// <summary>
	/// Run sound cues carried over from the runner's main stage: music while running, a metal hit and a shout when
	/// bouncing off an obstacle's side, and a hit and a cry on the crash, which also fades the music out (a big wave's
	/// wipeout brings its own splash instead of the hit). Plus a chime
	/// for each coin picked up, a pickup sound for power-ups, the side-hit sounds when the shield breaks and a whoosh on
	/// super jumps and glider take-offs, and wind rushing past her while she glides.
	/// </summary>
	public class RunSounds : MonoBehaviour
	{
		[SerializeField] private AudioClip hitSound;
		[SerializeField] private AudioClip damageSound;
		[SerializeField] private AudioClip deathSound;
		[SerializeField] private AudioClip coinSound;
		[SerializeField] private AudioClip powerUpSound;
		[SerializeField] private AudioClip superJumpSound;
		[Tooltip("Height above the board the vocals play at.")]
		[SerializeField] private float voiceHeight = 1.4f;

		[Header("Glide")]
		[Tooltip("Looping wind, faded in through the flight and cut when the glider is gone.")]
		[SerializeField] private AudioClip glideWindLoop;
		[SerializeField, Range(0f, 1f)] private float glideWindVolume = 0.45f;
		[SerializeField] private float glideWindFadeIn = 0.6f;
		[SerializeField] private float glideWindFadeOut = 0.15f;

		[Header("Volume")]
		[SerializeField, Range(0f, 1f)] private float hitVolume = 0.3f;
		[SerializeField, Range(0f, 1f)] private float voiceVolume = 0.6f;
		[SerializeField, Range(0f, 1f)] private float coinVolume = 0.2f;
		[SerializeField, Range(0f, 1f)] private float powerUpVolume = 0.5f;

		private ISoundController _sound;
		private IBoardState _board;
		private IRunFlow _run;
		private RunState _runState;
		private PowerUps _powerUps;
		private PickupCollector _pickups;
		private GlideFlight _glide;
		private AudioSource _wind;
		private float _windLevel;

		[Inject]
		public void Construct(ISoundController sound, IBoardState board, IRunFlow run, RunState runState,
			PowerUps powerUps, PickupCollector pickups, GlideFlight glide)
		{
			_sound = sound;
			_board = board;
			_run = run;
			_runState = runState;
			_powerUps = powerUps;
			_pickups = pickups;
			_glide = glide;

			// Subscribed at injection, before the run starts.
			_run.PhaseChanged += OnPhaseChanged;
			_board.SideHit += OnSideHit;
			_board.Crashed += OnCrashed;
			_runState.CoinsChanged += OnCoinsChanged;
			_board.Jumped += OnJumped;
			_powerUps.ShieldBroken += OnShieldBroken;
			_pickups.Collected += OnPickupCollected;
			_glide.Started += OnGlideStarted;
		}

		private void OnDestroy()
		{
			if (_run == null)
			{
				return;
			}

			_run.PhaseChanged -= OnPhaseChanged;
			_board.SideHit -= OnSideHit;
			_board.Crashed -= OnCrashed;
			_runState.CoinsChanged -= OnCoinsChanged;
			_board.Jumped -= OnJumped;
			_powerUps.ShieldBroken -= OnShieldBroken;
			_pickups.Collected -= OnPickupCollected;
			_glide.Started -= OnGlideStarted;
		}

		private void Awake()
		{
			if (glideWindLoop == null)
			{
				return;
			}

			_wind = gameObject.AddComponent<AudioSource>();
			_wind.clip = glideWindLoop;
			_wind.loop = true;
			_wind.playOnAwake = false;
			_wind.spatialBlend = 0f;
			_wind.volume = 0f;
		}

		private void Update()
		{
			if (_wind == null || _board == null)
			{
				return;
			}

			bool gliding = _board.IsGliding && !_board.IsCrashed;
			float fade = gliding ? glideWindFadeIn : glideWindFadeOut;
			_windLevel = Mathf.MoveTowards(_windLevel, gliding ? 1f : 0f, Time.deltaTime / Mathf.Max(fade, 0.01f));
			_wind.volume = _windLevel * glideWindVolume * _sound.EffectsVolume;

			if (_windLevel > 0f && !_wind.isPlaying)
			{
				_wind.Play();
			}
			else if (_windLevel <= 0f && _wind.isPlaying)
			{
				_wind.Stop();
			}
		}

		private void OnPhaseChanged(RunPhase phase)
		{
			if (phase == RunPhase.Running)
			{
				_sound.StartMusic();
			}
			else if (phase == RunPhase.Crashed)
			{
				_sound.StopMusic();
			}
		}

		private void OnSideHit(int side)
		{
			Vector3 rider = RiderPosition();
			// On the side that hit.
			_sound.PlayClipAtPosition(hitSound, rider + _board.PathRight * (side * 0.3f), hitVolume);
			_sound.PlayClipAtPosition(damageSound, rider, voiceVolume);
		}

		private void OnCrashed()
		{
			Vector3 rider = RiderPosition();

			if (_board.CrashKind != CrashKind.Wipeout)
			{
				_sound.PlayClipAtPosition(hitSound, rider + _board.PathForward * 0.5f, hitVolume);
			}

			_sound.PlayClipAtPosition(deathSound, rider, voiceVolume);
		}

		private void OnCoinsChanged(int coins)
		{
			if (coinSound != null)
			{
				_sound.PlayClipAtPosition(coinSound, RiderPosition(), coinVolume);
			}
		}

		private void OnPickupCollected(PickupKind kind)
		{
			if (powerUpSound != null && kind != PickupKind.Coin)
			{
				_sound.PlayClipAtPosition(powerUpSound, RiderPosition(), powerUpVolume);
			}
		}

		private void OnJumped()
		{
			if (superJumpSound != null && _powerUps.IsActive(PickupKind.SuperJump))
			{
				_sound.PlayClipAtPosition(superJumpSound, RiderPosition(), powerUpVolume);
			}
		}

		private void OnGlideStarted()
		{
			if (superJumpSound != null)
			{
				_sound.PlayClipAtPosition(superJumpSound, RiderPosition(), powerUpVolume);
			}
		}

		private void OnShieldBroken()
		{
			Vector3 rider = RiderPosition();
			_sound.PlayClipAtPosition(hitSound, rider + _board.PathForward * 0.5f, hitVolume);
			_sound.PlayClipAtPosition(damageSound, rider, voiceVolume);
		}

		private Vector3 RiderPosition()
		{
			return _board.PlanarPosition + Vector3.up * (_board.Height + voiceHeight);
		}
	}
}
