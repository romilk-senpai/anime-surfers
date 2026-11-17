using System;
using System.Globalization;
using Game.Core;
using Game.Online;
using Game.Surf;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace Game.UI
{
	/// <summary>
	/// Results after a crash: what was hit and how to avoid it, the run's numbers, the online rank (or signing in to get
	/// one), Surf again, Menu or the leaderboard.
	/// </summary>
	public sealed class GameOverPresenter : IScreen, IStartable, IDisposable
	{
		private const string Table = "UI";

		/// <summary>Matches the scale transition on .sound-effect, so each swing finishes before the next.</summary>
		private const long PulseIntervalMs = 450;

		private const string NoticeShownClass = "results__notice--shown";

		private readonly IRunFlow _runFlow;
		private readonly RunState _run;
		private readonly IBoardState _board;
		private readonly IPlayerProgress _progress;
		private readonly ILeaderboard _leaderboard;
		private readonly LeaderboardPresenter _leaderboardScreen;
		private readonly ScreenStack _screens;
		private readonly GameUi _ui;

		private VisualElement _screen;
		private Button _surfAgainButton;
		private Button _menuButton;
		private Button _leaderboardButton;
		private Label _soundEffect;
		private VisualElement _online;
		private VisualElement _onlineRank;
		private Label _rank;
		private Button _signInButton;
		private Label _popupBlocked;
		private IVisualElementScheduledItem _pulse;

		public GameOverPresenter(IRunFlow runFlow, RunState run, IBoardState board, IPlayerProgress progress,
			ILeaderboard leaderboard, LeaderboardPresenter leaderboardScreen, ScreenStack screens, GameUi ui)
		{
			_runFlow = runFlow;
			_run = run;
			_board = board;
			_progress = progress;
			_leaderboard = leaderboard;
			_leaderboardScreen = leaderboardScreen;
			_screens = screens;
			_ui = ui;
		}

		public void Start()
		{
			_runFlow.PhaseChanged += OnPhaseChanged;
			_leaderboard.Changed += UpdateOnline;
			_leaderboardScreen.Closed += OnLeaderboardClosed;
			OnPhaseChanged(_runFlow.Phase);
			_ui.RegisterReloadCallback(Bind);
		}

		public void Dispose()
		{
			_runFlow.PhaseChanged -= OnPhaseChanged;
			_leaderboard.Changed -= UpdateOnline;
			_leaderboardScreen.Closed -= OnLeaderboardClosed;
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
			_leaderboardButton = _screen.Q<Button>("leaderboard-button");
			_soundEffect = _screen.Q<Label>("sound-effect");
			_pulse = GameUi.Pulse(_soundEffect, "sound-effect--big", PulseIntervalMs);
			_online = _screen.Q("online");
			_onlineRank = _screen.Q("online-rank");
			_rank = _screen.Q<Label>("rank");
			_signInButton = _screen.Q<Button>("sign-in-button");
			_popupBlocked = _screen.Q<Label>("popup-blocked");

			_surfAgainButton.clicked += _runFlow.Restart;
			_menuButton.clicked += _runFlow.QuitToMenu;
			_leaderboardButton.clicked += _leaderboardScreen.Open;
			_signInButton.clicked += SignIn;

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
			_leaderboardButton.clicked -= _leaderboardScreen.Open;
			_signInButton.clicked -= SignIn;
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
			(string crash, string soundEffect) = _board.CrashKind switch
			{
				CrashKind.Barrier => ("crash.barrier", "ガツン!"),
				CrashKind.Wipeout => ("crash.wipeout", "ザバーン!"),
				_ => ("crash.wall", "ドカン!")
			};

			// The sound effect is lettering, not copy: it stays Japanese in every language.
			_soundEffect.text = soundEffect;
			_screen.Q<Label>("crash-title").SetBinding("text", new LocalizedString(Table, crash + ".title"));
			// The desktop tips name keys; the touch ones name swipes.
			string tip = crash + (_ui.IsMobile ? ".tip.touch" : ".tip");
			_screen.Q<Label>("crash-tip").SetBinding("text", new LocalizedString(Table, tip));

			_screen.Q<Label>("score").text = Format(_run.Score);
			_screen.Q<Label>("distance").text = Format(Mathf.FloorToInt(_board.Distance)) + " m";
			_screen.Q<Label>("coins").text = Format(_run.Coins);
			_screen.Q<Label>("best").text = Format(_progress.BestScore);
			GameUi.SetVisible(_screen.Q("new-best"), _run.IsNewBest);
			_popupBlocked.RemoveFromClassList(NoticeShownClass);
			UpdateOnline();
		}

		/// <summary>
		/// Signed in: the rank once the run has been sent ("…" while it's on its way). Signed out: the sign-in button;
		/// the run is held and sent once the player signs in. Hidden in builds that can't sign in.
		/// </summary>
		private void UpdateOnline()
		{
			if (_online == null)
			{
				return;
			}

			GameUi.SetVisible(_online, _leaderboard.CanSignIn);

			bool signedIn = _leaderboard.Player != null;
			LeaderboardStanding standing = _leaderboard.Standing;
			bool signInHadFocus = _signInButton.focusController?.focusedElement == _signInButton;

			GameUi.SetVisible(_signInButton, !signedIn);
			GameUi.SetVisible(_onlineRank, signedIn && (standing != null || _leaderboard.HasPendingRun));
			_rank.text = standing != null ? "#" + Format(standing.Rank) : "…";

			if (signedIn)
			{
				_popupBlocked.RemoveFromClassList(NoticeShownClass);

				// The sign-in button is gone; Space still means Surf again.
				if (signInHadFocus)
				{
					_surfAgainButton.Focus();
				}
			}
		}

		private void OnLeaderboardClosed()
		{
			if (_runFlow.Phase == RunPhase.Results)
			{
				_leaderboardButton.Focus();
			}
		}

		private void SignIn()
		{
			_popupBlocked.EnableInClassList(NoticeShownClass, !_leaderboard.SignIn());
		}

		private static string Format(int value)
		{
			return value.ToString("N0", CultureInfo.InvariantCulture);
		}
	}
}
