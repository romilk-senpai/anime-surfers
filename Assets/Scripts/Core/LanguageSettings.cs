using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Game.Core
{
	/// <summary>
	/// The localization's selected locale. Localization loads asynchronously, and waiting for it throws on WebGL, so
	/// until it is ready no language is selected and selecting one does nothing; <see cref="Changed"/> is raised once
	/// it is.
	/// </summary>
	public sealed class LanguageSettings : ILanguageSettings, IDisposable
	{
		/// <summary>Same key as the PlayerPrefLocaleSelector in the localization settings.</summary>
		private const string LocaleKey = "selected-locale";

		public LanguageSettings()
		{
			LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;

			if (!IsReady)
			{
				LocalizationSettings.InitializationOperation.Completed += _ => Changed?.Invoke();
			}
		}

		public string Selected
		{
			get
			{
				Locale locale = IsReady ? LocalizationSettings.SelectedLocale : null;
				return locale != null ? locale.Identifier.Code : null;
			}
		}

		private static bool IsReady => LocalizationSettings.InitializationOperation.IsDone;

		public event Action Changed;

		public void Select(string code)
		{
			if (!IsReady)
			{
				return;
			}

			Locale locale = LocalizationSettings.AvailableLocales.GetLocale(code);

			if (locale == null)
			{
				return;
			}

			LocalizationSettings.SelectedLocale = locale;
			PlayerPrefs.SetString(LocaleKey, code);
			PlayerPrefs.Save();
		}

		public void Dispose()
		{
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
		}

		private void OnSelectedLocaleChanged(Locale locale)
		{
			Changed?.Invoke();
		}
	}
}
