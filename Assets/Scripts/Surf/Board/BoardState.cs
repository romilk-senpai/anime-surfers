using System;
using UnityEngine;

namespace Game.Surf
{
	public enum CrashKind
	{
		/// <summary>Head-on into a solid face, like a train's front.</summary>
		Wall,
		/// <summary>Into an obstacle with a gap under it that could have been slid under.</summary>
		Barrier
	}

	/// <summary>
	/// Board simulation state shared by the board systems (writers) and the views (readers).
	/// Positions are in passage space: distance along the path, lateral offset from the centerline and height.
	/// </summary>
	public sealed class BoardState
	{
		private readonly BoardSettings _settings;

		public BoardState(BoardSettings settings)
		{
			_settings = settings;
		}

		public float Distance { get; set; }
		/// <summary>Distance at the start of this frame, before the board moved forward.</summary>
		public float PreviousDistance { get; set; }
		public float Speed { get; set; }
		public int Lane { get; set; }
		public float Lateral { get; set; }
		/// <summary>Lateral offset at the start of this frame, before lane smoothing and obstacle bounces.</summary>
		public float PreviousLateral { get; set; }
		public float LateralVelocity { get; set; }

		public float Height { get; set; }
		public float VerticalVelocity { get; set; }
		/// <summary>Water height under the board, sampled during the last simulation step.</summary>
		public float WaterHeight { get; set; }

		public bool IsOnWater { get; set; }
		public bool IsOnSurface { get; set; }
		public bool IsGrounded => IsOnWater || IsOnSurface;
		public bool IsCrashed { get; set; }
		public CrashKind CrashKind { get; set; }
		/// <summary>
		/// Riding on a train, or jumping over one: the rider runs with the board under her arm, and rolls instead of
		/// lying down on the board when sliding.
		/// </summary>
		public bool IsRunning { get; set; }

		public float SlideUntil { get; set; } = float.NegativeInfinity;
		public float JumpRequestTime { get; set; } = float.NegativeInfinity;
		public float IgnoreWaterUntil { get; set; }

		public Vector3 PathPosition { get; set; }
		public Vector3 PathForward { get; set; } = Vector3.forward;

		public bool IsSliding => Time.time < SlideUntil;
		public Vector3 PathRight => Vector3.Cross(Vector3.up, PathForward);
		/// <summary>Board position on the path plane; the height is in <see cref="Height"/>.</summary>
		public Vector3 PlanarPosition => PathPosition + PathRight * Lateral;

		/// <summary>Lane-change carve in -1..1 (negative = moving left).</summary>
		public float Carve => Mathf.Clamp(
			LateralVelocity / (_settings.LaneWidth / Mathf.Max(_settings.LaneChangeTime, 0.01f)), -1f, 1f);

		/// <summary>Fired when the board touches the water again; the argument is the impact speed.</summary>
		public event Action<float> Landed;
		/// <summary>Fired when the board comes down on an obstacle top, like a train roof; the argument is the impact speed.</summary>
		public event Action<float> LandedOnSurface;
		public event Action Jumped;
		public event Action Crashed;
		/// <summary>Fired when the board bounces off an obstacle's side; the argument is the side hit, -1 left or 1 right.</summary>
		public event Action<int> SideHit;

		public void NotifyLanded(float impactSpeed) => Landed?.Invoke(impactSpeed);
		public void NotifyLandedOnSurface(float impactSpeed) => LandedOnSurface?.Invoke(impactSpeed);
		public void NotifyJumped() => Jumped?.Invoke();
		public void NotifyCrashed() => Crashed?.Invoke();
		public void NotifySideHit(int side) => SideHit?.Invoke(side);
	}
}
