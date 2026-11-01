using Game.Audio;
using UnityEngine;
using VContainer;

namespace Game.Surf
{
	/// <summary>
	/// Run sound cues carried over from the runner's main stage: music while running, a metal hit and a shout when
	/// bouncing off an obstacle's side, and a hit and a cry on the crash, which also fades the music out. Plus a chime
	/// for each coin picked up, a pickup sound for power-ups, a shatter when the shield breaks and a whoosh on super jumps.
	/// </summary>
	public class RunSounds : MonoBehaviour
	{
		[SerializeField] private AudioClip hitSound;
		[SerializeField] private AudioClip damageSound;
		[SerializeField] private AudioClip deathSound;
		[SerializeField] private AudioClip coinSound;
		[SerializeField] private AudioClip powerUpSound;
		[SerializeField] private AudioClip shieldBreakSound;
		[SerializeField] private AudioClip superJumpSound;
		[Tooltip("Height above the board the vocals play at.")]
		[SerializeField] private float voiceHeight = 1.4f;

		[Header("Volume")]
		[SerializeField, Range(0f, 1f)] private float hitVolume = 0.3f;
		[SerializeField, Range(0f, 1f)] private float voiceVolume = 0.6f;
		[SerializeField, Range(0f, 1f)] private float coinVolume = 0.2f;
		[SerializeField, Range(0f, 1f)] private float powerUpVolume = 0.5f;

		private ISoundController _sound;
		private BoardState _board;
		private IRunFlow _run;
		private RunState _runState;
		private PowerUps _powerUps;
		private PickupCollector _pickups;

		[Inject]
		public void Construct(ISoundController sound, BoardState board, IRunFlow run, RunState runState,
			PowerUps powerUps, PickupCollector pickups)
		{
			_sound = sound;
			_board = board;
			_run = run;
			_runState = runState;
			_powerUps = powerUps;
			_pickups = pickups;

			// Subscribed at injection, before the run starts.
			_run.PhaseChanged += OnPhaseChanged;
			_board.SideHit += OnSideHit;
			_board.Crashed += OnCrashed;
			_runState.CoinsChanged += OnCoinsChanged;
			_board.Jumped += OnJumped;
			_powerUps.ShieldBroken += OnShieldBroken;
			_pickups.Collected += OnPickupCollected;
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
			_sound.PlayClipAtPosition(hitSound, rider + _board.PathForward * 0.5f, hitVolume);
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

		private void OnShieldBroken()
		{
			if (shieldBreakSound != null)
			{
				_sound.PlayClipAtPosition(shieldBreakSound, RiderPosition() + _board.PathForward * 0.5f, powerUpVolume);
			}
		}

		private Vector3 RiderPosition()
		{
			return _board.PlanarPosition + Vector3.up * (_board.Height + voiceHeight);
		}
	}
}
