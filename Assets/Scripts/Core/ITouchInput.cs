namespace Game.Core
{
	public enum Swipe
	{
		Left,
		Right,
		Up,
		Down
	}

	/// <summary>
	/// Feeds the on-screen touch controls into <see cref="IInputService"/>. Ignored while gameplay input is disabled.
	/// </summary>
	public interface ITouchInput
	{
		/// <summary>Left/right change lane, up jumps, down slides.</summary>
		void PushSwipe(Swipe swipe);

		/// <summary>The on-screen nitro button is held down.</summary>
		void SetBoostHeld(bool held);
	}
}
