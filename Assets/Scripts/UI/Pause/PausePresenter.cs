using System;
using Game.Surf;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace Game.UI
{
	/// <summary>
	/// Pause menu: Resume or Quit to menu. The pause key resumes.
	/// </summary>
	public sealed class PausePresenter : IScreen, IStartable, IDisposable
	{
		private readonly IRunFlow _runFlow;
		private readonly ScreenStack _screens;
		private readonly GameUi _ui;

		private VisualElement _screen;
		private Button _resumeButton;
		private Button _quitButton;

		public PausePresenter(IRunFlow runFlow, ScreenStack screens, GameUi ui)
		{
			_runFlow = runFlow;
			_screens = screens;
			_ui = ui;
		}

		public void Start()
		{
			_runFlow.PhaseChanged += OnPhaseChanged;
			OnPhaseChanged(_runFlow.Phase);
			_ui.RegisterReloadCallback(Bind);
		}

		public void Dispose()
		{
			_runFlow.PhaseChanged -= OnPhaseChanged;
			_ui.UnregisterReloadCallback(Bind);
			Unbind();
		}

		public void OnConfirm()
		{
		}

		public void OnBack()
		{
			_runFlow.Resume();
		}

		private void Bind(VisualElement root)
		{
			Unbind();

			_screen = root.Q("pause");
			_resumeButton = _screen.Q<Button>("resume-button");
			_quitButton = _screen.Q<Button>("quit-button");
			_resumeButton.clicked += _runFlow.Resume;
			_quitButton.clicked += _runFlow.QuitToMenu;

			OnPhaseChanged(_runFlow.Phase);
		}

		private void Unbind()
		{
			if (_resumeButton == null)
			{
				return;
			}

			_resumeButton.clicked -= _runFlow.Resume;
			_quitButton.clicked -= _runFlow.QuitToMenu;
		}

		private void OnPhaseChanged(RunPhase phase)
		{
			bool visible = phase == RunPhase.Paused;

			if (visible)
			{
				_screens.Show(this);
			}
			else
			{
				_screens.Hide(this);
			}

			if (_screen == null)
			{
				return;
			}

			GameUi.SetVisible(_screen, visible);

			if (visible)
			{
				_resumeButton.Focus();
			}
		}
	}
}
