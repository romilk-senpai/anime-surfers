using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Core;
using UnityEngine;
using VContainer;

namespace Game.Audio
{
	public class SoundController : MonoBehaviour, ISoundController
	{
		/// <summary>
		/// Music volume at full music setting; the track is mastered loud. Balanced to sit a little above the water
		/// loop and the footsteps.
		/// </summary>
		private const float MusicLevel = .1f;

		[SerializeField] private AudioSource globalAudioSource;
		[SerializeField] private AudioSource musicAudioSource;

		[SerializeField] private AudioSource pointAudioSourcePrefab;

		[SerializeField] private AudioClip musicClip;

		private Queue<AudioSource> _spawnedAudioSources;
		private IVolumeSettings _volume;
		// The fade before stopping the music; cleared when the music is started again, so that stop is skipped.
		private Tween _musicFadeOut;

		[Inject]
		public void Construct(IVolumeSettings volume)
		{
			_volume = volume;
			_volume.Changed += ApplyVolume;
			ApplyVolume();
		}

		private void Start()
		{
			_spawnedAudioSources = new Queue<AudioSource>();
		}

		private void OnDestroy()
		{
			if (_volume != null)
			{
				_volume.Changed -= ApplyVolume;
			}
		}

		public void StartMusic()
		{
			if (_musicFadeOut != null)
			{
				// Still fading out: cancel the stop and fade back in. Cleared before the kill, which resumes the
				// pending stop right away, so it sees it was cancelled.
				Tween fadeOut = _musicFadeOut;
				_musicFadeOut = null;
				fadeOut.Kill();
				musicAudioSource.DOFade(MusicVolume, .5f);
				return;
			}

			if (musicAudioSource.isPlaying)
			{
				return;
			}

			musicAudioSource.time = 1.2f; // this no good but not enough to cut it myself
			musicAudioSource.Play();
			musicAudioSource.DOFade(MusicVolume, .5f);
		}

		public void StopMusic()
		{
			StopMusicAsync().Forget();
		}

		private async UniTaskVoid StopMusicAsync()
		{
			Tween fadeOut = musicAudioSource.DOFade(0f, .5f);
			_musicFadeOut = fadeOut;
			// Also resumes when the fade is killed.
			await fadeOut;

			// Started again, or stopped again by a newer fade.
			if (_musicFadeOut != fadeOut)
			{
				return;
			}

			_musicFadeOut = null;
			musicAudioSource.Stop();
		}

		public float EffectsVolume => _volume.Effects;

		public void PlayOneShot(AudioClip clip)
		{
			globalAudioSource.PlayOneShot(clip);
		}

		public void PlayClipAtPosition(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
		{
			PlayClipAtPositionAsync(clip, position, volume, pitch).Forget();
		}

		private async UniTaskVoid PlayClipAtPositionAsync(AudioClip clip, Vector3 position, float volume, float pitch)
		{
			if (!_spawnedAudioSources.TryDequeue(out AudioSource pointSource))
			{
				pointSource = Instantiate(pointAudioSourcePrefab);
			}
			pointSource.transform.position = position;
			pointSource.gameObject.SetActive(true);

			pointSource.pitch = pitch;
			pointSource.PlayOneShot(clip, volume * _volume.Effects);

			bool isCancelled = await UniTask.WaitForSeconds(clip.length / pitch, cancellationToken: destroyCancellationToken)
				.SuppressCancellationThrow();

			if (isCancelled)
			{
				return;
			}

			pointSource.gameObject.SetActive(false);
			_spawnedAudioSources.Enqueue(pointSource);
		}

		private float MusicVolume => MusicLevel * _volume.Music;

		private void ApplyVolume()
		{
			AudioListener.volume = _volume.Muted ? 0f : 1f;
			globalAudioSource.volume = _volume.Effects;

			if (musicAudioSource.isPlaying)
			{
				musicAudioSource.DOKill();
				musicAudioSource.volume = MusicVolume;
			}
		}
	}
}
