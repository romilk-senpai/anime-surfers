using UnityEngine;

namespace Game.Rendering
{
	/// <summary>
	/// Feeds the head orientation to ToonCharacter materials that use Face Shading,
	/// so the face shadow follows the head instead of the mesh normals.
	/// </summary>
	[ExecuteAlways]
	public class ToonFaceDirection : MonoBehaviour
	{
		private static readonly int FaceCenterId = Shader.PropertyToID("_FaceCenter");
		private static readonly int FaceForwardId = Shader.PropertyToID("_FaceForward");
		private static readonly int FaceRightId = Shader.PropertyToID("_FaceRight");
		private static readonly int FaceUpId = Shader.PropertyToID("_FaceUp");

		[SerializeField] private Renderer faceRenderer;
		[Tooltip("Defaults to the humanoid head bone.")]
		[SerializeField] private Transform head;
		[Tooltip("Face center relative to the head bone, in meters along the head's axes.")]
		[SerializeField] private Vector3 centerOffset = new Vector3(0f, 0.06f, 0f);

		private MaterialPropertyBlock _block;

		private void OnEnable()
		{
			_block ??= new MaterialPropertyBlock();

			if (head == null && TryGetComponent(out Animator animator) && animator.isHuman)
			{
				head = animator.GetBoneTransform(HumanBodyBones.Head);
			}
		}

		private void LateUpdate()
		{
			if (faceRenderer == null || head == null)
			{
				return;
			}

			faceRenderer.GetPropertyBlock(_block);
			// Offset in meters: TransformPoint would apply the bone's scale (100x on centimeter rigs).
			_block.SetVector(FaceCenterId, head.position + head.rotation * centerOffset);
			_block.SetVector(FaceForwardId, head.forward);
			_block.SetVector(FaceRightId, head.right);
			_block.SetVector(FaceUpId, head.up);
			faceRenderer.SetPropertyBlock(_block);
		}
	}
}
