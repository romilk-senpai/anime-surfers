using UnityEngine;

namespace Player
{
    public class PlayerObject : MonoBehaviour
    {
        [SerializeField] private Transform playerTransform;
        [SerializeField] private CharacterController playerCharacterController;
        [SerializeField] private Animator playerAnimator;
        [SerializeField] private CharacterCollider playerCollider;
        [SerializeField] private Transform lookAtTarget;
        [SerializeField] private Transform groundCheck;

        public Transform PlayerTransform => playerTransform;

        public CharacterController PlayerCharacterController => playerCharacterController;

        public Animator PlayerAnimator => playerAnimator;
        public CharacterCollider PlayerCollider => playerCollider;
        public Transform LookAtTarget => lookAtTarget;
        public Transform GroundCheck => groundCheck;
    }
}