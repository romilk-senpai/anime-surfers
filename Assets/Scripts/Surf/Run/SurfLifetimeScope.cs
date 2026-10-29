using Game.Audio;
using Game.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Surf
{
    public class SurfLifetimeScope : LifetimeScope
    {
        [SerializeField] private BoardSettings boardSettings;
        [SerializeField] private RunSettings runSettings;
        [SerializeField] private PathSettings pathSettings;
        [SerializeField] private PickupSettings pickupSettings;
        [Tooltip("Where the passage starts and which way it faces.")]
        [SerializeField] private Transform pathOrigin;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(boardSettings);
            builder.RegisterInstance(runSettings);
            builder.RegisterInstance(pathSettings);
            builder.RegisterInstance(pickupSettings);

            builder.RegisterComponentInHierarchy<SoundController>().As<ISoundController>();
            builder.RegisterComponentInHierarchy<WaterSurface>();
            builder.RegisterComponentInHierarchy<ObstacleSpawner>();
            builder.RegisterComponentInHierarchy<PickupSpawner>();

            Pose origin = pathOrigin != null ? new Pose(pathOrigin.position, pathOrigin.rotation) : Pose.identity;
            builder.Register<SurfPath>(Lifetime.Singleton).WithParameter(origin);
            builder.Register<ObstacleField>(Lifetime.Singleton);
            builder.Register<PickupField>(Lifetime.Singleton);

            builder.Register<BoardState>(Lifetime.Singleton);
            builder.Register<BoardMovement>(Lifetime.Singleton);
            builder.Register<BoardBody>(Lifetime.Singleton);
            builder.Register<BoardCollision>(Lifetime.Singleton);
            builder.Register<PickupCollector>(Lifetime.Singleton);
            builder.Register<PowerUps>(Lifetime.Singleton);

            builder.Register<RunState>(Lifetime.Singleton);
            builder.Register<ScoreCounter>(Lifetime.Singleton);

            // Views: registered so they get injected.
            builder.RegisterComponentInHierarchy<BoardView>();
            builder.RegisterComponentInHierarchy<RiderIK>();
            builder.RegisterComponentInHierarchy<SurfboardWaterFX>();
            builder.RegisterComponentInHierarchy<RiderWaterFX>();
            builder.RegisterComponentInHierarchy<SurfCamera>();
            builder.RegisterComponentInHierarchy<PassageBounds>();
            builder.RegisterComponentInHierarchy<ShoreSpawner>();
            builder.RegisterComponentInHierarchy<SkyCycle>();
            builder.RegisterComponentInHierarchy<HudView>();
            builder.RegisterComponentInHierarchy<RunSounds>();
            // Footsteps, jumps and landings from the runner animations played on trains.
            builder.RegisterComponentInHierarchy<CharacterAnimationEventListener>();

            // RunLoop first: it initializes the board before the run starts.
            builder.RegisterEntryPoint<RunLoop>();
            builder.RegisterEntryPoint<RunStateMachine>().AsSelf();
            builder.RegisterEntryPoint<HudPresenter>();
        }
    }
}
