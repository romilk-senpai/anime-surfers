using UnityEngine;
using Zenject;

namespace Player
{
    public class PlayerController : MonoBehaviour, IPlayerInputHandler
    {
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float jumpHeight = 10f;
        [SerializeField] private float gravity = 20f;
        [SerializeField] private float lineWidth = 1f;
        [SerializeField] private float groundDistance = 0.4f;
        [SerializeField] private LayerMask groundMask;

        private PlayerObject _playerObject;
        private IPlayerCameraController _cameraController;
        private IPlayerAnimatorController _playerAnimatorController;

        private bool _running;
        private float _runSpeedMultiplier;
        private float _verticalVelocity;
        private bool _jump;

        private int _playerLine;
        private bool _isGrounded;

        public float MoveSpeed => moveSpeed;
        public float JumpHeight => jumpHeight;

        [Inject]
        private void Inject(PlayerObject playerObject, IPlayerCameraController cameraController,
            IPlayerAnimatorController playerAnimatorController)
        {
            _playerObject = playerObject;
            _cameraController = cameraController;
            _playerAnimatorController = playerAnimatorController;
        }

        private void Start()
        {
            _cameraController.StartFollowing(_playerObject.LookAtTarget);

            _playerAnimatorController.SetSpeed(0f);
        }

        public void StartRunning()
        {
            _running = true;

            _runSpeedMultiplier = 1f;
        }

        private void Update()
        {
            _isGrounded = Physics.CheckSphere(_playerObject.GroundCheck.position, groundDistance, groundMask);

            _playerAnimatorController.SetSpeed(_runSpeedMultiplier);

            if (_running)
            {
                Vector3 moveVector = _playerObject.PlayerTransform.forward
                                     + new Vector3(_playerLine * lineWidth - _playerObject.transform.position.x, 0f, 0f);
                _playerObject.PlayerCharacterController.Move(moveVector * (_runSpeedMultiplier * (moveSpeed * Time.deltaTime)));
            }

            if (_isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;

                if (_jump)
                {
                    _jump = false;

                    _verticalVelocity = Mathf.Sqrt(jumpHeight * 2f * gravity);

                    _playerAnimatorController.JumpAnimation();
                }
            }

            _verticalVelocity -= gravity * Time.deltaTime;

            _playerObject.PlayerCharacterController.Move(new Vector3(0f, _verticalVelocity, 0f) * Time.deltaTime);
        }

        public void ProcessLeft()
        {
            if (_playerLine == -1)
            {
            }
            else
            {
                _playerLine--;
            }
        }

        public void ProcessRight()
        {
            if (_playerLine == 1)
            {
            }
            else
            {
                _playerLine++;
            }
        }

        public void ProcessJump()
        {
            if (!_isGrounded)
            {
                return;
            }

            _jump = true;
        }

        public void ProcessDeath()
        {
            _running = false;

            _playerAnimatorController.SetSpeed(0f);

            _playerAnimatorController.PlayDeath();
        }
    }
}