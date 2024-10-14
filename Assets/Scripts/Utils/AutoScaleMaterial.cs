using UnityEngine;

public class AutoScaleMaterial : MonoBehaviour
{
    [SerializeField] private Renderer materialRenderer;

    private void Start()
    {
        Vector3 scale = materialRenderer.bounds.size;

        materialRenderer.material.mainTextureScale = new Vector2(scale.x, scale.z);
    }
}
