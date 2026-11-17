using Game.Core;
using UnityEngine;
using VContainer.Unity;

namespace Game.Surf
{
	/// <summary>
	/// Steps the run in a fixed order: the clock and input, the barrel ride and the glider flight, the board systems
	/// (with the big wave shaped once the board has moved, before it settles on the water), the weather, then the
	/// obstacle, pickup and sky coin generators. Runs before MonoBehaviour updates, so views always read this frame's
	/// state.
	/// </summary>
	public sealed class RunLoop : IStartable, ITickable
	{
		private readonly RunClock _clock;
		private readonly IInputService _input;
		private readonly BoardInput _boardInput;
		private readonly BarrelRide _barrel;
		private readonly BigWave _bigWave;
		private readonly GlideFlight _glide;
		private readonly Nitro _nitro;
		private readonly BoardMovement _movement;
		private readonly BoardBody _body;
		private readonly BoardCollision _collision;
		private readonly PickupCollector _pickups;
		private readonly PowerUps _powerUps;
		private readonly ScoreCounter _score;
		private readonly Weather _weather;
		private readonly ObstacleGenerator _obstacleGenerator;
		private readonly PickupGenerator _pickupGenerator;
		private readonly SkyCoinGenerator _skyCoins;
		private readonly RunStateMachine _flow;

		public RunLoop(RunClock clock, IInputService input, BoardInput boardInput, BarrelRide barrel, BigWave bigWave,
			GlideFlight glide, Nitro nitro, BoardMovement movement, BoardBody body, BoardCollision collision,
			PickupCollector pickups, PowerUps powerUps, ScoreCounter score, Weather weather,
			ObstacleGenerator obstacleGenerator, PickupGenerator pickupGenerator, SkyCoinGenerator skyCoins,
			RunStateMachine flow)
		{
			_clock = clock;
			_input = input;
			_boardInput = boardInput;
			_barrel = barrel;
			_bigWave = bigWave;
			_glide = glide;
			_nitro = nitro;
			_movement = movement;
			_body = body;
			_collision = collision;
			_pickups = pickups;
			_powerUps = powerUps;
			_score = score;
			_weather = weather;
			_obstacleGenerator = obstacleGenerator;
			_pickupGenerator = pickupGenerator;
			_skyCoins = skyCoins;
			_flow = flow;
		}

		public void Start()
		{
			_movement.Initialize();
			_body.Initialize();
			// After the board is placed: skipping the menu starts the run right away.
			_flow.Begin();
		}

		public void Tick()
		{
			float dt = Time.deltaTime;
			if (dt <= 0f)
			{
				return;
			}

			_clock.Advance(dt);
			_boardInput.Current = _input.ReadGameplay();
			_barrel.Tick(dt);
			_glide.Tick(dt);

			// Riding into and through the barrel, holding is the only control.
			if (_barrel.LocksControls)
			{
				_boardInput.Current = _boardInput.Current.HoldOnly();
			}
			else if (_glide.LocksControls)
			{
				_boardInput.Current = _boardInput.Current.LanesOnly();
			}


			_nitro.Tick(dt);
			_movement.Tick(dt);
			_bigWave.Tick();
			_body.Tick(dt);
			_collision.Tick();
			_pickups.Tick(dt);
			_powerUps.Tick(dt);
			_score.Tick();
			_weather.Tick();
			// Obstacles first, so pickup trails are shaped around this frame's rows.
			_obstacleGenerator.Tick();
			_pickupGenerator.Tick();
			_skyCoins.Tick();
		}
	}
}
