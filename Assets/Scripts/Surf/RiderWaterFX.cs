using UnityEngine;
using VContainer;

namespace Game.Surf
{
    /// <summary>
    /// Splashes where Miko's body hits the water after a crash, using the board's splash effects. Each tracked body
    /// part splashes as it enters the water fast enough, so a fall splashes where she lands and the limbs follow.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    // After RiderIK has placed the fallen body for the frame.
    [DefaultExecutionOrder(110)]
    public class RiderWaterFX : MonoBehaviour
    {
        private static readonly HumanBodyBones[] Parts =
        {
            HumanBodyBones.Hips, HumanBodyBones.Head, HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
            HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot
        };

        [Tooltip("Slowest downward speed of a body part entering the water that splashes; sinking stays quiet.")]
        [SerializeField] private float minEntrySpeed = 2f;
        [Tooltip("Entry speed of the strongest splash.")]
        [SerializeField] private float fullSplashSpeed = 6f;
        [Tooltip("Seconds before the same body part can splash again.")]
        [SerializeField] private float cooldown = 0.3f;

        private readonly Transform[] _bones = new Transform[Parts.Length];
        private readonly float[] _previousHeights = new float[Parts.Length];
        private readonly bool[] _wasAbove = new bool[Parts.Length];
        private readonly float[] _nextSplashTimes = new float[Parts.Length];

        private BoardState _state;
        private WaterSurface _water;
        private SurfboardWaterFX _boardFX;
        private bool _tracking;

        [Inject]
        public void Construct(BoardState state, WaterSurface water, SurfboardWaterFX boardFX)
        {
            _state = state;
            _water = water;
            _boardFX = boardFX;
        }

        private void Awake()
        {
            var animator = GetComponent<Animator>();
            for (int i = 0; i < Parts.Length; i++)
            {
                _bones[i] = animator.GetBoneTransform(Parts[i]);
            }
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (_state == null || !_state.IsCrashed || dt <= 0f)
            {
                return;
            }

            for (int i = 0; i < Parts.Length; i++)
            {
                Vector3 position = _bones[i].position;
                float surface = _water.GetHeight(position);
                bool above = position.y > surface;

                // Starts from the first crashed frame, so standing on the board doesn't count as entering.
                if (_tracking && _wasAbove[i] && !above && Time.time >= _nextSplashTimes[i])
                {
                    float speed = (_previousHeights[i] - position.y) / dt;
                    if (speed >= minEntrySpeed)
                    {
                        _boardFX.Splash(new Vector3(position.x, surface, position.z),
                            Mathf.Clamp01(speed / fullSplashSpeed));
                        _nextSplashTimes[i] = Time.time + cooldown;
                    }
                }

                _wasAbove[i] = above;
                _previousHeights[i] = position.y;
            }

            _tracking = true;
        }
    }
}
