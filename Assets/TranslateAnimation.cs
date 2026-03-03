using UnityEngine;

public class TranslateAnimation : MonoBehaviour
{
    [SerializeField]
    [Tooltip("How far the object moves up and down.")]
    float m_Amplitude = 0.3f;
    
    [SerializeField]
    [Tooltip("How fast the object oscillates.")]
    float m_Speed = 0.5f;
    
    [SerializeField]
    [Tooltip("Rotation speed in degrees per second (X, Y, Z).")]
    Vector3 m_RotationSpeed = new Vector3(0, 10f, 0);
    
    Vector3 m_StartPosition;
    float m_TimeOffset;
    
    void Start()
    {
        m_StartPosition = transform.position;
        m_TimeOffset = Random.Range(0f, 2f * Mathf.PI);
    }

    void Update()
    {
        float yOffset = Mathf.Sin(Time.time * m_Speed + m_TimeOffset) * m_Amplitude;
        transform.position = m_StartPosition + new Vector3(0, yOffset, 0);
        
        transform.Rotate(m_RotationSpeed * Time.deltaTime);
    }
}
