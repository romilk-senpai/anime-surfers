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
		/// <summary>True while the jump key or a finger on the screen is held; steers the barrel ride.</summary>
		public readonly bool Hold;

		public GameplayInput(int laneChange, bool jump, bool slide, bool boost, bool hold)
		{
			LaneChange = laneChange;
			Jump = jump;
			Slide = slide;
			Boost = boost;
			Hold = hold;
		}

		/// <summary>This input with everything but <see cref="Hold"/> cleared.</summary>
		public GameplayInput HoldOnly()
		{
			return new GameplayInput(0, false, false, false, Hold);
		}

		/// <summary>This input with everything but <see cref="LaneChange"/> cleared.</summary>
		public GameplayInput LanesOnly()
		{
			return new GameplayInput(LaneChange, false, false, false, false);
		}
	}
}
