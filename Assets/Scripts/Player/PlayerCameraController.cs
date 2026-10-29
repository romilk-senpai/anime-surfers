
using UnityEngine;

namespace Player
{
    public class PlayerCameraController : MonoBehaviour, IPlayerCameraController
    {
        [SerializeField] private Unity.Cinemachine.CinemachineBrain cinemachineBrain;
        [SerializeField] private Unity.Cinemachine.CinemachineVirtualCamera followCamera;
        [SerializeField] private Unity.Cinemachine.CinemachineVirtualCamera deathCamera;

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