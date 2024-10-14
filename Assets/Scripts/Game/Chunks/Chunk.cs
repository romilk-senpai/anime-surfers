using UnityEngine;

namespace Game.Chunks
{
    public abstract class Chunk : MonoBehaviour
    {
        [SerializeField] private float chunkLength = 1;
        [SerializeField] private int chunkWidth = 3;
        [SerializeField] private float cellWidth = 1;

        public int ChunkLength => Mathf.FloorToInt(chunkLength);
        public int ChunkWidth => chunkWidth;
        protected float MinCellX => -cellWidth;
        protected float CellWidth => cellWidth;

#if UNITY_EDITOR
        protected virtual void OnDrawGizmos()
        {
            int floorLength = Mathf.FloorToInt(chunkLength);

            for (int i = 0; i <= chunkWidth; i++)
            {
                Gizmos.DrawLine(transform.TransformPoint(new Vector3(MinCellX + CellWidth * i - cellWidth / 2f, 0, 0)),
                    transform.TransformPoint(new Vector3(MinCellX + CellWidth * i - cellWidth / 2f, 0, 0) + new Vector3(0, 0, floorLength)));
            }

            for (int i = 0; i <= floorLength; i++)
            {
                Gizmos.DrawLine(transform.TransformPoint(new Vector3(MinCellX - cellWidth / 2f, 0, 0) + new Vector3(0, 0, i)),
                    transform.TransformPoint(new Vector3(MinCellX + CellWidth * ChunkWidth - cellWidth / 2f, 0, 0) + new Vector3(0, 0, i)));
            }
        }
#endif

        public abstract void Generate(Vector2Int start, Vector2Int dest);
    }
}