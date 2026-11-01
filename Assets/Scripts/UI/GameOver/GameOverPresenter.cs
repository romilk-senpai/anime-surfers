using System;
using System.Globalization;
using Game.Core;
using Game.Surf;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace Game.UI
{
	/// <summary>
	/// Results after a crash: what was hit and how to avoid it, the run's numbers, Surf again or Menu.
	/// </summary>
	public sealed class GameOverPresenter : IScreen, IStartable, IDisposable
	{
		private const string Table = "UI";

		/// <summary>Matches the scale transition on .sound-effect, so each swing finishes before the next.</summary>
		private const long PulseIntervalMs = 450;

		private readonly IRunFlow _runFlow;
		private readonly RunState _run;
		private readonly BoardState _board;
		private readonly IPlayerProgress _progress;
		private readonly ScreenStack _screens;
		private readonly GameUi _ui;

		private VisualElement _screen;
		private Button _surfAgainButton;
		private Button _menuButton;
		private Label _soundEffect;
		private IVisualElementScheduledItem _pulse;

		public GameOverPresenter(IRunFlow runFlow, RunState run, BoardState board, IPlayerProgress progress,
			ScreenStack screens, GameUi ui)
		{
			_runFlow = runFlow;
			_run = run;
			_board = board;
			_progress = progress;
			_screens = screens;
			_ui = ui;
		}

		public void Start()
		{
			_runFlow.PhaseChanged += OnPhaseChanged;
			OnPhaseChanged(_runFlow.Phase);
			_ui.RegisterReloadCallback(Bind);
		}

		public void Dispose()
		{
			_runFlow.PhaseChanged -= OnPhaseChanged;
			_ui.UnregisterReloadCallback(Bind);
			Unbind();
		}

		public void OnConfirm()
		{
			_runFlow.Restart();
		}

		public void OnBack()
		{
		}

		private void Bind(VisualElement root)
		{
			Unbind();

			_screen = root.Q("game-over");
			_surfAgainButton = _screen.Q<Button>("surf-again-button");
			_menuButton = _screen.Q<Button>("menu-button");
			_soundEffect = _screen.Q<Label>("sound-effect");
			_pulse = GameUi.Pulse(_soundEffect, "sound-effect--big", PulseIntervalMs);

			_surfAgainButton.clicked += _runFlow.Restart;
			_menuButton.clicked += _runFlow.QuitToMenu;

			OnPhaseChanged(_runFlow.Phase);
		}

		private void Unbind()
		{
			if (_surfAgainButton == null)
			{
				return;
			}

			_surfAgainButton.clicked -= _runFlow.Restart;
			_menuButton.clicked -= _runFlow.QuitToMenu;
			_pulse.Pause();
		}

		private void OnPhaseChanged(RunPhase phase)
		{
			bool visible = phase == RunPhase.Results;

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

			if (visible)
			{
				Fill();
			}

			GameUi.SetVisible(_screen, visible);

			if (visible)
			{
				_surfAgainButton.Focus();
				_pulse.Resume();
			}
			else
			{
				_pulse.Pause();
			}
		}

		private void Fill()
		{
			bool barrier = _board.CrashKind == CrashKind.Barrier;
			string crash = barrier ? "crash.barrier" : "crash.wall";

			// The sound effect is lettering, not copy: it stays Japanese in every language.
			_soundEffect.text = barrier ? "ガツン!" : "ドカン!";
			_screen.Q<Label>("crash-title").SetBinding("text", new LocalizedString(Table, crash + ".title"));
			// The desktop tips name keys; the touch ones name swipes.
			string tip = crash + (_ui.IsMobile ? ".tip.touch" : ".tip");
			_screen.Q<Label>("crash-tip").SetBinding("text", new LocalizedString(Table, tip));

			_screen.Q<Label>("score").text = Format(_run.Score);
			_screen.Q<Label>("distance").text = Format(Mathf.FloorToInt(_board.Distance)) + " m";
			_screen.Q<Label>("coins").text = Format(_run.Coins);
			_screen.Q<Label>("best").text = Format(_progress.BestScore);
			GameUi.SetVisible(_screen.Q("new-best"), _run.IsNewBest);
		}

		private static string Format(int value)
		{
			return value.ToString("N0", CultureInfo.InvariantCulture);
		}
	}
}
