namespace Game.Core
{
	/// <summary>
	/// One frame of gameplay input, from <see cref="IInputService.ReadGameplay"/>.
	/// </summary>
	public readonly struct GameplayInput
	{
		/// <summary>-1 or 1 on the frame a lane change was requested, 0 otherwise.</summary>
		public readonly int LaneChange;
		public readonly bool Jump;
		public readonly bool Slide;
		/// <summary>True while the nitro boost button is held.</summary>
		public readonly bool Boost;

		public GameplayInput(int laneChange, bool jump, bool slide, bool boost)
		{
			LaneChange = laneChange;
			Jump = jump;
			Slide = slide;
			Boost = boost;
		}
	}
}
