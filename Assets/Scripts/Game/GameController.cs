using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using Game.Audio;
using Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace Game
{
    public class GameController : MonoBehaviour, IGameController
    {
        [SerializeField] private int maxHp = 2;
        [SerializeField] private int hpRegenTime = 10;
        [SerializeField] private float defaultScoreMultiplier = 2f;

        [SerializeField] private AudioClip hitSound;
        [SerializeField] private AudioClip damageSound;
        [SerializeField] private AudioClip deathSound;

        private PlayerObject _playerObject;
        private IPlayerController _playerController;
        private IPlayerInputController _inputController;
        private ISoundController _soundController;
        private IPlayerCameraController _cameraController;

        private int _currentHp;
        private int _gameScore;
        private int _scoreGlobalMultiplier = 1;
        private float _scoreRunMultiplier = 1f;

        private bool _gameStarted;
        private float _hpRegenTimer;
        private bool _playerDead;
        private float _gameStartTime;

        public int MaxHp => maxHp;

        public int CurrentHp
        {
            get => _currentHp;
            private set
            {
                _currentHp = value;

                OnPlayerHpUpdated?.Invoke(_currentHp);
            }
        }

        public int GameScore
        {
            get => _gameScore;
            private set
            {
                _gameScore = value;

                OnPlayerScoreUpdated?.Invoke(_gameScore);
            }
        }

        public int ScoreGlobalMultiplier => _scoreGlobalMultiplier;

        public event Action<int> OnPlayerHpUpdated;
        public event Action<int> OnPlayerScoreUpdated;

        [Inject]
        private void Inject(IPlayerController playerController, PlayerObject playerObject,
            IPlayerInputController inputController, ISoundController soundController,
            IPlayerCameraController cameraController)
        {
            _playerController = playerController;
            _playerObject = playerObject;
            _inputController = inputController;
            _soundController = soundController;
            _cameraController = cameraController;
        }

        private void Awake()
        {
            RenderSettings.fog = true;
        }

        private IEnumerator Start()
        {
            _cameraController.StartFollowing(_playerObject.LookAtTarget);

            yield return new WaitForSeconds(3f);

            StartGame();
        }

        private void StartGame()
        {
            _playerObject.PlayerCollider.OnCollisionHit += OnPlayerHit;

            CurrentHp = maxHp;

            _inputController.Activate();

            _playerController.StartRunning();

            _gameStarted = true;
            _gameStartTime = Time.time;

            _soundController.StartMusic();
        }

        private void Update()
        {
            if (_playerDead)
            {
                return;
            }

            if (_gameStarted)
            {
                GameScore = Mathf.FloorToInt(defaultScoreMultiplier * _scoreGlobalMultiplier * _scoreRunMultiplier *
                                             (Time.time - _gameStartTime));

                _scoreRunMultiplier = 1 + (Time.time - _gameStartTime) / 60f;
            }

            if (CurrentHp < maxHp)
            {
                _hpRegenTimer += Time.deltaTime;

                if (_hpRegenTimer > hpRegenTime)
                {
                    CurrentHp++;

                    _hpRegenTimer = 0f;
                }
            }

            if (_playerObject.PlayerTransform.position.y < -5f)
            {
                OnPlayerLost();
            }
        }

        private void OnPlayerHit(Collision col)
        {
            Vector3 playerCenter = _playerObject.PlayerTransform.position + _playerObject.PlayerCharacterController.center;

            Vector3 hitDirection = (col.contacts[0].point - playerCenter).normalized;
            float dotX = Vector3.Dot(_playerObject.PlayerTransform.right, hitDirection);
            float dotZ = Vector3.Dot(_playerObject.PlayerTransform.forward, hitDirection);

            if (!col.collider.gameObject.CompareTag("Obstacle") &&
                !col.collider.gameObject.CompareTag("Wall"))
            {
                return;
            }
            Logger.DrawRay(playerCenter, _playerObject.PlayerTransform.forward, Color.blue, 5f);
            Logger.DrawRay(playerCenter, _playerObject.PlayerTransform.right, Color.green, 5f);
            Logger.DrawRay(playerCenter, hitDirection, Color.red, 5f);

            _soundController.PlayClipAtPosition(hitSound, col.contacts[0].point);

            if (dotZ > .5f)
            {
                Logger.Log($"Hit frontal {dotZ}");

                CurrentHp = 0;
                OnPlayerLost();
            }
            else if (Mathf.Abs(dotX) > .5f)
            {
                Logger.Log($"Hit {(dotX > 0 ? "Right" : "Left")} {dotX}");

                CurrentHp--;
                if (CurrentHp > 0)
                {
                    _playerController.ProcessHit(dotX > 0 ? HitSide.Right : HitSide.Left);
                    _soundController.PlayClipAtPosition(damageSound, playerCenter);
                }
                else
                {
                    OnPlayerLost();
                }
            }
        }

        private async void OnPlayerLost()
        {
            if (_playerDead)
            {
                return;
            }

            CurrentHp = 0;

            _playerDead = true;

            _playerObject.PlayerCollider.OnCollisionHit -= OnPlayerHit;

            _inputController.Deactivate();
            _playerController.ProcessDeath();

            _cameraController.SetDeathCamera(_playerObject.PlayerTransform);

            _soundController.PlayClipAtPosition(deathSound, _playerObject.transform.position);

            _soundController.StopMusic();

            bool isCancelled = await UniTask.WaitForSeconds(3f, cancellationToken: destroyCancellationToken)
                .SuppressCancellationThrow();

            if (isCancelled)
            {
                return;
            }

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}