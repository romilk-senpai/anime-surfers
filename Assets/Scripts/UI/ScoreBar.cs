using Game;
using TMPro;
using UnityEngine;
using Zenject;

namespace UI
{
    public class ScoreBar : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI globalMultiplierText;

        private IGameController _gameController;

        [Inject]
        private void Inject(IGameController gameController)
        {
            _gameController = gameController;
        }

        private void Start()
        {
            _gameController.OnPlayerScoreUpdated += OnPlayerScoreUpdated;

            globalMultiplierText.text = _gameController.ScoreGlobalMultiplier.ToString("0x");
        }

        private void OnPlayerScoreUpdated(int score)
        {
            scoreText.text = score.ToString("000000");
        }
    }
}