using System;
using Game.Core;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace Game.UI
{
	/// <summary>
	/// Settings over the main menu: music and effects volume, the language, and the way to the graphics options.
	/// Changes apply at once; the volume is saved on closing.
	/// </summary>
	public sealed class SettingsPresenter : IScreen, IStartable, IDisposable
	{
		/// <summary>One "language-{code}" button each.</summary>
		private static readonly string[] LocaleCodes = { "en", "ja" };

		private readonly IVolumeSettings _volume;
		private readonly ILanguageSettings _language;
		private readonly GraphicsPresenter _graphics;
		private readonly ScreenStack _screens;
		private readonly GameUi _ui;
		private readonly Action[] _selectLocale = new Action[LocaleCodes.Length];

		private VisualElement _screen;
		private SliderInt _music;
		private SliderInt _effects;
		private Label _musicValue;
		private Label _effectsValue;
		private Button _graphicsButton;
		private Button _done;
		private Button[] _languageButtons;
		private bool _isOpen;

		public SettingsPresenter(IVolumeSettings volume, ILanguageSettings language, GraphicsPresenter graphics,
			ScreenStack screens, GameUi ui)
		{
			_volume = volume;
			_language = language;
			_graphics = graphics;
			_screens = screens;
			_ui = ui;

			for (int i = 0; i < LocaleCodes.Length; i++)
			{
				string code = LocaleCodes[i];
				_selectLocale[i] = () => _language.Select(code);
			}
		}

		public void Start()
		{
			_language.Changed += UpdateLanguageButtons;
			_graphics.Closed += OnGraphicsClosed;
			_ui.RegisterReloadCallback(Bind);
		}

		public void Dispose()
		{
			_language.Changed -= UpdateLanguageButtons;
			_graphics.Closed -= OnGraphicsClosed;
			_ui.UnregisterReloadCallback(Bind);
			Unbind();
		}

		public void Open()
		{
			if (_screen == null)
			{
				return;
			}

			_isOpen = true;
			Refresh();
			_screens.Show(this);
			_done.Focus();
		}

		public void Close()
		{
			if (_isOpen)
			{
				_volume.Save();
			}

			_isOpen = false;
			_screens.Hide(this);

			if (_screen != null)
			{
				GameUi.SetVisible(_screen, false);
			}
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

			_screen = root.Q("settings");
			_music = _screen.Q<SliderInt>("music-volume");
			_effects = _screen.Q<SliderInt>("effects-volume");
			_musicValue = _screen.Q<Label>("music-value");
			_effectsValue = _screen.Q<Label>("effects-value");
			_graphicsButton = _screen.Q<Button>("graphics-button");
			_done = _screen.Q<Button>("done-button");
			_languageButtons = new Button[LocaleCodes.Length];

			_music.RegisterValueChangedCallback(OnMusicChanged);
			_effects.RegisterValueChangedCallback(OnEffectsChanged);
			_graphicsButton.clicked += _graphics.Open;
			_done.clicked += Close;

			for (int i = 0; i < LocaleCodes.Length; i++)
			{
				_languageButtons[i] = _screen.Q<Button>("language-" + LocaleCodes[i]);
				_languageButtons[i].clicked += _selectLocale[i];
			}
			Refresh();
		}

		private void Unbind()
		{
			if (_screen == null)
			{
				return;
			}

			_music.UnregisterValueChangedCallback(OnMusicChanged);
			_effects.UnregisterValueChangedCallback(OnEffectsChanged);
			_graphicsButton.clicked -= _graphics.Open;
			_done.clicked -= Close;

			for (int i = 0; i < LocaleCodes.Length; i++)
			{
				_languageButtons[i].clicked -= _selectLocale[i];
			}		}

		private void Refresh()
		{
			_music.SetValueWithoutNotify(ToPercent(_volume.Music));
			_effects.SetValueWithoutNotify(ToPercent(_volume.Effects));
			_musicValue.text = _music.value.ToString();
			_effectsValue.text = _effects.value.ToString();
			UpdateLanguageButtons();
			GameUi.SetVisible(_screen, _isOpen);
		}

		private void OnMusicChanged(ChangeEvent<int> evt)
		{
			_volume.Music = evt.newValue / 100f;
			_musicValue.text = evt.newValue.ToString();
		}

		private void OnEffectsChanged(ChangeEvent<int> evt)
		{
			_volume.Effects = evt.newValue / 100f;
			_effectsValue.text = evt.newValue.ToString();
		}

		private void OnGraphicsClosed()
		{
			if (_isOpen)
			{
				_graphicsButton.Focus();
			}
		}

		private void UpdateLanguageButtons()
		{
			if (_languageButtons == null)
			{
				return;
			}

			string code = _language.Selected;

			for (int i = 0; i < LocaleCodes.Length; i++)
			{
				_languageButtons[i].EnableInClassList("language-button--selected", LocaleCodes[i] == code);
			}
		}

		private static int ToPercent(float value)
		{
			return Mathf.RoundToInt(value * 100f);
		}
	}
}
