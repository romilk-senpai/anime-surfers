using UnityEngine;

namespace Game.Audio
{
    public interface ISoundController
    {
        void PlayOneShot(AudioClip clip);
        void PlayClipAtPosition(AudioClip clip, Vector3 position, float volume = 1f);
        void StartMusic();
        void StopMusic();
    }
}
