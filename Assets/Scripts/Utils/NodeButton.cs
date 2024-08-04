using Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NodeButton : MonoBehaviour
{
    [SerializeField] private Image image;
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI text;

    public Button Button => button;
    public RectTransform RectTransform => image.rectTransform;

    public void SetColor(Color color)
    {
        image.color = color;
    }

    public void SetNodeData(AStarGrid.Node node)
    {
        text.text = $"F:{node.F:0.00}\nG:{node.G:0.00}\nH:{node.H:0.00}";
    }

    public void Clear()
    {
        text.text = string.Empty;
    }
}