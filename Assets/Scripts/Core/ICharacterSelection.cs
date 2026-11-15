using System;

namespace Game.Core
{
	/// <summary>
	/// The character the player rides as, kept between sessions.
	/// </summary>
	public interface ICharacterSelection
	{
		/// <summary>Id of the selected character, or null while none was picked yet.</summary>
		string Selected { get; }

		void Select(string id);

		event Action Changed;
	}
}
