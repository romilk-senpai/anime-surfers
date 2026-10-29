using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using VContainer.Unity;

namespace Game.Surf
{
    public enum RunPhase
    {
        Intro,
        Running,
        Crashed
    }

    /// <summary>
    /// Run flow: Intro (board floats, no input) → Running → Crashed → scene restart.
    /// </summary>
    public sealed class RunStateMachine : IStartable, IDisposable
    {
        private readonly RunSettings _settings;
        private readonly BoardState _board;
        private readonly BoardMovement _movement;
        private readonly IInputService _input;
        private readonly ISceneLoader _sceneLoader;
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

        public RunStateMachine(RunSettings settings, BoardState board, BoardMovement movement, IInputService input,
            ISceneLoader sceneLoader)
        {
            _settings = settings;
            _board = board;
            _movement = movement;
            _input = input;
            _sceneLoader = sceneLoader;
        }

        public RunPhase Phase { get; private set; }

        public event Action<RunPhase> PhaseChanged;

        public void Start()
        {
            _board.Crashed += OnCrashed;
            RunIntro().Forget();
        }

        public void Dispose()
        {
            _board.Crashed -= OnCrashed;
            _input.Disable();
            _cancellation.Cancel();
            _cancellation.Dispose();
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

            if (await Wait(_settings.RestartDelay))
            {
                return;
            }

            _sceneLoader.ReloadActiveScene();
        }

        /// <summary>Returns true if the wait was cancelled.</summary>
        private UniTask<bool> Wait(float seconds)
        {
            return UniTask.WaitForSeconds(seconds, cancellationToken: _cancellation.Token)
                .SuppressCancellationThrow();
        }

        private void SetPhase(RunPhase phase)
        {
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }
}
