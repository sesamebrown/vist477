using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

/// <summary>
/// Implements swimming locomotion for VR by tracking controller movements and applying
/// forces to the XR Origin in the opposite direction of hand movement.
/// 
/// Usage: Attach this to your XR Origin GameObject.
/// </summary>
[RequireComponent(typeof(XROrigin))]
[AddComponentMenu("XR/Swim Locomotion Controller")]
public class SwimLocomotionController : MonoBehaviour
{
    [HideInInspector]
    [SerializeField]
    private XROrigin xrOrigin;

    [HideInInspector]
    [SerializeField]
    private CharacterController characterController;

    [Header("Settings")]
    [Tooltip("Configuration settings for swimming behavior.")]
    public SwimLocomotionSettings settings = new SwimLocomotionSettings();

    [Header("Debug")]
    [Tooltip("Enable to see debug information in the console and Scene view.")]
    public bool debugMode = false;

    // Hand tracking data
    private HandSwimData leftHand;
    private HandSwimData rightHand;

    // Current swimming velocity
    private Vector3 swimVelocity;
    private Vector3 smoothedForce;

    // Cached transform for performance
    private Transform originTransform;

    /// <summary>
    /// Current swimming velocity (read-only).
    /// </summary>
    public Vector3 CurrentVelocity => swimVelocity;

    /// <summary>
    /// Whether swimming locomotion is currently enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    private void Awake()
    {
        // Get or cache components
        if (xrOrigin == null)
        {
            xrOrigin = GetComponent<XROrigin>();
            if (xrOrigin == null)
            {
                Debug.LogError("SwimLocomotionController: XROrigin component not found! This component must be attached to an XR Origin GameObject.");
                enabled = false;
                return;
            }
        }

        originTransform = xrOrigin.Origin != null ? xrOrigin.Origin.transform : transform;

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        // Initialize hand tracking
        leftHand = new HandSwimData(XRNode.LeftHand);
        rightHand = new HandSwimData(XRNode.RightHand);

        // Apply default settings if none exist
        if (settings == null)
        {
            settings = SwimLocomotionSettings.GetDefaults();
        }

        swimVelocity = Vector3.zero;
        smoothedForce = Vector3.zero;
    }

    private void Update()
    {
        if (!IsEnabled)
            return;

        float deltaTime = Time.deltaTime;

        // Update hand tracking
        leftHand.UpdateTracking(deltaTime);
        rightHand.UpdateTracking(deltaTime);

        // Calculate and apply swimming forces
        CalculateSwimmingForces(deltaTime);

        // Apply movement
        ApplySwimmingMovement(deltaTime);

        // Debug visualization
        if (debugMode)
        {
            DrawDebugInfo();
        }
    }

    /// <summary>
    /// Calculates the propulsion forces based on hand movements.
    /// </summary>
    private void CalculateSwimmingForces(float deltaTime)
    {
        Vector3 totalForce = Vector3.zero;
        int activeHands = 0;

        // Process left hand
        if (leftHand.ShouldApplyForce(settings.requireGripPress, settings.minimumSpeed))
        {
            Vector3 handForce = CalculateHandForce(leftHand);
            totalForce += handForce;
            activeHands++;

            if (debugMode)
            {
                Debug.Log($"Left Hand - Velocity: {leftHand.Velocity}, Speed: {leftHand.Speed:F3}, Force: {handForce}");
            }
        }

        // Process right hand
        if (rightHand.ShouldApplyForce(settings.requireGripPress, settings.minimumSpeed))
        {
            Vector3 handForce = CalculateHandForce(rightHand);
            totalForce += handForce;
            activeHands++;

            if (debugMode)
            {
                Debug.Log($"Right Hand - Velocity: {rightHand.Velocity}, Speed: {rightHand.Speed:F3}, Force: {handForce}");
            }
        }

        // Check if both hands are required
        if (settings.requireBothHands && activeHands < 2)
        {
            totalForce = Vector3.zero;
        }

        // Clamp total force
        if (totalForce.magnitude > settings.maxForcePerFrame)
        {
            totalForce = totalForce.normalized * settings.maxForcePerFrame;
        }

        // Smooth the force to prevent jittery movement
        smoothedForce = Vector3.Lerp(smoothedForce, totalForce, 1f - settings.forceSmoothing);

        // Add force to velocity (acceleration)
        if (deltaTime > 0)
        {
            swimVelocity += smoothedForce * deltaTime;
        }
    }

    /// <summary>
    /// Calculates the propulsion force from a single hand's movement.
    /// The force is applied in the OPPOSITE direction of hand movement (Newton's third law).
    /// Forces are calculated in the XR Origin's local space so they're relative to where the player is facing.
    /// </summary>
    private Vector3 CalculateHandForce(HandSwimData hand)
    {
        // Convert world space velocity to XR Origin local space
        // This ensures forces are relative to where the player is facing after snap turns
        Vector3 localVelocity = originTransform.InverseTransformDirection(hand.Velocity);
        
        // Base force is opposite to hand velocity (in local space)
        Vector3 localForce = -localVelocity * settings.forceMultiplier;

        // Apply rotation-based modification if enabled
        if (settings.considerHandRotation)
        {
            // Also transform palm direction to local space for consistent rotation checking
            Vector3 localPalmDirection = originTransform.InverseTransformDirection(hand.GetPalmDirection());
            float rotationFactor = settings.CalculateRotationFactor(localPalmDirection, localVelocity);
            localForce *= rotationFactor;
        }

        // Convert back to world space for applying to swim velocity
        return originTransform.TransformDirection(localForce);
    }

    /// <summary>
    /// Applies the calculated swimming velocity to move the XR Origin.
    /// </summary>
    private void ApplySwimmingMovement(float deltaTime)
    {
        // Apply gravity
        if (settings.gravityScale != 0f)
        {
            swimVelocity += Vector3.down * (settings.gravityScale * deltaTime);
        }

        // Apply drag/resistance
        swimVelocity *= (1f - settings.dragCoefficient);

        // Stop if below threshold
        if (swimVelocity.magnitude < settings.stopThreshold)
        {
            swimVelocity = Vector3.zero;
        }

        // Calculate displacement
        Vector3 movement = swimVelocity * deltaTime;

        // Apply movement
        if (characterController != null && characterController.enabled)
        {
            // Use CharacterController for collision handling
            characterController.Move(movement);
        }
        else
        {
            // Direct transform movement (no collision)
            originTransform.position += movement;
        }

        if (debugMode && swimVelocity.sqrMagnitude > 0.001f)
        {
            Debug.Log($"Swimming Velocity: {swimVelocity}, Movement: {movement}");
        }
    }

    /// <summary>
    /// Draws debug information in the Scene view.
    /// </summary>
    private void DrawDebugInfo()
    {
        if (xrOrigin.Camera == null)
            return;

        Vector3 cameraPos = xrOrigin.Camera.transform.position;

        // Draw velocity vector
        if (swimVelocity.sqrMagnitude > 0.001f)
        {
            Debug.DrawRay(cameraPos, swimVelocity, Color.cyan, 0f, false);
        }

        // Draw hand velocities
        if (leftHand.IsTracked)
        {
            Debug.DrawRay(leftHand.CurrentPosition, leftHand.Velocity, Color.red, 0f, false);
            if (settings.considerHandRotation)
            {
                Debug.DrawRay(leftHand.CurrentPosition, leftHand.GetPalmDirection() * 0.1f, Color.blue, 0f, false);
            }
        }

        if (rightHand.IsTracked)
        {
            Debug.DrawRay(rightHand.CurrentPosition, rightHand.Velocity, Color.green, 0f, false);
            if (settings.considerHandRotation)
            {
                Debug.DrawRay(rightHand.CurrentPosition, rightHand.GetPalmDirection() * 0.1f, Color.blue, 0f, false);
            }
        }
    }

    /// <summary>
    /// Stops all swimming motion immediately.
    /// </summary>
    public void StopSwimming()
    {
        swimVelocity = Vector3.zero;
        smoothedForce = Vector3.zero;
    }

    /// <summary>
    /// Gets the tracking data for the left hand (for advanced usage).
    /// </summary>
    public HandSwimData GetLeftHandData() => leftHand;

    /// <summary>
    /// Gets the tracking data for the right hand (for advanced usage).
    /// </summary>
    public HandSwimData GetRightHandData() => rightHand;

    private void OnValidate()
    {
        // Ensure settings exist in editor
        if (settings == null)
        {
            settings = SwimLocomotionSettings.GetDefaults();
        }
    }
}
