using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Obstacle hits: bounces the board back into its lane when it changes lanes into an obstacle's side
	/// and crashes it when hit head-on, unless the shield absorbs the hit and smashes the obstacle. After a crash the riderless board slides on under barriers and bounces back
	/// off walls, stopping against trains instead of passing through them.
	/// </summary>
	public sealed class BoardCollision
	{
		// Like a jump, so the water doesn't damp the bounce before it leaves the surface.
		private const float BounceWaterIgnoreTime = 0.1f;

		private readonly BoardSettings _settings;
		private readonly BoardState _state;
		private readonly ObstacleField _obstacles;
		private readonly PowerUps _powerUps;

		public BoardCollision(BoardSettings settings, BoardState state, ObstacleField obstacles, PowerUps powerUps)
		{
			_settings = settings;
			_state = state;
			_obstacles = obstacles;
			_powerUps = powerUps;
		}

		public void Tick()
		{
			if (_state.IsCrashed)
			{
				TickRiderless();
				return;
			}

			float halfWidth = _settings.BoardWidth * 0.5f;
			float bodyTop = _state.Height + (_state.IsSliding ? _settings.SlideBodyHeight : _settings.BodyHeight);

			if (!_obstacles.TryGetHit(_state.Distance, _settings.BodyHalfLength, _state.Lateral, halfWidth,
					_state.Height, bodyTop, _settings.StepHeight, _state.WaterHeight, out ObstacleBox hit))
			{
				return;
			}

			if (hit.OverlapsLateral(_state.PreviousLateral, halfWidth))
			{
				if (_powerUps.TryAbsorbHit())
				{
					hit.Smashed = true;
					return;
				}

				// Anything that starts above the board could have been slid under; the rest is a wall.
				float bottom = hit.Bottom + (hit.FollowsWater ? _state.WaterHeight : 0f);
				Crash(bottom > _state.Height ? CrashKind.Barrier : CrashKind.Wall);
				return;
			}

			// Changed lanes into its side: bounce back out.
			bool movingRight = _state.Lateral > _state.PreviousLateral;
			_state.Lateral = movingRight ? hit.Left - halfWidth : hit.Right + halfWidth;
			_state.LateralVelocity = 0f;
			_state.Lane = _settings.NearestLane(_state.Lateral);
			_state.NotifySideHit(movingRight ? 1 : -1);
		}

		/// <summary>Only the deck collides, so the board passes under the barrier that knocked her off.</summary>
		private void TickRiderless()
		{
			float halfWidth = _settings.BoardWidth * 0.5f;

			if (!_obstacles.TryGetHit(_state.Distance, _settings.BoardLength * 0.5f, _state.Lateral, halfWidth,
					_state.Height, _state.Height + _settings.StepHeight, _settings.StepHeight, _state.WaterHeight,
					out ObstacleBox hit))
			{
				return;
			}

			if (hit.OverlapsLateral(_state.PreviousLateral, halfWidth))
			{
				// Bouncing back off a wall moves away from it.
				if (_state.Speed > 0f)
				{
					_state.Speed = 0f;
				}

				return;
			}

			bool movingRight = _state.Lateral > _state.PreviousLateral;
			_state.Lateral = movingRight ? hit.Left - halfWidth : hit.Right + halfWidth;
			_state.LateralVelocity = 0f;
		}

		private void Crash(CrashKind kind)
		{
			_state.IsCrashed = true;
			_state.CrashKind = kind;
			_state.SlideUntil = float.NegativeInfinity;

			// Knocked off by a barrier, she leaves the board to carry on under it at full speed.
			if (kind == CrashKind.Wall)
			{
				// Back off the wall, up, and away toward the middle of the passage.
				float side = Mathf.Abs(_state.Lateral) > 0.1f ? -Mathf.Sign(_state.Lateral) : Random.value < 0.5f ? -1f : 1f;
				_state.Speed = -_settings.WallBounceSpeed;
				_state.LateralVelocity = side * _settings.WallBounceSideSpeed;
				_state.VerticalVelocity = Mathf.Max(_state.VerticalVelocity,
					Mathf.Sqrt(2f * _settings.Gravity * _settings.WallBounceHeight));
				_state.IgnoreWaterUntil = Time.time + BounceWaterIgnoreTime;
			}

			_state.NotifyCrashed();
		}
	}
}
