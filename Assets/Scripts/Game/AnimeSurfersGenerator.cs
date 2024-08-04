using UnityEngine;

namespace Game
{
    public class AnimeSurfersGenerator
    {
        public enum NodeType
        {
            Empty,
            Placeholder,
            Obstacle
        }

        public struct AnimeSurfersNode
        {
            public Vector3Int Position;
            public NodeType NodeType;

            public AnimeSurfersNode(Vector3Int position, NodeType nodeType)
            {
                Position = position;
                NodeType = nodeType;
            }
        }

        public static AnimeSurfersNode[,] GenerateRandom(int sizeX, int sizeY)
        {
            var grid = new AnimeSurfersNode[sizeX, sizeY];

            for (int i = 0; i < sizeX; i++)
            {
                // try generate
                //validate
                
                for (int j = 0; j < sizeY; j++)
                {
                    grid[i, j] = new AnimeSurfersNode(new Vector3Int(i, 0, j), NodeType.Empty);
                }
            }
            
            return grid;
        }
    }
}