using Game.Audio;
using UnityEngine;
using VContainer;

namespace Game.Surf
{
    /// <summary>
    /// Run sound cues carried over from the runner's main stage: music while running, a metal hit and a shout when
    /// bouncing off an obstacle's side, and a hit and a cry on the crash, which also fades the music out.
    /// </summary>
    public class RunSounds : MonoBehaviour
    {
        [SerializeField] private AudioClip hitSound;
        [SerializeField] private AudioClip damageSound;
        [SerializeField] private AudioClip deathSound;
        [Tooltip("Height above the board the vocals play at.")]
        [SerializeField] private float voiceHeight = 1.4f;

        private ISoundController _sound;
        private BoardState _board;
        private RunStateMachine _run;

        [Inject]
        public void Construct(ISoundController sound, BoardState board, RunStateMachine run)
        {
            _sound = sound;
            _board = board;
            _run = run;

            // Subscribed at injection, before the run starts.
            _run.PhaseChanged += OnPhaseChanged;
            _board.SideHit += OnSideHit;
            _board.Crashed += OnCrashed;
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
            _sound.PlayClipAtPosition(hitSound, rider + _board.PathRight * (side * 0.3f));
            _sound.PlayClipAtPosition(damageSound, rider);
        }

        private void OnCrashed()
        {
            Vector3 rider = RiderPosition();
            _sound.PlayClipAtPosition(hitSound, rider + _board.PathForward * 0.5f);
            _sound.PlayClipAtPosition(deathSound, rider);
        }

        private Vector3 RiderPosition()
        {
            return _board.PlanarPosition + Vector3.up * (_board.Height + voiceHeight);
        }
    }
}
