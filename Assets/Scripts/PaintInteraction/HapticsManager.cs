using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Feedback;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using System.Collections;

public class HapticsManager : MonoBehaviour
{
    [Header("Color Haptics")]
    [SerializeField]
    [Min(1)]
    public int totalNumColors = 7;

    [SerializeField]
    [Tooltip("Impulse intensity sent for color haptics (0-1).")]
    [Range(0f, 1f)]
    float m_HapticIntensity = 0.5f;

    [SerializeField]
    [Tooltip("Duration of each color haptic impulse in seconds.")]
    [Min(0f)]
    float m_PulseDuration = 0.08f;

    [SerializeField]
    [Tooltip("Total time in seconds to sustain the color haptic.")]
    [Min(0f)]
    float m_TotalPlayDuration = 2f;

    [SerializeField]
    [Tooltip("Gap between pulse starts while sustaining haptics.")]
    [Min(0.01f)]
    float m_PulseInterval = 0.08f;

    [SerializeField]
    [Tooltip("Lowest haptic frequency used for color index 0.")]
    [Range(0f, 1f)]
    float m_MinFrequency = 0f;

    [SerializeField]
    [Tooltip("Highest haptic frequency used for the last color index.")]
    [Range(0f, 1f)]
    float m_MaxFrequency = 1f;

    SimpleHapticFeedback m_HapticFeedback;
    HapticImpulsePlayer m_HapticPlayer;
    bool m_WarnedMissingHaptics;
    Coroutine m_ColorHapticRoutine;

    void Awake()
    {
        m_HapticFeedback = GetComponent<SimpleHapticFeedback>();
        ResolveHapticPlayer();
    }

    /// <summary>
    /// Plays a haptic impulse where frequency is based on the normalized color index.
    /// Example with 7 colors: index 0 -> 0.0, index 3 -> 0.5, index 6 -> 1.0.
    /// </summary>
    /// <param name="index">Color preset index to map into the configured frequency range.</param>
    public void PlayColorHaptic(int index)
    {
        if (!ResolveHapticPlayer())
            return;

        int safeTotal = Mathf.Max(1, totalNumColors);
        int clampedIndex = Mathf.Clamp(index, 0, safeTotal - 1);

        float normalized = safeTotal <= 1
            ? 0f
            : clampedIndex / (float)(safeTotal - 1);

        float minFrequency = Mathf.Min(m_MinFrequency, m_MaxFrequency);
        float maxFrequency = Mathf.Max(m_MinFrequency, m_MaxFrequency);
        float frequency = Mathf.Lerp(minFrequency, maxFrequency, normalized);

        if (m_ColorHapticRoutine != null)
            StopCoroutine(m_ColorHapticRoutine);

        m_ColorHapticRoutine = StartCoroutine(PlayColorHapticRoutine(frequency));
    }

    IEnumerator PlayColorHapticRoutine(float frequency)
    {
        if (m_TotalPlayDuration <= 0f)
        {
            m_HapticPlayer.SendHapticImpulse(m_HapticIntensity, m_PulseDuration, frequency);
            m_ColorHapticRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        float pulseInterval = Mathf.Max(0.01f, m_PulseInterval);

        while (elapsed < m_TotalPlayDuration)
        {
            m_HapticPlayer.SendHapticImpulse(m_HapticIntensity, m_PulseDuration, frequency);
            yield return new WaitForSeconds(pulseInterval);
            elapsed += pulseInterval;
        }

        m_ColorHapticRoutine = null;
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
            Debug.LogWarning("No HapticImpulsePlayer found for HapticsManager. Color haptics will not play.");
            m_WarnedMissingHaptics = true;
        }

        return m_HapticPlayer != null;
    }
}
