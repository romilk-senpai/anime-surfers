using UnityEngine;
using VContainer;

namespace Game.Surf
{
	/// <summary>
	/// Chase camera that trails along the passage curve behind the board. It rises with the train roofs she rides on,
	/// while jumps and wave bobbing above that level are only partly followed. After a crash it stays where she fell
	/// instead of following the board on. The field of view springs wider with the nitro boost, overshooting a little
	/// as it kicks in, and the boost shakes the camera; on screens wider than maxAspect the horizontal field of view
	/// is held instead of stretching.
	/// </summary>
	[RequireComponent(typeof(Camera))]
	public class SurfCamera : MonoBehaviour
	{
		[SerializeField] private float followDistance = 6f;
		[SerializeField] private float height = 2.8f;
		[Range(0f, 1f)]
		[SerializeField] private float lateralFollow = 0.5f;
		[Tooltip("How much of the board's height above the floor (water or train roof) the camera follows.")]
		[Range(0f, 1f)]
		[SerializeField] private float verticalFollow = 0.4f;
		[Tooltip("Seconds to settle on a new floor, stepping onto or off a train.")]
		[SerializeField] private float floorSmoothTime = 0.25f;
		[SerializeField] private float lookAhead = 6f;
		[SerializeField] private float lookHeight = 0.8f;
		[SerializeField] private float positionSmoothTime = 0.12f;
		[SerializeField] private float rotationSharpness = 10f;
		[Tooltip("Degrees added to the field of view at full nitro boost.")]
		[SerializeField] private float nitroFovBoost = 12f;
		[Tooltip("How fast the field of view springs toward the boost target, in hertz.")]
		[SerializeField, Min(0.01f)] private float fovSpringFrequency = 1.6f;
		[Tooltip("Below 1 the field of view overshoots the target as the boost kicks in.")]
		[SerializeField, Range(0f, 1f)] private float fovSpringDamping = 0.45f;
		[Header("Boost Shake")]
		[Tooltip("Pitch, yaw and roll shake in degrees at full nitro boost.")]
		[SerializeField] private Vector3 shakeRotation = new(0.35f, 0.25f, 0.5f);
		[Tooltip("Positional shake in meters at full nitro boost.")]
		[SerializeField] private float shakePosition = 0.03f;
		[Tooltip("How fast the shake jitters.")]
		[SerializeField] private float shakeFrequency = 14f;
		[Tooltip("Widest aspect the field of view is tuned for. Wider screens keep this aspect's horizontal FOV instead of stretching.")]
		[SerializeField] private float maxAspect = 16f / 9f;

		private BoardState _board;
		private SurfPath _path;
		private Nitro _nitro;
		private Camera _camera;
		private float _baseFov;
		private Vector3 _position;
		private Quaternion _rotation;
		private Vector3 _velocity;
		private float _fovOffset;
		private float _fovVelocity;
		private float _floorTarget;
		private float _floor;
		private float _floorVelocity;
		private float _followDistance;
		private float _followLateral;
		private float _followHeight;

		[Inject]
		public void Construct(BoardState board, SurfPath path, Nitro nitro)
		{
			_board = board;
			_path = path;
			_nitro = nitro;
		}

		private void Awake()
		{
			_camera = GetComponent<Camera>();
			_baseFov = _camera.fieldOfView;
			_position = transform.position;
			_rotation = transform.rotation;
		}

		private void LateUpdate()
		{
			if (!_board.IsCrashed)
			{
				_followDistance = _board.Distance;
				_followLateral = _board.Lateral;
				_followHeight = _board.Height;
			}

			_path.Sample(_followDistance - followDistance, out Vector3 behind, out Vector3 behindForward);
			_path.Sample(_followDistance, out Vector3 followPosition, out Vector3 followForward);
			Vector3 right = Vector3.Cross(Vector3.up, behindForward);
			float boardHeight = _followHeight;

			// Water is the zero level; in the air the floor stays where she left it.
			if (_board.IsOnSurface)
			{
				_floorTarget = boardHeight;
			}
			else if (_board.IsOnWater)
			{
				_floorTarget = 0f;
			}

			_floor = Mathf.SmoothDamp(_floor, _floorTarget, ref _floorVelocity, floorSmoothTime);
			float rise = _floor + (boardHeight - _floor) * verticalFollow;

			Vector3 desired = behind + right * (_followLateral * lateralFollow);
			desired.y = behind.y + rise + height;

			_position = Vector3.SmoothDamp(_position, desired, ref _velocity, positionSmoothTime);

			Vector3 lookTarget = followPosition + followForward * lookAhead +
			                     Vector3.Cross(Vector3.up, followForward) * (_followLateral * lateralFollow);
			lookTarget.y = followPosition.y + rise + lookHeight;

			Quaternion targetRotation = Quaternion.LookRotation(lookTarget - _position);
			_rotation = Quaternion.Slerp(_rotation, targetRotation, 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));

			// The shake rides on top of the smoothed pose so it never feeds back into the follow.
			float shake = _nitro.Blend;
			float t = Time.time * shakeFrequency;
			Vector3 angles = Vector3.Scale(shakeRotation, new Vector3(Noise(t, 0f), Noise(t, 10f), Noise(t, 20f)));
			Vector3 offset = new Vector3(Noise(t, 30f), Noise(t, 40f), 0f) * shakePosition;
			transform.SetPositionAndRotation(_position + _rotation * offset * shake,
				_rotation * Quaternion.Euler(angles * shake));

			// Damped spring toward the boost FOV; underdamped so the boost kicks in with a little overshoot.
			float omega = 2f * Mathf.PI * fovSpringFrequency;
			float dt = Time.deltaTime;
			_fovVelocity += (omega * omega * (nitroFovBoost * _nitro.Blend - _fovOffset) -
			                 2f * fovSpringDamping * omega * _fovVelocity) * dt;
			_fovOffset += _fovVelocity * dt;

			float fov = _baseFov + _fovOffset;
			if (_camera.aspect > maxAspect)
			{
				float horizontalFov = Camera.VerticalToHorizontalFieldOfView(fov, maxAspect);
				fov = Camera.HorizontalToVerticalFieldOfView(horizontalFov, _camera.aspect);
			}

			_camera.fieldOfView = fov;
		}

		/// <summary>Perlin noise in -1..1 along one row of the noise field.</summary>
		private static float Noise(float t, float row) => Mathf.PerlinNoise(t, row) * 2f - 1f;
	}
}
