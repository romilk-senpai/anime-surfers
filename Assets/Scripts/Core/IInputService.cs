namespace Game.Core
{
	/// <summary>
	/// Gameplay input, read once per frame. Everything reads false/0 while disabled.
	/// Menu input (confirm, pause) is always on.
	/// </summary>
	public interface IInputService
	{
		bool IsEnabled { get; }

		/// <summary>This frame's gameplay input. Consumes swipes, so call it once per frame.</summary>
		GameplayInput ReadGameplay();

		/// <summary>Starts a run from the menu or the results screen.</summary>
		bool ConfirmPressed { get; }

		/// <summary>Toggles pause during a run; closes menus elsewhere.</summary>
		bool PausePressed { get; }

		void Enable();
		void Disable();
	}
}
