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

        public async void JumpAnimation()
        {
            _playerObject.PlayerAnimator.SetTrigger(_jumpProp);
            _playerObject.PlayerAnimator.SetBool(_airborneProp, true);

            await UniTask.Yield(PlayerLoopTiming.FixedUpdate);

            await UniTask.WaitForSeconds(.7f);
            
            while (true)
            {
                if (Physics.Raycast(_playerObject.PlayerTransform.position, transform.TransformDirection(Vector3.down),
                        out RaycastHit hit, 100f))
                {
                    if (hit.distance < 3f)
                    {
                        _playerObject.PlayerAnimator.SetBool(_airborneProp, false);
                        break;
                    }
                }

                await UniTask.Yield(PlayerLoopTiming.FixedUpdate);
            }
        }
    }
}