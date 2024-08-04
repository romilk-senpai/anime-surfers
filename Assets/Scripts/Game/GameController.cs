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
        [SerializeField] private int hpRegenTime = 2;

        private PlayerObject _playerObject;
        private PlayerController _playerController;
        private IPlayerInputController _inputController;

        private int _playerHp;
        private float _hpRegenTimer;
        private bool _playerDead = false;

        public int MaxHp => maxHp;
        public int CurrentHp
        {
            get => _playerHp;
            private set
            {
                _playerHp = value;

                OnPlayerHpUpdated?.Invoke(_playerHp);
            }
        }
        
        public event Action<int> OnPlayerHpUpdated;

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
        }

        private void Update()
        {
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

            _playerDead = true;

            _playerObject.PlayerCollider.OnCollisionHit -= OnPlayerHit;

            _inputController.Deactivate();
            _playerController.ProcessDeath();

            await UniTask.WaitForSeconds(3f);

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}