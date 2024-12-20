using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace Player
{
    public class PlayerInputController : MonoBehaviour, IPlayerInputController
    {
        private IPlayerInputHandler _playerInputHandler;

        private SurfersInputActions _inputActions;

        [Inject]
        private void Inject(IPlayerInputHandler playerInputHandler)
        {
            _playerInputHandler = playerInputHandler;
        }

        public void Awake()
        {
            _inputActions = new SurfersInputActions();
        }
        
        private void Update()
        {
            if (_inputActions.Player.Move.triggered)
            {
                float moveValue = _inputActions.Player.Move.ReadValue<float>();

                if (moveValue > 0)
                {
                    _playerInputHandler.ProcessRight();
                }
                else
                {
                    _playerInputHandler.ProcessLeft();
                }
            }
        }

        private void ProcessJump(InputAction.CallbackContext ctx)
        {
            _playerInputHandler.ProcessJump();
        }

        public void Activate()
        {
            _inputActions.Player.Jump.performed += ProcessJump;

            _inputActions.Enable();
        }

        public void Deactivate()
        {
            _inputActions.Player.Jump.performed -= ProcessJump;

            _inputActions.Disable();
        }
    }
}