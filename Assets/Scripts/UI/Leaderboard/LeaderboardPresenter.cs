using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Online;
using UnityEngine.Localization;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace Game.UI
{
	/// <summary>
	/// The online leaderboard over the main menu: the top of the board on playnesc.com with the player's own row
	/// highlighted, and the way to sign in.
	/// </summary>
	public sealed class LeaderboardPresenter : IScreen, IStartable, IDisposable
	{
		private const string Table = "UI";
		private const int Limit = 50;
		private const string NoticeShownClass = "leaderboard__notice--shown";

		private readonly ILeaderboard _leaderboard;
		private readonly ScreenStack _screens;
		private readonly GameUi _ui;

		private VisualElement _screen;
		private VisualElement _signedIn;
		private VisualElement _signedOut;
		private Label _playerName;
		private Label _popupBlocked;
		private Button _signIn;
		private ScrollView _rows;
		private Label _status;
		private Button _done;
		private bool _isOpen;
		private CancellationTokenSource _loading;

		public LeaderboardPresenter(ILeaderboard leaderboard, ScreenStack screens, GameUi ui)
		{
			_leaderboard = leaderboard;
			_screens = screens;
			_ui = ui;
		}

		/// <summary>After the screen is closed, so the screen below can take the focus back.</summary>
		public event Action Closed;

		public void Start()
		{
			_leaderboard.Changed += OnLeaderboardChanged;
			_ui.RegisterReloadCallback(Bind);
		}

		public void Dispose()
		{
			_leaderboard.Changed -= OnLeaderboardChanged;
			_ui.UnregisterReloadCallback(Bind);
			Unbind();
			CancelLoading();
		}

		public void Open()
		{
			if (_screen == null)
			{
				return;
			}

			_isOpen = true;
			_popupBlocked.RemoveFromClassList(NoticeShownClass);
			Refresh();
			_screens.Show(this);
			_done.Focus();
			LoadAsync().Forget();
		}

		public void Close()
		{
			if (!_isOpen)
			{
				return;
			}

			_isOpen = false;
			CancelLoading();
			_screens.Hide(this);

			if (_screen != null)
			{
				GameUi.SetVisible(_screen, false);
			}

			Closed?.Invoke();
		}

		public void OnConfirm()
		{
		}

		public void OnBack()
		{
			Close();
		}

		private void Bind(VisualElement root)
		{
			Unbind();

			_screen = root.Q("leaderboard");
			_signedIn = _screen.Q("signed-in");
			_signedOut = _screen.Q("signed-out");
			_playerName = _screen.Q<Label>("player-name");
			_popupBlocked = _screen.Q<Label>("popup-blocked");
			_signIn = _screen.Q<Button>("sign-in-button");
			_rows = _screen.Q<ScrollView>("rows");
			_status = _screen.Q<Label>("status");
			_done = _screen.Q<Button>("done-button");

			// Names are typed on the site; don't let one read as rich-text tags.
			_playerName.enableRichText = false;

			_done.clicked += Close;
			_signIn.clicked += SignIn;

			Refresh();

			if (_isOpen)
			{
				LoadAsync().Forget();
			}
		}

		private void Unbind()
		{
			if (_screen == null)
			{
				return;
			}

			_done.clicked -= Close;
			_signIn.clicked -= SignIn;
		}

		private void Refresh()
		{
			UpdateAccount();
			GameUi.SetVisible(_screen, _isOpen);
		}

		private void UpdateAccount()
		{
			LeaderboardPlayer player = _leaderboard.Player;
			GameUi.SetVisible(_signedIn, player != null);
			GameUi.SetVisible(_signedOut, player == null && _leaderboard.CanSignIn);
			_playerName.text = player?.Name ?? string.Empty;

			if (player != null)
			{
				_popupBlocked.RemoveFromClassList(NoticeShownClass);
			}
		}

		private void SignIn()
		{
			_popupBlocked.EnableInClassList(NoticeShownClass, !_leaderboard.SignIn());
		}

		private void OnLeaderboardChanged()
		{
			if (_screen == null)
			{
				return;
			}

			bool signInHadFocus = _signIn.focusController?.focusedElement == _signIn;
			UpdateAccount();

			if (signInHadFocus && _leaderboard.Player != null)
			{
				_done.Focus();
			}

			// Signing in or a new score changes which row is the player's and where it sits.
			if (_isOpen)
			{
				LoadAsync().Forget();
			}
		}

		private async UniTaskVoid LoadAsync()
		{
			CancelLoading();
			_loading = new CancellationTokenSource();
			CancellationToken cancellation = _loading.Token;

			// Rows already shown stay up while a reload is on its way.
			if (_rows.childCount == 0)
			{
				SetStatus("leaderboard.loading");
			}

			IReadOnlyList<LeaderboardEntry> entries;

			try
			{
				entries = await _leaderboard.GetTopAsync(Limit, cancellation);
			}
			catch (OperationCanceledException)
			{
				return;
			}

			if (cancellation.IsCancellationRequested || _screen == null)
			{
				return;
			}

			_rows.Clear();

			if (entries == null)
			{
				SetStatus("leaderboard.error");
				return;
			}

			if (entries.Count == 0)
			{
				SetStatus("leaderboard.empty");
				return;
			}

			SetStatus(null);
			string me = _leaderboard.Player?.Uid;

			foreach (LeaderboardEntry entry in entries)
			{
				var row = new VisualElement { pickingMode = PickingMode.Ignore };
				row.AddToClassList("leaderboard-row");
				row.EnableInClassList("leaderboard-row--me", entry.Uid == me);
				row.Add(MakeLabel("#" + Format(entry.Rank), "leaderboard-row__rank"));
				row.Add(MakeLabel(entry.Name, "leaderboard-row__name"));
				row.Add(MakeLabel(Format(entry.Score), "leaderboard-row__score"));
				_rows.Add(row);
			}
		}

		private void CancelLoading()
		{
			if (_loading == null)
			{
				return;
			}

			_loading.Cancel();
			_loading.Dispose();
			_loading = null;
		}

		/// <summary>Shows the status line with a localized message, or hides it for null.</summary>
		private void SetStatus(string entry)
		{
			GameUi.SetVisible(_status, entry != null);

			if (entry != null)
			{
				_status.SetBinding("text", new LocalizedString(Table, entry));
			}
		}

		/// <summary>
		/// A row label in the display font, which has the Cyrillic that names typed on the site often use. Rich text
		/// is off so a name can't read as tags.
		/// </summary>
		private static Label MakeLabel(string text, string className)
		{
			var label = new Label(text) { pickingMode = PickingMode.Ignore, enableRichText = false };
			label.AddToClassList(className);
			label.AddToClassList("display");
			return label;
		}

		private static string Format(int value)
		{
			return value.ToString("N0", CultureInfo.InvariantCulture);
		}
	}
}
