using System.Text;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public class HudView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI coinsText;
        [SerializeField] private TextMeshProUGUI powerUpsText;

        private readonly StringBuilder _powerUps = new StringBuilder();

        public void SetScore(int score)
        {
            scoreText.text = score.ToString("000000");
        }

        public void SetCoins(int coins)
        {
            coinsText.text = coins.ToString();
        }

        /// <summary>Remaining whole seconds of each power-up; 0 hides it.</summary>
        public void SetPowerUps(int magnetSeconds, int multiplierSeconds, int multiplier)
        {
            _powerUps.Clear();

            if (magnetSeconds > 0)
            {
                _powerUps.Append("MAGNET ").Append(magnetSeconds).Append('\n');
            }

            if (multiplierSeconds > 0)
            {
                _powerUps.Append('x').Append(multiplier).Append(" SCORE ").Append(multiplierSeconds);
            }

            powerUpsText.text = _powerUps.ToString();
        }
    }
}
