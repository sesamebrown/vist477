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
    [Tooltip("Curve that maps normalized color index to overall haptic strength.")]
    AnimationCurve m_HapticStrengthCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [SerializeField]
    [Tooltip("Minimum impulse intensity sent for color haptics (0-1).")]
    [Range(0f, 1f)]
    float m_MinHapticIntensity = 0.2f;

    [SerializeField]
    [Tooltip("Maximum impulse intensity sent for the highest color index (0-1).")]
    [Range(0f, 1f)]
    float m_MaxHapticIntensity = 0.8f;

    [SerializeField]
    [Tooltip("Duration of each color haptic impulse in seconds.")]
    [Min(0f)]
    float m_PulseDuration = 0.08f;

    [SerializeField]
    [Tooltip("Minimum total time in seconds for sustained color haptics at the lowest index.")]
    [Min(0f)]
    float m_MinPlayDuration = 0.8f;

    [SerializeField]
    [Tooltip("Maximum total time in seconds for sustained color haptics at the highest index.")]
    [Min(0f)]
    float m_MaxPlayDuration = 2f;

    [SerializeField]
    [Tooltip("Minimum impulses per second used for the lowest color index.")]
    [Min(0.01f)]
    float m_MinImpulsesPerSecond = 3f;

    [SerializeField]
    [Tooltip("Maximum impulses per second used for the highest color index.")]
    [Min(0.01f)]
    float m_MaxImpulsesPerSecond = 15f;

    [SerializeField]
    [Tooltip("Lowest haptic frequency used for color index 0.")]
    [Range(0f, 1f)]
    float m_MinFrequency = 0f;

    [SerializeField]
    [Tooltip("Highest haptic frequency used for the last color index.")]
    [Range(0f, 1f)]
    float m_MaxFrequency = 1f;

    [Header("Wrong Color Haptics")]
    [SerializeField]
    [Tooltip("Total buzz time when WrongColorHaptic is triggered.")]
    [Min(0f)]
    float m_WrongColorDuration = 3f;

    [SerializeField]
    [Tooltip("Pulse duration for wrong color buzz.")]
    [Min(0f)]
    float m_WrongColorPulseDuration = 0.08f;

    [SerializeField]
    [Tooltip("Pulse rate for wrong color buzz.")]
    [Min(0.01f)]
    float m_WrongColorImpulsesPerSecond = 20f;

    [SerializeField]
    [Tooltip("Frequency used for wrong color buzz.")]
    [Range(0f, 1f)]
    float m_WrongColorFrequency = 1f;

    SimpleHapticFeedback m_HapticFeedback;
    HapticImpulsePlayer m_HapticPlayer;
    bool m_WarnedMissingHaptics;
    Coroutine m_ColorHapticRoutine;
    Coroutine m_WrongColorHapticRoutine;

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
        float strength = Mathf.Clamp01(m_HapticStrengthCurve.Evaluate(normalized));
        float frequency = Mathf.Lerp(minFrequency, maxFrequency, strength);
        float intensity = Mathf.Lerp(Mathf.Min(m_MinHapticIntensity, m_MaxHapticIntensity), Mathf.Max(m_MinHapticIntensity, m_MaxHapticIntensity), strength);
        float impulsesPerSecond = Mathf.Lerp(Mathf.Min(m_MinImpulsesPerSecond, m_MaxImpulsesPerSecond), Mathf.Max(m_MinImpulsesPerSecond, m_MaxImpulsesPerSecond), strength);
        float playDuration = Mathf.Lerp(Mathf.Min(m_MinPlayDuration, m_MaxPlayDuration), Mathf.Max(m_MinPlayDuration, m_MaxPlayDuration), strength);

        if (m_WrongColorHapticRoutine != null)
        {
            StopCoroutine(m_WrongColorHapticRoutine);
            m_WrongColorHapticRoutine = null;
        }

        if (m_ColorHapticRoutine != null)
            StopCoroutine(m_ColorHapticRoutine);

        m_ColorHapticRoutine = StartCoroutine(PlayColorHapticRoutine(frequency, intensity, impulsesPerSecond, playDuration));
    }

    /// <summary>
    /// Plays a strong buzzing haptic intended for wrong color feedback.
    /// </summary>
    public void WrongColorHaptic()
    {
        if (!ResolveHapticPlayer())
            return;

        if (m_ColorHapticRoutine != null)
        {
            StopCoroutine(m_ColorHapticRoutine);
            m_ColorHapticRoutine = null;
        }

        if (m_WrongColorHapticRoutine != null)
            StopCoroutine(m_WrongColorHapticRoutine);

        m_WrongColorHapticRoutine = StartCoroutine(PlayWrongColorHapticRoutine());
    }

    IEnumerator PlayColorHapticRoutine(float frequency, float intensity, float impulsesPerSecond, float totalPlayDuration)
    {
        if (totalPlayDuration <= 0f)
        {
            m_HapticPlayer.SendHapticImpulse(intensity, m_PulseDuration, frequency);
            m_ColorHapticRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        float pulseInterval = 1f / Mathf.Max(0.01f, impulsesPerSecond);

        while (elapsed < totalPlayDuration)
        {
            m_HapticPlayer.SendHapticImpulse(intensity, m_PulseDuration, frequency);
            yield return new WaitForSeconds(pulseInterval);
            elapsed += pulseInterval;
        }

        m_ColorHapticRoutine = null;
    }

    IEnumerator PlayWrongColorHapticRoutine()
    {
        float intensity = 1f;
        float frequency = Mathf.Clamp01(m_WrongColorFrequency);
        float totalDuration = Mathf.Max(0f, m_WrongColorDuration);
        float pulseDuration = Mathf.Max(0f, m_WrongColorPulseDuration);
        float pulseInterval = 1f / Mathf.Max(0.01f, m_WrongColorImpulsesPerSecond);

        if (totalDuration <= 0f)
        {
            m_HapticPlayer.SendHapticImpulse(intensity, pulseDuration, frequency);
            m_WrongColorHapticRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            m_HapticPlayer.SendHapticImpulse(intensity, pulseDuration, frequency);
            yield return new WaitForSeconds(pulseInterval);
            elapsed += pulseInterval;
        }

        m_WrongColorHapticRoutine = null;
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
