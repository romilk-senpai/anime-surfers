using UnityEngine;

namespace SurferTest
{
    public class SurferTest : MonoBehaviour
    {
        [SerializeField] private Vector4 waveA;
        [SerializeField] private Vector4 waveB;
        [SerializeField] private Vector4 waveC;

        [SerializeField] private Renderer waterRenderer;

        private readonly int _waveA = Shader.PropertyToID("_WaveA");
        private readonly int _waveB = Shader.PropertyToID("_WaveB");
        private readonly int _waveC = Shader.PropertyToID("_WaveC");

        private void Start()
        {
            waterRenderer.material.SetVector(_waveA, waveA);
            waterRenderer.material.SetVector(_waveB, waveB);
            waterRenderer.material.SetVector(_waveC, waveC);
        }

        private Vector3 GerstnerWave(Vector4 wave, Vector3 position, out Vector3 tangent, out Vector3 binormal)
        {
            float steepness = wave.z;
            float wavelength = wave.w;
            float k = 2 * Mathf.PI / wavelength;
            float c = Mathf.Sqrt(9.8f / k);
            Vector2 d = new Vector2(wave.x, wave.y).normalized;
            float f = k * (Vector2.Dot(d, new Vector2(position.x, position.z)) - c * Time.time);
            float a = steepness / k;

            tangent = new Vector3(
                -d.x * d.x * (steepness * Mathf.Sin(f)),
                d.x * (steepness * Mathf.Cos(f)),
                -d.x * d.y * (steepness * Mathf.Sin(f))
            );
            binormal = new Vector3(
                -d.x * d.y * (steepness * Mathf.Sin(f)),
                d.y * (steepness * Mathf.Cos(f)),
                -d.y * d.y * (steepness * Mathf.Sin(f))
            );

            return new Vector3(
                d.x * (a * Mathf.Cos(f)),
                a * Mathf.Sin(f),
                d.y * (a * Mathf.Cos(f))
            );
        }

        public void GetWaveHeightAndNormal(Vector3 position, out float height, out Vector3 normal)
        {
            Vector3 pos = position;

            pos.y = 0;
            
            Vector3 tangent = new Vector3(1, 0, 0);
            Vector3 binormal = new Vector3(0, 0, 1);

            Vector3 displacementA = GerstnerWave(waveA, pos, out var tanA, out var biA);
            tangent += tanA;
            binormal += biA;

            Vector3 displacementB = GerstnerWave(waveB, pos, out var tanB, out var biB);
            tangent += tanB;
            binormal += biB;

            Vector3 displacementC = GerstnerWave(waveC, pos, out var tanC, out var biC);
            tangent += tanC;
            binormal += biC;

            Vector3 displacement = pos + displacementA + displacementB + displacementC;

            height = displacement.y;

            normal = Vector3.Cross(binormal, tangent).normalized;
        }
    }
}