using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Feedback;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
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

    [Header("Haptics")]
    [SerializeField]
    [Tooltip("Intensity for the quick buzz when the lighter turns on.")]
    [Range(0f, 1f)]
    float m_LightOnHapticIntensity = 0.6f;

    [SerializeField]
    [Tooltip("Duration for the quick buzz when the lighter turns on.")]
    [Min(0f)]
    float m_LightOnHapticDuration = 0.04f;

    [SerializeField]
    [Tooltip("Frequency for the quick buzz when the lighter turns on.")]
    [Range(0f, 1f)]
    float m_LightOnHapticFrequency = 0.2f;

    [SerializeField]
    [Tooltip("Intensity for each buzz when a star is lit.")]
    [Range(0f, 1f)]
    float m_StarLitHapticIntensity = 0.85f;

    [SerializeField]
    [Tooltip("Duration for each buzz when a star is lit.")]
    [Min(0f)]
    float m_StarLitHapticDuration = 0.03f;

    [SerializeField]
    [Tooltip("Frequency for star lit buzzes.")]
    [Range(0f, 1f)]
    float m_StarLitHapticFrequency = 0.75f;

    [SerializeField]
    [Tooltip("How many quick buzzes to play when a star is newly lit.")]
    [Min(1)]
    int m_StarLitBuzzCount = 4;

    [SerializeField]
    [Tooltip("Time between star lit buzzes.")]
    [Min(0.01f)]
    float m_StarLitBuzzGap = 0.06f;

    /// <summary>
    /// Input reader for the light trigger button.
    /// </summary>
    public XRInputButtonReader lightInput
    {
        get => m_LightInput;
        set => m_LightInput = value;
    }

    private bool m_IsLit = false;
    SimpleHapticFeedback m_HapticFeedback;
    HapticImpulsePlayer m_HapticPlayer;
    bool m_WarnedMissingHaptics;
    Coroutine m_StarLitHapticRoutine;

    void Start()
    {
        // Ensure the light starts in the correct state
        PutOut();
        m_HapticFeedback = GetComponent<SimpleHapticFeedback>();
        ResolveHapticPlayer();
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
        PlayLightOnBuzz();
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

    public void PlayStarLitBuzzSequence()
    {
        if (!ResolveHapticPlayer())
            return;

        if (m_StarLitHapticRoutine != null)
            StopCoroutine(m_StarLitHapticRoutine);

        m_StarLitHapticRoutine = StartCoroutine(PlayStarLitBuzzSequenceRoutine());
    }

    IEnumerator PlayStarLitBuzzSequenceRoutine()
    {
        int buzzCount = Mathf.Max(1, m_StarLitBuzzCount);
        float gap = Mathf.Max(0.01f, m_StarLitBuzzGap);

        for (int i = 0; i < buzzCount; i++)
        {
            m_HapticPlayer.SendHapticImpulse(m_StarLitHapticIntensity, m_StarLitHapticDuration, m_StarLitHapticFrequency);

            if (i < buzzCount - 1)
                yield return new WaitForSeconds(gap);
        }

        m_StarLitHapticRoutine = null;
    }

    void PlayLightOnBuzz()
    {
        if (!ResolveHapticPlayer())
            return;

        m_HapticPlayer.SendHapticImpulse(m_LightOnHapticIntensity, m_LightOnHapticDuration, m_LightOnHapticFrequency);
    }

    bool ResolveHapticPlayer()
    {
        if (m_HapticPlayer != null)
            return true;

        if (m_HapticFeedback == null)
            m_HapticFeedback = GetComponent<SimpleHapticFeedback>();

        if (m_HapticFeedback != null)
            m_HapticPlayer = m_HapticFeedback.hapticImpulsePlayer;

        if (m_HapticPlayer == null)
            m_HapticPlayer = GetComponentInParent<HapticImpulsePlayer>();

        if (m_HapticPlayer == null)
            m_HapticPlayer = GetComponentInChildren<HapticImpulsePlayer>(true);

        if (m_HapticPlayer == null && !m_WarnedMissingHaptics)
        {
            Debug.LogWarning("No HapticImpulsePlayer found for LightInteractor. Lighter haptics will not play.");
            m_WarnedMissingHaptics = true;
        }

        return m_HapticPlayer != null;
    }
}
