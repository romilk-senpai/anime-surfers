using System;
using UnityEngine;
using VContainer;

namespace Game.Surf
{
	/// <summary>
	/// Places the board from <see cref="BoardState"/>: fits it to the water or obstacle surface, leans into carves
	/// and turns, and squashes the rider while sliding on the water. Bouncing off a wall it spins away.
	/// Raises <see cref="Placed"/> once the board is in place each frame, for the views that follow it.
	/// </summary>
	public class BoardView : MonoBehaviour
	{
		[Header("Tilt")]
		[SerializeField] private float tiltSharpness = 10f;
		[Tooltip("Roll in degrees at full lane-change speed.")]
		[SerializeField] private float carveRoll = 22f;
		[Tooltip("Yaw in degrees at full lane-change speed.")]
		[SerializeField] private float carveYaw = 12f;
		[Tooltip("Roll in degrees per degree/second of path turn rate.")]
		[SerializeField] private float turnLean = 0.15f;

		[Header("Crash")]
		[Tooltip("Yaw spin in degrees per second when bouncing off a wall.")]
		[SerializeField] private float wallBounceSpin = 540f;
		[SerializeField] private float spinDrag = 1.5f;

		[Header("Rider")]
		[Tooltip("The board's meshes, which the rider carries under her arm on trains.")]
		[SerializeField] private Transform model;
		[SerializeField] private Transform rider;
		[Tooltip("The glider, pivoted on its grip, which the rider holds up while gliding.")]
		[SerializeField] private Transform glider;
		[SerializeField, Range(0.2f, 1f)] private float slideRiderScale = 0.5f;
		[SerializeField] private float riderScaleSharpness = 20f;

		private BoardSettings _settings;
		private IBoardState _state;
		private SurfPath _path;
		private IWaterSurface _water;
		private ObstacleField _obstacles;

		private bool _placed;
		private float _riderScale = 1f;
		private float _spin;
		private float _spinVelocity;
		private Vector3 _riderBaseScale = Vector3.one;

		/// <summary>
		/// Raised at the end of every Update, paused frames included, once the board's transform is set for the frame.
		/// </summary>
		public event Action Placed;

		/// <summary>The board's meshes, apart from the riders and effects on the board.</summary>
		public Transform Model => model;

		/// <summary>The glider, which <see cref="RiderIK"/> unfolds above the rider while gliding.</summary>
		public Transform Glider => glider;

		[Inject]
		public void Construct(BoardSettings settings, IBoardState state, SurfPath path, IWaterSurface water,
			ObstacleField obstacles)
		{
			_settings = settings;
			_state = state;
			_path = path;
			_water = water;
			_obstacles = obstacles;
			_state.Crashed += OnCrashed;
		}

		private void OnDestroy()
		{
			if (_state != null)
			{
				_state.Crashed -= OnCrashed;
			}
		}

		private void OnCrashed()
		{
			if (_state.CrashKind == CrashKind.Wall)
			{
				// Turning toward the side it flies off to.
				_spinVelocity = Mathf.Sign(_state.LateralVelocity) * wallBounceSpin;
			}
		}

		private void Awake()
		{
			if (rider != null)
			{
				_riderBaseScale = rider.localScale;
			}
		}

		private void Update()
		{
			float dt = Time.deltaTime;
			Vector3 position = _state.PlanarPosition;
			transform.position = new Vector3(position.x, _state.Height, position.z);

			if (!_placed)
			{
				_placed = true;
				transform.rotation = Quaternion.LookRotation(_state.PathForward);
			}

			if (dt > 0f)
			{
				UpdateRotation(position, dt);
				UpdateRider(dt);
			}

			Placed?.Invoke();
		}

		private void UpdateRotation(Vector3 position, float dt)
		{
			float carve = _state.Carve;
			_spinVelocity *= Mathf.Exp(-spinDrag * dt);
			_spin += _spinVelocity * dt;
			Vector3 pathForward = _state.PathForward;
			Vector3 right = _state.PathRight;
			float boardLength = _settings.BoardLength;
			float boardWidth = _settings.BoardWidth;

			Vector3 forward = Quaternion.AngleAxis(carve * carveYaw + _spin, Vector3.up) * pathForward;
			Vector3 up = Vector3.up;

			if (_state.IsOnSurface)
			{
				// Follow ramps; obstacle tops are level across.
				float half = boardLength * 0.5f;
				float nose = SupportHeight(_state.Distance + half);
				float tail = SupportHeight(_state.Distance - half);
				up = Vector3.Cross(pathForward * boardLength + Vector3.up * (nose - tail), right).normalized;
			}
			else if (_state.IsOnWater)
			{
				// Fit the board to the surface under its nose, tail and rails.
				Vector3 nose = SurfacePoint(position + forward * (boardLength * 0.5f));
				Vector3 tail = SurfacePoint(position - forward * (boardLength * 0.5f));
				Vector3 rightRail = SurfacePoint(position + right * (boardWidth * 0.5f));
				Vector3 leftRail = SurfacePoint(position - right * (boardWidth * 0.5f));
				up = Vector3.Cross(nose - tail, rightRail - leftRail).normalized;
			}

			_path.Sample(_state.Distance + 1f, out _, out Vector3 aheadForward);
			float turnRate = Vector3.SignedAngle(pathForward, aheadForward, Vector3.up) * _state.Speed;
			float roll = carve * carveRoll + turnRate * turnLean;

			Quaternion target = Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, up), up) *
								Quaternion.Euler(0f, 0f, -roll);
			transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-tiltSharpness * dt));
		}

		private void UpdateRider(float dt)
		{
			if (rider == null)
			{
				return;
			}

			// The roll on trains already ducks low enough.
			float target = _state.IsSliding && !_state.IsRunning ? slideRiderScale : 1f;
			_riderScale = Mathf.Lerp(_riderScale, target, 1f - Mathf.Exp(-riderScaleSharpness * dt));
			rider.localScale = new Vector3(_riderBaseScale.x, _riderBaseScale.y * _riderScale, _riderBaseScale.z);
		}

		private float SupportHeight(float distance)
		{
			return _obstacles.TryGetSupport(distance, distance, _state.Lateral, _settings.BoardWidth * 0.5f,
				_state.Height + _settings.StepHeight, _state.WaterHeight, out float support)
				? support
				: _state.Height;
		}

		private Vector3 SurfacePoint(Vector3 position)
		{
			return new Vector3(position.x, _water.GetHeight(position), position.z);
		}
	}
}
