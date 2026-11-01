using System;
using System.Globalization;
using Game.Core;
using Game.Surf;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace Game.UI
{
	/// <summary>
	/// Main menu over the floating board: best score, total coins, Surf, sound toggle and the shop and settings
	/// entries.
	/// </summary>
	public sealed class MenuPresenter : IScreen, IStartable, IDisposable
	{
		/// <summary>Matches the scale transition on .menu__title.</summary>
		private const long TitlePulseIntervalMs = 600;

		private readonly IRunFlow _runFlow;
		private readonly IPlayerProgress _progress;
		private readonly IVolumeSettings _volume;
		private readonly SettingsPresenter _settings;
		private readonly ShopPresenter _shop;
		private readonly ScreenStack _screens;
		private readonly GameUi _ui;

		private VisualElement _screen;
		private Button _surfButton;
		private Button _settingsButton;
		private Button _shopButton;
		private Label _totalCoins;
		private Button _soundButton;
		private VisualElement _soundIcon;
		private IVisualElementScheduledItem _titlePulse;

		public MenuPresenter(IRunFlow runFlow, IPlayerProgress progress, IVolumeSettings volume,
			SettingsPresenter settings, ShopPresenter shop, ScreenStack screens, GameUi ui)
		{
			_runFlow = runFlow;
			_progress = progress;
			_volume = volume;
			_settings = settings;
			_shop = shop;
			_screens = screens;
			_ui = ui;
		}

		public void Start()
		{
			_volume.Changed += UpdateSoundIcon;
			_shop.Closed += OnShopClosed;
			_runFlow.PhaseChanged += OnPhaseChanged;
			OnPhaseChanged(_runFlow.Phase);
			_ui.RegisterReloadCallback(Bind);
		}

		public void Dispose()
		{
			_volume.Changed -= UpdateSoundIcon;
			_shop.Closed -= OnShopClosed;
			_runFlow.PhaseChanged -= OnPhaseChanged;
			_ui.UnregisterReloadCallback(Bind);
			Unbind();
		}

		public void OnConfirm()
		{
			_runFlow.StartRun();
		}

		public void OnBack()
		{
		}

		private void Bind(VisualElement root)
		{
			Unbind();

			_screen = root.Q("menu");
			_surfButton = _screen.Q<Button>("surf-button");
			_settingsButton = _screen.Q<Button>("settings-button");
			_shopButton = _screen.Q<Button>("shop-button");
			_totalCoins = _screen.Q<Label>("total-coins");
			_soundButton = _screen.Q<Button>("sound-button");
			_soundIcon = _screen.Q("sound-icon");
			_titlePulse = GameUi.Pulse(_screen.Q("title"), "menu__title--big", TitlePulseIntervalMs);

			_screen.Q<Label>("best-score").text = _progress.BestScore.ToString("N0", CultureInfo.InvariantCulture);
			UpdateTotalCoins();

			_surfButton.clicked += _runFlow.StartRun;
			_settingsButton.clicked += _settings.Open;
			_shopButton.clicked += _shop.Open;
			_soundButton.clicked += ToggleSound;

			UpdateSoundIcon();
			OnPhaseChanged(_runFlow.Phase);
		}

		private void Unbind()
		{
			if (_surfButton == null)
			{
				return;
			}

			_surfButton.clicked -= _runFlow.StartRun;
			_settingsButton.clicked -= _settings.Open;
			_shopButton.clicked -= _shop.Open;
			_soundButton.clicked -= ToggleSound;
			_titlePulse.Pause();
		}

		private void OnPhaseChanged(RunPhase phase)
		{
			bool visible = phase == RunPhase.Menu;

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

			if (visible)
			{
				_surfButton.Focus();
				_titlePulse.Resume();
			}
			else
			{
				_titlePulse.Pause();
			}
		}

		private void OnShopClosed()
		{
			UpdateTotalCoins();

			if (_runFlow.Phase == RunPhase.Menu)
			{
				_shopButton.Focus();
			}
		}

		private void UpdateTotalCoins()
		{
			_totalCoins.text = _progress.TotalCoins.ToString("N0", CultureInfo.InvariantCulture);
		}

		private void ToggleSound()
		{
			_volume.Muted = !_volume.Muted;
			_volume.Save();
		}

		private void UpdateSoundIcon()
		{
			if (_soundIcon == null)
			{
				return;
			}

			bool muted = _volume.Muted;
			_soundIcon.EnableInClassList("icon--speaker", !muted);
			_soundIcon.EnableInClassList("icon--speaker-muted", muted);
		}
	}
}
