using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Surf
{
	/// <summary>
	/// The run's plain C# simulation: the clock and randomness, the big wave schedule, the path, obstacle and pickup fields and their
	/// generators, the board systems, the run flow and the weather. Needs the settings assets, an
	/// <see cref="IWaterHeight"/> and an <see cref="IWaterSurface"/> registered next to it.
	/// </summary>
	public sealed class SurfSimulationInstaller : IInstaller
	{
		private readonly Pose _pathOrigin;

		public SurfSimulationInstaller(Pose pathOrigin)
		{
			_pathOrigin = pathOrigin;
		}

		public void Install(IContainerBuilder builder)
		{
			builder.Register<RunClock>(Lifetime.Singleton);
			builder.Register<RunRandom>(Lifetime.Singleton);
			builder.Register<BoardInput>(Lifetime.Singleton);

			builder.Register<WaveSchedule>(Lifetime.Singleton);
			builder.Register<SurfPath>(Lifetime.Singleton).WithParameter(_pathOrigin);
			builder.Register<ObstacleField>(Lifetime.Singleton);
			builder.Register<PickupField>(Lifetime.Singleton);
			builder.Register<ObstacleGenerator>(Lifetime.Singleton);
			builder.Register<PickupGenerator>(Lifetime.Singleton);

			builder.Register<BoardState>(Lifetime.Singleton).AsSelf().As<IBoardState>();
			builder.Register<Nitro>(Lifetime.Singleton);
			builder.Register<BoardMovement>(Lifetime.Singleton);
			builder.Register<BoardBody>(Lifetime.Singleton);
			builder.Register<BoardCollision>(Lifetime.Singleton);
			builder.Register<PickupCollector>(Lifetime.Singleton);
			builder.Register<PowerUpUpgrades>(Lifetime.Singleton);
			builder.Register<PowerUps>(Lifetime.Singleton);

			builder.Register<RunState>(Lifetime.Singleton);
			builder.Register<ScoreCounter>(Lifetime.Singleton);
			builder.Register<Weather>(Lifetime.Singleton);
			builder.Register<RunStateMachine>(Lifetime.Singleton).AsSelf().As<IRunFlow>();

			builder.RegisterEntryPoint<RunLoop>();
		}
	}
}
