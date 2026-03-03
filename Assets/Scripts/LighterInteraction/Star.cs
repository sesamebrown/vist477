using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class Star : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Duration in seconds for the lighting transition.")]
    float m_LightDuration = 1f;
    
    [SerializeField]
    [Tooltip("Duration in seconds for the spin animation.")]
    float m_SpinDuration = 2f;
    
    [SerializeField]
    [Tooltip("Number of full rotations during the spin.")]
    float m_SpinRotations = 2f;
    
    [SerializeField]
    [Tooltip("Maximum spread radius to cover the mesh.")]
    float m_MaxSpreadRadius = 10f;
    
    Material m_LitMaterial;
    bool m_IsLit = false;
    
    void Awake()
    {
        m_LitMaterial = GetComponent<Renderer>().material;
        m_LitMaterial.SetFloat("_Blend", 0f);
    }
    
    public void Light()
    {
        Light(transform.position); // Default to center if no collision point provided
    }
    
    public void Light(Vector3 worldCollisionPoint)
    {
        if (!m_IsLit)
        {
            Vector3 localCollisionPoint = transform.InverseTransformPoint(worldCollisionPoint);
            StartCoroutine(LightTransition(localCollisionPoint));
        }
    }
    
    IEnumerator LightTransition(Vector3 localCollisionPoint)
    {
        m_IsLit = true;
        float elapsed = 0f;
        float startBlend = m_LitMaterial.GetFloat("_Blend");
        Quaternion startRotation = transform.rotation;
        
        // Set collision point in material
        m_LitMaterial.SetVector("_CollisionPoint", localCollisionPoint);
        Debug.Log($"Star lighting from local point: {localCollisionPoint}, max radius: {m_MaxSpreadRadius}");
        
        while (elapsed < Mathf.Max(m_LightDuration, m_SpinDuration))
        {
            elapsed += Time.deltaTime;
            
            // Animate spread radius
            if (elapsed < m_LightDuration)
            {
                float spreadProgress = elapsed / m_LightDuration;
                float radius = Mathf.Lerp(0f, m_MaxSpreadRadius, spreadProgress);
                m_LitMaterial.SetFloat("_SpreadRadius", radius);
                
                // Still update blend for fallback/combination
                float blend = Mathf.Lerp(startBlend, 1f, spreadProgress);
                m_LitMaterial.SetFloat("_Blend", blend);
            }
            else
            {
                m_LitMaterial.SetFloat("_SpreadRadius", m_MaxSpreadRadius);
                m_LitMaterial.SetFloat("_Blend", 1f);
            }
            
            // Spin animation with ease in/out
            if (elapsed < m_SpinDuration)
            {
                float spinProgress = elapsed / m_SpinDuration;
                // Smoother step for gentler ease in/out effect
                float smoothProgress = spinProgress * spinProgress * spinProgress * (spinProgress * (spinProgress * 6f - 15f) + 10f);
                float angle = smoothProgress * 360f * m_SpinRotations;
                transform.rotation = startRotation * Quaternion.Euler(0, angle, 0);
            }
            else
            {
                transform.rotation = startRotation * Quaternion.Euler(0, 360f * m_SpinRotations, 0);
            }
            
            yield return null;
        }
        
        m_LitMaterial.SetFloat("_Blend", 1f);
        m_LitMaterial.SetFloat("_SpreadRadius", m_MaxSpreadRadius);
    }
}
