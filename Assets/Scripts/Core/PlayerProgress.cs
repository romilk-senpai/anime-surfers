using UnityEngine;

namespace Game.Core
{
	public sealed class PlayerProgress : IPlayerProgress
	{
		private const string BestScoreKey = "progress.best-score";
		private const string TotalCoinsKey = "progress.total-coins";

		public int BestScore => PlayerPrefs.GetInt(BestScoreKey, 0);
		public int TotalCoins => PlayerPrefs.GetInt(TotalCoinsKey, 0);

		public bool RecordRun(int score, int coins)
		{
			bool isNewBest = score > BestScore;

			if (isNewBest)
			{
				PlayerPrefs.SetInt(BestScoreKey, score);
			}

			PlayerPrefs.SetInt(TotalCoinsKey, TotalCoins + coins);
			PlayerPrefs.Save();
			return isNewBest;
		}

		public bool TrySpend(int coins)
		{
			int total = TotalCoins;

			if (coins > total)
			{
				return false;
			}

			PlayerPrefs.SetInt(TotalCoinsKey, total - coins);
			PlayerPrefs.Save();
			return true;
		}
	}
}
