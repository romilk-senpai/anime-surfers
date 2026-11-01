using System;
using System.Globalization;
using Game.Surf;
using Game.UI.Elements;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace Game.UI
{
	/// <summary>
	/// In-run HUD: score, distance, coins, nitro, power-up timers and the pause button, plus speed lines that
	/// flicker while the nitro boost is on.
	/// </summary>
	public sealed class HudPresenter : IScreen, IStartable, ITickable, IDisposable
	{
		private static readonly (string Name, PickupKind Kind)[] TimerElements =
		{
			("magnet-timer", PickupKind.Magnet),
			("multiplier-timer", PickupKind.ScoreMultiplier),
			("shield-timer", PickupKind.Shield),
			("super-jump-timer", PickupKind.SuperJump),
			("coin-doubler-timer", PickupKind.CoinDoubler)
		};

		private const float SpeedLinesFlickerInterval = 0.05f;

		private readonly RunState _run;
		private readonly PowerUps _powerUps;
		private readonly Nitro _nitro;
		private readonly BoardState _board;
		private readonly IRunFlow _runFlow;
		private readonly ScreenStack _screens;
		private readonly GameUi _ui;

		private VisualElement _screen;
		private Label _score;
		private Label _distance;
		private Label _coins;
		private VisualElement _nitroPanel;
		private VisualElement _nitroFill;
		private VisualElement _multiplierSticker;
		private FocusLines _speedLines;
		private float _nextSpeedLinesFlicker;
		private readonly RadialProgress[] _timers = new RadialProgress[TimerElements.Length];
		private Button _pauseButton;
		private int _meters = -1;

		public HudPresenter(RunState run, PowerUps powerUps, Nitro nitro, BoardState board, IRunFlow runFlow,
			ScreenStack screens, GameUi ui)
		{
			_run = run;
			_powerUps = powerUps;
			_nitro = nitro;
			_board = board;
			_runFlow = runFlow;
			_screens = screens;
			_ui = ui;
		}

		public void Start()
		{
			_run.ScoreChanged += SetScore;
			_run.CoinsChanged += SetCoins;
			_runFlow.PhaseChanged += OnPhaseChanged;
			OnPhaseChanged(_runFlow.Phase);
			_ui.RegisterReloadCallback(Bind);
		}

		public void Tick()
		{
			if (_screen == null)
			{
				return;
			}

			int meters = Mathf.FloorToInt(_board.Distance);

			if (meters != _meters)
			{
				_meters = meters;
				_distance.text = meters.ToString("N0", CultureInfo.InvariantCulture) + " m";
			}

			_nitroFill.style.width = Length.Percent(_nitro.Fill * 100f);
			_nitroPanel.EnableInClassList("nitro-panel--active", _nitro.IsActive);
			UpdateSpeedLines();

			for (int i = 0; i < _timers.Length; i++)
			{
				PickupKind kind = TimerElements[i].Kind;
				UpdateTimer(_timers[i], _powerUps.Remaining(kind), _powerUps.Duration(kind));
			}

			GameUi.SetVisible(_multiplierSticker, _powerUps.ScoreMultiplier > 1);
		}

		public void Dispose()
		{
			_run.ScoreChanged -= SetScore;
			_run.CoinsChanged -= SetCoins;
			_runFlow.PhaseChanged -= OnPhaseChanged;
			_ui.UnregisterReloadCallback(Bind);
			Unbind();
		}

		public void OnConfirm()
		{
		}

		public void OnBack()
		{
			_runFlow.Pause();
		}

		private void Bind(VisualElement root)
		{
			Unbind();

			_screen = root.Q("hud");
			_score = _screen.Q<Label>("score");
			_distance = _screen.Q<Label>("distance");
			_coins = _screen.Q<Label>("coins");
			_nitroPanel = _screen.Q("nitro-panel");
			_nitroFill = _screen.Q("nitro-fill");
			_multiplierSticker = _screen.Q("multiplier-sticker");
			_speedLines = _screen.Q<FocusLines>("speed-lines");
			_pauseButton = _screen.Q<Button>("pause-button");
			_pauseButton.clicked += _runFlow.Pause;

			for (int i = 0; i < _timers.Length; i++)
			{
				_timers[i] = _screen.Q<RadialProgress>(TimerElements[i].Name);
			}

			_meters = -1;
			SetScore(_run.Score);
			SetCoins(_run.Coins);
			OnPhaseChanged(_runFlow.Phase);
		}

		private void Unbind()
		{
			if (_pauseButton != null)
			{
				_pauseButton.clicked -= _runFlow.Pause;
			}
		}

		private void OnPhaseChanged(RunPhase phase)
		{
			bool visible = phase != RunPhase.Menu && phase != RunPhase.Results;

			if (visible)
			{
				_screens.Show(this);
			}
			else
			{
				_screens.Hide(this);
			}

			if (_screen == null)
			{
				return;
			}

			GameUi.SetVisible(_screen, visible);
			GameUi.SetVisible(_pauseButton, phase == RunPhase.Running);
		}

		private void SetScore(int score)
		{
			if (_score != null)
			{
				_score.text = score.ToString("000000");
			}
		}

		private void SetCoins(int coins)
		{
			if (_coins != null)
			{
				_coins.text = coins.ToString("N0", CultureInfo.InvariantCulture);
			}
		}

		private void UpdateSpeedLines()
		{
			float blend = _nitro.Blend;
			_speedLines.style.opacity = blend;
			GameUi.SetVisible(_speedLines, blend > 0f);

			if (blend > 0f && Time.time >= _nextSpeedLinesFlicker)
			{
				_nextSpeedLinesFlicker = Time.time + SpeedLinesFlickerInterval;
				_speedLines.Phase = UnityEngine.Random.value * Mathf.PI * 2f;
			}
		}

		private static void UpdateTimer(RadialProgress timer, float remaining, float duration)
		{
			GameUi.SetVisible(timer, remaining > 0f);
			timer.Value = duration > 0f ? remaining / duration : 0f;
		}
	}
}
