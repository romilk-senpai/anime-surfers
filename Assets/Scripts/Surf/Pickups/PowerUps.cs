using System;
using UnityEngine;

namespace Game.Surf
{
    /// <summary>
    /// Timers of the active power-ups. Picking up one that is already active restarts its timer.
    /// </summary>
    public sealed class PowerUps
    {
        private readonly PickupSettings _settings;

        public PowerUps(PickupSettings settings)
        {
            _settings = settings;
        }

        public float MagnetRemaining { get; private set; }
        public float MultiplierRemaining { get; private set; }

        public bool IsMagnetActive => MagnetRemaining > 0f;
        public int ScoreMultiplier => MultiplierRemaining > 0f ? _settings.ScoreMultiplier : 1;

        /// <summary>Fired when a power-up starts or runs out.</summary>
        public event Action Changed;

        public void Activate(PickupKind kind)
        {
            switch (kind)
            {
                case PickupKind.Magnet:
                    MagnetRemaining = _settings.MagnetDuration;
                    break;
                case PickupKind.ScoreMultiplier:
                    MultiplierRemaining = _settings.MultiplierDuration;
                    break;
                default:
                    return;
            }

            Changed?.Invoke();
        }

        public void Tick(float dt)
        {
            bool wasMagnet = IsMagnetActive;
            bool wasMultiplier = MultiplierRemaining > 0f;

            MagnetRemaining = Mathf.Max(MagnetRemaining - dt, 0f);
            MultiplierRemaining = Mathf.Max(MultiplierRemaining - dt, 0f);

            if (wasMagnet != IsMagnetActive || wasMultiplier != MultiplierRemaining > 0f)
            {
                Changed?.Invoke();
            }
        }
    }
}
