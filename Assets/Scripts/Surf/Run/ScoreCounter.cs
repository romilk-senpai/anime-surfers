using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Scores the run by distance travelled, multiplied while the score multiplier is active.
	/// The nitro boost scores faster through speed alone.
	/// </summary>
	public sealed class ScoreCounter
	{
		private readonly RunSettings _settings;
		private readonly BoardState _board;
		private readonly RunState _run;
		private readonly PowerUps _powerUps;
		private float _lastDistance;
		private float _score;

		public ScoreCounter(RunSettings settings, BoardState board, RunState run, PowerUps powerUps)
		{
			_settings = settings;
			_board = board;
			_run = run;
			_powerUps = powerUps;
		}

		public void Tick()
		{
			// The board drifting on its own after a crash scores nothing.
			float travelled = _board.IsCrashed ? 0f : _board.Distance - _lastDistance;
			_lastDistance = _board.Distance;

			_score += travelled * _settings.ScorePerMeter * _powerUps.ScoreMultiplier;
			_run.SetScore(Mathf.FloorToInt(_score));
		}
	}
}
