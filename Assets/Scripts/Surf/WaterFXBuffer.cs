using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Surf
{
    /// <summary>
    /// Renders foam particles top-down into a small texture that follows the board, which the water shader
    /// samples for foam and normal bumps (Boat Attack's WaterFX buffer, without a second camera).
    /// Sources should use the AnimeSurfers/WaterFX material, so the regular cameras never draw them.
    /// </summary>
    [DefaultExecutionOrder(20)]
    public class WaterFXBuffer : MonoBehaviour
    {
        private static readonly int MapId = Shader.PropertyToID("_WaterFXMap");
        private static readonly int ParamsId = Shader.PropertyToID("_WaterFXParams");
        private static readonly int NormalStrengthId = Shader.PropertyToID("_WaterFXNormalStrength");

        [SerializeField] private Transform followTarget;
        [Tooltip("World-space width of the square buffer, in meters.")]
        [SerializeField] private float size = 32f;
        [SerializeField] private int resolution = 256;
        [SerializeField] private float normalStrength = 3f;
        [SerializeField] private ParticleSystemRenderer[] sources;

        private readonly List<Mesh> _meshes = new List<Mesh>();
        private RenderTexture _texture;
        private CommandBuffer _commands;

        private void OnEnable()
        {
            _texture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = "WaterFXMap",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            _texture.Create();
            _commands = new CommandBuffer { name = "WaterFX" };

            for (int i = 0; i < sources.Length; i++)
            {
                var mesh = new Mesh { name = "WaterFX Particles" };
                mesh.MarkDynamic();
                _meshes.Add(mesh);
            }
        }

        private void OnDisable()
        {
            Shader.SetGlobalVector(ParamsId, Vector4.zero);

            foreach (Mesh mesh in _meshes)
            {
                Destroy(mesh);
            }
            _meshes.Clear();

            _commands.Release();
            _texture.Release();
            Destroy(_texture);
        }

        private void LateUpdate()
        {
            // Snap to whole texels so foam doesn't shimmer as the window slides.
            float texel = size / resolution;
            Vector3 target = followTarget.position;
            var center = new Vector3(Mathf.Round(target.x / texel) * texel, 0f, Mathf.Round(target.z / texel) * texel);

            float half = size * 0.5f;
            // SetViewProjectionMatrices already applies the platform's render-texture flip; don't flip again.
            Matrix4x4 projection = Matrix4x4.Ortho(-half, half, -half, half, 0.1f, 200f);
            // Looking straight down with +Z up, so buffer u follows world x and v follows world z.
            Matrix4x4 view = Matrix4x4.TRS(center + Vector3.up * 100f, Quaternion.Euler(90f, 0f, 0f), new Vector3(1f, 1f, -1f)).inverse;

            _commands.Clear();
            _commands.SetRenderTarget(_texture);
            _commands.ClearRenderTarget(false, true, Color.clear);
            _commands.SetViewProjectionMatrices(view, projection);

            for (int i = 0; i < sources.Length; i++)
            {
                ParticleSystemRenderer source = sources[i];
                if (source == null || !source.gameObject.activeInHierarchy)
                {
                    continue;
                }

                source.BakeMesh(_meshes[i], ParticleSystemBakeMeshOptions.BakeRotationAndScale | ParticleSystemBakeMeshOptions.BakePosition);
                _commands.DrawMesh(_meshes[i], Matrix4x4.identity, source.sharedMaterial, 0, 0);
            }

            Graphics.ExecuteCommandBuffer(_commands);

            Shader.SetGlobalTexture(MapId, _texture);
            Shader.SetGlobalVector(ParamsId, new Vector4(center.x - half, center.z - half, 1f / size, 1f / resolution));
            Shader.SetGlobalFloat(NormalStrengthId, normalStrength);
        }
    }
}
