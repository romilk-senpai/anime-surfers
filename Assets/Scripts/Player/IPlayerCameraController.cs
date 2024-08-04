using UnityEngine;

namespace Player
{
    public interface IPlayerCameraController
    {
        void StartFollowing(Transform cameraTarget);
    }
}
