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

        private void OnEnable()
        {
            _inputActions.Enable();
        }

        private void OnDisable()
        {
            _inputActions.Enable();
        }

        private void Update()
        {
            if (_inputActions.Player.Jump.triggered)
            {
                _playerInputHandler.ProcessJump();
            }
        }
    }
}