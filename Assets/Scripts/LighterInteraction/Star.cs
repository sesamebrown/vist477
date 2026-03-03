using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class Star : MonoBehaviour
{
    [SerializeField]
    Material m_LitMaterial;
    public void Light()
    {
        GetComponent<Renderer>().material = m_LitMaterial;
    }
}
