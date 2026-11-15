using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;

namespace Game.Surf
{
	/// <summary>
	/// The characters the player can ride as, in menu order. The first one rides until the player picks another.
	/// </summary>
	[CreateAssetMenu(menuName = "Anime Surfers/Character Roster", fileName = "CharacterRoster")]
	public sealed class CharacterRoster : ScriptableObject
	{
		[Serializable]
		public sealed class Character
		{
			[Tooltip("Saved as the player's pick.")]
			[SerializeField] private string id;
			[SerializeField] private LocalizedString displayName;
			[SerializeField] private Texture2D portrait;
			[Tooltip("Her rider prefab, placed for her stance. Loaded only while she rides, so the others take no memory.")]
			[SerializeField] private AssetReferenceGameObject rider;

			public string Id => id;
			public LocalizedString DisplayName => displayName;
			public Texture2D Portrait => portrait;
			public AssetReferenceGameObject Rider => rider;
		}

		[SerializeField] private Character[] characters;

		public int Count => characters.Length;

		public Character this[int index] => characters[index];

		/// <summary>The character's index, or 0 for an unknown or null id.</summary>
		public int IndexOf(string id)
		{
			int index = Array.FindIndex(characters, c => c.Id == id);
			return Mathf.Max(index, 0);
		}
	}
}
