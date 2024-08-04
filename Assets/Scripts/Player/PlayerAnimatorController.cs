using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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

        [Inject]
        private void Inject(PlayerObject playerObject)
        {
            _playerObject = playerObject;
        }

        public void SetSpeed(float speed)
        {
            _playerObject.PlayerAnimator.SetFloat(_speedProp, speed);
        }

        public void JumpAnimation()
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
    }
}