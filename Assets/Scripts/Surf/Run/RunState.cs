using System;

namespace Game.Surf
{
	/// <summary>
	/// Per-run progress shown by the HUD.
	/// </summary>
	public sealed class RunState
	{
		public int Score { get; private set; }
		public int Coins { get; private set; }

		/// <summary>Set when the run ends.</summary>
		public bool IsNewBest { get; set; }

		public event Action<int> ScoreChanged;
		public event Action<int> CoinsChanged;

		public void AddCoins(int amount)
		{
			Coins += amount;
			CoinsChanged?.Invoke(Coins);
		}

		public void SetScore(int score)
		{
			if (score == Score)
			{
				return;
			}

			Score = score;
			ScoreChanged?.Invoke(score);
		}
	}
}
