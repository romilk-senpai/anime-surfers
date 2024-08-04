using System;
using UnityEngine;

namespace Player
{
    public class CharacterCollider : MonoBehaviour
    {
        public event Action<ControllerColliderHit> OnCollisionHit;

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {            
            OnCollisionHit?.Invoke(hit);
        }
    }
}