namespace Game.Core
{
	/// <summary>
	/// State carried across scene reloads within one session.
	/// </summary>
	public sealed class GameSession
	{
		/// <summary>Set by "Surf again": the next run starts without showing the main menu.</summary>
		public bool SkipMenu { get; set; }
	}
}
