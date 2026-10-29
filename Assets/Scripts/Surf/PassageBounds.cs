using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;

namespace Game.Surf
{
    /// <summary>
    /// Visualization of the passage limits: buoys along both edges of the path, drawn with GPU instancing.
    /// </summary>
    public class PassageBounds : MonoBehaviour
    {
        private struct PostPair
        {
            public Matrix4x4 Left;
            public Matrix4x4 Right;
            public float Distance;
        }

        [SerializeField] private float wallMargin = 0.75f;
        [SerializeField] private float postSpacing = 3f;
        [SerializeField] private float spawnAhead = 140f;
        [SerializeField] private float despawnBehind = 20f;
        [Tooltip("Mesh and materials are taken from this prefab; it is never instantiated.")]
        [SerializeField] private GameObject postPrefab;
        [SerializeField] private float postRadius = 0.5f;
        [SerializeField] private float postHeight;

        private readonly Queue<PostPair> _active = new Queue<PostPair>();
        private Matrix4x4[] _matrices = new Matrix4x4[128];
        private Mesh _mesh;
        private RenderParams[] _renderParams;
        private BoardState _board;
        private BoardSettings _boardSettings;
        private SurfPath _path;
        private float _nextDistance;

        [Inject]
        public void Construct(BoardState board, BoardSettings boardSettings, SurfPath path)
        {
            _board = board;
            _boardSettings = boardSettings;
            _path = path;
        }

        private void Awake()
        {
            _mesh = postPrefab.GetComponentInChildren<MeshFilter>().sharedMesh;
            Material[] materials = postPrefab.GetComponentInChildren<MeshRenderer>().sharedMaterials;

            _renderParams = new RenderParams[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                _renderParams[i] = new RenderParams(materials[i])
                {
                    layer = gameObject.layer,
                    shadowCastingMode = ShadowCastingMode.On,
                    receiveShadows = true
                };
            }
        }

        private void Update()
        {
            float distance = _board.Distance;

            while (_nextDistance < distance + spawnAhead)
            {
                SpawnPair(_nextDistance);
                _nextDistance += postSpacing;
            }

            while (_active.Count > 0 && _active.Peek().Distance < distance - despawnBehind)
            {
                _active.Dequeue();
            }

            Draw();
        }

        private void SpawnPair(float distance)
        {
            _path.Sample(distance, out Vector3 center, out Vector3 forward);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Quaternion rotation = Quaternion.LookRotation(forward);
            float halfWidth = _boardSettings.PassageHalfWidth + wallMargin + postRadius;
            Vector3 up = Vector3.up * postHeight;

            _active.Enqueue(new PostPair
            {
                Left = Matrix4x4.TRS(center - right * halfWidth + up, rotation, Vector3.one),
                Right = Matrix4x4.TRS(center + right * halfWidth + up, rotation, Vector3.one),
                Distance = distance
            });
        }

        private void Draw()
        {
            int count = _active.Count * 2;
            if (count == 0)
            {
                return;
            }

            if (_matrices.Length < count)
            {
                _matrices = new Matrix4x4[Mathf.NextPowerOfTwo(count)];
            }

            int i = 0;
            foreach (PostPair pair in _active)
            {
                _matrices[i++] = pair.Left;
                _matrices[i++] = pair.Right;
            }

            for (int submesh = 0; submesh < _renderParams.Length; submesh++)
            {
                Graphics.RenderMeshInstanced(_renderParams[submesh], _mesh, submesh, _matrices, count);
            }
        }
    }
}
