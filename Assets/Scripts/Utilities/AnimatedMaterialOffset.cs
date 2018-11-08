using UnityEngine;

public class AnimatedMaterialOffset : MonoBehaviour
{
    [Header("Animated offsets")]
    [SerializeField]
    private Vector2 m_offsetAnimationSpeed;

    private Material m_material;

    private void Awake()
    {
        m_material = GetComponent<Renderer>().material;
    }

    private void Update()
    {
        Vector2 materialOffset = m_material.GetTextureOffset("_MainTex");

        m_material.SetTextureOffset("_MainTex", materialOffset + m_offsetAnimationSpeed * Time.deltaTime);
    }
}
