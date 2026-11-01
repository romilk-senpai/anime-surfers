using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Surf
{
	/// <summary>
	/// The run's plain C# simulation: the path, obstacle and pickup fields, the board systems and the run flow.
	/// Needs the settings assets and an <see cref="IWaterHeight"/> registered next to it.
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
			builder.Register<SurfPath>(Lifetime.Singleton).WithParameter(_pathOrigin);
			builder.Register<ObstacleField>(Lifetime.Singleton);
			builder.Register<PickupField>(Lifetime.Singleton);

			builder.Register<BoardState>(Lifetime.Singleton);
			builder.Register<Nitro>(Lifetime.Singleton);
			builder.Register<BoardMovement>(Lifetime.Singleton);
			builder.Register<BoardBody>(Lifetime.Singleton);
			builder.Register<BoardCollision>(Lifetime.Singleton);
			builder.Register<PickupCollector>(Lifetime.Singleton);
			builder.Register<PowerUpUpgrades>(Lifetime.Singleton);
			builder.Register<PowerUps>(Lifetime.Singleton);

			builder.Register<RunState>(Lifetime.Singleton);
			builder.Register<ScoreCounter>(Lifetime.Singleton);

			// RunLoop first: it initializes the board before the run starts.
			builder.RegisterEntryPoint<RunLoop>();
			builder.RegisterEntryPoint<RunStateMachine>();
		}
	}
}
