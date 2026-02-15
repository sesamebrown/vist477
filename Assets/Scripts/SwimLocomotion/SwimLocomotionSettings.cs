using UnityEngine;

/// <summary>
/// Configuration settings for the swimming locomotion system.
/// </summary>
[System.Serializable]
public class SwimLocomotionSettings
{
    [Header("Input Requirements")]
    [Tooltip("If true, the grip button (side handle) must be held down for swimming movements to apply force. " +
             "This helps prevent the 'return stroke' from negating forward motion.")]
    public bool requireGripPress = true;

    [Tooltip("Minimum speed (m/s) the controller must be moving to generate propulsion.")]
    [Range(0f, 2f)]
    public float minimumSpeed = 0.1f;

    [Header("Force Application")]
    [Tooltip("Base multiplier for the propulsion force. Higher values = faster swimming.")]
    [Range(0f, 100f)]
    public float forceMultiplier = 10f;

    [Tooltip("Maximum force that can be applied per frame (prevents extreme velocities).")]
    [Range(0f, 50f)]
    public float maxForcePerFrame = 20f;

    [Tooltip("How quickly the swimming velocity decays (simulates drag/resistance). 0 = no decay, 1 = instant stop. For smooth gliding, use 0.01-0.03. Higher values = faster stop.")]
    [Range(0f, 0.99f)]
    public float dragCoefficient = 0.015f;

    [Tooltip("Gravity scale applied to swimming. 0 = no gravity (floating), positive = downward pull, negative = upward buoyancy. For air swimming, use 0 or very small values (0.5-2).")]
    [Range(-20f, 20f)]
    public float gravityScale = 0f;

    [Header("Advanced Options")]
    [Tooltip("If true, considers hand rotation when calculating force. " +
             "Movements with palm perpendicular to direction generate more force.")]
    public bool considerHandRotation = false;

    [Tooltip("When considerHandRotation is enabled, this controls how much rotation affects force. " +
             "0 = no effect, 1 = full effect (palm parallel to movement = no force).")]
    [Range(0f, 1f)]
    public float rotationInfluence = 0.5f;

    [Tooltip("If true, swimming propulsion only works when both hands are moving (like real swimming strokes).")]
    public bool requireBothHands = false;

    [Header("Smoothing")]
    [Tooltip("Smoothing factor for applied forces. Higher = smoother but less responsive. Lower = more immediate response.")]
    [Range(0f, 0.95f)]
    public float forceSmoothing = 0.15f;

    [Tooltip("Velocity threshold below which swimming velocity is set to zero (prevents drifting). Set very low for smooth gliding.")]
    [Range(0f, 0.1f)]
    public float stopThreshold = 0.001f;

    /// <summary>
    /// Calculates the rotation factor for a hand based on its palm direction and velocity.
    /// Returns a value between 0 and 1, where 1 means optimal hand orientation.
    /// </summary>
    /// <param name="palmDirection">The direction the palm is facing (world space)</param>
    /// <param name="velocityDirection">The direction of movement (world space)</param>
    /// <returns>Multiplier for force (0-1)</returns>
    public float CalculateRotationFactor(Vector3 palmDirection, Vector3 velocityDirection)
    {
        if (!considerHandRotation || velocityDirection.sqrMagnitude < 0.001f)
            return 1f;

        // Calculate how perpendicular the palm is to the direction of movement
        // Dot product: 1 = parallel, 0 = perpendicular, -1 = anti-parallel
        float dot = Vector3.Dot(palmDirection.normalized, velocityDirection.normalized);
        
        // We want perpendicular (cupped hand) to give maximum force
        // abs(dot) close to 0 = perpendicular = good
        // abs(dot) close to 1 = parallel = bad (hand slicing through water)
        float perpendicularFactor = 1f - Mathf.Abs(dot);
        
        // Blend between full effect and no effect based on rotationInfluence
        return Mathf.Lerp(1f, perpendicularFactor, rotationInfluence);
    }

    /// <summary>
    /// Returns default swimming settings.
    /// </summary>
    public static SwimLocomotionSettings GetDefaults()
    {
        return new SwimLocomotionSettings
        {
            requireGripPress = true,
            minimumSpeed = 0.1f,
            forceMultiplier = 10f,
            maxForcePerFrame = 20f,
            dragCoefficient = 0.015f,
            gravityScale = 0f,
            considerHandRotation = false,
            rotationInfluence = 0.5f,
            requireBothHands = false,
            forceSmoothing = 0.15f,
            stopThreshold = 0.001f
        };
    }
}
