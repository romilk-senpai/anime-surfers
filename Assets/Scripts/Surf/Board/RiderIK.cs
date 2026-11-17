using System;
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
	/// On the glider she hangs from it facing ahead, her left hand up on its grip and the board under her right arm
	/// as on trains, and the glider unfolds above her and banks into lane changes. Hanging from the grip she swings
	/// like a pendulum: sideways behind lane changes and back on the climb, her legs swinging after her body and
	/// dangling. When the flight ends the glider is gone at once and she drops to the water, taking the surf stance on
	/// the way down. Through the flight and the fall, her tails stream in the air rushing past.
	/// Bouncing off an obstacle's side she flinches away from it. On a crash she turns to the obstacle, lets go of the
	/// board and is knocked back off it into the water, moved by the crash animation's root motion. After a wall she
	/// sinks.
	/// Updates when <see cref="BoardView.Placed"/> fires and raises <see cref="BodyPlaced"/> once the body is placed
	/// for the frame.
	/// Needs IK Pass enabled on the animator layer.
	/// </summary>
	[RequireComponent(typeof(Animator))]
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
		private static readonly int BarrelParameter = Animator.StringToHash("Barrel");
		private static readonly int RunningParameter = Animator.StringToHash("Running");
		private static readonly int GlidingParameter = Animator.StringToHash("Gliding");
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
		[Tooltip("The board's meshes, moved from under the feet to under the arm while running on trains. Defaults to " +
		         "the BoardView's model, since a rider spawned from a prefab can't reference the scene.")]
		[SerializeField] private Transform carriedBoard;
		[Tooltip("Seconds to switch between riding and running; matches the Surf/Run transitions.")]
		[SerializeField] private float runBlendTime = 0.15f;
		[Tooltip("Board position relative to the body, in Miko's space (x = right, z = forward).")]
		[SerializeField] private Vector3 carryOffset = new Vector3(0.17f, -0.08f, 0f);
		[Tooltip("Board rotation in Miko's space: deck against her right side, rail up.")]
		[SerializeField] private Vector3 carryRotation = new Vector3(-5f, 0f, 90f);
		[Tooltip("Where the right hand holds the board, in the board's space.")]
		[SerializeField] private Vector3 carryGrip = new Vector3(0.12f, -0.08f, 0.1f);

		[Header("Glide")]
		[Tooltip("The glider, pivoted on its grip. Defaults to the BoardView's, since a rider spawned from a prefab " +
		         "can't reference the scene.")]
		[SerializeField] private Transform glider;
		[Tooltip("Seconds to unfold the glider and turn to hang from it, and back for the landing.")]
		[SerializeField] private float glideBlendTime = 0.3f;
		[Tooltip("Direction from the left shoulder up to the grip, in Miko's space (x = right, y = up, z = forward).")]
		[SerializeField] private Vector3 gliderReachDirection = new Vector3(-0.15f, 1f, 0.05f);
		[Tooltip("How far the arm reaches up to the grip, as a share of its length; below 1 keeps the elbow soft.")]
		[SerializeField, Range(0.5f, 1f)] private float gliderReach = 0.97f;
		[Tooltip("From the wrist to the grip bar in the palm.")]
		[SerializeField] private float gliderPalm = 0.07f;
		[Tooltip("Nose-up pitch of the glider, in degrees.")]
		[SerializeField] private float gliderPitch = 8f;
		[Tooltip("Glider bank in degrees per unit of carve.")]
		[SerializeField] private float gliderBank = 30f;
		[Tooltip("Sideways swing under the grip, in degrees per unit of carve; she trails behind lane changes.")]
		[SerializeField] private float glideSwingAngle = 35f;
		[Tooltip("Swing back on the climb and forward on the descent, in degrees per m/s of vertical speed.")]
		[SerializeField] private float glidePitchAngle = 1.5f;
		[SerializeField] private float glidePitchLimit = 18f;
		[Tooltip("Lower than the lean spring and lightly damped, so the swing overshoots and settles like a pendulum.")]
		[SerializeField] private float glideSwingStiffness = 25f;
		[SerializeField] private float glideSwingDamping = 3f;
		[Tooltip("How much the legs follow their IK while hanging, against the glide pose.")]
		[SerializeField, Range(0f, 1f)] private float glideLegWeight = 0.8f;
		[Tooltip("How far the feet hang from the hips, as a share of the leg's length; below 1 bends the knees.")]
		[SerializeField, Range(0.5f, 1f)] private float glideLegReach = 0.92f;
		[Tooltip("Legs swing back from straight down, in degrees: left, right.")]
		[SerializeField] private Vector2 glideLegTrail = new Vector2(12f, -6f);
		[Tooltip("Softer than the body's swing, so the legs swing after it.")]
		[SerializeField] private float glideLegStiffness = 12f;
		[SerializeField] private float glideLegDamping = 2f;
		[Tooltip("Alternating dangle of the legs back and forth, in degrees.")]
		[SerializeField] private float glideLegKick = 10f;
		[SerializeField] private float glideLegKickRate = 0.7f;
		[Tooltip("Gusts in the air streaming past her tails in flight: sideways speed in m/s, and how often.")]
		[SerializeField] private float glideFlutter = 3f;
		[SerializeField] private float glideFlutterRate = 1.3f;

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

		private IBoardState _state;
		private BoardSettings _settings;
		private IWaterSurface _water;
		private BarrelRide _barrel;
		private Animator _animator;
		private BoardView _boardView;

		private Calibration _calibration;
		private int _calibrationWait;

		private float _crouch;
		private float _crouchVelocity;
		private float _lean;
		private float _leanVelocity;
		private float _air;
		private float _slide;
		private float _run;
		private float _glide;
		private float _swing;
		private float _swingVelocity;
		private float _pitch;
		private float _pitchVelocity;
		private float _legSwing;
		private float _legSwingVelocity;
		private float _legPitch;
		private float _legPitchVelocity;
		/// <summary>From the glider's take-off until she lands on the water again.</summary>
		private bool _flying;
		/// <summary>The hang's swing about the grip, which the carried board follows too.</summary>
		private Quaternion _hangSwing = Quaternion.identity;
		private float _armLength;
		private float _legLength;
		/// <summary>Left shoulder and hips in body space, read from last frame's pose.</summary>
		private Vector3 _shoulder;
		private Vector3 _leftHip;
		private Vector3 _rightHip;
		private bool _hasShoulder;
		/// <summary>Where she last held the grip, relative to her; the swing eases out about it as she lets go.</summary>
		private Vector3 _lastGrip;
		/// <summary>Body pose at the end of last frame's IK, relative to the rider, to read the shoulder against.</summary>
		private Vector3 _lastBodyPosition;
		private Quaternion _lastBodyRotation = Quaternion.identity;
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

		/// <summary>
		/// Raised at the end of every LateUpdate, once the body is placed for the frame: on the board, or after a crash
		/// lying on the deck or in the water.
		/// </summary>
		public event Action BodyPlaced;

		[Inject]
		public void Construct(IBoardState state, BoardSettings settings, IWaterSurface water, BarrelRide barrel)
		{
			_state = state;
			_settings = settings;
			_water = water;
			_barrel = barrel;
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

			// Kept after the crash, when she is no longer the board's child.
			_boardView = board.GetComponentInParent<BoardView>();
			_boardView.Placed += OnBoardPlaced;

			if (carriedBoard == null)
			{
				carriedBoard = _boardView.Model;
			}

			if (glider == null)
			{
				glider = _boardView.Glider;
			}

			if (glider != null)
			{
				glider.gameObject.SetActive(false);
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
			if (_boardView != null)
			{
				_boardView.Placed -= OnBoardPlaced;
			}

			if (_state != null)
			{
				_state.Jumped -= OnJumped;
				_state.SideHit -= OnSideHit;
				_state.Crashed -= OnCrashed;
			}
		}

		// The board's events can still reach a rider RiderSpawner is replacing; it is inactive by then.
		private void OnJumped()
		{
			if (!isActiveAndEnabled)
			{
				return;
			}

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
			if (!isActiveAndEnabled)
			{
				return;
			}

			_leanVelocity -= side * sideHitKick;
			_animator.SetTrigger(side < 0 ? HitLeftParameter : HitRightParameter);
		}

		private void OnCrashed()
		{
			if (!isActiveAndEnabled)
			{
				return;
			}

			_crashed = true;
			// Wiped out by a wave, she's knocked back off the board like by a barrier.
			_animator.SetTrigger(_state.CrashKind == CrashKind.Wall ? CrashWallParameter : CrashBarrierParameter);

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

		// Right after BoardView places the board, so the upright running pose doesn't lag a frame behind it.
		private void OnBoardPlaced()
		{
			if (_state == null || !isActiveAndEnabled)
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
			// Crouches as the board drops in, and stays down through the barrel.
			_animator.SetBool(BarrelParameter, _barrel.LocksControls);
			_slide = Mathf.MoveTowards(_slide, sliding ? 1f : 0f, dt / slideBlendTime);

			bool running = _state.IsRunning;
			_animator.SetBool(RunningParameter, running);
			_animator.SetBool(GroundedParameter, grounded);
			_run = Mathf.MoveTowards(_run, running ? 1f : 0f, dt / runBlendTime);
			_animator.SetBool(GlidingParameter, _state.IsGliding);
			_glide = Mathf.MoveTowards(_glide, _state.IsGliding ? 1f : 0f, dt / glideBlendTime);
			float pitchTarget = Mathf.Clamp(_state.VerticalVelocity * glidePitchAngle, -glidePitchLimit, glidePitchLimit);
			Spring(ref _swing, ref _swingVelocity, -_state.Carve * glideSwingAngle * _glide, glideSwingStiffness,
				glideSwingDamping, dt);
			Spring(ref _pitch, ref _pitchVelocity, pitchTarget * _glide, glideSwingStiffness, glideSwingDamping, dt);
			Spring(ref _legSwing, ref _legSwingVelocity, _swing, glideLegStiffness, glideLegDamping, dt);
			Spring(ref _legPitch, ref _legPitchVelocity, _pitch, glideLegStiffness, glideLegDamping, dt);
			_flying = _state.IsGliding || _flying && !grounded;
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

			// Turn from the sideways stance to face down the train or the flight, upright on ramps and through the
			// board's roll.
			Quaternion upright = Quaternion.LookRotation(Vector3.ProjectOnPlane(board.forward, Vector3.up), Vector3.up);
			transform.localRotation = Quaternion.Slerp(_stanceRotation,
				Quaternion.Inverse(transform.parent.rotation) * upright, Upright);

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
			float stand = (1f - _slide) * (1f - Upright) * hold;

			// Lean toward the side the board moves to and turn the body into the move, pivoting over the feet.
			Quaternion lean = Quaternion.AngleAxis(-_lean * leanAngle * stand, board.forward) *
			                  Quaternion.AngleAxis(_lean * twistAngle * stand, up);
			Vector3 offset = (up * _crouch + board.right * (_lean * hipShift)) * stand;

			_animator.bodyPosition = pivot + lean * (_animator.bodyPosition - pivot) + offset;
			_animator.bodyRotation = lean * _animator.bodyRotation;

			// The prone pose lies along Miko's left, which the stance yaw turns a few degrees off the board's axis.
			// On trains the slide is a roll instead, already facing ahead.
			float prone = _slide * (1f - Upright) * hold;
			if (prone > 0f)
			{
				Vector3 origin = transform.position;
				float yaw = Vector3.SignedAngle(-transform.right, board.forward, up);
				Quaternion align = Quaternion.AngleAxis(yaw * prone, up);
				// The stance may stand her off the board's center line to keep her feet on the deck; lying down she
				// centers on it.
				Vector3 offCenter = Vector3.Project(origin - board.position, board.right);
				_animator.bodyPosition = origin + align * (_animator.bodyPosition - origin) - offCenter * prone;
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

			HangFromGlider();
			CarryBoard();

			_animator.SetLookAtWeight(lookWeight * hold, 0f, 0.8f, 1f, 0.5f);
			_animator.SetLookAtPosition(board.position + _state.PathForward * lookAhead + Vector3.up * lookHeight);

			_lastBodyPosition = transform.InverseTransformPoint(_animator.bodyPosition);
			_lastBodyRotation = Quaternion.Inverse(transform.rotation) * _animator.bodyRotation;
		}

		/// <summary>Turned to face ahead with the board under the arm: running on a train or hanging from the glider.</summary>
		private float Upright => Mathf.Max(_run, _glide);

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
			if (_state.CrashKind != CrashKind.Barrier && _crashTime > drownDelay)
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
		/// How fast the air streams past her, in m/s: against the way the board travels. Only the forward speed, since
		/// the wave bob and lane changes would flap anything that follows it. Still once she has crashed.
		/// </summary>
		public Vector3 Airflow
		{
			get
			{
				if (_state == null || _crashed)
				{
					return Vector3.zero;
				}

				Vector3 air = -_state.PathForward * _state.Speed;
				if (!_flying)
				{
					return air;
				}

				// In flight the climb and the fall stream the air past her too, and it gusts.
				float gust = Mathf.PerlinNoise(Time.time * glideFlutterRate, 0f) * 2f - 1f;
				return air - Vector3.up * _state.VerticalVelocity + _state.PathRight * (gust * glideFlutter);
			}
		}

		/// <summary>Lifts a point that sinks into the deck back onto it, while the point is over the board.</summary>
		public Vector3 KeepAboveDeck(Vector3 point, float clearance)
		{
			// In flight the board is under her arm.
			if (_settings == null || board == null || _flying)
			{
				return point;
			}

			Vector3 onBoard = board.InverseTransformPoint(point);
			float deck = _stancePosition.y + clearance;
			if (onBoard.y >= deck || Mathf.Abs(onBoard.x) > _settings.BoardWidth * 0.5f ||
			    Mathf.Abs(onBoard.z) > _settings.BoardLength * 0.5f)
			{
				return point;
			}

			onBoard.y = deck;
			return board.TransformPoint(onBoard);
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
			PlaceBody();
			BodyPlaced?.Invoke();
		}

		/// <summary>Rests the crashed body on the ground, or calibrates the stance from the animated pose.</summary>
		private void PlaceBody()
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
				_air * (1f - _slide) * (1f - Upright) * (1f - _crash));
		}

		/// <summary>Swings the board from under the feet to her side, following the body, with the right hand on it.</summary>
		private void CarryBoard()
		{
			if (carriedBoard == null)
			{
				return;
			}

			// Dropped in a crash.
			float carry = Upright;
			if (carry <= 0f || _crashed)
			{
				carriedBoard.SetLocalPositionAndRotation(_boardRestPosition, _boardRestRotation);
				return;
			}

			// Level with her heading, even through the roll, where it only follows the body up and down. Hanging from
			// the glider it swings with her.
			Quaternion facing = _hangSwing * transform.rotation;
			Vector3 carryPosition = _animator.bodyPosition + facing * carryOffset;
			Quaternion held = facing * Quaternion.Euler(carryRotation);
			carriedBoard.SetPositionAndRotation(
				Vector3.Lerp(board.TransformPoint(_boardRestPosition), carryPosition, carry),
				Quaternion.Slerp(board.rotation * _boardRestRotation, held, carry));

			// Position only: the hand keeps the animation's grip. Let go while rolling, which needs the arm.
			_animator.SetIKPositionWeight(AvatarIKGoal.RightHand, carry * (1f - _roll));
			_animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
			_animator.SetIKPosition(AvatarIKGoal.RightHand, carriedBoard.TransformPoint(carryGrip));
		}

		/// <summary>
		/// Unfolds the glider above her, banking into lane changes, with the grip where her left arm reaches straight up
		/// from the shoulder, and swings the body under it. Placed for the unswung body, so the swing about the grip
		/// keeps it in reach.
		/// </summary>
		private void HangFromGlider()
		{
			_hangSwing = Quaternion.identity;

			if (glider == null)
			{
				return;
			}

			// Unfolds as she takes off, but is gone at once when the flight ends.
			bool shown = _state.IsGliding && !_crashed;
			if (glider.gameObject.activeSelf != shown)
			{
				glider.gameObject.SetActive(shown);
			}

			if (!shown)
			{
				_hasShoulder = false;

				// Let go, the swing eases out instead of snapping the body straight under the grip.
				if (_glide > 0f && !_crashed)
				{
					Swing(transform.TransformPoint(_lastGrip), transform.rotation);
				}

				return;
			}

			_shoulder = ReadBodyPoint(HumanBodyBones.LeftUpperArm, _shoulder);
			_leftHip = ReadBodyPoint(HumanBodyBones.LeftUpperLeg, _leftHip);
			_rightHip = ReadBodyPoint(HumanBodyBones.RightUpperLeg, _rightHip);
			_hasShoulder = true;

			if (_armLength <= 0f)
			{
				_armLength = LimbLength(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
					HumanBodyBones.LeftHand);
				_legLength = LimbLength(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
					HumanBodyBones.LeftFoot);
			}

			Quaternion facing = transform.rotation;
			Vector3 reach = facing * gliderReachDirection.normalized;
			Vector3 wrist = _animator.bodyPosition + _animator.bodyRotation * _shoulder +
			                reach * (_armLength * gliderReach);
			Vector3 grip = wrist + reach * gliderPalm;

			glider.SetPositionAndRotation(grip, facing * Quaternion.Euler(-gliderPitch, 0f, -_lean * gliderBank));
			glider.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, _glide);

			_lastGrip = transform.InverseTransformPoint(grip);
			Swing(grip, facing);

			_animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _glide);
			_animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
			_animator.SetIKPosition(AvatarIKGoal.LeftHand, _hangSwing * (wrist - grip) + grip);

			DangleLeg(AvatarIKGoal.LeftFoot, AvatarIKHint.LeftKnee, _leftHip, glideLegTrail.x, 0f, facing);
			DangleLeg(AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee, _rightHip, glideLegTrail.y, Mathf.PI, facing);
		}

		/// <summary>Swings the body about the grip: positive pitch takes the legs back, positive swing takes it right.</summary>
		private void Swing(Vector3 grip, Quaternion facing)
		{
			_hangSwing = Quaternion.AngleAxis(_swing, facing * Vector3.forward) *
			             Quaternion.AngleAxis(_pitch, facing * Vector3.right);
			_animator.bodyPosition = grip + _hangSwing * (_animator.bodyPosition - grip);
			_animator.bodyRotation = _hangSwing * _animator.bodyRotation;
		}

		/// <summary>
		/// Hangs a foot below its hip, swinging after the body as a slower pendulum, trailing back by
		/// <paramref name="trail"/> degrees and dangling back and forth out of step with the other leg.
		/// </summary>
		private void DangleLeg(AvatarIKGoal foot, AvatarIKHint knee, Vector3 hip, float trail, float phase,
			Quaternion facing)
		{
			Vector3 forward = facing * Vector3.forward;
			Vector3 right = facing * Vector3.right;
			float kick = glideLegKick * Mathf.Sin(2f * Mathf.PI * glideLegKickRate * Time.time + phase);
			// Relative to the body, which the hang already swings: only how far the legs lag behind it.
			Quaternion lag = Quaternion.AngleAxis(_legSwing - _swing, forward) *
			                 Quaternion.AngleAxis(_legPitch - _pitch + trail + kick, right);
			Vector3 down = _hangSwing * (lag * (facing * Vector3.down));

			Vector3 hipPosition = _animator.bodyPosition + _animator.bodyRotation * hip;
			float weight = glideLegWeight * _glide;
			_animator.SetIKPositionWeight(foot, weight);
			_animator.SetIKPosition(foot, hipPosition + down * (_legLength * glideLegReach));
			// Knees bend forward.
			_animator.SetIKHintPositionWeight(knee, weight);
			_animator.SetIKHintPosition(knee,
				hipPosition + down * (_legLength * 0.5f) + _hangSwing * forward * (_legLength * 0.5f));
		}

		/// <summary>
		/// A bone's position in body space. The bones still hold last frame's pose, so it is read against last frame's
		/// body, and eased in to ride out any jitter.
		/// </summary>
		private Vector3 ReadBodyPoint(HumanBodyBones bone, Vector3 previous)
		{
			Vector3 point = transform.InverseTransformPoint(_animator.GetBoneTransform(bone).position);
			point = Quaternion.Inverse(_lastBodyRotation) * (point - _lastBodyPosition);
			return _hasShoulder ? Vector3.Lerp(previous, point, 0.2f) : point;
		}

		private float LimbLength(HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end)
		{
			Vector3 middle = _animator.GetBoneTransform(lower).position;
			return Vector3.Distance(_animator.GetBoneTransform(upper).position, middle) +
			       Vector3.Distance(middle, _animator.GetBoneTransform(end).position);
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
