using Cinemachine;
using UnityEngine;

namespace Player
{
    public class PlayerCameraController : MonoBehaviour, IPlayerCameraController
    {
        [SerializeField] private CinemachineBrain cinemachineBrain;
        [SerializeField] private CinemachineVirtualCamera followCamera;
        [SerializeField] private CinemachineVirtualCamera deathCamera;

        private Transform _cameraTarget;

        public void StartFollowing(Transform cameraTarget)
        {
            _cameraTarget = cameraTarget;

            followCamera.Follow = _cameraTarget;
            followCamera.LookAt = _cameraTarget;

            followCamera.gameObject.SetActive(true);
        }

        public void SetDeathCamera(Transform cameraTarget)
        {
            deathCamera.Follow = _cameraTarget;
            deathCamera.LookAt = _cameraTarget;

            followCamera.gameObject.SetActive(false);
            deathCamera.gameObject.SetActive(true);
        }
    }
}