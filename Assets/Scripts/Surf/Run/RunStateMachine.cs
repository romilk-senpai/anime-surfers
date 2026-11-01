using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;
using VContainer.Unity;

namespace Game.Surf
{
	public enum RunPhase
	{
		Menu,
		Intro,
		Running,
		Paused,
		Crashed,
		Results
	}

	/// <summary>
	/// Run flow: Menu → Intro (board floats, no input) → Running ⇄ Paused → Crashed → Results.
	/// Leaving the results or the pause menu reloads the scene; "Surf again" skips the menu on the next load.
	/// </summary>
	public sealed class RunStateMachine : IRunFlow, IStartable, IDisposable
	{
		private readonly RunSettings _settings;
		private readonly BoardState _board;
		private readonly BoardMovement _movement;
		private readonly RunState _run;
		private readonly IInputService _input;
		private readonly ISceneLoader _sceneLoader;
		private readonly IPlayerProgress _progress;
		private readonly GameSession _session;
		private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
		private bool _leaving;

		public RunStateMachine(RunSettings settings, BoardState board, BoardMovement movement, RunState run,
			IInputService input, ISceneLoader sceneLoader, IPlayerProgress progress, GameSession session)
		{
			_settings = settings;
			_board = board;
			_movement = movement;
			_run = run;
			_input = input;
			_sceneLoader = sceneLoader;
			_progress = progress;
			_session = session;
		}

		public RunPhase Phase { get; private set; }

		public event Action<RunPhase> PhaseChanged;

		public void Start()
		{
			_board.Crashed += OnCrashed;

			if (_session.SkipMenu)
			{
				_session.SkipMenu = false;
				RunIntro().Forget();
			}
			else
			{
				SetPhase(RunPhase.Menu);
			}
		}

		public void Dispose()
		{
			_board.Crashed -= OnCrashed;
			_input.Disable();
			SetTimeFrozen(false);
			_cancellation.Cancel();
			_cancellation.Dispose();
		}

		public void StartRun()
		{
			if (Phase == RunPhase.Menu)
			{
				RunIntro().Forget();
			}
		}

		public void Pause()
		{
			if (Phase != RunPhase.Running)
			{
				return;
			}

			_input.Disable();
			SetTimeFrozen(true);
			SetPhase(RunPhase.Paused);
		}

		public void Resume()
		{
			if (Phase != RunPhase.Paused)
			{
				return;
			}

			SetTimeFrozen(false);
			_input.Enable();
			SetPhase(RunPhase.Running);
		}

		/// <summary>From the results screen: reloads straight into a new run.</summary>
		public void Restart()
		{
			if (Phase == RunPhase.Results)
			{
				_session.SkipMenu = true;
				Leave();
			}
		}

		/// <summary>From the pause menu or the results screen.</summary>
		public void QuitToMenu()
		{
			if (Phase == RunPhase.Paused || Phase == RunPhase.Results)
			{
				Leave();
			}
		}

		private void Leave()
		{
			// Buttons and keys can both fire in one frame.
			if (_leaving)
			{
				return;
			}

			_leaving = true;
			SetTimeFrozen(false);
			_sceneLoader.ReloadActiveScene();
		}

		private async UniTaskVoid RunIntro()
		{
			SetPhase(RunPhase.Intro);

			if (_settings.IntroDuration > 0f && await Wait(_settings.IntroDuration))
			{
				return;
			}

			SetPhase(RunPhase.Running);
			_input.Enable();
			_movement.StartRunning();
		}

		private void OnCrashed()
		{
			RunCrashed().Forget();
		}

		private async UniTaskVoid RunCrashed()
		{
			SetPhase(RunPhase.Crashed);
			_input.Disable();

			if (await Wait(_settings.ResultsDelay))
			{
				return;
			}

			_run.IsNewBest = _progress.RecordRun(_run.Score, _run.Coins);
			SetPhase(RunPhase.Results);
		}

		/// <summary>Returns true if the wait was cancelled.</summary>
		private UniTask<bool> Wait(float seconds)
		{
			return UniTask.WaitForSeconds(seconds, cancellationToken: _cancellation.Token)
				.SuppressCancellationThrow();
		}

		private static void SetTimeFrozen(bool frozen)
		{
			Time.timeScale = frozen ? 0f : 1f;
			AudioListener.pause = frozen;
		}

		private void SetPhase(RunPhase phase)
		{
			Phase = phase;
			PhaseChanged?.Invoke(phase);
		}
	}
}
