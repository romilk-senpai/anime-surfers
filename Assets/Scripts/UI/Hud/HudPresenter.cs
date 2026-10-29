using System;
using Game.Surf;
using UnityEngine;
using VContainer.Unity;

namespace Game.UI
{
    public sealed class HudPresenter : IStartable, ITickable, IDisposable
    {
        private readonly RunState _run;
        private readonly PowerUps _powerUps;
        private readonly HudView _view;
        private int _magnetSeconds = -1;
        private int _multiplierSeconds = -1;

        public HudPresenter(RunState run, PowerUps powerUps, HudView view)
        {
            _run = run;
            _powerUps = powerUps;
            _view = view;
        }

        public void Start()
        {
            _view.SetScore(_run.Score);
            _view.SetCoins(_run.Coins);
            _run.ScoreChanged += _view.SetScore;
            _run.CoinsChanged += _view.SetCoins;
        }

        public void Tick()
        {
            // Countdowns are shown in whole seconds; only refresh the text when one changes.
            int magnetSeconds = Mathf.CeilToInt(_powerUps.MagnetRemaining);
            int multiplierSeconds = Mathf.CeilToInt(_powerUps.MultiplierRemaining);

            if (magnetSeconds == _magnetSeconds && multiplierSeconds == _multiplierSeconds)
            {
                return;
            }

            _magnetSeconds = magnetSeconds;
            _multiplierSeconds = multiplierSeconds;
            _view.SetPowerUps(magnetSeconds, multiplierSeconds, _powerUps.ScoreMultiplier);
        }

        public void Dispose()
        {
            _run.ScoreChanged -= _view.SetScore;
            _run.CoinsChanged -= _view.SetCoins;
        }
    }
}
