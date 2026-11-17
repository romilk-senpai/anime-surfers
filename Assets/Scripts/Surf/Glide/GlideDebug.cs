#if UNITY_EDITOR || DEBUG
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Game.Surf
{
	/// <summary>
	/// Testing aid, left out of release builds (no DEBUG): F10 takes off on the glider right away, as if it had just
	/// been picked up.
	/// </summary>
	public sealed class GlideDebug : ITickable
	{
		private readonly GlideFlight _glide;
		private readonly PowerUps _powerUps;
		private readonly WaveSchedule _waves;
		private readonly BoardState _board;

		public GlideDebug(GlideFlight glide, PowerUps powerUps, WaveSchedule waves, BoardState board)
		{
			_glide = glide;
			_powerUps = powerUps;
			_waves = waves;
			_board = board;
		}

		public void Tick()
		{
			if (Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame)
			{
				TakeOff();
			}
		}

		public void TakeOff()
		{
			if (_board.IsCrashed || _board.Speed <= 0f)
			{
				Debug.Log("The glider only takes off during a run.");
				return;
			}

			if (_glide.IsActive)
			{
				Debug.Log("Already gliding.");
				return;
			}

			float landing = _board.Distance + _powerUps.Duration(PickupKind.Glider) * _board.Speed;
			if (_waves.TryGetClearOverlap(_board.Distance, landing, out _))
			{
				Debug.Log("A big wave is in the way of the flight.");
				return;
			}

			_powerUps.Activate(PickupKind.Glider);
			Debug.Log($"Gliding to {landing:0} m, {landing - _board.Distance:0} m ahead.");
		}
	}
}
#endif
