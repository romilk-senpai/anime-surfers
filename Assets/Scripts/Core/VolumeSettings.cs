using System;
using UnityEngine;

namespace Game.Core
{
	public sealed class VolumeSettings : IVolumeSettings
	{
		private const string MusicKey = "volume.music";
		private const string EffectsKey = "volume.effects";
		private const string MutedKey = "volume.muted";

		public float Music
		{
			get => PlayerPrefs.GetFloat(MusicKey, 1f);
			set
			{
				PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
				Changed?.Invoke();
			}
		}

		public float Effects
		{
			get => PlayerPrefs.GetFloat(EffectsKey, 1f);
			set
			{
				PlayerPrefs.SetFloat(EffectsKey, Mathf.Clamp01(value));
				Changed?.Invoke();
			}
		}

		public bool Muted
		{
			get => PlayerPrefs.GetInt(MutedKey, 0) != 0;
			set
			{
				PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
				Changed?.Invoke();
			}
		}

		public event Action Changed;

		public void Save()
		{
			PlayerPrefs.Save();
		}
	}
}
