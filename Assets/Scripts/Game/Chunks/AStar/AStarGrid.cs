using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public static class AStarGrid
    {
        public class Node
        {
            public int X { get; set; }
            public int Y { get; set; }

            public float F { get; set; }
            public float G { get; set; }
            public float H { get; set; }

            public Node Parent { get; set; }
            public bool IsBlocked { get; set; }
        }

        public static bool Calculate(bool[,] input, Vector2Int start, Vector2Int dest, Action<Node> onNodeProcessed,
            out List<Node> path)
        {
            if (input[start.x, start.y])
            {
                path = null;

                return false;
            }

            if (input[dest.x, dest.y])
            {
                path = null;

                return false;
            }

            if (start == dest)
            {
                path = null;

                return false;
            }

            Node[,] nodes = new Node[input.GetLength(0), input.GetLength(1)];

            int sizeX = input.GetLength(0);
            int sizeY = input.GetLength(1);

            for (int i = 0; i < sizeX; i++)
            {
                for (int j = 0; j < sizeY; j++)
                {
                    nodes[i, j] = new Node()
                    {
                        X = i,
                        Y = j,
                        F = float.MaxValue,
                        G = float.MaxValue,
                        H = float.MaxValue,
                        IsBlocked = input[i, j]
                    };
                }
            }

            nodes[start.x, start.y].F = 0f;
            nodes[start.x, start.y].G = 0f;
            nodes[start.x, start.y].H = 0f;
            nodes[start.x, start.y].Parent = nodes[start.x, start.y];

            onNodeProcessed?.Invoke(nodes[start.x, start.y]);

            List<Node> openList = new List<Node>();
            List<Node> closedList = new List<Node>();

            openList.Add(nodes[start.x, start.y]);

            while (openList.Count > 0)
            {
                Node q = new();
                float minF = float.MaxValue;

                for (int i = 0; i < openList.Count; i++)
                {
                    if (openList[i].F < minF)
                    {
                        q = openList[i];

                        minF = openList[i].F;
                    }
                }

                List<Node> qNeighbours = GetNodeNeighbours(nodes, q);

                openList.Remove(q);
                closedList.Add(q);

                foreach (var neighbour in qNeighbours)
                {
                    if (neighbour.X == dest.x && neighbour.Y == dest.y)
                    {
                        neighbour.Parent = q;

                        path = TracePath(nodes, dest);
                        return true;
                    }

                    if (!closedList.Contains(neighbour) && !neighbour.IsBlocked)
                    {
                        float gNew = q.G + 1f;
                        float hNew =
                            Mathf.Sqrt(Mathf.Pow(neighbour.X - dest.x, 2) + Mathf.Pow(neighbour.Y - dest.y, 2));
                        float fNew = gNew + hNew;

                        if (Mathf.Approximately(neighbour.F, float.MaxValue) || neighbour.F > fNew)
                        {
                            openList.Add(neighbour);

                            neighbour.F = fNew;
                            neighbour.G = gNew;
                            neighbour.H = hNew;
                            neighbour.Parent = q;

                            onNodeProcessed?.Invoke(neighbour);
                        }
                    }
                }

                openList.Remove(q);
            }

            path = null;

            return false;
        }

        private static List<Node> TracePath(Node[,] nodes, Vector2Int dest)
        {
            int x = dest.x;
            int y = dest.y;

            List<Node> resultPath = new List<Node>();

            while (!(nodes[x, y].Parent.X == x && nodes[x, y].Parent.Y == y))
            {
                resultPath.Add(nodes[x, y]);

                int tmpX = nodes[x, y].Parent.X;
                int tmpY = nodes[x, y].Parent.Y;

                x = tmpX;
                y = tmpY;
            }

            resultPath.Add(nodes[x, y]);

            return resultPath;
        }

        private static List<Node> GetNodeNeighbours(Node[,] nodes, Node node)
        {
            List<Node> neighbours = new List<Node>();

            int sizeX = nodes.GetLength(0);
            int sizeY = nodes.GetLength(1);

            if (node.X + 1 < sizeX)
            {
                neighbours.Add(nodes[node.X + 1, node.Y]);
            }

            if (node.Y + 1 < sizeY)
            {
                neighbours.Add(nodes[node.X, node.Y + 1]);
            }

            if (node.Y - 1 >= 0)
            {
                neighbours.Add(nodes[node.X, node.Y - 1]);
            }

            return neighbours;
        }
    }
}