using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Game.Audio
{
    public class SoundController : MonoBehaviour, ISoundController
    {
        [SerializeField] private AudioSource globalAudioSource;
        [SerializeField] private AudioSource musicAudioSource;

        [SerializeField] private AudioSource pointAudioSourcePrefab;

        [SerializeField] private AudioClip musicClip;

        private Queue<AudioSource> _spawnedAudioSources;

        private void Start()
        {
            _spawnedAudioSources = new Queue<AudioSource>();
        }

        public void StartMusic()
        {
            if (musicAudioSource.isPlaying)
            {
                return;
            }

            musicAudioSource.time = 1.2f; // this no good but not enough to cut it myself
            musicAudioSource.Play();
            musicAudioSource.DOFade(.03f, .5f);
        }

        public async void StopMusic()
        {
            await musicAudioSource.DOFade(0f, .5f);
            
            musicAudioSource.Stop();
        }

        public void PlayOneShot(AudioClip clip)
        {
            globalAudioSource.PlayOneShot(clip);
        }

        public async void PlayClipAtPosition(AudioClip clip, Vector3 position)
        {
            if (!_spawnedAudioSources.TryDequeue(out AudioSource pointSource))
            {
                pointSource = Instantiate(pointAudioSourcePrefab);
            }
            pointSource.transform.position = position;
            pointSource.gameObject.SetActive(true);

            pointSource.PlayOneShot(clip);

            await UniTask.WaitForSeconds(clip.length);

            pointSource.gameObject.SetActive(false);
            _spawnedAudioSources.Enqueue(pointSource);
        }
    }
}