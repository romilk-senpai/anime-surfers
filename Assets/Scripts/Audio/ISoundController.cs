using UnityEngine;

namespace Game.Audio
{
	public interface ISoundController
	{
		/// <summary>The player's effects volume, 0..1, for sources that set their own volume, like loops.</summary>
		float EffectsVolume { get; }

		void PlayOneShot(AudioClip clip);
		void PlayClipAtPosition(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f);
		void StartMusic();
		void StopMusic();
	}
}
