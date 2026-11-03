using System;
using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Read-only view of <see cref="BoardState"/> for views and presenters; only the board systems write to it.
	/// Positions are in passage space: distance along the path, lateral offset from the centerline and height.
	/// </summary>
	public interface IBoardState
	{
		float Distance { get; }
		/// <summary>Distance at the start of this frame, before the board moved forward.</summary>
		float PreviousDistance { get; }
		float Speed { get; }
		int Lane { get; }
		float Lateral { get; }
		/// <summary>Lateral offset at the start of this frame, before lane smoothing and obstacle bounces.</summary>
		float PreviousLateral { get; }
		float LateralVelocity { get; }

		float Height { get; }
		float VerticalVelocity { get; }
		/// <summary>Water height under the board, sampled during the last simulation step.</summary>
		float WaterHeight { get; }

		bool IsOnWater { get; }
		bool IsOnSurface { get; }
		bool IsGrounded { get; }
		bool IsCrashed { get; }
		CrashKind CrashKind { get; }
		/// <summary>
		/// Riding on a train, or jumping over one: the rider runs with the board under her arm, and rolls instead of
		/// lying down on the board when sliding.
		/// </summary>
		bool IsRunning { get; }

		/// <summary>Times are on the <see cref="RunClock"/>.</summary>
		float SlideUntil { get; }
		float JumpRequestTime { get; }
		float IgnoreWaterUntil { get; }

		Vector3 PathPosition { get; }
		Vector3 PathForward { get; }

		bool IsSliding { get; }
		Vector3 PathRight { get; }
		/// <summary>Board position on the path plane; the height is in <see cref="Height"/>.</summary>
		Vector3 PlanarPosition { get; }

		/// <summary>Lane-change carve in -1..1 (negative = moving left).</summary>
		float Carve { get; }

		/// <summary>Fired when the board touches the water again; the argument is the impact speed.</summary>
		event Action<float> Landed;
		/// <summary>Fired when the board comes down on an obstacle top, like a train roof; the argument is the impact speed.</summary>
		event Action<float> LandedOnSurface;
		event Action Jumped;
		event Action Crashed;
		/// <summary>Fired when the board bounces off an obstacle's side; the argument is the side hit, -1 left or 1 right.</summary>
		event Action<int> SideHit;
	}
}
