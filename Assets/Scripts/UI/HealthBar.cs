using Game;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace UI
{
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Image lifeImagePrefab;

        [SerializeField] private RectTransform lifeImageContainer;

        private IGameController _gameController;
        private Image[] _spawnedLives;

        [Inject]
        private void Inject(IGameController gameController)
        {
            _gameController = gameController;
        }

        private void Start()
        {
            _gameController.OnPlayerHpUpdated += OnPlayerHpUpdated;

            _spawnedLives = new Image[_gameController.MaxHp];

            for (int i = 0; i < _gameController.MaxHp; i++)
            {
                var lifeImage = Instantiate(lifeImagePrefab, lifeImageContainer);

                lifeImage.gameObject.SetActive(true);

                _spawnedLives[i] = lifeImage;

                lifeImage.color = Color.white;
            }
        }

        private void OnPlayerHpUpdated(int playerHp)
        {
            int hp = Mathf.Clamp(playerHp, 0, _gameController.MaxHp);

            for (int i = 0; i < _spawnedLives.Length; i++)
            {
                _spawnedLives[i].color = i < hp ? Color.white : new Color(1, 1, 1, 0);
            }
        }
    }
}