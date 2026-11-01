using UnityEngine;
using VContainer;

namespace Game.Surf
{
	/// <summary>
	/// Layers board reactions on top of the surf stance with humanoid IK: the feet stay pinned to the deck while the
	/// body pops and tucks on jumps and wave airtime, absorbs landings, and leans and twists into lane changes.
	/// While sliding the animator lies Miko down on the board and these reactions fade out.
	/// On trains she steps off and runs upright, carrying the board under her right arm, and jumps, rolls and dodges
	/// with the runner animations. Heading for the water in the air she takes the surf stance again to land on the board.
	/// Bouncing off an obstacle's side she flinches away from it. On a crash she turns to the obstacle, lets go of the
	/// board and is knocked back off it into the water, moved by the crash animation's root motion. After a wall she
	/// sinks.
	/// Needs IK Pass enabled on the animator layer.
	/// </summary>
	[RequireComponent(typeof(Animator))]
	// After BoardView places the board, so the upright running pose doesn't lag a frame behind it.
	[DefaultExecutionOrder(100)]
	public class RiderIK : MonoBehaviour
	{
		// The generated stance clip has no IK goal curves, so the animator can't report where the hands and feet are.
		// Instead, read the stance limbs from the bones, then drive the goals once to measure how goals map to bones.
		private enum Calibration
		{
			ReadStance,
			MeasureGoals,
			Done
		}

		// The first animated frames still show the bind pose.
		private const int CalibrationDelayFrames = 3;
		// How far the deck may rise under her after a crash and still count as the floor rather than knock her off.
		private const float DeckTolerance = 0.1f;

		private static readonly int SlidingParameter = Animator.StringToHash("Sliding");
		private static readonly int RunningParameter = Animator.StringToHash("Running");
		private static readonly int JumpParameter = Animator.StringToHash("Jump");
		private static readonly int GroundedParameter = Animator.StringToHash("Grounded");
		private static readonly int DodgeLeftParameter = Animator.StringToHash("DodgeLeft");
		private static readonly int DodgeRightParameter = Animator.StringToHash("DodgeRight");
		private static readonly int HitLeftParameter = Animator.StringToHash("HitLeft");
		private static readonly int HitRightParameter = Animator.StringToHash("HitRight");
		private static readonly int CrashWallParameter = Animator.StringToHash("CrashWall");
		private static readonly int CrashBarrierParameter = Animator.StringToHash("CrashBarrier");

		[Tooltip("Board the feet are pinned to; defaults to the parent.")]
		[SerializeField] private Transform board;

		[Header("Crouch")]
		[SerializeField] private float crouchStiffness = 140f;
		[SerializeField] private float crouchDamping = 12f;
		[Tooltip("How far the hips may drop below the stance, in meters.")]
		[SerializeField] private float maxCrouch = 0.24f;
		[Tooltip("How far the hips may rise above the stance, in meters.")]
		[SerializeField] private float maxExtension = 0.07f;
		[Tooltip("Downward hip speed per m/s of landing speed.")]
		[SerializeField] private float landingCompression = 0.25f;
		[SerializeField] private float maxLandingCompression = 3f;
		[Tooltip("Upward hip speed when jumping, before the tuck.")]
		[SerializeField] private float jumpPop = 1f;

		[Header("Air")]
		[Tooltip("Hip drop at the top of a jump, pulling the board up under the body.")]
		[SerializeField] private float airTuck = 0.24f;
		[Tooltip("Seconds of airtime before the full tuck and arm raise, so short wave hops stay subtle.")]
		[SerializeField] private float airBlendTime = 0.2f;
		[SerializeField] private float armRaise = 0.2f;
		[Tooltip("How far the hands spread toward the nose and tail in the air.")]
		[SerializeField] private float armSpread = 0.1f;

		[Header("Lane Change")]
		// Per unit of BoardState.Carve, which peaks around 0.4-0.5 during a lane change.
		[Tooltip("Lean toward the side the board moves to, in degrees per unit of carve.")]
		[SerializeField] private float leanAngle = 40f;
		[Tooltip("Body yaw into the move, in degrees per unit of carve.")]
		[SerializeField] private float twistAngle = 50f;
		[Tooltip("Hip shift toward the move, in meters per unit of carve.")]
		[SerializeField] private float hipShift = 0.2f;
		[Tooltip("Lower than the crouch spring so the lean outlasts the quick lane change and overshoots a little.")]
		[SerializeField] private float leanStiffness = 80f;
		[SerializeField] private float leanDamping = 10f;

		[Header("Slide")]
		[Tooltip("Seconds to blend the stance reactions out and in; matches the Surf/Prone transitions.")]
		[SerializeField] private float slideBlendTime = 0.12f;

		[Header("Run")]
		[Tooltip("The board's meshes, moved from under the feet to under the arm while running on trains.")]
		[SerializeField] private Transform carriedBoard;
		[Tooltip("Seconds to switch between riding and running; matches the Surf/Run transitions.")]
		[SerializeField] private float runBlendTime = 0.15f;
		[Tooltip("Board position relative to the body, in Miko's space (x = right, z = forward).")]
		[SerializeField] private Vector3 carryOffset = new Vector3(0.17f, -0.08f, 0f);
		[Tooltip("Board rotation in Miko's space: deck against her right side, rail up.")]
		[SerializeField] private Vector3 carryRotation = new Vector3(-5f, 0f, 90f);
		[Tooltip("Where the right hand holds the board, in the board's space.")]
		[SerializeField] private Vector3 carryGrip = new Vector3(0.12f, -0.08f, 0.1f);

		[Header("Hit")]
		[Tooltip("Lean speed away from an obstacle's side when bouncing off it, in units of carve per second.")]
		[SerializeField] private float sideHitKick = 4f;

		[Header("Crash")]
		[Tooltip("Seconds to turn to the obstacle and let go of the board; matches the crash transitions.")]
		[SerializeField] private float crashBlendTime = 0.25f;
		[Tooltip("Speed she is thrown back toward the tail at, on top of the animation's own knock back.")]
		[SerializeField] private float knockBack = 2f;
		[SerializeField] private float knockBackDrag = 3f;
		[Tooltip("How far below the water surface she lies once off the board.")]
		[SerializeField] private float sinkDepth = 0.15f;
		[Tooltip("Seconds after hitting a wall before she starts to sink; she has fallen by then.")]
		[SerializeField] private float drownDelay = 1f;
		[SerializeField] private float drownSpeed = 0.8f;

		[Header("Look")]
		[SerializeField] private float lookAhead = 8f;
		[SerializeField] private float lookHeight = 1f;
		[SerializeField, Range(0f, 1f)] private float lookWeight = 0.6f;

		private readonly Limb _leftFoot = new Limb(AvatarIKGoal.LeftFoot, HumanBodyBones.LeftFoot);
		private readonly Limb _rightFoot = new Limb(AvatarIKGoal.RightFoot, HumanBodyBones.RightFoot);
		private readonly Limb _leftHand = new Limb(AvatarIKGoal.LeftHand, HumanBodyBones.LeftHand);
		private readonly Limb _rightHand = new Limb(AvatarIKGoal.RightHand, HumanBodyBones.RightHand);
		private Limb[] _limbs;

		private BoardState _state;
		private BoardSettings _settings;
		private WaterSurface _water;
		private Animator _animator;

		private Calibration _calibration;
		private int _calibrationWait;

		private float _crouch;
		private float _crouchVelocity;
		private float _lean;
		private float _leanVelocity;
		private float _air;
		private float _slide;
		private float _run;
		private float _roll;
		private int _lastLane = -1;
		private bool _wasGrounded = true;
		private float _previousVerticalVelocity;

		private bool _crashed;
		private float _crash;
		private Vector3 _fallPosition;
		private float _fallSpeed;
		private float _ground;
		private bool _onDeck;
		private float _crashTime;
		private float _drown;
		private Vector3 _knockBackVelocity;
		private float _rootDrop;
		private Quaternion _rootTurn = Quaternion.identity;
		private Quaternion _crashStartRotation;
		private Quaternion _crashFacing;

		private Quaternion _stanceRotation;
		private Vector3 _stancePosition;
		private Vector3 _boardRestPosition;
		private Quaternion _boardRestRotation;

		[Inject]
		public void Construct(BoardState state, BoardSettings settings, WaterSurface water)
		{
			_state = state;
			_settings = settings;
			_water = water;
			_state.Jumped += OnJumped;
			_state.SideHit += OnSideHit;
			_state.Crashed += OnCrashed;
		}

		private void Awake()
		{
			_animator = GetComponent<Animator>();
			_limbs = new[] { _leftFoot, _rightFoot, _leftHand, _rightHand };

			if (board == null)
			{
				board = transform.parent;
			}

			_stanceRotation = transform.localRotation;
			_stancePosition = transform.localPosition;

			if (carriedBoard != null)
			{
				_boardRestPosition = carriedBoard.localPosition;
				_boardRestRotation = carriedBoard.localRotation;
			}
		}

		private void OnDestroy()
		{
			if (_state != null)
			{
				_state.Jumped -= OnJumped;
				_state.SideHit -= OnSideHit;
				_state.Crashed -= OnCrashed;
			}
		}

		private void OnJumped()
		{
			_crouchVelocity += jumpPop;

			if (_state.IsRunning)
			{
				_animator.SetTrigger(JumpParameter);
				// Leaving the ground this frame; the jump only ends on the next landing.
				_animator.SetBool(GroundedParameter, false);
			}
		}

		private void OnSideHit(int side)
		{
			_leanVelocity -= side * sideHitKick;
			_animator.SetTrigger(side < 0 ? HitLeftParameter : HitRightParameter);
		}

		private void OnCrashed()
		{
			_crashed = true;
			_animator.SetTrigger(_state.CrashKind == CrashKind.Barrier ? CrashBarrierParameter : CrashWallParameter);

			// From here on she moves on her own while the board stays behind.
			transform.SetParent(board.parent, true);
			_fallPosition = transform.position;
			_ground = float.NegativeInfinity;
			_onDeck = true;
			_crashStartRotation = transform.rotation;
			Vector3 forward = Vector3.ProjectOnPlane(_state.PathForward, Vector3.up).normalized;
			_crashFacing = Quaternion.LookRotation(forward, Vector3.up);
			_knockBackVelocity = -forward * knockBack;
		}

		private void Update()
		{
			if (_state == null)
			{
				return;
			}

			// Capped so the springs stay stable on frame spikes.
			float dt = Mathf.Min(Time.deltaTime, 1f / 30f);

			if (_crashed)
			{
				UpdateCrash(dt);
				return;
			}
			bool grounded = _state.IsGrounded;

			// Covers water and obstacle landings, from jumps as well as from waves throwing the board.
			if (grounded && !_wasGrounded)
			{
				float impact = Mathf.Max(0f, -_previousVerticalVelocity);
				_crouchVelocity -= Mathf.Min(impact * landingCompression, maxLandingCompression);
			}

			_wasGrounded = grounded;
			_previousVerticalVelocity = _state.VerticalVelocity;

			_air = Mathf.MoveTowards(_air, grounded ? 0f : 1f, dt / (grounded ? 0.08f : airBlendTime));

			bool sliding = _state.IsSliding;
			_animator.SetBool(SlidingParameter, sliding);
			_slide = Mathf.MoveTowards(_slide, sliding ? 1f : 0f, dt / slideBlendTime);

			bool running = _state.IsRunning;
			_animator.SetBool(RunningParameter, running);
			_animator.SetBool(GroundedParameter, grounded);
			_run = Mathf.MoveTowards(_run, running ? 1f : 0f, dt / runBlendTime);
			_roll = Mathf.MoveTowards(_roll, sliding && running ? 1f : 0f, dt / slideBlendTime);

			if (!running)
			{
				// A jump from the water is the board's own; don't leave it queued for the next train.
				_animator.ResetTrigger(JumpParameter);
			}

			// A dodge only plays as the lane change starts; one the animator couldn't take is dropped, not delayed.
			_animator.ResetTrigger(DodgeLeftParameter);
			_animator.ResetTrigger(DodgeRightParameter);
			int lane = _state.Lane;
			if (running && grounded && _lastLane >= 0 && lane != _lastLane)
			{
				_animator.SetTrigger(lane < _lastLane ? DodgeLeftParameter : DodgeRightParameter);
			}

			_lastLane = lane;

			// Turn from the sideways stance to face down the train, upright on ramps and through the board's roll.
			Quaternion upright = Quaternion.LookRotation(Vector3.ProjectOnPlane(board.forward, Vector3.up), Vector3.up);
			transform.localRotation = Quaternion.Slerp(_stanceRotation,
				Quaternion.Inverse(transform.parent.rotation) * upright, _run);

			// Deepest tuck at the apex, legs reaching down again for the landing.
			float jumpSpeed = Mathf.Sqrt(2f * _settings.Gravity * _settings.JumpHeight);
			float apex = 1f - Mathf.Clamp01(Mathf.Abs(_state.VerticalVelocity) / jumpSpeed);
			float crouchTarget = -airTuck * _air * Mathf.Lerp(0.5f, 1f, apex);

			Spring(ref _crouch, ref _crouchVelocity, crouchTarget, crouchStiffness, crouchDamping, dt);
			if (_crouch < -maxCrouch || _crouch > maxExtension)
			{
				_crouch = Mathf.Clamp(_crouch, -maxCrouch, maxExtension);
				_crouchVelocity = 0f;
			}

			Spring(ref _lean, ref _leanVelocity, _state.Carve, leanStiffness, leanDamping, dt);
		}

		private void OnAnimatorIK(int layerIndex)
		{
			if (_state == null || board == null || _calibration == Calibration.ReadStance)
			{
				return;
			}

			if (_calibration == Calibration.MeasureGoals)
			{
				// Known goals: every limb where the stance puts it, oriented like the board.
				foreach (Limb limb in _limbs)
				{
					SetGoal(limb.Goal, board.TransformPoint(limb.Position), board.rotation, 1f);
				}

				return;
			}

			Vector3 up = board.up;
			Vector3 pivot = board.TransformPoint((_leftFoot.Position + _rightFoot.Position) * 0.5f);
			// Everything below fades out as she lets go of the board in a crash.
			float hold = 1f - _crash;
			float stand = (1f - _slide) * (1f - _run) * hold;

			// Lean toward the side the board moves to and turn the body into the move, pivoting over the feet.
			Quaternion lean = Quaternion.AngleAxis(-_lean * leanAngle * stand, board.forward) *
			                  Quaternion.AngleAxis(_lean * twistAngle * stand, up);
			Vector3 offset = (up * _crouch + board.right * (_lean * hipShift)) * stand;

			_animator.bodyPosition = pivot + lean * (_animator.bodyPosition - pivot) + offset;
			_animator.bodyRotation = lean * _animator.bodyRotation;

			// The prone pose lies along Miko's left, which the stance yaw turns a few degrees off the board's axis.
			// On trains the slide is a roll instead, already facing ahead.
			float prone = _slide * (1f - _run) * hold;
			if (prone > 0f)
			{
				Vector3 origin = transform.position;
				float yaw = Vector3.SignedAngle(-transform.right, board.forward, up);
				Quaternion align = Quaternion.AngleAxis(yaw * prone, up);
				_animator.bodyPosition = origin + align * (_animator.bodyPosition - origin);
				_animator.bodyRotation = align * _animator.bodyRotation;
			}

			PinLimb(_leftFoot, board.TransformPoint(_leftFoot.Position), board.rotation * _leftFoot.Rotation, stand);
			PinLimb(_rightFoot, board.TransformPoint(_rightFoot.Position), board.rotation * _rightFoot.Rotation, stand);

			// In the air the arms rise and spread for balance: front hand toward the nose, back hand toward the tail.
			// On the water the hands follow the animation, which the body motion above already carries along.
			if (_air > 0f)
			{
				Vector3 raise = up * armRaise;
				MoveHand(_leftHand, pivot, lean, offset + raise + board.forward * armSpread);
				MoveHand(_rightHand, pivot, lean, offset + raise - board.forward * armSpread);
			}
			else
			{
				_animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
				_animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
				_animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
				_animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
			}

			CarryBoard();

			_animator.SetLookAtWeight(lookWeight * hold, 0f, 0.8f, 1f, 0.5f);
			_animator.SetLookAtPosition(board.position + _state.PathForward * lookAhead + Vector3.up * lookHeight);
		}

		/// <summary>The crash animation's root motion, applied on top of the fall in <see cref="UpdateCrash"/>.</summary>
		private void OnAnimatorMove()
		{
			// Riding, she stays in place on the board, as without root motion.
			if (!_crashed)
			{
				return;
			}

			Vector3 delta = _animator.deltaPosition;
			_fallPosition += new Vector3(delta.x, 0f, delta.z);
			_rootDrop += delta.y;
			_rootTurn *= _animator.deltaRotation;
		}

		/// <summary>Thrown back toward the tail, she stays on the deck until clear of it, then drops into the water.</summary>
		private void UpdateCrash(float dt)
		{
			_crash = Mathf.MoveTowards(_crash, 1f, dt / crashBlendTime);
			_crashTime += dt;
			if (_state.CrashKind == CrashKind.Wall && _crashTime > drownDelay)
			{
				_drown += drownSpeed * dt;
			}

			_knockBackVelocity *= Mathf.Exp(-knockBackDrag * dt);
			_fallPosition += _knockBackVelocity * dt;

			Vector3 onBoard = board.InverseTransformPoint(_fallPosition);
			bool overBoard = Mathf.Abs(onBoard.x) < _settings.BoardWidth * 0.5f &&
			                 Mathf.Abs(onBoard.z) < _settings.BoardLength * 0.5f;
			float deck = board.TransformPoint(new Vector3(onBoard.x, _stancePosition.y, onBoard.z)).y;
			// Once clear of the board, or with the board knocked out from under her, she stays in the water.
			_onDeck = _onDeck && overBoard && deck <= _fallPosition.y + DeckTolerance;
			float ground = _water.GetHeight(_fallPosition) - sinkDepth - _drown;
			if (_onDeck)
			{
				ground = Mathf.Max(ground, deck);
			}

			_fallSpeed -= _settings.Gravity * dt;
			_fallPosition.y += _fallSpeed * dt;
			if (_fallPosition.y < ground)
			{
				_fallPosition.y = ground;
				_fallSpeed = 0f;
			}

			_ground = ground;
			Quaternion facing = Quaternion.Slerp(_crashStartRotation, _crashFacing, Mathf.SmoothStep(0f, 1f, _crash));
			transform.SetPositionAndRotation(_fallPosition + Vector3.up * _rootDrop, facing * _rootTurn);
		}

		/// <summary>
		/// Lifts the lying body back onto the deck or the water: the animation's root drop is scaled to Miko's avatar
		/// and overshoots the floor it was made for.
		/// </summary>
		private void RestOnGround()
		{
			float lowest = Mathf.Min(_animator.GetBoneTransform(HumanBodyBones.Hips).position.y,
				_animator.GetBoneTransform(HumanBodyBones.Head).position.y);
			float lift = _ground - lowest;
			if (lift > 0f)
			{
				_rootDrop += lift;
				transform.position += Vector3.up * lift;
			}
		}

		private void LateUpdate()
		{
			if (_crashed)
			{
				RestOnGround();
				return;
			}

			if (_state == null || board == null || _calibration == Calibration.Done)
			{
				return;
			}

			if (_calibration == Calibration.ReadStance)
			{
				if (++_calibrationWait < CalibrationDelayFrames)
				{
					return;
				}

				foreach (Limb limb in _limbs)
				{
					limb.ReadStance(_animator, board);
				}

				_calibration = Calibration.MeasureGoals;
			}
			else
			{
				foreach (Limb limb in _limbs)
				{
					limb.MeasureGoal(_animator, board);
				}

				_calibration = Calibration.Done;
			}
		}

		private void MoveHand(Limb hand, Vector3 pivot, Quaternion lean, Vector3 offset)
		{
			Vector3 position = pivot + lean * (board.TransformPoint(hand.Position) - pivot) + offset;
			PinLimb(hand, position, lean * board.rotation * hand.Rotation,
				_air * (1f - _slide) * (1f - _run) * (1f - _crash));
		}

		/// <summary>Swings the board from under the feet to her side, following the body, with the right hand on it.</summary>
		private void CarryBoard()
		{
			if (carriedBoard == null)
			{
				return;
			}

			// Dropped in a crash.
			if (_run <= 0f || _crashed)
			{
				carriedBoard.SetLocalPositionAndRotation(_boardRestPosition, _boardRestRotation);
				return;
			}

			// Level with her heading, even through the roll, where it only follows the body up and down.
			Quaternion facing = transform.rotation;
			Vector3 carryPosition = _animator.bodyPosition + facing * carryOffset;
			Quaternion held = facing * Quaternion.Euler(carryRotation);
			carriedBoard.SetPositionAndRotation(
				Vector3.Lerp(board.TransformPoint(_boardRestPosition), carryPosition, _run),
				Quaternion.Slerp(board.rotation * _boardRestRotation, held, _run));

			// Position only: the hand keeps the animation's grip. Let go while rolling, which needs the arm.
			_animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _run * (1f - _roll));
			_animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
			_animator.SetIKPosition(AvatarIKGoal.RightHand, carriedBoard.TransformPoint(carryGrip));
		}

		/// <summary>Places the limb's bone at the given pose by solving for the goal that produces it.</summary>
		private void PinLimb(Limb limb, Vector3 bonePosition, Quaternion boneRotation, float weight)
		{
			Quaternion goalRotation = boneRotation * Quaternion.Inverse(limb.GoalToBoneRotation);
			SetGoal(limb.Goal, bonePosition - goalRotation * limb.GoalToBonePosition, goalRotation, weight);
		}

		private void SetGoal(AvatarIKGoal goal, Vector3 position, Quaternion rotation, float weight)
		{
			_animator.SetIKPositionWeight(goal, weight);
			_animator.SetIKRotationWeight(goal, weight);
			_animator.SetIKPosition(goal, position);
			_animator.SetIKRotation(goal, rotation);
		}

		private static void Spring(ref float value, ref float velocity, float target, float stiffness, float damping,
			float dt)
		{
			velocity += (stiffness * (target - value) - damping * velocity) * dt;
			value += velocity * dt;
		}

		private sealed class Limb
		{
			public readonly AvatarIKGoal Goal;
			private readonly HumanBodyBones _bone;

			/// <summary>Bone pose in board space, as the stance places it.</summary>
			public Vector3 Position;
			public Quaternion Rotation = Quaternion.identity;
			/// <summary>Bone pose relative to its IK goal.</summary>
			public Vector3 GoalToBonePosition;
			public Quaternion GoalToBoneRotation = Quaternion.identity;

			public Limb(AvatarIKGoal goal, HumanBodyBones bone)
			{
				Goal = goal;
				_bone = bone;
			}

			public void ReadStance(Animator animator, Transform board)
			{
				Transform bone = animator.GetBoneTransform(_bone);
				Position = board.InverseTransformPoint(bone.position);
				Rotation = Quaternion.Inverse(board.rotation) * bone.rotation;
			}

			/// <summary>Called on the frame the goal was set to the stance position with the board's rotation.</summary>
			public void MeasureGoal(Animator animator, Transform board)
			{
				Transform bone = animator.GetBoneTransform(_bone);
				Quaternion goalRotation = board.rotation;
				GoalToBoneRotation = Quaternion.Inverse(goalRotation) * bone.rotation;
				GoalToBonePosition = Quaternion.Inverse(goalRotation) * (bone.position - board.TransformPoint(Position));
			}
		}
	}
}
