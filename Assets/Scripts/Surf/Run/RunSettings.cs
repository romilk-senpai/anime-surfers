using UnityEngine;

namespace Game.Surf
{
    [CreateAssetMenu(menuName = "Anime Surfers/Run Settings", fileName = "RunSettings")]
    public sealed class RunSettings : ScriptableObject
    {
        [Tooltip("Time the board floats in place before the run starts.")]
        [SerializeField, Min(0f)] private float introDuration;
        [Tooltip("Time between the crash and the restart.")]
        [SerializeField, Min(0f)] private float restartDelay = 2f;
        [SerializeField, Min(0f)] private float scorePerMeter = 1f;

        public float IntroDuration => introDuration;
        public float RestartDelay => restartDelay;
        public float ScorePerMeter => scorePerMeter;
    }
}
