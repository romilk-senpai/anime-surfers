using UnityEngine;
using Zenject;

namespace Game.Audio
{
    public class CharacterAnimationEventListener : MonoBehaviour
    {
        [Inject]
        private ISoundController _soundController;

        [SerializeField] private AudioClip[] footstepClips;
        [SerializeField] private AudioClip[] jumpClips;
        [SerializeField] private AudioClip[] landClips;

        private int _lastRandFootstep = 0;

        private void OnFootstepEvent(int foot)
        {
            int rand = _lastRandFootstep;

            while (rand == _lastRandFootstep && footstepClips.Length > 1)
            {
                rand = Random.Range(0, footstepClips.Length);
            }

            Vector3 playPos = transform.position;

            playPos.x += 0.15f * (foot * 2 - 1);

            _soundController.PlayClipAtPosition(footstepClips[rand], playPos);
        }

        private void OnJumpEvent()
        {
            int rand = Random.Range(0, jumpClips.Length);

            _soundController.PlayClipAtPosition(jumpClips[rand], transform.position);
        }

        private void OnLandEvent()
        {
            int rand = Random.Range(0, landClips.Length);

            _soundController.PlayClipAtPosition(landClips[rand], transform.position);
        }
    }
}
