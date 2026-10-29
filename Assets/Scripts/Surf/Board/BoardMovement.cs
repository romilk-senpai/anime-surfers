using Game.Core;
using UnityEngine;

namespace Game.Surf
{
    /// <summary>
    /// Runner-style motion along the passage: forward speed and lane changes.
    /// </summary>
    public sealed class BoardMovement
    {
        private readonly BoardSettings _settings;
        private readonly BoardState _state;
        private readonly SurfPath _path;
        private readonly IInputService _input;

        private bool _running;

        public BoardMovement(BoardSettings settings, BoardState state, SurfPath path, IInputService input)
        {
            _settings = settings;
            _state = state;
            _path = path;
            _input = input;
        }

        public void Initialize()
        {
            _state.Lane = _settings.LaneCount / 2;
            _state.Lateral = _settings.LaneOffset(_state.Lane);
            SamplePath();
        }

        public void StartRunning()
        {
            _running = true;
            _state.Speed = _settings.StartSpeed;
        }

        public void Tick(float dt)
        {
            if (_state.IsCrashed)
            {
                Coast(dt);
                return;
            }

            if (_running)
            {
                ReadLaneInput();

                _state.Speed = Mathf.MoveTowards(_state.Speed, _settings.MaxSpeed, _settings.Acceleration * dt);
                _state.Distance += _state.Speed * dt;
            }

            _state.PreviousLateral = _state.Lateral;

            float lateralVelocity = _state.LateralVelocity;
            _state.Lateral = Mathf.SmoothDamp(_state.Lateral, _settings.LaneOffset(_state.Lane), ref lateralVelocity,
                _settings.LaneChangeTime, Mathf.Infinity, dt);
            _state.LateralVelocity = lateralVelocity;

            SamplePath();
            _path.TrimBehind(_state.Distance);
        }

        /// <summary>The riderless board drifts on with the speed the crash left it, free of the lanes.</summary>
        private void Coast(float dt)
        {
            float drag = Mathf.Exp(-_settings.CrashDrag * dt);
            _state.Speed *= drag;
            _state.Distance += _state.Speed * dt;

            _state.PreviousLateral = _state.Lateral;
            _state.LateralVelocity *= drag;
            float edge = _settings.PassageHalfWidth - _settings.BoardWidth * 0.5f;
            _state.Lateral += _state.LateralVelocity * dt;
            if (Mathf.Abs(_state.Lateral) > edge)
            {
                _state.Lateral = Mathf.Clamp(_state.Lateral, -edge, edge);
                _state.LateralVelocity = 0f;
            }

            SamplePath();
        }

        private void ReadLaneInput()
        {
            int direction = _input.LaneChange;

            if (direction != 0)
            {
                _state.Lane = Mathf.Clamp(_state.Lane + direction, 0, _settings.LaneCount - 1);
            }
        }

        private void SamplePath()
        {
            _path.Sample(_state.Distance, out Vector3 position, out Vector3 forward);
            _state.PathPosition = position;
            _state.PathForward = forward;
        }
    }
}
