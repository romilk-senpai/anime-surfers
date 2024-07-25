using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace Player
{
    public class PlayerAnimatorController : MonoBehaviour
    {
        private PlayerObject _playerObject;

        private readonly int _speedProp = Animator.StringToHash("Speed");
        private readonly int _jumpProp = Animator.StringToHash("Jump");
        private readonly int _airborneProp = Animator.StringToHash("Airborne");

        [Inject]
        private void Inject(PlayerObject playerObject)
        {
            _playerObject = playerObject;
        }

        public void SetRunAnimation()
        {
            _playerObject.PlayerAnimator.SetFloat(_speedProp, 1f);
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
    }
}