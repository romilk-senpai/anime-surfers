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
			if (musicAudioSource.isPlaying)
			{
				return;
			}

			musicAudioSource.time = 1.2f; // this no good but not enough to cut it myself
			musicAudioSource.Play();
			musicAudioSource.DOFade(MusicVolume, .5f);
		}

		public async void StopMusic()
		{
			await musicAudioSource.DOFade(0f, .5f);

			musicAudioSource.Stop();
		}

		public float EffectsVolume => _volume.Effects;

		public void PlayOneShot(AudioClip clip)
		{
			globalAudioSource.PlayOneShot(clip);
		}

		public async void PlayClipAtPosition(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
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
