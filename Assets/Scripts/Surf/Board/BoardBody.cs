using Game.Core;
using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Vertical motion: buoyancy against the water surface, riding on obstacle tops, jump (higher with super jump)
	/// and slide.
	/// </summary>
	public sealed class BoardBody
	{
		private const float MaxSimulationStep = 1f / 60f;
		private const float JumpWaterIgnoreTime = 0.1f;

		private readonly BoardSettings _settings;
		private readonly BoardState _state;
		private readonly IWaterHeight _water;
		private readonly ObstacleField _obstacles;
		private readonly IInputService _input;
		private readonly PowerUps _powerUps;

		public BoardBody(BoardSettings settings, BoardState state, IWaterHeight water, ObstacleField obstacles,
			IInputService input, PowerUps powerUps)
		{
			_settings = settings;
			_state = state;
			_water = water;
			_obstacles = obstacles;
			_input = input;
			_powerUps = powerUps;
		}

		public void Initialize()
		{
			_state.WaterHeight = _water.GetHeight(_state.PlanarPosition);
			_state.Height = _state.WaterHeight + _settings.RideHeight;
		}

		public void Tick(float dt)
		{
			if (_input.JumpPressed)
			{
				_state.JumpRequestTime = Time.time;
			}

			if (_input.SlidePressed)
			{
				Slide();
			}

			Simulate(dt);
		}

		private void Slide()
		{
			// On trains the roll plays out before another one, rather than stretching into a held last frame.
			if (_state.IsCrashed || _state.IsRunning && _state.IsSliding)
			{
				return;
			}

			_state.SlideUntil = Time.time + _settings.SlideDuration;
			_state.JumpRequestTime = float.NegativeInfinity;

			if (!_state.IsGrounded)
			{
				_state.VerticalVelocity = Mathf.Min(_state.VerticalVelocity, -_settings.SlideDropSpeed);
			}
		}

		private void Simulate(float dt)
		{
			float gravity = _settings.Gravity;
			float buoyancy = _settings.Buoyancy;
			float rideHeight = _settings.RideHeight;

			float waterHeight = _water.GetHeight(_state.PlanarPosition);
			// Includes both the wave motion and the board sliding up/down slopes.
			float waterVelocity = (waterHeight - _state.WaterHeight) / dt;
			_state.WaterHeight = waterHeight;

			// Offset so that buoyancy balances gravity exactly at ride height.
			float floatLine = waterHeight + rideHeight + gravity / buoyancy;

			float height = _state.Height;
			float verticalVelocity = _state.VerticalVelocity;

			bool hasSupport = _obstacles.TryGetSupport(_state.Distance, _state.PreviousDistance, _state.Lateral,
				_settings.BoardWidth * 0.5f, height + _settings.StepHeight, waterHeight, out float support);

			if (_state.IsGrounded && Time.time - _state.JumpRequestTime <= Mathf.Max(_settings.JumpBufferTime, dt))
			{
				_state.JumpRequestTime = float.NegativeInfinity;
				_state.SlideUntil = float.NegativeInfinity;
				float jumpHeight = _powerUps.JumpHeight(_settings.JumpHeight);
				verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight) + Mathf.Max(0f, waterVelocity);
				_state.IgnoreWaterUntil = Time.time + JumpWaterIgnoreTime;
				_state.NotifyJumped();
			}

			bool wasGrounded = _state.IsGrounded;
			float impactSpeed = Mathf.Max(0f, waterVelocity - verticalVelocity);
			bool ignoreWater = Time.time < _state.IgnoreWaterUntil;
			int steps = Mathf.CeilToInt(dt / MaxSimulationStep);
			float step = dt / steps;

			for (int i = 0; i < steps; i++)
			{
				float submersion = floatLine - height;
				float acceleration = -gravity;

				if (submersion > 0f && !ignoreWater)
				{
					acceleration += buoyancy * submersion - _settings.WaterDrag * (verticalVelocity - waterVelocity);
				}

				verticalVelocity += acceleration * step;
				height += verticalVelocity * step;
			}

			// Hard landings shouldn't visibly sink the deck: the spring still softens the impact above this.
			float lowest = waterHeight + rideHeight - _settings.MaxSink;
			if (height < lowest)
			{
				height = lowest;
				// Sliding, a rising wave doesn't throw the board up toward the barrier past it.
				verticalVelocity = Mathf.Max(verticalVelocity, _state.IsSliding ? 0f : waterVelocity);
			}

			// Launched off a crest mid-slide: drop back down to stay under the barrier.
			if (_state.IsSliding && height > floatLine)
			{
				verticalVelocity = Mathf.Min(verticalVelocity, -_settings.SlideDropSpeed);
			}

			bool onSurface = hasSupport && height <= support;
			float surfaceImpactSpeed = Mathf.Max(0f, -verticalVelocity);
			if (onSurface)
			{
				height = support;
				verticalVelocity = Mathf.Max(verticalVelocity, 0f);
			}

			_state.Height = height;
			_state.VerticalVelocity = verticalVelocity;
			_state.IsOnSurface = onSurface;
			_state.IsOnWater = !ignoreWater && height < floatLine;
			// Kept through jumps and rolls over the train, dropped once the board heads for the water so it lands under
			// her feet.
			_state.IsRunning = !_state.IsOnWater && (onSurface || _state.IsRunning && hasSupport);

			if (_state.IsOnWater && !wasGrounded)
			{
				_state.NotifyLanded(impactSpeed);
			}
			else if (onSurface && !wasGrounded)
			{
				_state.NotifyLandedOnSurface(surfaceImpactSpeed);
			}
		}
	}
}
