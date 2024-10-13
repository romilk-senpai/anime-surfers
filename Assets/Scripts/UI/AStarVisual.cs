using System;
using System.Collections.Generic;
using Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Utils
{
    public class AStarVisual : MonoBehaviour
    {
        [SerializeField] private RectTransform cellContainer;
        [SerializeField] private NodeButton cellPrefab;

        [SerializeField] private Button generateFiledButton;
        [SerializeField] private Button calculateButton;

        [SerializeField] private TMP_InputField gridSizeXInput;
        [SerializeField] private TMP_InputField gridSizeYInput;

        private List<NodeButton> _spawnedCells;

        private NodeButton[,] _cellsArray;

        private bool[,] _grid;

        private void Start()
        {
            generateFiledButton.onClick.AddListener(GenerateField);
            calculateButton.onClick.AddListener(Calculate);
        }

        private void GenerateField()
        {
            Vector2Int gridSize =
                new Vector2Int(Convert.ToInt32(gridSizeXInput.text), Convert.ToInt32(gridSizeYInput.text));

            if (_spawnedCells != null)
            {
                foreach (var cell in _spawnedCells)
                {
                    Destroy(cell.gameObject);
                }
            }

            _spawnedCells = new List<NodeButton>();

            _grid = new bool[gridSize.x, gridSize.y];
            _cellsArray = new NodeButton[gridSize.x, gridSize.y];

            for (int i = 0; i < gridSize.x; i++)
            {
                for (int j = 0; j < gridSize.y; j++)
                {
                    _grid[i, j] = Random.Range(0, 2) == 0;
                }
            }

            _grid[1, 0] = false;
            _grid[gridSize.x - 2, gridSize.y - 1] = false;

            for (int i = 0; i < gridSize.x; i++)
            {
                for (int j = 0; j < gridSize.y; j++)
                {
                    NodeButton button = Instantiate(cellPrefab, cellContainer);

                    _spawnedCells.Add(button);

                    button.gameObject.SetActive(true);

                    button.RectTransform.anchoredPosition = new Vector2(i * button.RectTransform.rect.size.x,
                        j * button.RectTransform.rect.size.y);

                    _cellsArray[i, j] = button;

                    button.SetColor(_grid[i, j] ? Color.gray : Color.white);

                    int buttonI = i;
                    int buttonJ = j;

                    button.Button.onClick.AddListener(() =>
                    {
                        _grid[buttonI, buttonJ] = !_grid[buttonI, buttonJ];
                        button.SetColor(_grid[buttonI, buttonJ] ? Color.gray : Color.white);
                    });
                }
            }

            _cellsArray[1, 0].SetColor(Color.green);
            _cellsArray[gridSize.x - 2, gridSize.y - 1].SetColor(Color.blue);

            _cellsArray[1, 0].Button.onClick.RemoveAllListeners();
            _cellsArray[gridSize.x - 2, gridSize.y - 1].Button.onClick.RemoveAllListeners();
        }

        private void Calculate()
        {
            int sizeX = _grid.GetLength(0);
            int sizeY = _grid.GetLength(1);

            Vector2Int start = new Vector2Int(1, 0);
            Vector2Int end = new Vector2Int(sizeX - 2, sizeY - 1);

            bool result = AStarGrid.Calculate(_grid, start, end, OnNodeProcessed, out List<AStarGrid.Node> path);

            if (result)
            {
                for (int i = 0; i < sizeX; i++)
                {
                    for (int j = 0; j < sizeY; j++)
                    {
                        _cellsArray[i, j].SetColor(_grid[i, j] ? Color.gray : Color.white);
                    
                    }
                }

                _cellsArray[start.x, start.y].SetColor(Color.green);
                _cellsArray[end.x, end.y].SetColor(Color.blue);

                foreach (var node in path)
                {
                    if ((node.X == start.x && node.Y == start.y) || (node.X == end.x && node.Y == end.y))
                    {
                        continue;
                    }

                    _cellsArray[node.X, node.Y].SetColor(Color.yellow);

                    if (_grid[node.X, node.Y])
                    {
                        _cellsArray[node.X, node.Y].SetColor(Color.red);
                    }
                }
            }
        }

        private void OnNodeProcessed(AStarGrid.Node node)
        {
            _cellsArray[node.X, node.Y].SetNodeData(node);
        }
    }
}