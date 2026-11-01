namespace Game.Core
{
	/// <summary>
	/// Progress kept between sessions.
	/// </summary>
	public interface IPlayerProgress
	{
		int BestScore { get; }
		int TotalCoins { get; }

		/// <summary>Adds the run's coins and keeps its score if it beats the best. Returns true on a new best.</summary>
		bool RecordRun(int score, int coins);

		/// <summary>Takes the coins if there are enough. Returns false and takes nothing otherwise.</summary>
		bool TrySpend(int coins);
	}
}
