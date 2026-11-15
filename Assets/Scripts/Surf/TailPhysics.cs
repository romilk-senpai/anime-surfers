using System.Collections.Generic;
using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Swings a tail or braid: the chain of bones follows the animated pose loosely, lagging behind her moves and
	/// settling back. Each bone is a point that keeps its momentum, is pulled toward where the animation puts it and is
	/// held at its bone's length from its parent; the bones then turn to point at them.
	/// The points ride along with her, so only her body's moves swing the chain, not the board carrying her.
	/// Updates when <see cref="RiderIK.BodyPlaced"/> fires, once the body is placed for the frame.
	/// </summary>
	public class TailPhysics : MonoBehaviour
	{
		// Stiffness and damping are per frame at this frame rate, then scaled to the real frame time.
		private const float ReferenceFrameRate = 60f;
		// Longest step simulated, so a frame spike doesn't fling the chain.
		private const float MaxStep = 1f / 30f;

		[Tooltip("Bones from the base to the tip. Empty: this bone down through first children. " +
		         "Bones that aren't children of the previous one are moved along as if they were.")]
		[SerializeField] private Transform[] chain;
		[Tooltip("Bones at the base of the chain that follow the animation exactly, counting the first one.")]
		[SerializeField, Min(1)] private int pinnedBones = 2;
		[Tooltip("How much of the way back to the animated pose each bone moves per frame.")]
		[SerializeField, Range(0f, 1f)] private float stiffness = 0.1f;
		[Tooltip("How much of its speed each bone loses per frame.")]
		[SerializeField, Range(0f, 1f)] private float damping = 0.2f;
		[Tooltip("Constant acceleration on the tail, in m/s². Upward keeps it perky.")]
		[SerializeField] private Vector3 gravity = new Vector3(0f, 2f, 0f);
		[Tooltip("How quickly the air streaming past her drags the chain along with it, per second. " +
		         "Streams it back at speed and calms its swinging.")]
		[SerializeField, Min(0f)] private float drag;
		[Tooltip("Rests the chain on the board's deck instead of letting it pass through.")]
		[SerializeField] private bool restOnDeck;
		[Tooltip("How far the bones stay above the deck, in meters: about the chain's thickness.")]
		[SerializeField, Min(0f)] private float deckClearance = 0.03f;

		private Transform[] _bones;
		private Vector3[] _restPositions;
		private Quaternion[] _restRotations;
		// Index of the nearest earlier bone in the chain that this one hangs under, or -1.
		private int[] _chainParents;
		private Vector3[] _positions;
		private Vector3[] _previousPositions;
		private RiderIK _rider;
		private Vector3 _riderPosition;
		private float _previousStep;
		private bool _reset = true;

		private void Awake()
		{
			if (chain != null && chain.Length > 0)
			{
				_bones = chain;
			}
			else
			{
				var bones = new List<Transform>();
				for (Transform bone = transform; bone != null; bone = bone.childCount > 0 ? bone.GetChild(0) : null)
				{
					bones.Add(bone);
				}

				_bones = bones.ToArray();
			}

			_restPositions = new Vector3[_bones.Length];
			_restRotations = new Quaternion[_bones.Length];
			_chainParents = new int[_bones.Length];
			_positions = new Vector3[_bones.Length];
			_previousPositions = new Vector3[_bones.Length];
			for (int i = 0; i < _bones.Length; i++)
			{
				_restPositions[i] = _bones[i].localPosition;
				_restRotations[i] = _bones[i].localRotation;
				_chainParents[i] = -1;
				for (int j = i - 1; j >= 0; j--)
				{
					if (_bones[i].IsChildOf(_bones[j]))
					{
						_chainParents[i] = j;
						break;
					}
				}
			}

			pinnedBones = Mathf.Clamp(pinnedBones, 1, _bones.Length);

			_rider = GetComponentInParent<RiderIK>();
			_rider.BodyPlaced += OnBodyPlaced;
		}

		private void OnEnable()
		{
			_reset = true;
		}

		// Undoes last frame's swing before the animator poses the tail, in case a clip doesn't key it.
		private void Update()
		{
			RestorePose();
		}

		private void OnDisable()
		{
			RestorePose();
		}

		private void RestorePose()
		{
			for (int i = 0; i < _bones.Length; i++)
			{
				_bones[i].SetLocalPositionAndRotation(_restPositions[i], _restRotations[i]);
			}
		}

		private void OnBodyPlaced()
		{
			if (!isActiveAndEnabled)
			{
				return;
			}

			if (_reset)
			{
				for (int i = 0; i < _bones.Length; i++)
				{
					_positions[i] = _previousPositions[i] = _bones[i].position;
				}

				_riderPosition = _rider.transform.position;
				// Nothing is moving yet, so any step length works.
				_previousStep = MaxStep;
				_reset = false;
				return;
			}

			// While paused, hold the last swing rather than snap back to the animated pose.
			float dt = Mathf.Min(Time.deltaTime, MaxStep);
			if (dt > 0f)
			{
				Simulate(dt);
			}

			TurnBones();
		}

		private void Simulate(float dt)
		{
			// Carry the points along with her.
			Vector3 riderPosition = _rider.transform.position;
			Vector3 carry = riderPosition - _riderPosition;
			_riderPosition = riderPosition;

			float frames = dt * ReferenceFrameRate;
			// Last frame's move, rescaled to this frame's length so uneven frames keep the speed.
			float keep = Mathf.Pow(1f - damping, frames) * dt / _previousStep;
			_previousStep = dt;
			float pull = 1f - Mathf.Pow(1f - stiffness, frames);
			Vector3 fall = gravity * (dt * dt);
			// Per frame, like the moves below: the air's move this frame, and how much of the gap to it the drag closes.
			Vector3 wind = _rider.Airflow * dt;
			float dragged = Mathf.Min(1f, drag * dt);

			for (int i = 0; i < pinnedBones; i++)
			{
				_positions[i] = _bones[i].position;
			}

			for (int i = pinnedBones; i < _bones.Length; i++)
			{
				Vector3 parent = _positions[i - 1];
				// Where the animation puts this bone from its parent.
				Vector3 segment = _bones[i].position - _bones[i - 1].position;

				Vector3 velocity = (_positions[i] - _previousPositions[i]) * keep;
				Vector3 position = _positions[i] + carry;
				_previousPositions[i] = position;

				velocity += (wind - velocity) * dragged;
				position += velocity + fall;
				position = Vector3.Lerp(position, parent + segment, pull);
				position = parent + (position - parent).normalized * segment.magnitude;
				_positions[i] = restOnDeck ? _rider.KeepAboveDeck(position, deckClearance) : position;
			}
		}

		private void TurnBones()
		{
			// Turning a bone moves the rest of the chain, so each child is measured after its parent turned.
			for (int i = pinnedBones - 1; i < _bones.Length - 1; i++)
			{
				Transform bone = _bones[i];
				Vector3 animated = _bones[i + 1].position - bone.position;
				Vector3 simulated = _positions[i + 1] - bone.position;
				Quaternion turn = Quaternion.FromToRotation(animated, simulated);
				bone.rotation = turn * bone.rotation;

				// The hierarchy carries bones that hang under this one, directly or through a moved bone; swing the
				// rest the same way around this bone.
				for (int j = i + 1; j < _bones.Length; j++)
				{
					if (_chainParents[j] < i)
					{
						Transform loose = _bones[j];
						loose.SetPositionAndRotation(bone.position + turn * (loose.position - bone.position),
							turn * loose.rotation);
					}
				}
			}
		}

		private void OnDestroy()
		{
			if (_rider != null)
			{
				_rider.BodyPlaced -= OnBodyPlaced;
			}
		}
	}
}
