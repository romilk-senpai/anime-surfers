using Game.Core;

namespace Game.Surf
{
	/// <summary>
	/// This frame's gameplay input, read once by <see cref="RunLoop"/> before the board systems run, so each of them
	/// sees the same input and a swipe is only acted on once.
	/// </summary>
	public sealed class BoardInput
	{
		public GameplayInput Current { get; set; }
	}
}
