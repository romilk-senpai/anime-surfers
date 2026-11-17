using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Generates coin trails along single lanes ahead of the board, shaped around the obstacles already placed there:
	/// arcs over barriers, up ramps and along train roofs, ending before walls that cannot be climbed. While super jump
	/// will still be active when the board gets there, trains without a ramp get a coin arc along the jump onto the roof.
	/// Some trails carry a power-up in the middle, and big waves are left clear. Nothing is placed under a glider
	/// flight, and a glider is only placed where no flight from it could reach a big wave. Pickups that are collected
	/// or left behind are removed.
	/// Ticked after <see cref="ObstacleGenerator"/>, and stays behind it, so trails are only shaped around obstacles
	/// that already exist.
	/// </summary>
	public sealed class PickupGenerator
	{
		private readonly PickupSettings _settings;
		private readonly BoardSettings _boardSettings;
		private readonly BoardState _board;
		private readonly PowerUps _powerUps;
		private readonly IWaterSurface _water;
		private readonly ObstacleField _obstacles;
		private readonly ObstacleGenerator _obstacleGenerator;
		private readonly PickupField _field;
		private readonly WaveSchedule _waves;
		private readonly GlideFlight _glide;
		private readonly IGraphicsSettings _graphics;
		private readonly RandomStream _random;
		private float _nextTrailDistance;

		public PickupGenerator(PickupSettings settings, BoardSettings boardSettings, BoardState board,
			PowerUps powerUps, IWaterSurface water, ObstacleField obstacles, ObstacleGenerator obstacleGenerator,
			PickupField field, WaveSchedule waves, GlideFlight glide, IGraphicsSettings graphics, RunRandom random)
		{
			_settings = settings;
			_boardSettings = boardSettings;
			_board = board;
			_powerUps = powerUps;
			_water = water;
			_obstacles = obstacles;
			_obstacleGenerator = obstacleGenerator;
			_field = field;
			_waves = waves;
			_glide = glide;
			_graphics = graphics;
			_random = random.Stream(RunRandom.PickupStream);
			_nextTrailDistance = settings.FirstTrailDistance;
		}

		public void Tick()
		{
			float distance = _board.Distance;
			float longestTrail = (_settings.CoinCount.y - 1) * _settings.CoinSpacing;
			float horizon = Mathf.Min(distance + _settings.SpawnAhead * _graphics.DistanceScale,
				_obstacleGenerator.GeneratedUntil - longestTrail);

			while (_nextTrailDistance < horizon)
			{
				if (_waves.TryGetClearOverlap(_nextTrailDistance, _nextTrailDistance + longestTrail, out WaveEvent wave))
				{
					_nextTrailDistance = wave.ClearEnd;
					continue;
				}

				// Out of reach under the flight.
				if (_glide.IsActive && _nextTrailDistance < _glide.LandingDistance)
				{
					_nextTrailDistance = _glide.LandingDistance;
					continue;
				}

				_nextTrailDistance = GenerateTrail(_nextTrailDistance) +
					_random.Range(_settings.TrailGap.x, _settings.TrailGap.y);
			}

			IReadOnlyList<Pickup> pickups = _field.Pickups;

			for (int i = pickups.Count - 1; i >= 0; i--)
			{
				Pickup pickup = pickups[i];

				if (pickup.Collected || pickup.Distance < distance - _settings.DespawnBehind)
				{
					_field.Remove(pickup);
				}
			}
		}

		/// <summary>Generates one trail starting at the given distance and returns where it ends.</summary>
		private float GenerateTrail(float start)
		{
			float lateral = _boardSettings.LaneOffset(_random.Range(0, _boardSettings.LaneCount));
			int count = _random.Range(_settings.CoinCount.x, _settings.CoinCount.y + 1);
			int powerUpIndex = _random.Value < _settings.PowerUpChance ? count / 2 : -1;
			float previousSurface = float.NaN;
			float end = start;
			// Where super jump runs out, at the current speed.
			float superJumpUntil = _board.Distance + _powerUps.Remaining(PickupKind.SuperJump) * _board.Speed;

			for (int i = 0; i < count; i++)
			{
				float distance = start + i * _settings.CoinSpacing;
				float surface = Placement(distance, lateral, superJumpUntil, out float height, out bool followsWater,
					out bool onJumpArc);

				if (!onJumpArc && surface - previousSurface > _settings.MaxStep)
				{
					break;
				}

				previousSurface = surface;
				end = distance;

				PickupKind kind = i != powerUpIndex ? PickupKind.Coin : RandomPowerUp(distance);
				_field.Add(kind, distance, lateral, height, followsWater);
			}

			return end;
		}

		private PickupKind RandomPowerUp(float distance)
		{
			float gliderWeight = CanGlideFrom(distance) ? _settings.GliderWeight : 0f;
			float coinDoublerWeight = _settings.CoinDoublerWeight;
			float nitroWeight = _settings.NitroWeight;
			float magnetWeight = _settings.MagnetWeight;
			float multiplierWeight = _settings.MultiplierWeight;
			float shieldWeight = _settings.ShieldWeight;
			float superJumpWeight = _settings.SuperJumpWeight;
			float roll = _random.Range(0f, nitroWeight + magnetWeight + multiplierWeight + shieldWeight +
				superJumpWeight + coinDoublerWeight + gliderWeight);

			if ((roll -= nitroWeight) < 0f)
			{
				return PickupKind.Nitro;
			}

			if ((roll -= magnetWeight) < 0f)
			{
				return PickupKind.Magnet;
			}

			if ((roll -= multiplierWeight) < 0f)
			{
				return PickupKind.ScoreMultiplier;
			}

			if ((roll -= shieldWeight) < 0f)
			{
				return PickupKind.Shield;
			}

			if ((roll -= superJumpWeight) < 0f)
			{
				return PickupKind.SuperJump;
			}

			return roll - coinDoublerWeight < 0f || gliderWeight <= 0f ? PickupKind.CoinDoubler : PickupKind.Glider;
		}

		/// <summary>
		/// Whether a glider picked up at the given distance would land short of the next big wave, however fast the
		/// board goes by then, and isn't taken while already gliding.
		/// </summary>
		private bool CanGlideFrom(float distance)
		{
			float longestFlight = _powerUps.Duration(PickupKind.Glider) * _boardSettings.MaxSpeed;
			return !_glide.IsActive && !_waves.TryGetClearOverlap(distance, distance + longestFlight, out _);
		}

		/// <summary>
		/// Where a coin goes at the given point: hovering over the water, a barrier arc, a super jump arc or a train
		/// roof. Returns the nominal height of the surface under it, or of the board along a jump arc.
		/// </summary>
		private float Placement(float distance, float lateral, float superJumpUntil, out float height,
			out bool followsWater, out bool onJumpArc)
		{
			float hoverHeight = _settings.HoverHeight;
			float waterLevel = _water.BaseHeight;
			float surface = waterLevel;
			height = hoverHeight;
			followsWater = true;
			onJumpArc = false;

			foreach (ObstacleBox box in _obstacles.Boxes)
			{
				if (!box.OverlapsLateral(lateral, 0f))
				{
					continue;
				}

				if (!box.Walkable)
				{
					float arc = Mathf.Abs(distance - (box.Start + box.End) * 0.5f) / _settings.BarrierArcLength;
					if (arc < 1f && followsWater)
					{
						height = Mathf.Max(height, Mathf.Lerp(hoverHeight, _settings.BarrierArcHeight, 1f - arc * arc));
					}

					continue;
				}

				if (distance < box.Start && box.RampLength <= 0f &&
					TryJumpArc(box.Start - distance, box.Top - waterLevel, superJumpUntil - box.Start, out float rise))
				{
					float arc = waterLevel + rise;
					if (arc > surface)
					{
						surface = arc;
						height = arc + hoverHeight;
						followsWater = false;
						onJumpArc = true;
					}

					continue;
				}

				if (distance < box.Start || distance > box.End)
				{
					continue;
				}

				// Ramps start below the water; follow the water until they rise out of it.
				float top = box.SurfaceAt(distance);
				if (top > surface)
				{
					surface = top;
					height = top + hoverHeight;
					followsWater = false;
				}
			}

			return surface;
		}

		/// <summary>
		/// Height of the board along a super jump that clears a wall of the given height exactly at it, at the given
		/// distance before the wall. Fails before the take-off, if the jump is too low, or if super jump runs out
		/// before the take-off (<paramref name="activeAfterWall"/> is how far past the wall it lasts).
		/// </summary>
		private bool TryJumpArc(float beforeWall, float wallHeight, float activeAfterWall, out float rise)
		{
			rise = 0f;
			float speed = _board.Speed;
			float gravity = _boardSettings.Gravity;
			float jumpHeight = Mathf.Max(_boardSettings.JumpHeight, _settings.SuperJumpHeight);

			if (speed <= 0f || wallHeight >= jumpHeight)
			{
				return false;
			}

			float launchSpeed = Mathf.Sqrt(2f * gravity * jumpHeight);
			float climbTime = (launchSpeed - Mathf.Sqrt(launchSpeed * launchSpeed - 2f * gravity * wallHeight)) / gravity;
			float runUp = speed * climbTime;

			if (beforeWall > runUp || activeAfterWall < -runUp)
			{
				return false;
			}

			float time = (runUp - beforeWall) / speed;
			rise = launchSpeed * time - 0.5f * gravity * time * time;
			return true;
		}
	}
}
