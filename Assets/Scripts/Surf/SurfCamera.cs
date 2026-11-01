using UnityEngine;
using VContainer;

namespace Game.Surf
{
	/// <summary>
	/// Chase camera that trails along the passage curve behind the board. It rises with the train roofs she rides on,
	/// while jumps and wave bobbing above that level are only partly followed. After a crash it stays where she fell
	/// instead of following the board on. The field of view widens with the nitro boost.
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

		private BoardState _board;
		private SurfPath _path;
		private Nitro _nitro;
		private Camera _camera;
		private float _baseFov;
		private Vector3 _velocity;
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

			transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, positionSmoothTime);

			Vector3 lookTarget = followPosition + followForward * lookAhead +
			                     Vector3.Cross(Vector3.up, followForward) * (_followLateral * lateralFollow);
			lookTarget.y = followPosition.y + rise + lookHeight;

			Quaternion targetRotation = Quaternion.LookRotation(lookTarget - transform.position);
			transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
				1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));

			_camera.fieldOfView = _baseFov + nitroFovBoost * _nitro.Blend;
		}
	}
}
