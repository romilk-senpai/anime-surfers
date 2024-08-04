using UnityEngine;

namespace Game.Chunks
{
    public abstract class ChunkObject : MonoBehaviour
    {
        [SerializeField] private float chunkLength = 1;
        [SerializeField] private int chunkWidth = 3;

        public int ChunkLength => Mathf.FloorToInt(chunkLength);
        public int ChunkWidth => chunkWidth;

        private bool[,] _chunkArray;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            int floorLength = Mathf.FloorToInt(chunkLength);

            Gizmos.DrawLine(transform.TransformPoint(new Vector3(1.5f, 0, 0)),
                transform.TransformPoint(new Vector3(1.5f, 0, 0) + new Vector3(0, 0, floorLength)));

            Gizmos.DrawLine(transform.TransformPoint(new Vector3(0.5f, 0, 0)),
                transform.TransformPoint(new Vector3(0.5f, 0, 0) + new Vector3(0, 0, floorLength)));

            Gizmos.DrawLine(transform.TransformPoint(new Vector3(-0.5f, 0, 0)),
                transform.TransformPoint(new Vector3(-0.5f, 0, 0) + new Vector3(0, 0, floorLength)));

            Gizmos.DrawLine(transform.TransformPoint(new Vector3(-1.5f, 0, 0)),
                transform.TransformPoint(new Vector3(-1.5f, 0, 0) + new Vector3(0, 0, floorLength)));

            for (int i = 0; i <= floorLength; i++)
            {
                Gizmos.DrawLine(transform.TransformPoint(new Vector3(1.5f, 0, 0) + new Vector3(0, 0, i)),
                    transform.TransformPoint(new Vector3(-1.5f, 0, 0) + new Vector3(0, 0, i)));
            }

            if (_chunkArray == null)
            {
                return;
            }

            for (int i = 0; i < chunkLength; i++)
            {
                for (int j = 0; j < chunkWidth; j++)
                {
                    if (_chunkArray[i, j])
                    {
                        //Gizmos.DrawCube(transform.TransformPoint(new Vector3(j - 1, 0, i + 0.5f)), new Vector3(1, 0.1f, 1));
                    }
                }
            }
        }
#endif

        public abstract void Generate(Vector2Int start, Vector2Int dest);
    }
}