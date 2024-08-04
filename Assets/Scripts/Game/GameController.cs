using System;
using System.Collections;
using Cysharp.Threading.Tasks;
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
        
        private PlayerObject _playerObject;
        private PlayerController _playerController;
        private IPlayerInputController _inputController;

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
        private void Inject(PlayerController playerController, PlayerObject playerObject,
            IPlayerInputController inputController)
        {
            _playerController = playerController;
            _playerObject = playerObject;
            _inputController = inputController;
        }

        private IEnumerator Start()
        {
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
        }

        private void Update()
        {
            if (_gameStarted && !_playerDead)
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

        private void OnPlayerHit(ControllerColliderHit hit)
        {
            switch (hit.gameObject.tag)
            {
                case "Obstacle":
                {
                    OnPlayerLost();

                    break;
                }
                case "Wall":
                {
                    CurrentHp--;

                    if (CurrentHp <= 0)
                    {
                        OnPlayerLost();
                    }

                    break;
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

            await UniTask.WaitForSeconds(3f);

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}