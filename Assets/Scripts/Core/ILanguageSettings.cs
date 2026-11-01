using System;

namespace Game.Core
{
	/// <summary>
	/// The player's language, kept between sessions.
	/// </summary>
	public interface ILanguageSettings
	{
		/// <summary>Code of the selected locale, like "en", or null while none is selected.</summary>
		string Selected { get; }

		/// <summary>Ignored for a code without a matching locale.</summary>
		void Select(string code);

		event Action Changed;
	}
}
