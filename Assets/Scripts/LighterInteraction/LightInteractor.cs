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
        transform.GetChild(0).gameObject.SetActive(true);
        m_IsLit = true;
    }
    private void PutOut()
    {
        transform.GetChild(0).gameObject.SetActive(false);
        m_IsLit = false;
    }
}
