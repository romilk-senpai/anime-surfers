using System;
using UnityEngine;

namespace Game.Core
{
	public sealed class CharacterSelection : ICharacterSelection
	{
		private const string SelectedKey = "character.selected";

		public string Selected
		{
			get
			{
				string id = PlayerPrefs.GetString(SelectedKey, string.Empty);
				return id.Length > 0 ? id : null;
			}
		}

		public event Action Changed;

		public void Select(string id)
		{
			if (id == Selected)
			{
				return;
			}

			PlayerPrefs.SetString(SelectedKey, id);
			PlayerPrefs.Save();
			Changed?.Invoke();
		}
	}
}
