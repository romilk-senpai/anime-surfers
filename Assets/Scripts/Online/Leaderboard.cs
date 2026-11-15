using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using VContainer.Unity;

namespace Game.Online
{
	/// <summary>
	/// Talks to the playnesc.com leaderboard. The site trades its session cookie for a game token
	/// (<c>/auth/game-token</c>), which only works on the score endpoints and lasts an hour; when it runs out, a new one
	/// is fetched the same way.
	/// </summary>
	public sealed class Leaderboard : ILeaderboard, IStartable
	{
		private const string SiteUrl = "https://playnesc.com";
		private const string ApiUrl = "https://api.playnesc.com";

		/// <summary>Fetch a new token this long before the old one expires, so a request never goes out with a stale one.</summary>
		private static readonly TimeSpan TokenMargin = TimeSpan.FromMinutes(1);

		private string _token;
		private DateTimeOffset _tokenExpiresAt;
		private bool _refreshing;
		private bool _submitting;
		private PendingRun _pending;

		public LeaderboardPlayer Player { get; private set; }
		public bool CanSignIn => BrowserAuth.IsAvailable;
		public LeaderboardStanding Standing { get; private set; }
		public bool HasPendingRun => _pending != null;

		public event Action Changed;

		public void Start()
		{
			// Already signed in on the site: no prompt needed.
			RefreshTokenAsync().Forget();
		}

		public bool SignIn()
		{
			return BrowserAuth.OpenLogin(SiteUrl + "/game-login", () => RefreshTokenAsync().Forget());
		}

		public void ReportRun(int score, float durationSeconds)
		{
			Standing = null;
			Changed?.Invoke();

			// The board keeps only each player's best, so of the runs not sent yet only the best matters.
			if (score > 0 && (_pending == null || score > _pending.Score))
			{
				_pending = new PendingRun(score, durationSeconds);
			}

			SubmitPendingAsync().Forget();
		}

		public async UniTask<IReadOnlyList<LeaderboardEntry>> GetTopAsync(int limit, CancellationToken cancellation)
		{
			string url = ApiUrl + "/api/surfers/leaderboard?limit=" + limit.ToString(CultureInfo.InvariantCulture);
			(long status, string body) = await SendAsync(UnityWebRequest.Get(url), cancellation);

			if (status != 200)
			{
				return null;
			}

			// JsonUtility can't read a top-level array.
			var rows = JsonUtility.FromJson<EntryListDto>("{\"items\":" + body + "}").items;
			var entries = new List<LeaderboardEntry>(rows.Length);

			foreach (EntryDto row in rows)
			{
				entries.Add(new LeaderboardEntry(row.rank, row.uid, row.name, row.score));
			}

			return entries;
		}

		private async UniTaskVoid RefreshTokenAsync()
		{
			if (!BrowserAuth.IsAvailable || _refreshing)
			{
				return;
			}

			_refreshing = true;

			try
			{
				(int status, string body) = await BrowserAuth.FetchGameTokenAsync(SiteUrl + "/auth/game-token");

				if (status == 200)
				{
					var dto = JsonUtility.FromJson<GameTokenDto>(body);
					_token = dto.token;
					_tokenExpiresAt = DateTimeOffset.Parse(dto.expiresAt, CultureInfo.InvariantCulture);
					SetPlayer(new LeaderboardPlayer(dto.uid, dto.name));
					await SubmitPendingCoreAsync();
				}
				else if (status == 401)
				{
					_token = null;
					SetPlayer(null);
				}
				// Anything else (offline, site down, rate limited): keep what we have and try again on the next run.
			}
			finally
			{
				_refreshing = false;
			}
		}

		private async UniTaskVoid SubmitPendingAsync()
		{
			if (_refreshing)
			{
				// The refresh sends the pending run when it's done.
				return;
			}

			if (_token == null || DateTimeOffset.UtcNow > _tokenExpiresAt - TokenMargin)
			{
				RefreshTokenAsync().Forget();
				return;
			}

			await SubmitPendingCoreAsync();
		}

		private async UniTask SubmitPendingCoreAsync()
		{
			if (_submitting)
			{
				return;
			}

			_submitting = true;

			try
			{
				// Loops when a better run is reported while one is on its way.
				while (_pending != null && _token != null)
				{
					PendingRun run = _pending;
					string json = JsonUtility.ToJson(new SubmitDto { score = run.Score, durationSeconds = run.DurationSeconds });
					UnityWebRequest request = UnityWebRequest.Post(ApiUrl + "/api/surfers/scores", json, "application/json");
					request.SetRequestHeader("Authorization", "Bearer " + _token);
					(long status, string body) = await SendAsync(request, CancellationToken.None);

					if (status == 200)
					{
						var dto = JsonUtility.FromJson<StandingDto>(body);
						Standing = new LeaderboardStanding(dto.best, dto.isNewBest, dto.rank);
						ClearPending(run);
						Changed?.Invoke();
					}
					else if (status == 400)
					{
						// The server thinks the run is impossible; resending won't change its mind.
						Debug.LogWarning($"Leaderboard rejected a score of {run.Score} in {run.DurationSeconds:0.#}s: {body}");
						ClearPending(run);
					}
					else if (status == 401)
					{
						// The token stopped working early. The next run fetches a new one; refreshing right away could
						// loop if the site keeps handing out tokens the API turns down.
						_token = null;
					}
					else
					{
						// Offline or the server is down: keep the run for the next try.
						break;
					}
				}
			}
			finally
			{
				_submitting = false;
			}
		}

		private void ClearPending(PendingRun sent)
		{
			if (_pending == sent)
			{
				_pending = null;
			}
		}

		private void SetPlayer(LeaderboardPlayer player)
		{
			if (player?.Uid == Player?.Uid && player?.Name == Player?.Name)
			{
				return;
			}

			Player = player;
			Changed?.Invoke();
		}

		/// <summary>Status 0 if the request never got an answer.</summary>
		private static async UniTask<(long Status, string Body)> SendAsync(UnityWebRequest request, CancellationToken cancellation)
		{
			using (request)
			{
				try
				{
					await request.SendWebRequest().WithCancellation(cancellation);
				}
				catch (UnityWebRequestException)
				{
					// Error statuses land here too; they're read below.
				}

				return (request.responseCode, request.downloadHandler?.text);
			}
		}

		private sealed class PendingRun
		{
			public PendingRun(int score, float durationSeconds)
			{
				Score = score;
				DurationSeconds = durationSeconds;
			}

			public int Score { get; }
			public float DurationSeconds { get; }
		}

		// JsonUtility reads and writes public fields by name, so these match the API's camelCase JSON.
#pragma warning disable CS0649
		[Serializable]
		private sealed class GameTokenDto
		{
			public string token;
			public string expiresAt;
			public string uid;
			public string name;
		}

		[Serializable]
		private sealed class SubmitDto
		{
			public int score;
			public float durationSeconds;
		}

		[Serializable]
		private sealed class StandingDto
		{
			public int best;
			public bool isNewBest;
			// Never null here: a sent run is always on the board.
			public int rank;
		}

		[Serializable]
		private sealed class EntryDto
		{
			public int rank;
			public string uid;
			public string name;
			public int score;
		}

		[Serializable]
		private sealed class EntryListDto
		{
			public EntryDto[] items;
		}
#pragma warning restore CS0649
	}
}
