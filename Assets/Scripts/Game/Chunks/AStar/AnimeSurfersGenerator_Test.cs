using System;
using UnityEngine;

namespace Game
{
    public class AnimeSurfersGenerator_Test : MonoBehaviour
    {
        [SerializeField] private Vector2Int fieldSize;

        [SerializeField] private GameObject prefab;
        
        [ContextMenu("Test Generator")]
        private void TestGenerator()
        {
            foreach (Transform tr in transform)
            {
                DestroyImmediate(tr.gameObject);
            }
            
            var grid = AnimeSurfersGenerator.GenerateRandom(fieldSize.x, fieldSize.y);

            for (int i = 0; i < fieldSize.x; i++)
            {
                for (int j = 0; j < fieldSize.y; j++)
                {
                    var node = grid[i, j];
                    
                    var cube = Instantiate(prefab, transform);

                    Color color = Color.white;

                    switch (node.NodeType)
                    {
                        case AnimeSurfersGenerator.NodeType.Empty:
                        {
                            color = Color.white;
                            break;
                        }
                        case AnimeSurfersGenerator.NodeType.Obstacle:
                        {
                            color = Color.grey;
                            break;
                        }
                        case AnimeSurfersGenerator.NodeType.Placeholder:
                        {
                            color = Color.green;
                            break;
                        }
                    }
                    
                    prefab.GetComponent<Renderer>().material.SetColor("_Color", color);

                    cube.transform.position = new Vector3(node.Position.x, node.Position.y + 0.5f, node.Position.z);
                }
            }
        }
    }
}
