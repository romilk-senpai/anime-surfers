using System;
using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Drives helper bones that a rig leaves to its game's constraints, like the hip, skirt and knee correctives on
	/// Yuzuha's rig: each turns partway from its parent toward a driver bone, keeping its bind-pose offset from both.
	/// Without this, skin weighted to them stays with the pelvis when she raises a leg and tears at the hip.
	/// A bone listed more than once blends on from the previous entry, e.g. a center skirt bone following both thighs.
	/// </summary>
	public class HelperBones : MonoBehaviour
	{
		[Serializable]
		private struct Helper
		{
			public Transform bone;
			public Transform driver;
			[Tooltip("0 stays with the parent, 1 moves rigidly with the driver.")]
			[Range(0f, 1f)] public float follow;
		}

		[SerializeField] private Helper[] helpers;

		private Vector3[] _localPositions;
		private Quaternion[] _localRotations;
		private Vector3[] _driverPositions;
		private Quaternion[] _driverRotations;
		private bool[] _repeats;

		// Read from the bind pose, before the animator first poses the body.
		private void Awake()
		{
			_localPositions = new Vector3[helpers.Length];
			_localRotations = new Quaternion[helpers.Length];
			_driverPositions = new Vector3[helpers.Length];
			_driverRotations = new Quaternion[helpers.Length];
			_repeats = new bool[helpers.Length];

			for (int i = 0; i < helpers.Length; i++)
			{
				Transform bone = helpers[i].bone;
				Transform driver = helpers[i].driver;
				_localPositions[i] = bone.localPosition;
				_localRotations[i] = bone.localRotation;
				_driverPositions[i] = driver.InverseTransformPoint(bone.position);
				_driverRotations[i] = Quaternion.Inverse(driver.rotation) * bone.rotation;
				_repeats[i] = Array.FindIndex(helpers, 0, i, h => h.bone == bone) >= 0;
			}
		}

		private void OnDisable()
		{
			for (int i = 0; i < helpers.Length; i++)
			{
				helpers[i].bone.SetLocalPositionAndRotation(_localPositions[i], _localRotations[i]);
			}
		}

		// After the animator and IK have posed the body.
		private void LateUpdate()
		{
			for (int i = 0; i < helpers.Length; i++)
			{
				Transform bone = helpers[i].bone;
				Transform driver = helpers[i].driver;

				Vector3 position;
				Quaternion rotation;
				if (_repeats[i])
				{
					position = bone.position;
					rotation = bone.rotation;
				}
				else
				{
					// From the parent each frame, so the turns don't build up.
					Transform parent = bone.parent;
					position = parent.TransformPoint(_localPositions[i]);
					rotation = parent.rotation * _localRotations[i];
				}

				float follow = helpers[i].follow;
				bone.SetPositionAndRotation(
					Vector3.Lerp(position, driver.TransformPoint(_driverPositions[i]), follow),
					Quaternion.Slerp(rotation, driver.rotation * _driverRotations[i], follow));
			}
		}
	}
}
