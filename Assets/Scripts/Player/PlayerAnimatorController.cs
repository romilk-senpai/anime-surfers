using System.Collections;
using UnityEngine;
using Zenject;

namespace Player
{
    public class PlayerAnimatorController : MonoBehaviour, IPlayerAnimatorController
    {
        private PlayerObject _playerObject;

        private readonly int _speedProp = Animator.StringToHash("Speed");
        private readonly int _jumpProp = Animator.StringToHash("Jump");
        private readonly int _airborneProp = Animator.StringToHash("Airborne");
        private readonly int _deathProp = Animator.StringToHash("Death");
        private readonly int _hitLeftProp = Animator.StringToHash("HitLeft");
        private readonly int _hitRightProp = Animator.StringToHash("HitRight");
        private readonly int _hitLowProp = Animator.StringToHash("HitLow");
        private readonly int _dodgeLeftProp = Animator.StringToHash("DodgeLeft");
        private readonly int _dodgeRightProp = Animator.StringToHash("DodgeRight");

        [Inject]
        private void Inject(PlayerObject playerObject)
        {
            _playerObject = playerObject;
        }

        public void SetSpeed(float speed)
        {
            _playerObject.PlayerAnimator.SetFloat(_speedProp, speed);
        }

        public void PlayJump()
        {
            StartCoroutine(JumpCoroutine());
        }

        private IEnumerator JumpCoroutine()
        {
            _playerObject.PlayerAnimator.SetTrigger(_jumpProp);
            _playerObject.PlayerAnimator.SetBool(_airborneProp, true);

            yield return new WaitForSeconds(.15f);

            while (true)
            {
                if (Physics.Raycast(_playerObject.PlayerTransform.position, Vector3.down,
                        out RaycastHit hit, 100f))
                {
                    if (hit.distance < .5f)
                    {
                        _playerObject.PlayerAnimator.SetBool(_airborneProp, false);
                        break;
                    }
                }

                yield return new WaitForFixedUpdate();
            }
        }

        public void PlayDeath()
        {
            _playerObject.PlayerAnimator.SetTrigger(_deathProp);
        }

        public void PlayHit(HitSide hitSide)
        {
            int prop = hitSide switch
            {
                HitSide.Left => _hitLeftProp,
                HitSide.Right => _hitRightProp,
                HitSide.Low => _hitLowProp,
                _ => throw new System.NotImplementedException()
            };

            _playerObject.PlayerAnimator.SetTrigger(prop);
        }

        public void PlayDodgeLeft()
        {
            _playerObject.PlayerAnimator.SetTrigger(_dodgeLeftProp);
        }

        public void PlayDodgeRight()
        {
            _playerObject.PlayerAnimator.SetTrigger(_dodgeRightProp);
        }
    }
}