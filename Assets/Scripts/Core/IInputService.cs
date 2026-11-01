namespace Game.Core
{
	/// <summary>
	/// Gameplay input polled once per frame. Everything reads false/0 while disabled.
	/// Menu input (confirm, pause) is always on.
	/// </summary>
	public interface IInputService
	{
		bool IsEnabled { get; }

		/// <summary>-1 or 1 on the frame a lane change was requested, 0 otherwise.</summary>
		int LaneChange { get; }

		bool JumpPressed { get; }
		bool SlidePressed { get; }

		/// <summary>True while the nitro boost button is held.</summary>
		bool BoostHeld { get; }

		/// <summary>Starts a run from the menu or the results screen.</summary>
		bool ConfirmPressed { get; }

		/// <summary>Toggles pause during a run; closes menus elsewhere.</summary>
		bool PausePressed { get; }

		void Enable();
		void Disable();
	}
}
