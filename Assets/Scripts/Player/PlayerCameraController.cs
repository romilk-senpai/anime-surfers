using Cinemachine;
using UnityEngine;

namespace Player
{
    public class PlayerCameraController : MonoBehaviour
    {
        [SerializeField] private CinemachineVirtualCamera cinemachineCamera;

        private Transform _cameraTarget;

        public void StartFollowing(Transform cameraTarget)
        {
            _cameraTarget = cameraTarget;

            cinemachineCamera.Follow = _cameraTarget;
            cinemachineCamera.LookAt = _cameraTarget;
        }
    }
}