using UnityEngine;

namespace Player
{
    public class PlayerObject : MonoBehaviour
    {
        [SerializeField] private Transform playerTransform;
        [SerializeField] private CharacterController playerCharacterController;
        [SerializeField] private Animator playerAnimator;

        [SerializeField] private Transform lookAtTarget;
        
        public Transform PlayerTransform => playerTransform;

        public CharacterController PlayerCharacterController => playerCharacterController;

        public Animator PlayerAnimator => playerAnimator;
        public Transform LookAtTarget => lookAtTarget;
    }
}
