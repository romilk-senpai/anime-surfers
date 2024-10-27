using System;
using UnityEngine;

namespace Player
{
    public class CharacterCollider : MonoBehaviour
    {
        public event Action<Collision> OnCollisionHit;

        private void OnCollisionEnter(Collision col) {
            OnCollisionHit?.Invoke(col);
        }
    }
}