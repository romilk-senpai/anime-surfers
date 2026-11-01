using Game.Audio;
using Game.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Surf
{
	/// <summary>
	/// The surf scene's scope. Scene objects other systems depend on are referenced here; views that only consume
	/// services are listed under Auto Inject Game Objects (children included) and get injected without being
	/// registered.
	/// </summary>
	public class SurfLifetimeScope : LifetimeScope
	{
		[SerializeField] private BoardSettings boardSettings;
		[SerializeField] private RunSettings runSettings;
		[SerializeField] private PathSettings pathSettings;
		[SerializeField] private PickupSettings pickupSettings;
		[Tooltip("Where the passage starts and which way it faces.")]
		[SerializeField] private Transform pathOrigin;

		[Header("Scene Services")]
		[SerializeField] private WaterSurface water;
		[SerializeField] private SoundController sound;
		[SerializeField] private GameUi gameUi;

		protected override void Configure(IContainerBuilder builder)
		{
			builder.RegisterInstance(boardSettings);
			builder.RegisterInstance(runSettings);
			builder.RegisterInstance(pathSettings);
			builder.RegisterInstance(pickupSettings);

			builder.RegisterComponent(water).AsSelf().As<IWaterHeight>();
			builder.RegisterComponent(sound).As<ISoundController>();
			builder.RegisterComponent(gameUi);

			Pose origin = pathOrigin != null ? new Pose(pathOrigin.position, pathOrigin.rotation) : Pose.identity;
			new SurfSimulationInstaller(origin).Install(builder);
			new UiInstaller().Install(builder);
		}
	}
}
