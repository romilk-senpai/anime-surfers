using System;
using UnityEngine.InputSystem;

namespace Game.Core
{
    public sealed class InputService : IInputService, IDisposable
    {
        private readonly SurfersInputActions _actions = new SurfersInputActions();

        public bool IsEnabled => _actions.Player.enabled;

        public int LaneChange
        {
            get
            {
                InputAction move = _actions.Player.Move;

                if (!move.triggered)
                {
                    return 0;
                }

                return move.ReadValue<float>() > 0f ? 1 : -1;
            }
        }

        public bool JumpPressed => _actions.Player.Jump.WasPerformedThisFrame();
        public bool SlidePressed => _actions.Player.Slide.WasPerformedThisFrame();

        public void Enable()
        {
            _actions.Player.Enable();
        }

        public void Disable()
        {
            _actions.Player.Disable();
        }

        public void Dispose()
        {
            _actions.Dispose();
        }
    }
}
