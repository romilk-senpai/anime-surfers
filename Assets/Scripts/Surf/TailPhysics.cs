using System.Collections.Generic;
using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Swings Miko's tail: the chain from this bone down through first children follows the animated pose loosely,
	/// lagging behind her moves and settling back. Each bone is a point that keeps its momentum, is pulled toward where
	/// the animation puts it and is held at its bone's length from its parent; the bones then turn to point at them.
	/// The points ride along with her, so only her body's moves swing the tail, not the board carrying her.
	/// Updates when <see cref="RiderIK.BodyPlaced"/> fires, once the body is placed for the frame.
	/// </summary>
	public class TailPhysics : MonoBehaviour
	{
		// Stiffness and damping are per frame at this frame rate, then scaled to the real frame time.
		private const float ReferenceFrameRate = 60f;

		[Tooltip("Bones at the base of the chain that follow the animation exactly, counting this one.")]
		[SerializeField, Min(1)] private int pinnedBones = 2;
		[Tooltip("How much of the way back to the animated pose each bone moves per frame.")]
		[SerializeField, Range(0f, 1f)] private float stiffness = 0.1f;
		[Tooltip("How much of its speed each bone loses per frame.")]
		[SerializeField, Range(0f, 1f)] private float damping = 0.2f;
		[Tooltip("Constant acceleration on the tail, in m/s². Upward keeps it perky.")]
		[SerializeField] private Vector3 gravity = new Vector3(0f, 2f, 0f);

		private Transform[] _bones;
		private Quaternion[] _restRotations;
		private Vector3[] _positions;
		private Vector3[] _previousPositions;
		private RiderIK _rider;
		private Vector3 _riderPosition;
		private bool _reset = true;

		private void Awake()
		{
			var bones = new List<Transform>();
			for (Transform bone = transform; bone != null; bone = bone.childCount > 0 ? bone.GetChild(0) : null)
			{
				bones.Add(bone);
			}

			_bones = bones.ToArray();
			_restRotations = new Quaternion[_bones.Length];
			_positions = new Vector3[_bones.Length];
			_previousPositions = new Vector3[_bones.Length];
			for (int i = 0; i < _bones.Length; i++)
			{
				_restRotations[i] = _bones[i].localRotation;
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
				_bones[i].localRotation = _restRotations[i];
			}
		}

		private void OnBodyPlaced()
		{
			float dt = Time.deltaTime;
			if (!isActiveAndEnabled || dt <= 0f)
			{
				return;
			}

			Vector3 riderPosition = _rider.transform.position;
			if (_reset)
			{
				for (int i = 0; i < _bones.Length; i++)
				{
					_positions[i] = _previousPositions[i] = _bones[i].position;
				}

				_riderPosition = riderPosition;
				_reset = false;
				return;
			}

			// Carry the points along with her.
			Vector3 carry = riderPosition - _riderPosition;
			_riderPosition = riderPosition;

			float frames = dt * ReferenceFrameRate;
			float keep = Mathf.Pow(1f - damping, frames);
			float pull = 1f - Mathf.Pow(1f - stiffness, frames);
			Vector3 fall = gravity * (dt * dt);

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

				position += velocity + fall;
				position = Vector3.Lerp(position, parent + segment, pull);
				_positions[i] = parent + (position - parent).normalized * segment.magnitude;
			}

			// Turning a bone moves the rest of the chain, so each child is measured after its parent turned.
			for (int i = pinnedBones - 1; i < _bones.Length - 1; i++)
			{
				Vector3 animated = _bones[i + 1].position - _bones[i].position;
				Vector3 simulated = _positions[i + 1] - _bones[i].position;
				_bones[i].rotation = Quaternion.FromToRotation(animated, simulated) * _bones[i].rotation;
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
