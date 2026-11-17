#if UNITY_EDITOR || DEBUG
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Game.Surf
{
	/// <summary>
	/// Testing aid, left out of release builds (no DEBUG): F9 brings the next big wave in right past everything
	/// generated so far.
	/// </summary>
	public sealed class BigWaveDebug : ITickable
	{
		private readonly BigWaveSettings _settings;
		private readonly WaveSchedule _waves;
		private readonly SurfPath _path;
		private readonly ObstacleGenerator _obstacles;
		private readonly BoardState _board;

		public BigWaveDebug(BigWaveSettings settings, WaveSchedule waves, SurfPath path, ObstacleGenerator obstacles,
			BoardState board)
		{
			_settings = settings;
			_waves = waves;
			_path = path;
			_obstacles = obstacles;
			_board = board;
		}

		public void Tick()
		{
			if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
			{
				StartWaveAhead();
			}
		}

		public void StartWaveAhead()
		{
			// The path's own curves fade out over the blend distance before the wave.
			float start = Mathf.Max(_path.GeneratedUntil, _obstacles.GeneratedUntil) + _settings.PathBlend + 1f;

			if (_waves.TryStartWaveAt(start))
			{
				Debug.Log($"Big wave coming in at {start:0} m, {start - _board.Distance:0} m ahead.");
			}
			else
			{
				Debug.Log("A big wave is already under way.");
			}
		}
	}
}
#endif
