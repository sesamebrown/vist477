using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

public class LightInteractor : MonoBehaviour
{
    [Header("Input")]
    [SerializeField]
    [Tooltip("Input to trigger the light. Typically the controller trigger.")]
    XRInputButtonReader m_LightInput = new XRInputButtonReader("Light");
    [SerializeField]
    GameObject m_LightCollider;
    [SerializeField]
    ParticleSystem m_LightParticleSystem;

    /// <summary>
    /// Input reader for the light trigger button.
    /// </summary>
    public XRInputButtonReader lightInput
    {
        get => m_LightInput;
        set => m_LightInput = value;
    }
    private bool m_IsLit = false;

    void Start()
    {
        // Ensure the light starts in the correct state
        PutOut();
    }
    
    void Update()
    {
        // Check light input state
        bool lightInputActive = m_LightInput.ReadIsPerformed();

        // Light
        if (lightInputActive && !m_IsLit)
        {
            Light();
        }
        // Put out
        else if (!lightInputActive && m_IsLit)
        {
            PutOut();
        }
    }
    private void Light()
    {
        m_LightCollider.SetActive(true);
        if (m_LightParticleSystem != null)
        {
            m_LightParticleSystem.Play();
        }
        m_IsLit = true;
    }
    private void PutOut()
    {
        m_LightCollider.SetActive(false);
        if (m_LightParticleSystem != null)
        {
            m_LightParticleSystem.Stop();
        }
        m_IsLit = false;
    }
}
