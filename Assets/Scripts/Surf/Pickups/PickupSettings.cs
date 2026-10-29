using UnityEngine;

namespace Game.Surf
{
    [CreateAssetMenu(menuName = "Anime Surfers/Pickup Settings", fileName = "PickupSettings")]
    public sealed class PickupSettings : ScriptableObject
    {
        [Tooltip("How far from the rider's body a pickup is still collected.")]
        [SerializeField, Min(0f)] private float pickupRadius = 0.6f;

        [Header("Magnet")]
        [SerializeField, Min(0f)] private float magnetDuration = 10f;
        [Tooltip("Coins up to this far ahead are pulled in, from every lane.")]
        [SerializeField, Min(0f)] private float magnetRange = 10f;
        [Tooltip("Pull speed relative to the board.")]
        [SerializeField, Min(0f)] private float magnetPullSpeed = 25f;

        [Header("Score Multiplier")]
        [SerializeField, Min(0f)] private float multiplierDuration = 10f;
        [SerializeField, Min(1)] private int scoreMultiplier = 2;

        public float PickupRadius => pickupRadius;

        public float MagnetDuration => magnetDuration;
        public float MagnetRange => magnetRange;
        public float MagnetPullSpeed => magnetPullSpeed;

        public float MultiplierDuration => multiplierDuration;
        public int ScoreMultiplier => scoreMultiplier;
    }
}
