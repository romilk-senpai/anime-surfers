using System;
using UnityEngine;

namespace SurferTest
{
    public class MikoSurfer : MonoBehaviour
    {
        [SerializeField] private SurferTest water;

        private void Update()
        {
            var pos = transform.position;

            water.GetWaveHeightAndNormal(pos, out float yPos, out Vector3 normal);

            pos.y = yPos;

            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(Vector3.forward, normal));
        }
    }
}