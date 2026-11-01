using VContainer;
using VContainer.Unity;

namespace Game.Core
{
	/// <summary>
	/// Project-wide services. Spawned once from VContainerSettings and kept across scene loads;
	/// every scene scope gets it as parent.
	/// </summary>
	public class RootLifetimeScope : LifetimeScope
	{
		protected override void Configure(IContainerBuilder builder)
		{
			builder.Register<InputService>(Lifetime.Singleton).As<IInputService, ITouchInput>();
			builder.Register<ISceneLoader, SceneLoader>(Lifetime.Singleton);
			builder.Register<IPlayerProgress, PlayerProgress>(Lifetime.Singleton);
			builder.Register<IVolumeSettings, VolumeSettings>(Lifetime.Singleton);
			builder.Register<ILanguageSettings, LanguageSettings>(Lifetime.Singleton);
			builder.Register<IGraphicsSettings, GraphicsSettings>(Lifetime.Singleton);
			builder.RegisterEntryPoint<GraphicsApplier>();
			builder.Register<GameSession>(Lifetime.Singleton);
		}
	}
}
