using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class Star : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Duration in seconds for the lighting transition.")]
    float m_LightDuration = 1f;
    
    Material m_LitMaterial;
    bool m_IsLit = false;
    
    void Awake()
    {
        m_LitMaterial = GetComponent<Renderer>().material;
        m_LitMaterial.SetFloat("_Blend", 0f);
    }
    
    public void Light()
    {
        if (!m_IsLit)
        {
            StartCoroutine(LightTransition());
        }
    }
    
    IEnumerator LightTransition()
    {
        m_IsLit = true;
        float elapsed = 0f;
        float startBlend = m_LitMaterial.GetFloat("_Blend");
        
        while (elapsed < m_LightDuration)
        {
            elapsed += Time.deltaTime;
            float blend = Mathf.Lerp(startBlend, 1f, elapsed / m_LightDuration);
            m_LitMaterial.SetFloat("_Blend", blend);
            yield return null;
        }
        
        m_LitMaterial.SetFloat("_Blend", 1f);
    }
}
