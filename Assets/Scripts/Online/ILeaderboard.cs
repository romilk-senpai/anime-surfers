using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Online
{
	/// <summary>The playnesc.com account the game is signed in with.</summary>
	public sealed class LeaderboardPlayer
	{
		public LeaderboardPlayer(string uid, string name)
		{
			Uid = uid;
			Name = name;
		}

		public string Uid { get; }
		public string Name { get; }
	}

	/// <summary>The player's standing after a run was sent: their best on the board and its rank (ties share one).</summary>
	public sealed class LeaderboardStanding
	{
		public LeaderboardStanding(int best, bool isNewBest, int rank)
		{
			Best = best;
			IsNewBest = isNewBest;
			Rank = rank;
		}

		public int Best { get; }
		public bool IsNewBest { get; }
		public int Rank { get; }
	}

	public sealed class LeaderboardEntry
	{
		public LeaderboardEntry(int rank, string uid, string name, int score)
		{
			Rank = rank;
			Uid = uid;
			Name = name;
			Score = score;
		}

		public int Rank { get; }
		public string Uid { get; }
		public string Name { get; }
		public int Score { get; }
	}

	/// <summary>
	/// The online leaderboard on playnesc.com. Signing in happens on the site; the game only holds a short-lived token
	/// for posting scores. Lives for the whole session, so a score still goes out if the scene reloads meanwhile.
	/// </summary>
	public interface ILeaderboard
	{
		/// <summary>Null while signed out (or not known yet).</summary>
		LeaderboardPlayer Player { get; }

		/// <summary>Only a browser build can sign in.</summary>
		bool CanSignIn { get; }

		/// <summary>Where the last reported run left the player; null until it has been sent.</summary>
		LeaderboardStanding Standing { get; }

		/// <summary>A run is waiting to be sent: on its way, or held until the player signs in.</summary>
		bool HasPendingRun { get; }

		/// <summary><see cref="Player"/> or <see cref="Standing"/> changed.</summary>
		event Action Changed;

		/// <summary>Opens the site's login in a popup. False if the browser blocked it.</summary>
		bool SignIn();

		/// <summary>
		/// Sends a finished run. Signed out, the best unsent run is kept and goes out once the player signs in.
		/// </summary>
		void ReportRun(int score, float durationSeconds);

		/// <summary>The top of the board, best first. Null if it couldn't be loaded.</summary>
		UniTask<IReadOnlyList<LeaderboardEntry>> GetTopAsync(int limit, CancellationToken cancellation);
	}
}
