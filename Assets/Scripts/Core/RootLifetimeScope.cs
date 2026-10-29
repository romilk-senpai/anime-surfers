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
            builder.Register<IInputService, InputService>(Lifetime.Singleton);
            builder.Register<ISceneLoader, SceneLoader>(Lifetime.Singleton);
        }
    }
}
