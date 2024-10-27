using UnityEngine;

namespace Game.Audio
{
    public interface ISoundController
    {
        void PlayOneShot(AudioClip clip);
        void PlayClipAtPosition(AudioClip clip, Vector3 position);
        void StartMusic();
        void StopMusic();
    }
}
