using VContainer;
using VContainer.Unity;

namespace Game.UI
{
	/// <summary>
	/// The screen presenters and the stack that routes the menu keys between them. Needs a <see cref="GameUi"/>.
	/// </summary>
	public sealed class UiInstaller : IInstaller
	{
		public void Install(IContainerBuilder builder)
		{
			builder.RegisterEntryPoint<ScreenStack>().AsSelf();
			builder.RegisterEntryPoint<HudPresenter>();
			builder.RegisterEntryPoint<GraphicsPresenter>().AsSelf();
			builder.RegisterEntryPoint<SettingsPresenter>().AsSelf();
			builder.RegisterEntryPoint<ShopPresenter>().AsSelf();
			builder.RegisterEntryPoint<MenuPresenter>();
			builder.RegisterEntryPoint<PausePresenter>();
			builder.RegisterEntryPoint<GameOverPresenter>();
		}
	}
}
