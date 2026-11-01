using Game.Surf;
using UnityEngine;
using VContainer;

namespace Game.Audio
{
	public class CharacterAnimationEventListener : MonoBehaviour
	{
		[SerializeField] private AudioClip[] footstepClips;
		[SerializeField] private AudioClip[] jumpClips;
		[SerializeField] private AudioClip[] landClips;
		[SerializeField, Range(0f, 1f)] private float footstepVolume = 1f;
		[SerializeField, Range(0f, 1f)] private float jumpVolume = 1f;
		[Tooltip("The landing clips are recorded much louder than the steps.")]
		[SerializeField, Range(0f, 1f)] private float landVolume = 0.35f;
		[Tooltip("Slowest fall onto a train roof or ramp that makes a landing sound.")]
		[SerializeField] private float minLandingSpeed = 2f;
		[Tooltip("A landing sounds once: the board touching down and the landing animation's own events within this " +
		         "many seconds of each other are one landing.")]
		[SerializeField] private float landSoundInterval = 0.3f;

		// Null until injected; the events are ignored outside a scope, like in a preview.
		private ISoundController _soundController;
		private BoardState _board;
		private int _lastRandFootstep = 0;
		private float _nextLandSoundTime;

		[Inject]
		public void Construct(ISoundController soundController, BoardState board)
		{
			_soundController = soundController;
			_board = board;
			_board.LandedOnSurface += OnLandedOnSurface;
		}

		private void OnDestroy()
		{
			if (_board != null)
			{
				_board.LandedOnSurface -= OnLandedOnSurface;
			}
		}

		// Covers landings the animator has no landing for, like coming down on a train from a jump off the water.
		private void OnLandedOnSurface(float impactSpeed)
		{
			// After a crash the board lands without her.
			if (impactSpeed >= minLandingSpeed && !_board.IsCrashed)
			{
				PlayLand();
			}
		}

		private void OnFootstepEvent(int foot)
		{
			if (_soundController == null)
			{
				return;
			}

			int rand = _lastRandFootstep;

			while (rand == _lastRandFootstep && footstepClips.Length > 1)
			{
				rand = Random.Range(0, footstepClips.Length);
			}

			Vector3 playPos = transform.position;

			playPos.x += 0.15f * (foot * 2 - 1);

			_soundController.PlayClipAtPosition(footstepClips[rand], playPos, footstepVolume);
		}

		private void OnJumpEvent()
		{
			if (_soundController == null)
			{
				return;
			}

			int rand = Random.Range(0, jumpClips.Length);

			_soundController.PlayClipAtPosition(jumpClips[rand], transform.position, jumpVolume);
		}

		private void OnLandEvent()
		{
			PlayLand();
		}

		private void PlayLand()
		{
			if (_soundController == null || Time.time < _nextLandSoundTime)
			{
				return;
			}

			_nextLandSoundTime = Time.time + landSoundInterval;
			int rand = Random.Range(0, landClips.Length);

			_soundController.PlayClipAtPosition(landClips[rand], transform.position, landVolume);
		}
	}
}
